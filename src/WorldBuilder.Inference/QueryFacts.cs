using System.Globalization;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>
/// Role and outcome, carried as fields rather than left to be read out of a sentence.
///
/// "How many times was Paernmel Has the target of an attempt?" retrieves seven records and the
/// right answer is four. Getting there needs two independent distinctions, and missing either one
/// produces a number that is wrong and looks right:
///
/// <list type="bullet">
/// <item><b>role</b> — five records have him as the target and two have him ordering a killing.
/// Counting the records gives seven.</item>
/// <item><b>outcome</b> — of the five aimed at him, four failed and the fifth killed him.
/// Counting by role alone gives five.</item>
/// </list>
///
/// Both are on the event and neither survives being turned into English. The sentence for a
/// failed attempt and the sentence for a successful one are different sentences, so a reader
/// could in principle tell them apart — but "could in principle" is what produced five, and the
/// engine already knows the answer. It is stated.
/// </summary>
public static class QueryFacts
{
    /// <summary>The fields one event line carries, or empty where it has none worth carrying.</summary>
    public static string Fields(WorldView view, Event e, EntityId subject)
    {
        List<string> fields = [];

        // Both ends of the act, named. Which of two men in a sentence did the thing is exactly
        // what a role field exists to settle, so naming only one settles nothing.
        (EntityId actor, EntityId target) = Sides(e);

        if (actor.Kind == EntityKind.Actor && target.Kind == EntityKind.Actor)
        {
            fields.Add($"acted by {view.State.NameOf(actor)}");
            fields.Add($"done to {view.State.NameOf(target)}");
        }

        if (!subject.IsNone && RoleOf(e, subject) is { Length: > 0 } role)
            fields.Add($"{view.State.NameOf(subject)} is {role}");

        if (Outcome(e) is { Length: > 0 } outcome) fields.Add($"outcome {outcome}");

        return fields.Count == 0 ? "" : $"  {{{string.Join("; ", fields)}}}";
    }

    /// <summary>
    /// Who did it and who it was done to — which is not the schema's subject and object.
    ///
    /// For most kinds it is, and reading the roles straight off the schema is why this was
    /// wrong. A violent death records the <em>victim</em> as its subject and the killer as its
    /// object, because the event is the death rather than the killing; a disputed succession
    /// records the named heir as its subject and the man contesting him as its object. Both came
    /// out backwards, which turned a field written to settle "which of these two did it" into a
    /// confident statement of the wrong one.
    ///
    /// Anything not listed here follows the ordinary reading. Kinds with no doer at all — a
    /// birth, a marriage — return nothing rather than inventing a direction for a mutual act.
    /// </summary>
    private static (EntityId Actor, EntityId Target) Sides(Event e) => e.Kind switch
    {
        EventKind.LifeDeathViolent => (e.Object, e.Subject),
        EventKind.PolitySuccessionDisputed => (e.Object, e.Subject),
        EventKind.LifeBirth or EventKind.LifeMarriage => (EntityId.None, EntityId.None),
        _ => (e.Subject, e.Object),
    };

    /// <summary>
    /// The counts an answer would otherwise have to work out, grouped by kind, then by the
    /// subject's role, then by outcome.
    ///
    /// Empty when there is nothing to disambiguate — a single record, or a set where every record
    /// puts the subject on the same side with the same result. Offering a count of one under
    /// three headings is how a block meant to prevent arithmetic starts inviting it.
    /// </summary>
    public static string Block(WorldView view, IReadOnlyList<EventId> events, EntityId subject)
    {
        if (subject.IsNone || events.Count < 2) return "";

        // Ordered, so the block is stable across runs. A prompt that reorders itself between
        // two runs of the same question makes every difference in the answer unattributable.
        List<string> kinds = [];
        Dictionary<string, Dictionary<string, Tally>> byKind = new(StringComparer.Ordinal);

        foreach (EventId id in events)
        {
            Event e = view.Log.Get(id);
            string kind = EventKinds.Name(e.Kind);
            string role = RoleOf(e, subject);
            if (role.Length == 0) continue;

            if (!byKind.TryGetValue(kind, out Dictionary<string, Tally>? roles))
            {
                byKind[kind] = roles = new Dictionary<string, Tally>(StringComparer.Ordinal);
                kinds.Add(kind);
            }

            if (!roles.TryGetValue(role, out Tally? tally)) roles[role] = tally = new Tally();
            tally.Add(e.Outcome);
        }

        if (!WorthStating(byKind)) return "";

        StringBuilder sb = new();
        sb.Append("\nWHICH SIDE THE SUBJECT WAS ON, AND HOW EACH ENDED — counted for you over the\n");
        sb.Append("records above. A record where ").Append(view.State.NameOf(subject))
          .Append(" acted is not a record of something\ndone to ")
          .Append(view.State.NameOf(subject))
          .Append(", and the two are never added together.\n");

        foreach (string kind in kinds)
        {
            sb.Append("  ").Append(kind).Append('\n');

            List<string> roles = [.. byKind[kind].Keys];
            roles.Sort(StringComparer.Ordinal);

            foreach (string role in roles)
            {
                Tally tally = byKind[kind][role];
                sb.Append("    with ").Append(view.State.NameOf(subject)).Append(" as ").Append(role)
                  .Append(": ").Append(N(tally.Total))
                  .Append(tally.Total == 1 ? " record" : " records");

                if (tally.Failed > 0 || tally.Succeeded > 0)
                {
                    List<string> split = [];
                    if (tally.Failed > 0) split.Add($"{N(tally.Failed)} failed");
                    if (tally.Succeeded > 0) split.Add($"{N(tally.Succeeded)} succeeded");
                    if (tally.Unstated > 0) split.Add($"{N(tally.Unstated)} with no outcome recorded");
                    sb.Append(" — ").Append(string.Join(", ", split));
                }
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Whether these counts say anything the answer cannot get from the records themselves.
    ///
    /// Two roles or two outcomes, plainly. But also a plain count of two or more of one kind,
    /// which was left out at first as the set restating its own length — and is the one figure
    /// that stops an answer stopping early. Asked who conspired against Paernmel Has, with three
    /// uncovered plots in front of it, generation named one man and stopped; it did so on one
    /// run and not the next, from the same prompt, which is not something another sentence of
    /// instruction fixes. A supplied "3 records" is a figure the answer is required to state,
    /// and an answer that names one man against it contradicts its own material.
    /// </summary>
    private static bool WorthStating(Dictionary<string, Dictionary<string, Tally>> byKind)
    {
        foreach (Dictionary<string, Tally> roles in byKind.Values)
        {
            if (roles.Count > 1) return true;

            foreach (Tally t in roles.Values)
            {
                if (t.Failed > 0 && t.Succeeded > 0) return true;
                if (t.Total > 1) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Which side of an event the subject is on, in words a sentence can use.
    ///
    /// Deliberately not "subject" and "object". Those are schema words, and a model handed them
    /// writes them into the prose — a passage has copied a pack's own field names before.
    /// </summary>
    private static string RoleOf(Event e, EntityId subject)
    {
        (EntityId actor, EntityId target) = Sides(e);

        if (!actor.IsNone && actor == subject) return "the one who acted";
        if (!target.IsNone && target == subject) return "the one it was done to";
        if (e.Faction == subject) return "the power acting";
        if (e.Where == subject) return "the place it happened to";
        return "";
    }

    private static string Outcome(Event e) => e.Outcome switch
    {
        Core.Outcome.Succeeded => "succeeded",
        Core.Outcome.Failed => "failed",
        _ => "",
    };

    private sealed class Tally
    {
        public int Total { get; private set; }
        public int Failed { get; private set; }
        public int Succeeded { get; private set; }
        public int Unstated { get; private set; }

        public void Add(Outcome outcome)
        {
            Total++;
            switch (outcome)
            {
                case Core.Outcome.Succeeded: Succeeded++; break;
                case Core.Outcome.Failed: Failed++; break;
                default: Unstated++; break;
            }
        }
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
