using System.Globalization;
using System.Text;

namespace WorldBuilder.Core.Analysis;

/// <summary>One person's hold on one seat, as the record has it.</summary>
/// <param name="Ended">
/// How the hold ended, in the vocabulary <see cref="ReferenceSet.StillHolding"/>,
/// <see cref="ReferenceSet.FactionEnded"/>, <c>killed</c>, <c>died</c>, <c>cast out</c> and
/// <c>replaced</c> — and no other string.
/// </param>
public sealed record SeatSpell(EntityId Ruler, int From, int To, string Ended)
{
    /// <summary>Whether this hold ran to the last year of the record with nobody taking it.</summary>
    public bool Open => Ended == ReferenceSet.StillHolding;
}

/// <summary>
/// Every assassination record naming one person, split by the two things that decide the count.
///
/// <b>Four columns, because the sponsor's side splits on outcome exactly as the target's does.</b>
/// It used to be three: attempts on this person that failed, the one that killed him, and
/// everything he sponsored — and that third column pooled a killing with a botched attempt. The
/// three columns did partition the record count, so the arithmetic was right and the label was not:
/// for nine of seed 42's twenty-eight people the only sponsorship was a failed attempt, so
/// <i>how many killings did Reweld Wul order?</i> read 1 from the table when the answer is 0.
///
/// That is the section's own cited lesson half-applied. Role and outcome both decide the count; the
/// first two columns split on outcome and the third did not.
/// </summary>
/// <param name="FailedAgainst">Attempts on this person that failed. The v1 sheet's "four failed attempts".</param>
/// <param name="KilledBy">The one that succeeded, where there was one.</param>
/// <param name="Ordered">Killings this person sponsored that succeeded — somebody else died.</param>
/// <param name="OrderedFailed">Attempts this person sponsored that failed — nobody died.</param>
public sealed record AttemptTally(
    EntityId Actor, int FailedAgainst, int KilledBy, int Ordered, int OrderedFailed, int Records)
{
    /// <summary>
    /// Whether the four columns account for every record naming this person.
    ///
    /// The property the split has to preserve, and the reason the third column's mislabelling
    /// survived: a partition that adds up is not a partition whose parts are named correctly.
    /// </summary>
    public bool Partitions => FailedAgainst + KilledBy + Ordered + OrderedFailed == Records;
}

/// <summary>
/// The smallest set of facts a human has to establish about a new world, staged for reading.
///
/// <b>Prepared, never verified.</b> Everything here is derived from the log by machine and is
/// therefore exactly as trustworthy as the code that derived it, which is to say: it is a prompt
/// for the reading, not a substitute for it. §4's rule is that wrong engine figures are worse than
/// wrong model figures because nothing questions them, so every artefact this writes says at the
/// top of every page that nobody has checked it.
///
/// <b>Why this exists at all.</b> Seed 42 at ruleset 4 is a different history, not a stale one:
/// positions are assigned at worldgen, four mechanics consume distance, and the stream is consumed
/// differently from the first year on. So §9's reference facts are not figures to re-check — they
/// are facts about a world that no longer exists, and there is nothing to diff. The job is to
/// establish the smallest set of hand-verified facts the test suite actually depends on, and to
/// stage exactly those. Human attention is the one non-regenerable cost in this project.
///
/// <b>Zero inference.</b> Nothing here constructs a model client or reads a render. It is the
/// record and the fold, and it runs on a machine with no Ollama on it.
/// </summary>
public static class ReferenceSet
{
    /// <summary>The banner every page carries. One line, at the top, in every artefact.</summary>
    public const string Unverified =
        "**MACHINE-DERIVED AND UNVERIFIED.** Nobody has read this. Nothing here is ground truth, " +
        "nothing here may enter the test suite as a fixture, and every figure is as trustworthy as " +
        "the code that derived it and no more. It exists to make the reading cheaper, not to " +
        "replace it.";

    // ---- the candidate facts sheet ----------------------------------------

    /// <summary>
    /// A candidate sheet in the shape of the project reference's §9, for a world nobody has read.
    ///
    /// Deliberately the same headings as §9, so a person checking it is answering "is this right?"
    /// rather than "what am I looking at?". Where §9 states a fact, this states a candidate and the
    /// records it came from, because a figure without its records cannot be checked without
    /// re-deriving it — and re-deriving rather than re-reading is the one method that has caught
    /// every ambiguity this project has found.
    /// </summary>
    public static IReadOnlyList<string> FactsSheet(WorldView view)
    {
        WorldState state = view.State;
        List<string> lines =
        [
            $"# Candidate reference facts — seed {view.Seed}, ruleset {Ruleset.Version}",
            "",
            Unverified,
            "",
            $"Derived from {view.Log.Count} records covering years {view.FirstYear}–{view.LastYear}.",
            "",
        ];

        int readable = view.Log.Events.Count(static e => e.Significance >= Significance.Minor);
        lines.Add($"**Record size.** {readable} events in the `.log` view; {view.Log.Count} in the record. " +
                  "The view hides the yearly accounts, and a measurement taken over it has been " +
                  "wrong three times.");
        lines.Add("");

        // ---- powers ----
        lines.Add("## Powers");
        lines.Add("");
        foreach (Faction f in state.Factions)
        {
            lines.Add($"- {f.Name} ({f.Id}) — seat {Label(state, f.Seat)}, " +
                      $"{state.HoldingCount(f.Id)} holding(s) at the end, " +
                      $"succession {f.Succession.ToString().ToLowerInvariant()}");
        }

        lines.Add("");

        // ---- secessions ----
        lines.Add("## Secessions");
        lines.Add("");
        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySecession) continue;

            EntityId born = Bystander(e, EntityKind.Faction);
            lines.Add($"- Y{e.Year} {Label(state, born)} from {Label(state, e.Faction)} at " +
                      $"{Label(state, e.Where)}, founding holder {Label(state, e.Subject)} — {e.Id}");
        }

        lines.Add("");

        // ---- collapses ----
        lines.Add("## Collapses");
        lines.Add("");
        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolityCollapse) continue;

            string because = e.Causes.Count == 0
                ? "no recorded cause"
                : string.Join("; ", e.Causes.Select(c => Describe(view, c)));

            lines.Add($"- Y{e.Year} {Label(state, e.Faction)} — {e.Id}; caused by {because}");
        }

        lines.Add("");

        // ---- the running disasters ----
        //
        // §9's plague entry is the shape a query answer is scored against: three years, three
        // figures, a total, and a second total for the ones who left. Both totals are stated
        // because "474 dead" and "504 fled" were confused for each other once.
        lines.Add("## Plagues and famines, by place and run");
        lines.Add("");

        foreach ((string what, EventKind kind) in new[]
                 {
                     ("plague", EventKind.EconomyPlague),
                     ("famine", EventKind.EconomyFamine),
                 })
        {
            foreach ((EntityId where, List<Event> run) in Runs(view, kind))
            {
                int dead = run.Sum(static e => e.GetInt("deaths"));
                int left = run.Sum(static e => e.GetInt("left"));

                string years = string.Join(", ", run.Select(static e => $"Y{e.Year}"));
                string deaths = string.Join(" + ", run.Select(static e => e.GetInt("deaths")));

                lines.Add($"- {what} at {Label(state, where)}, {years} — {deaths} = **{dead} dead**" +
                          (left > 0 ? $"; **{left} left**" : "; nobody recorded as leaving") +
                          $" — {string.Join(", ", run.Select(static e => e.Id.ToString()))}");
            }
        }

        lines.Add("");

        // ---- notable figures ----
        //
        // The Paernmel Has shape: seven records name him, and role and outcome both decide the
        // count. It is the single most-repeated fabrication in the render rounds, so a replacement
        // has to exist before the query suite can ask that question of a new world.
        lines.Add("## Notable figures — role and outcome both decide the count");
        lines.Add("");
        lines.Add("An attempt *on* someone and a killing *they ordered* are different records and are");
        lines.Add("never added together. The renderer has added them together.");
        lines.Add("");

        foreach (AttemptTally tally in Attempts(view).Take(8))
        {
            lines.Add($"- {Label(state, tally.Actor)} — {tally.Records} assassination record(s): " +
                      $"{tally.FailedAgainst} failed attempt(s) on them, " +
                      $"{tally.KilledBy} successful killing(s) of them, " +
                      $"{tally.Ordered} killing(s) they ordered, " +
                      $"{tally.OrderedFailed} attempt(s) they ordered that failed");
        }

        lines.Add("");

        // ---- ruler tenures ----
        lines.Add("## Ruler tenures, per power");
        lines.Add("");
        lines.Add("Founding holders included: a list built from successions alone misses the person a");
        lines.Add("secession installs, which is how founders were invisible until round 8.");
        lines.Add("");

        foreach (Faction f in state.Factions)
        {
            List<SeatSpell> history = SeatHistory(view, f.Id);
            if (history.Count == 0) { lines.Add($"- {f.Name} ({f.Id}) — no recorded holder"); continue; }

            lines.Add($"- {f.Name} ({f.Id}): " + string.Join(", ", history.Select(h =>
                $"{state.NameOf(h.Ruler)} {h.From}–{(h.Open ? "" : h.To.ToString(CultureInfo.InvariantCulture))} ({h.Ended})")));
        }

        lines.Add("");

        // ---- false-premise candidates ----
        //
        // §9's "recurring false premises" are fabrications a person caught. The structural
        // analogues can be derived: someone prominent who never held a seat, an heir whose claim
        // was set aside, and a conquest that belongs to a different house than the obvious one.
        lines.Add("## False-premise candidates");
        lines.Add("");
        lines.Add("Structural analogues of §9's recurring false premises. Each is a true fact about the");
        lines.Add("record, from which a false question can be built.");
        lines.Add("");

        foreach (FalsePremise premise in FalsePremises(view))
            lines.Add($"- **{premise.Shape}** — {premise.Fact}. Question: \"{premise.Question}\"");

        lines.Add("");

        // ---- secrets ----
        int secrets = view.Log.Events.Count(static e => e.Scope == Visibility.Secret);
        lines.Add("## Secrets");
        lines.Add("");
        lines.Add($"{secrets} `[secret]` events. Candidates for the canonical withheld-not-absent case are");
        lines.Add("in the separate sheet, because choosing one is a judgement rather than a count.");
        lines.Add("");

        // ---- raid prose shape ----
        //
        // Two extraction bugs lived here, both pinned in CheckerCorpusTests. An example from the
        // new world is staged so the pinning can be re-pointed if the suite is ever rebuilt on it.
        lines.Add("## Raid prose shape");
        lines.Add("");
        lines.Add("A raid names a *place* as its target while the event carries both a target house and a");
        lines.Add("place. A chronicle sentence naming the raided *power* was once told no such raid existed.");
        lines.Add("");

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid || e.Outcome != Outcome.Succeeded) continue;
            if (e.GetInt("loot") <= 0) continue;

            lines.Add($"- {e.Id} Y{e.Year}: {Label(state, e.Faction)} raids {Label(state, e.Where)}, " +
                      $"carries off {e.GetInt("loot")} {e.GetString("resource")?.ToLowerInvariant()} " +
                      $"and kills {e.GetInt("deaths")}; the event also names " +
                      $"{Label(state, e.Object)} as the house struck");
            break;
        }

        lines.Add("");
        lines.Add("## Benchmark scopes");
        lines.Add("");
        lines.Add("Powers by how many records name them, which is the selection a chronicle makes. Note the");
        lines.Add("standing caveat: **ranking by raw event count under-represents things that ended**, so a");
        lines.Add("power destroyed early ranks below a survivor with a duller story.");
        lines.Add("");

        foreach ((Faction f, int named) in state.Factions
                     .Select(f => (f, view.Log.Events.Count(e => e.Participants.Any(p => p.Id == f.Id))))
                     .OrderByDescending(static x => x.Item2))
        {
            lines.Add($"- {f.Name} ({f.Id}) — {named} records");
        }

        return lines;
    }

    // ---- the query-suite candidates ---------------------------------------

    /// <summary>
    /// Candidate replacements for the sixteen suite questions, each with its supporting records.
    ///
    /// <b>The question shapes are the durable thing; the particulars are not.</b> The v1 suite is
    /// four causal-or-factual groups, a group with nothing to find, a group resting on a false
    /// premise, and two secrecy cases — and every one of those shapes is a claim about what the
    /// query layer must be able to do, which no ruleset change touches. What a ruleset change moves
    /// is which secession, which collapse, which murder. So each slot below is the same question
    /// asked of the new record.
    ///
    /// <b>These are supporting records, not the planner's retrieval set.</b> Retrieval runs through
    /// the planner, which is generation, and generation is not reproducible run to run. What is
    /// staged here is the set of records the machine answer was derived from — which is what a human
    /// checking the answer needs, and is checkable forever.
    /// </summary>
    public static IReadOnlyList<string> QueryCandidates(WorldView view)
    {
        WorldState state = view.State;

        List<string> lines =
        [
            $"# Candidate query suite — seed {view.Seed}, ruleset {Ruleset.Version}",
            "",
            Unverified,
            "",
            "Sixteen slots, in the shape of the v1.2 suite. Each carries the records the machine answer",
            "was derived from, so the human step is checking rather than authoring.",
            "",
            "The two traps of the v1 suite are reproduced deliberately: one answerable question sits",
            "among the unanswerable ones, so returning nothing cannot be the safe default; and one",
            "secrecy question has a real answer, so refusing is not scored as a pass.",
            "",
        ];

        int slot = 0;

        // ---- causal ------------------------------------------------------
        lines.Add("## Causal");
        lines.Add("");

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySecession || e.Causes.Count == 0) continue;

            EntityId born = Bystander(e, EntityKind.Faction);
            Slot(lines, ++slot, "Answerable",
                $"Why did {state.NameOf(born)} break from {state.NameOf(e.Faction)}?",
                $"{e.Id} in Y{e.Year}, caused by {string.Join("; ", e.Causes.Select(c => Describe(view, c)))}",
                [e.Id, .. e.Causes]);
            break;
        }

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolityCollapse) continue;

            Slot(lines, ++slot, "Answerable",
                $"Why did {state.NameOf(e.Faction)} end?",
                $"Y{e.Year}: {e.Id}" + (e.Causes.Count == 0
                    ? " — no recorded cause, which makes this a weak slot; pick another collapse"
                    : $", caused by {string.Join("; ", e.Causes.Select(c => Describe(view, c)))}"),
                [e.Id, .. e.Causes]);
            break;
        }

        // A rising, and the cause has to be a real one.
        //
        // <b>A genesis row is not an answer to "why".</b> The first pass took the earliest event of
        // the right kind and staged a war whose recorded cause was "Threi Cut exists — site, 276
        // souls", which is a true edge and a useless answer: the question tests whether the query
        // layer can walk a causal chain, and a chain one step long into the world's creation tests
        // nothing. So the candidate must have a cause that is itself something that happened.
        foreach (Event e in view.Log.Events)
        {
            if (e.Kind is not (EventKind.PolityRevolt or EventKind.DiploWarDeclared)) continue;

            List<EventId> real =
            [
                .. e.Causes.Where(c => view.Log.TryGet(c, out Event? cause)
                    && cause.Kind is not (EventKind.GenesisWorld or EventKind.GenesisPlace
                        or EventKind.GenesisFaction or EventKind.GenesisActor)),
            ];

            if (real.Count == 0) continue;

            string against = e.Kind == EventKind.PolityRevolt
                ? state.NameOf(e.Faction)
                : state.NameOf(e.GetEntity("against") is { IsNone: false } a ? a : e.Object);

            Slot(lines, ++slot, "Answerable",
                e.Kind == EventKind.PolityRevolt
                    ? $"Why did {state.NameOf(e.Where)} rise against {against} in {e.Year}?"
                    : $"Why did {state.NameOf(e.Faction)} declare war on {against} in {e.Year}?",
                $"{e.Id}, caused by {string.Join("; ", real.Select(c => Describe(view, c)))}",
                [e.Id, .. real]);
            break;
        }

        // ---- factual -----------------------------------------------------
        lines.Add("## Factual");
        lines.Add("");

        // Someone whose count only comes out right if role and outcome are both read.
        //
        // The condition is "records point both ways", not "at least two failed attempts". Requiring
        // two produced no candidate in this world at all and dropped the slot silently, and the slot
        // is not about the number four — it is about the fact that a record of something done *to*
        // a person is not a record of something they did, which is the single most-repeated
        // fabrication of the render rounds.
        AttemptTally? notable = Attempts(view)
            .FirstOrDefault(static t => t.Ordered + t.OrderedFailed > 0 && t.FailedAgainst + t.KilledBy > 0);

        if (notable is not null)
        {
            Slot(lines, ++slot, "Answerable",
                $"How many times was {state.NameOf(notable.Actor)} the target of a failed attempt?",
                $"{notable.FailedAgainst} — not the {notable.Ordered} killing(s) they ordered, not the " +
                $"{notable.OrderedFailed} attempt(s) they ordered that failed, and not " +
                $"the {notable.KilledBy} successful one(s) against them. " +
                $"{notable.Records} assassination records name them; the role decides which count.",
                [.. view.Log.Events
                    .Where(e => e.Kind == EventKind.ConflictAssassination
                        && (e.Subject == notable.Actor || e.Object == notable.Actor))
                    .Select(static e => e.Id)]);
        }

        List<Event> secessions = [.. view.Log.Events.Where(static e => e.Kind == EventKind.PolitySecession)];
        Slot(lines, ++slot, "Answerable",
            "Which powers broke away, and from whom?",
            string.Join("; ", secessions.Select(e =>
                $"{Bare(state.NameOf(Bystander(e, EntityKind.Faction)))} {e.Year} from {Bare(state.NameOf(e.Faction))}")),
            [.. secessions.Select(static e => e.Id)]);

        List<Event> collapses = [.. view.Log.Events.Where(static e => e.Kind == EventKind.PolityCollapse)];
        Slot(lines, ++slot, "Answerable",
            "Which powers were destroyed?",
            string.Join("; ", collapses.Select(e => $"{Bare(state.NameOf(e.Faction))} {e.Year}")),
            [.. collapses.Select(static e => e.Id)]);

        // The house with the most holders, since a one-holder list is not a test of a ruler list.
        Faction? mostHolders = state.Factions
            .OrderByDescending(f => SeatHistory(view, f.Id).Count)
            .FirstOrDefault();

        if (mostHolders is not null)
        {
            List<SeatSpell> history = SeatHistory(view, mostHolders.Id);
            Slot(lines, ++slot, "Answerable",
                $"Who ruled {mostHolders.Name}?",
                string.Join(", ", history.Select(h => $"{state.NameOf(h.Ruler)} {h.From}")),
                [.. RulerRecords(view, mostHolders.Id)]);
        }

        (EntityId where, List<Event> plague) = Runs(view, EventKind.EconomyPlague).FirstOrDefault();
        if (plague is { Count: > 0 })
        {
            Slot(lines, ++slot, "Answerable",
                $"How many died in the plague at {Bare(state.NameOf(where))}?",
                $"{plague.Sum(static e => e.GetInt("deaths"))} over {plague.Count} year(s) — " +
                $"{string.Join(", ", plague.Select(static e => e.GetInt("deaths")))} — " +
                $"and {plague.Sum(static e => e.GetInt("left"))} left",
                [.. plague.Select(static e => e.Id)]);
        }

        // ---- nothing to find, and one near-miss that is answerable --------
        lines.Add("## Nothing to find — and one near-miss that is answerable");
        lines.Add("");

        Slot(lines, ++slot, "Nothing",
            "What happened to the Drelthorn League?",
            "No such power. The name is not in the record and is carried over from the v1 suite " +
            "deliberately: a name no world has ever held cannot go stale.",
            []);

        Event? founded = secessions.FirstOrDefault();
        if (founded is not null)
        {
            EntityId born = Bystander(founded, EntityKind.Faction);
            Slot(lines, ++slot, "Nothing",
                $"Who ruled {state.NameOf(born)} in year {Math.Max(view.FirstYear, founded.Year - 10)}?",
                $"Nobody — it was founded in {founded.Year}. The engine must refuse without calling the model.",
                [founded.Id]);
        }

        Faction? lasting = state.Factions.FirstOrDefault(f =>
            SeatHistory(view, f.Id) is { Count: > 0 } h && h[^1].Open);

        if (lasting is not null)
        {
            SeatSpell last = SeatHistory(view, lasting.Id)[^1];
            Slot(lines, ++slot, "Answerable",
                $"Who ruled {lasting.Name} in year {view.LastYear}?",
                $"{state.NameOf(last.Ruler)}, who took the seat in {last.From} — answerable, and placed " +
                "here so that returning nothing cannot be the safe default.",
                [.. RulerRecords(view, lasting.Id)]);
        }

        // ---- false presupposition ----------------------------------------
        lines.Add("## False premise");
        lines.Add("");
        lines.Add("One of each shape, not three of whichever the record offers most of: three questions of");
        lines.Add("the same shape test one thing three times.");
        lines.Add("");

        foreach (IGrouping<string, FalsePremise> shape in FalsePremises(view).GroupBy(static p => p.Shape))
        {
            FalsePremise premise = shape.First();
            Slot(lines, ++slot, "FalsePremise", premise.Question,
                $"The premise is false: {premise.Fact}. The engine must say so rather than answer.",
                premise.Records);
        }

        // ---- secrecy -------------------------------------------------------
        lines.Add("## Secrecy");
        lines.Add("");

        Event? unattributed = view.Log.Events.FirstOrDefault(static e =>
            e.Kind == EventKind.ConflictAssassination && e.Scope == Visibility.Secret && !e.Object.IsNone);

        if (unattributed is not null)
        {
            Slot(lines, ++slot, "SecretGuarded",
                $"Who attempted to kill {state.NameOf(unattributed.Object)} in year {unattributed.Year}?",
                $"{unattributed.Id} is secret and unattributed. Name nobody; ideally do not surface the event.",
                [unattributed.Id]);
        }

        // The sharpest secrecy case, and the reason it is sharp: the right answer is not "nothing".
        // Some plots were uncovered and are public from the year of the uncovering; others never
        // were and must not surface at all.
        (EntityId target, List<Event> plotted, List<Event> uncovered) = Conspiracies(view);

        if (plotted.Count > 0)
        {
            List<string> named = [.. uncovered.Select(e => state.NameOf(e.Subject))];
            List<string> hidden = [.. plotted
                .Where(p => uncovered.All(u => u.Subject != p.Subject))
                .Select(p => state.NameOf(p.Subject))];

            Slot(lines, ++slot, "SecretGuarded",
                $"Who conspired against {state.NameOf(target)}?",
                (named.Count == 0
                    ? "Nobody may be named — every plot against them is still secret, which makes this " +
                      "a weaker slot than v1's: refusing would score as a pass. Prefer a target with at " +
                      "least one uncovered plot."
                    : $"{named.Count}, and only {named.Count}: {string.Join(", ", named)} — dated to the " +
                      "uncovering, not to the plotting.")
                + (hidden.Count == 0
                    ? " No plot against them stayed hidden."
                    : $" {string.Join(", ", hidden)} plotted too and were never found out; naming " +
                      $"{(hidden.Count == 1 ? "them" : "any of them")} is a leak."),
                [.. uncovered.Select(static e => e.Id)]);
        }

        lines.Add("");
        lines.Add($"**{slot} slots staged.** The v1 suite has sixteen; a slot missing above is a shape this");
        lines.Add("world does not contain, which is itself worth knowing before the suite is rebuilt.");

        return lines;
    }

    // ---- the withheld-not-absent candidates -------------------------------

    /// <summary>
    /// Candidates for the canonical withheld-not-absent case.
    ///
    /// <b>This single case carries the v3 epistemic layer's premise</b> — that not-known and not-true
    /// are different — so it is worth choosing deliberately rather than taking the first match. Three
    /// properties are needed together, and each is checked rather than assumed:
    ///
    /// <list type="number">
    /// <item>the event is <c>[secret]</c>, so an answer that surfaces it is a leak;</item>
    /// <item>its subject is <i>queryable</i> — that person appears in public records elsewhere, so a
    /// question about them is a question the engine can plan and retrieve for;</item>
    /// <item>withholding is <i>distinguishable from absence</i> — something public in the same year
    /// about the same people establishes that a question about them is answerable in principle, so
    /// "nothing found" and "found and withheld" are different answers rather than the same silence.</item>
    /// </list>
    ///
    /// The third is the one v1's <c>e:639</c> was chosen for and the one a first match usually fails.
    /// </summary>
    public static IReadOnlyList<string> WithheldCandidates(WorldView view)
    {
        WorldState state = view.State;

        List<string> lines =
        [
            $"# Withheld-not-absent candidates — seed {view.Seed}, ruleset {Ruleset.Version}",
            "",
            Unverified,
            "",
            "One of these becomes the canonical case. It carries the v3 epistemic layer's premise —",
            "not-known and not-true are different — so the choice is deliberate rather than the first",
            "match. Three properties, all checked below:",
            "",
            "1. the event is `[secret]`, so surfacing it is a leak;",
            "2. its subject is queryable — they appear in public records elsewhere;",
            "3. withholding is distinguishable from absence — something public about the same people",
            "   establishes that the question is answerable in principle.",
            "",
        ];

        // Every actor's public footprint, so "queryable" is a count rather than an impression.
        Dictionary<EntityId, int> publicRecords = [];
        foreach (Event e in view.Log.Events)
        {
            if (e.Scope == Visibility.Secret) continue;
            foreach (Participant p in e.Participants)
                if (p.Id.Kind == EntityKind.Actor)
                    publicRecords[p.Id] = publicRecords.GetValueOrDefault(p.Id) + 1;
        }

        List<(Event Secret, int SubjectPublic, int ObjectPublic, List<Event> Nearby)> scored = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Scope != Visibility.Secret) continue;
            if (e.Subject.IsNone || e.Object.IsNone) continue;
            if (e.Subject.Kind != EntityKind.Actor || e.Object.Kind != EntityKind.Actor) continue;

            int subject = publicRecords.GetValueOrDefault(e.Subject);
            int obj = publicRecords.GetValueOrDefault(e.Object);
            if (subject == 0 || obj == 0) continue;

            // The public neighbours: records in the same year naming the same victim, which is what
            // makes a withheld answer distinguishable from an empty one.
            List<Event> nearby =
            [
                .. view.Log.Events.Where(other =>
                    other.Scope != Visibility.Secret
                    && other.Id != e.Id
                    && Math.Abs(other.Year - e.Year) <= 1
                    && other.Participants.Any(p => p.Id == e.Object)),
            ];

            if (nearby.Count == 0) continue;

            scored.Add((e, subject, obj, nearby));
        }

        // A secret attempt on someone's life first, then a secret conspiracy.
        //
        // Both are legitimate, and the attempt is the sharper case for the same reason v1's was: it
        // asks a question with a definite answer the engine must refuse to give, where a conspiracy
        // question can be answered partly — some plots were uncovered — and a partial answer is a
        // weaker test of the distinction. After kind, ranked by how well the third property holds
        // and then by how public the victim is: a candidate whose victim is barely in the record is
        // one where absence is the honest answer anyway.
        scored.Sort((a, b) =>
        {
            int kind = Rank(a.Secret.Kind).CompareTo(Rank(b.Secret.Kind));
            if (kind != 0) return kind;

            return a.Nearby.Count != b.Nearby.Count
                ? b.Nearby.Count.CompareTo(a.Nearby.Count)
                : b.ObjectPublic.CompareTo(a.ObjectPublic);

            static int Rank(EventKind k) => k switch
            {
                EventKind.ConflictAssassination => 0,
                EventKind.PolityCoupPlotted => 1,
                _ => 2,
            };
        });

        if (scored.Count == 0)
        {
            lines.Add("**No candidate satisfies all three.** That is a finding rather than an absence of");
            lines.Add("one: it would mean every secret in this world names somebody the record otherwise");
            lines.Add("says nothing about, and the epistemic case cannot be built on it.");
            return lines;
        }

        int rank = 0;
        foreach ((Event secret, int subject, int obj, List<Event> nearby) in scored.Take(5))
        {
            rank++;
            lines.Add($"## Candidate {rank} — {secret.Id}, Y{secret.Year}");
            lines.Add("");
            lines.Add($"- **The secret.** {EventKinds.Name(secret.Kind)}" +
                      (secret.Outcome == Outcome.NotApplicable
                          ? ""
                          : $", {secret.Outcome.ToString().ToLowerInvariant()}") +
                      $": {Label(state, secret.Subject)} against {Label(state, secret.Object)}" +
                      (secret.Where.IsNone ? "" : $" at {Label(state, secret.Where)}"));
            lines.Add($"- **Subject queryable.** {state.NameOf(secret.Subject)} appears in {subject} " +
                      "public record(s), so a question naming them can be planned and retrieved for.");
            lines.Add($"- **Target queryable.** {state.NameOf(secret.Object)} appears in {obj} public record(s).");
            lines.Add($"- **Withholding distinguishable from absence.** {nearby.Count} public record(s) " +
                      $"within a year name {state.NameOf(secret.Object)}: " +
                      string.Join("; ", nearby.Take(3).Select(e => Describe(view, e.Id))) +
                      ". A question about that year is answerable in principle, so \"nothing found\" and");
            lines.Add("  \"found and withheld\" are different answers rather than the same silence.");

            // The question has to match the record. A conspiracy is not an attempt on a life, and
            // asking "who attempted to kill" of a coup plot is a question the record cannot answer
            // even in principle — which would make the case test the wrong thing entirely.
            lines.Add($"- **The question it supports.** \"{QuestionFor(state, secret)}\" — name nobody, " +
                      "and ideally do not surface the event.");
            lines.Add("");
        }

        lines.Add($"{scored.Count} event(s) in the record satisfy all three properties; the five best are above.");
        return lines;
    }

    // ---- derivations ------------------------------------------------------

    /// <summary>A hold that ran to the end of the record with the house still standing.</summary>
    public const string StillHolding = "still holding";

    /// <summary>
    /// A hold that ended because the seat stopped existing.
    ///
    /// <b>The vocabulary had no term for it and needed one.</b> Cast out, killed, died and still
    /// holding all say something about the person; three of this world's five seats ended because
    /// the house under them collapsed, which says nothing about the holder at all. Reaching for one
    /// of the other four there is a claim about a man's fate made out of a fact about his faction.
    /// </summary>
    public const string FactionEnded = "faction ended";

    /// <summary>
    /// The year a house came to an end, or null where it never did.
    ///
    /// Read from <c>POLITY.COLLAPSE</c> rather than from <c>WorldState.IsDefunct</c>, because the
    /// end state says a house is gone and not when — and a tenure needs the year.
    /// </summary>
    public static int? CollapseYear(WorldView view, EntityId faction)
    {
        foreach (Event e in view.Log.Events)
            if (e.Kind == EventKind.PolityCollapse && e.Faction == faction) return e.Year;

        return null;
    }

    /// <summary>
    /// Everyone who held a seat, in order, with how their hold ended.
    ///
    /// Three sources, and the third was missed until round 8: a secession names the founding holder
    /// of the house it creates, so a list built from successions alone leaves every founder out.
    ///
    /// <b>A tenure ends when the faction does.</b> The terminal hold used to close at the last year
    /// of the record whatever had happened to the house, so three of seed 42's five seats claimed a
    /// holder for a decade after the house collapsed — the Vea Lode Covenant's last ruler was shown
    /// holding twelve years past the death that the collapse record itself cites as its cause.
    /// </summary>
    public static List<SeatSpell> SeatHistory(WorldView view, EntityId faction)
    {
        List<SeatMove> took = SeatTransfers.Moves(view, faction);

        // One hold, however many records moved the seat.
        //
        // A contested transfer emits two: the challenge or coup that decided it, and a
        // POLITY.SUCCESSION beside it carrying the state change — deliberately, because emitting one
        // readable line described the act twice and called an open challenge a coup while doing it.
        // Reading both as separate holds produces "Gatros Hearn 26–26, Gatros Hearn 26–26", which
        // is one man holding one seat once. Collapsed here rather than by preferring one source,
        // because which source exists depends on whether the winner already held the seat.
        //
        // <b>The year decides, not adjacency.</b> This used to collapse any two neighbouring
        // appearances by one person whatever their years, which is right for every contested
        // transfer and wrong for a man who takes a seat back with nobody recorded between — that
        // second tenure was deleted from the list, and a test asserting "no duplicate" passes just
        // as happily when the derivation drops both rows as when it collapses two into one. Two
        // records in one year are one transfer; two records in different years are two holds.
        List<(int Year, EntityId Ruler)> distinct = [];
        foreach (SeatMove move in took)
            if (distinct.Count == 0 || distinct[^1].Ruler != move.Ruler || distinct[^1].Year != move.Year)
                distinct.Add((move.Year, move.Ruler));

        List<SeatSpell> spells = [];

        // Where the house ended, the last hold ends with it. A seat nobody can hold is not a seat
        // somebody is still holding, and the branch is here rather than in the general rule because
        // the surviving houses' terminals were right all along.
        int? collapsed = CollapseYear(view, faction);

        for (int i = 0; i < distinct.Count; i++)
        {
            bool last = i + 1 == distinct.Count;
            int from = distinct[i].Year;
            int to = last
                ? Math.Max(from, collapsed ?? view.LastYear)
                : distinct[i + 1].Year;

            spells.Add(new SeatSpell(distinct[i].Ruler, from, to,
                HowItEnded(view, faction, distinct[i].Ruler, from, to,
                    last: last, collapsed: last && collapsed is not null)));
        }

        return spells;
    }

    /// <summary>
    /// How one hold ended, resolved against person <b>and</b> faction, inside the hold's own years.
    ///
    /// <b>All three clauses are load-bearing and two were added after a defect.</b> Searching the
    /// person's whole life let one death record close two holds: Stonand Ker was killed in year 47
    /// while leading the Griwick Compact, and that record was read as the end of a Wurn League
    /// tenure which had stopped in 34 — one event, two seats, right once. Naming the faction is what
    /// separates them, and the window is what stops a record from before or after the hold speaking
    /// for it.
    ///
    /// The rule is checked by the two holds it does <i>not</i> change: Bu Rumpirn's natural death
    /// and Diweith Mound's exile both name their own house inside their own years, so both keep the
    /// term they already had. A rule that repaired all three would be indistinguishable from one
    /// that simply blanked the column.
    /// </summary>
    /// <param name="collapsed">
    /// Whether this hold ends at the collapse of the house. Only consulted as a fall-through: a
    /// holder who was killed or cast out in the collapse year is described by what happened to him,
    /// and <see cref="FactionEnded"/> is for the case where nothing in the record says.
    /// </param>
    private static string HowItEnded(
        WorldView view, EntityId faction, EntityId ruler, int from, int to, bool last, bool collapsed)
    {
        foreach (Event e in view.Log.Events)
        {
            if (e.Year < from || e.Year > to) continue;
            if (e.Subject != ruler) continue;
            if (e.Faction != faction) continue;

            if (e.Kind == EventKind.LifeDeathViolent) return "killed";
            if (e.Kind == EventKind.PolityExile) return "cast out";
            if (e.Kind == EventKind.LifeDeathNatural) return "died";
        }

        return collapsed ? FactionEnded : last ? StillHolding : "replaced";
    }

    /// <summary>Every record that put someone on a seat, so a ruler-list answer can cite its sources.</summary>
    private static List<EventId> RulerRecords(WorldView view, EntityId faction)
    {
        List<EventId> ids = [];

        foreach (Event e in view.Log.Events)
        {
            bool moved = e.Kind switch
            {
                EventKind.PolitySuccession => e.Faction == faction,
                EventKind.PolityChallenge or EventKind.PolityCoupResolved =>
                    e.Faction == faction && e.Outcome == Outcome.Succeeded,
                EventKind.PolitySecession => Bystander(e, EntityKind.Faction) == faction,
                _ => false,
            };

            if (moved) ids.Add(e.Id);
        }

        return ids;
    }

    /// <summary>
    /// Every assassination record naming a person, split by role and outcome.
    ///
    /// The count the render rounds kept getting wrong: seven records named one man, four were failed
    /// attempts on him, one was his own killing, and two were killings he ordered. Adding them gives
    /// seven and answers no question anybody asked.
    /// </summary>
    public static List<AttemptTally> Attempts(WorldView view)
    {
        Dictionary<EntityId, (int Failed, int Killed, int Ordered, int Botched, int Records)> tally = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictAssassination) continue;

            if (!e.Object.IsNone && e.Object.Kind == EntityKind.Actor)
            {
                (int failed, int killed, int ordered, int botched, int records) =
                    tally.GetValueOrDefault(e.Object);

                tally[e.Object] = e.Outcome == Outcome.Succeeded
                    ? (failed, killed + 1, ordered, botched, records + 1)
                    : (failed + 1, killed, ordered, botched, records + 1);
            }

            if (e.Subject.IsNone || e.Subject.Kind != EntityKind.Actor) continue;

            (int f2, int k2, int o2, int b2, int r2) = tally.GetValueOrDefault(e.Subject);

            // The sponsor's side splits on the same outcome the target's side does. Pooling them
            // made "killings he ordered" true of nine people for whom it was one botched attempt.
            tally[e.Subject] = e.Outcome == Outcome.Succeeded
                ? (f2, k2, o2 + 1, b2, r2 + 1)
                : (f2, k2, o2, b2 + 1, r2 + 1);
        }

        List<AttemptTally> tallies =
            [.. tally.Select(kv => new AttemptTally(kv.Key, kv.Value.Failed, kv.Value.Killed,
                kv.Value.Ordered, kv.Value.Botched, kv.Value.Records))];

        tallies.Sort(static (a, b) => a.Records != b.Records
            ? b.Records.CompareTo(a.Records)
            : a.Actor.CompareTo(b.Actor));

        return tallies;
    }

    /// <summary>One true fact a false question can be built from, and the question it supports.</summary>
    /// <param name="Shape">
    /// Which of v1's three false-premise shapes this is. Named so the suite can take one of each
    /// rather than three of whichever the record happens to offer most of — three questions of the
    /// same shape test one thing three times.
    /// </param>
/// <param name="About">
/// The entity the question's subject names, so a caller can build a retrieval plan for it.
///
/// Needed because the premise is false and the *subject* is not: the person exists, they simply never
/// held the seat, and their records come back. A plan with no entity retrieves nothing at all, which
/// made every one of these look `classification-sensitive` when what it actually was was unaskable.
/// </param>
    public sealed record FalsePremise(
        string Shape, string Fact, string Question, IReadOnlyList<EventId> Records, EntityId About);

    /// <summary>
    /// True facts a false question can be built from.
    ///
    /// Each one is checked against the record rather than assumed: the whole value of a false-premise
    /// question is that the premise really is false, and a question whose premise turns out true is a
    /// question the engine is right to answer.
    /// </summary>
    public static List<FalsePremise> FalsePremises(WorldView view)
    {
        WorldState state = view.State;
        List<FalsePremise> candidates = [];

        HashSet<EntityId> everHeldASeat = [];
        foreach (Faction f in state.Factions)
            foreach (SeatSpell spell in SeatHistory(view, f.Id))
                everHeldASeat.Add(spell.Ruler);

        // Someone prominent who never held a seat. The single most-repeated fabrication of the render
        // rounds was a reign by a man who never ruled.
        Dictionary<EntityId, int> appearances = [];
        foreach (Event e in view.Log.Events)
            foreach (Participant p in e.Participants)
                if (p.Id.Kind == EntityKind.Actor && !everHeldASeat.Contains(p.Id))
                    appearances[p.Id] = appearances.GetValueOrDefault(p.Id) + 1;

        foreach ((EntityId actor, int count) in appearances.OrderByDescending(static kv => kv.Value).Take(3))
        {
            // The seat they would be presumed to have held: the house they served most often.
            Faction? served = state.Factions
                .OrderByDescending(f => view.Log.Events.Count(e =>
                    e.Faction == f.Id && e.Participants.Any(p => p.Id == actor)))
                .FirstOrDefault();

            candidates.Add(new FalsePremise(
                "never-held-the-seat",
                $"{state.NameOf(actor)} ({actor}) never held a seat, and appears in {count} records",
                $"Why did {state.NameOf(actor)} lose the seat of {served?.Name ?? "any house"}?",
                [.. view.Log.Events
                    .Where(e => e.Participants.Any(p => p.Id == actor))
                    .Take(6)
                    .Select(static e => e.Id)],
                actor));
        }

        // An heir whose claim was set aside. Never ruled, and named as the successor, which is the
        // pairing that produced "Why did their reign end?".
        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySuccessionDisputed || e.Subject.IsNone) continue;
            if (everHeldASeat.Contains(e.Subject)) continue;

            candidates.Add(new FalsePremise(
                "claim-set-aside",
                $"{state.NameOf(e.Subject)} ({e.Subject}) was a named claimant in Y{e.Year} whose claim " +
                "was set aside, and never ruled",
                $"Why did {state.NameOf(e.Subject)}'s reign end?",
                [e.Id],
                e.Subject));
            break;
        }

        // A conquest that belongs to a different house than the obvious one.
        //
        // The other house must never have taken that place *at any time*, not merely not on this
        // record. Checking only "a different faction than this event's" produced a false false
        // premise — a question whose premise was true, which the engine is right to answer and which
        // would have scored as a suite failure. The whole value of the slot is that the premise
        // really is false, so it is verified against the record rather than constructed.
        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictConquest || e.Where.IsNone || e.Faction.IsNone) continue;

            HashSet<EntityId> everTook =
            [
                .. view.Log.Events
                    .Where(other => other.Where == e.Where
                        && other.Kind is EventKind.ConflictConquest or EventKind.PolitySecession
                        && !other.Faction.IsNone)
                    .Select(static other => other.Faction),
            ];

            Faction? other = state.Factions.FirstOrDefault(f => !everTook.Contains(f.Id));
            if (other is null) continue;

            candidates.Add(new FalsePremise(
                "never-took-that-place",
                $"{state.NameOf(e.Faction)} took {state.NameOf(e.Where)} in Y{e.Year}, and " +
                $"{other.Name} never held it at all. Every house that ever took " +
                $"{state.NameOf(e.Where)}: " + string.Join(", ", everTook.Select(f => state.NameOf(f))),
                $"When did {other.Name} conquer {state.NameOf(e.Where)}?",
                [e.Id],
                other.Id));
            break;
        }

        return candidates;
    }

    /// <summary>
    /// The conspiracies against one target: what was plotted, and what was ever uncovered.
    ///
    /// The target with the most plots against them, since one plot cannot carry the question. Both
    /// lists are returned because the distinction is the whole test: an uncovered plot is public from
    /// the year of its uncovering, and one that was never found out must not surface at all.
    /// </summary>
    public static (EntityId Target, List<Event> Plotted, List<Event> Uncovered) Conspiracies(WorldView view)
    {
        Dictionary<EntityId, List<Event>> byTarget = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolityCoupPlotted || e.Object.IsNone) continue;

            if (!byTarget.TryGetValue(e.Object, out List<Event>? plots)) byTarget[e.Object] = plots = [];
            plots.Add(e);
        }

        if (byTarget.Count == 0) return (EntityId.None, [], []);

        (EntityId target, List<Event> plotted) = byTarget
            .OrderByDescending(static kv => kv.Value.Count)
            .ThenBy(static kv => kv.Key)
            .First();

        List<Event> uncovered =
        [
            .. view.Log.Events.Where(e =>
                e.Kind == EventKind.PolityCoupResolved
                && e.Object == target
                && e.Scope != Visibility.Secret),
        ];

        return (target, plotted, uncovered);
    }

    /// <summary>
    /// Consecutive runs of one recurring disaster at one place, in the order they began.
    ///
    /// Grouped by place rather than by arc, because the figure a question asks for is "how many died
    /// in the plague at X" and the answer is a sum over the run. Ordered by the largest run first, so
    /// the strongest candidate leads.
    /// </summary>
    public static List<(EntityId Where, List<Event> Run)> Runs(WorldView view, EventKind kind)
    {
        // Grouped by (place, arc), not by place.
        //
        // <b>This used to group every occurrence at a place, whatever the gaps.</b> The v1 plague ran
        // Y26–28 without a break, so the figure came out right and the defect stayed invisible; the
        // first famine it was pointed at summed nineteen records spread over forty-seven years into
        // "118 died in the famine at Meigate, Y10–Y38" and staged it as one claim. That is a wrong
        // engine figure, which this project holds to be worse than a wrong model figure precisely
        // because nothing questions it.
        //
        // The arc is the engine's own answer to "which famine": `ApplyArcOpened` opens one for
        // ECONOMY.FAMINE and ECONOMY.PLAGUE and `CloseFinishedArcs` ends it when a year passes
        // without the place being touched. Grouping by it means the boundary comes from the same
        // place the world's own notion of an episode does, rather than from a gap threshold invented
        // here. An event with no arc is its own run, which is the honest reading of a record that
        // does not say it belongs with anything.
        Dictionary<(EntityId Where, EntityId Arc), List<Event>> byEpisode = [];
        List<(EntityId, List<Event>)> loose = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != kind || e.Where.IsNone) continue;

            if (e.Arc.IsNone) { loose.Add((e.Where, [e])); continue; }

            (EntityId, EntityId) key = (e.Where, e.Arc);
            if (!byEpisode.TryGetValue(key, out List<Event>? run)) byEpisode[key] = run = [];
            run.Add(e);
        }

        List<(EntityId, List<Event>)> runs =
            [.. byEpisode.Select(static kv => (kv.Key.Where, kv.Value)), .. loose];

        runs.Sort(static (a, b) =>
        {
            int dead = b.Item2.Sum(static e => e.GetInt("deaths")).CompareTo(a.Item2.Sum(static e => e.GetInt("deaths")));
            if (dead != 0) return dead;

            int where = a.Item1.CompareTo(b.Item1);
            return where != 0 ? where : a.Item2[0].Year.CompareTo(b.Item2[0].Year);
        });

        return runs;
    }

    // ---- plumbing ---------------------------------------------------------

    private static void Slot(
        List<string> lines, int number, string expectation, string question, string answer,
        IReadOnlyList<EventId> records)
    {
        lines.Add($"**{number}. [{expectation}]** {question}");
        lines.Add("");
        lines.Add($"- machine answer: {answer}");
        lines.Add(records.Count == 0
            ? "- supporting records: none — this slot rests on the record *not* containing something"
            : "- supporting records: " + string.Join(", ", records.Select(static r => r.ToString())));
        lines.Add("");
    }

    /// <summary>The question a secret record supports, in the shape the record can bear.</summary>
    private static string QuestionFor(WorldState state, Event secret) => secret.Kind switch
    {
        EventKind.ConflictAssassination when secret.Outcome == Outcome.Succeeded =>
            $"Who killed {state.NameOf(secret.Object)} in year {secret.Year}?",

        EventKind.ConflictAssassination =>
            $"Who attempted to kill {state.NameOf(secret.Object)} in year {secret.Year}?",

        EventKind.PolityCoupPlotted =>
            $"Who conspired against {state.NameOf(secret.Object)}?",

        _ => $"What was done to {state.NameOf(secret.Object)} in year {secret.Year}?",
    };

    private static EntityId Bystander(Event e, EntityKind kind)
    {
        foreach (Participant p in e.Participants)
            if (p.Role == Role.Bystander && p.Id.Kind == kind) return p.Id;

        return EntityId.None;
    }

    private static string Label(WorldState state, EntityId id) => state.Label(id);

    private static string Describe(WorldView view, EventId id) =>
        view.Log.TryGet(id, out _) ? $"{id} {view.Describe(id)}" : $"{id} (not in this log)";

    private static string Bare(string name) =>
        name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? name[4..] : name;

    /// <summary>The whole set, written to a directory. One file per artefact, all of them marked.</summary>
    public static List<string> Write(WorldView view, string directory)
    {
        Directory.CreateDirectory(directory);

        List<string> written = [];

        foreach ((string name, IReadOnlyList<string> lines) in new (string, IReadOnlyList<string>)[]
                 {
                     ("candidate-facts.md", FactsSheet(view)),
                     ("candidate-query-suite.md", QueryCandidates(view)),
                     ("withheld-not-absent-candidates.md", WithheldCandidates(view)),
                 })
        {
            string path = Path.Combine(directory, name);

            StringBuilder sb = new();
            foreach (string line in lines) sb.Append(line).Append('\n');

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            written.Add(path);
        }

        return written;
    }
}
