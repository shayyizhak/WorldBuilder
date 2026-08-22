using System.Globalization;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>What a pack is a view of. Determines the prompt and how the passage is framed.</summary>
public enum PackKind
{
    SingleEvent,
    Year,
    Reign,
    War,
    FactionArc,
    CausalChain,
}

/// <summary>
/// The complete input to one render: the events, the people and places they name, and the
/// causal edges between them. Nothing outside a pack may appear in the passage, which is what
/// makes fabrication checkable — the pack is both the prompt source and the ground truth.
///
/// Packs are kept terse on purpose. Prompt evaluation on the reference machine runs at roughly
/// 25 tokens per second, so every token costs about forty milliseconds before generation even
/// starts; the engine's own log lines are already compact and greppable, so they are fed almost
/// as they stand rather than expanded into verbose JSON.
/// </summary>
public sealed class ContextPack
{
    public required PackKind Kind { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<EventId> Events { get; init; }
    public required int FromYear { get; init; }
    public required int ToYear { get; init; }

    /// <summary>
    /// The first year the world itself records.
    ///
    /// Distinct from <see cref="FromYear"/>, and the difference is what separates a section
    /// boundary from the edge of knowledge. A twenty-year window opening in 24 may perfectly
    /// well say its ruler took the seat in 23 — the boundary is an editorial cut, not a fact.
    /// A claim of a seat held "since year 1" is different in kind: nothing anywhere records it,
    /// and it has now reached canon twice.
    /// </summary>
    public required int WorldFromYear { get; init; }

    /// <summary>Entities named by the events, in first-appearance order.</summary>
    public required IReadOnlyList<EntityId> Cast { get; init; }

    /// <summary>
    /// What the bookkeeping rows behind these events establish, as state — see
    /// <see cref="PackCauses"/>. Separate from <see cref="Events"/> and deliberately so.
    ///
    /// These carry no ids and are not records. A pack that folded them into the event list
    /// would be inviting the passage to narrate a measurement, which is the failure the render
    /// filter exists to prevent; a pack that drops them throws away the explanation retrieval
    /// worked to find. They are a third thing and are shaped as one.
    /// </summary>
    public required IReadOnlyList<string> Causes { get; init; }

    /// <summary>Stable identity for caching: same events, same facts, same passage.</summary>
    public required string Key { get; init; }

    /// <summary>
    /// A hash of everything the model is actually shown for this pack.
    ///
    /// <see cref="Key"/> answers "which events is this about" and deliberately survives an engine
    /// that renumbers them. That is the right identity for a scope and the wrong one for a cache,
    /// because a pack is not only its events: it also carries figures the engine computed over
    /// them. Change how a statistic is derived and the events are untouched, the key is
    /// unchanged, and the model would be handed different facts while the cache serves the
    /// passage written for the old ones — a cached render restating a figure that is now wrong,
    /// permanently, because cached renders are canon.
    ///
    /// So the cache keys on the inputs rather than on a version number. A ruleset bump that
    /// touches no pack invalidates nothing, which matters: the cache is the LoRA corpus and the
    /// cost lever, and a coarse rule throws that away on every release. A change to a computed
    /// figure invalidates exactly the packs whose figures moved.
    /// </summary>
    public required string InputHash { get; init; }

    /// <summary>Every literal a passage is allowed to contain, for the fabrication check.</summary>
    public required IReadOnlyList<string> Vocabulary { get; init; }

    /// <summary>
    /// Pairs of people who appear together in at least one event, as lower-cased surnames.
    /// A passage may only assert a relationship between two people who actually met in the
    /// record — otherwise adjacent facts get joined into causal chains that never happened.
    /// </summary>
    public required IReadOnlySet<string> ActorPairs { get; init; }

    /// <summary>
    /// Ordered pairs "predecessor|successor" taken from the seat history, plus every surname
    /// that ever held the seat. A passage may only say A was succeeded by B where B actually
    /// followed A — the one hard fabrication that survived two fix rounds was a man who never
    /// held the seat being described as having been succeeded from it.
    /// </summary>
    public required IReadOnlySet<string> SuccessionPairs { get; init; }
    public required IReadOnlySet<string> SeatHolders { get; init; }

    /// <summary>
    /// For each person whose rule ended inside this period, the surname of whoever ended it —
    /// the killer, or the man who took the seat. Empty string where nobody did: a natural
    /// death, or a rule the house itself ended by casting the holder out.
    ///
    /// Anchors the one claim a proper-noun check cannot see. "The rule of Dreld ended when he
    /// was beaten in an open challenge by Meastouth" names two real people who really met, in a
    /// year they really met, and is false: Meastouth lost that challenge and Dreld ruled two
    /// more years. Only the seat history knows which of them ended the other.
    /// </summary>
    public required IReadOnlyDictionary<string, string> RuleEnders { get; init; }

    /// <summary>
    /// Everything that was raided in these records, as lower-cased phrases — the place, and the
    /// full name of whoever held it at the time.
    ///
    /// Direction is a particular, and it has now been got wrong in three places. A raid the
    /// subject launched and lost was rendered as a raid *against* the subject that it repelled,
    /// which turns a legitimacy-losing failure into a success and makes the secession it caused
    /// look arbitrary. Naming what was actually raided makes the inversion checkable.
    /// </summary>
    public required IReadOnlySet<string> RaidTargets { get; init; }

    /// <summary>
    /// Short forms that do not identify anything here, because two powers in this pack end in
    /// the same word — "the Compact" where both a Griwick and a Kebarrow Compact appear.
    /// Accurate and unreadable is still a failure, and the reader who guesses wrong has no way
    /// to find out.
    /// </summary>
    public required IReadOnlySet<string> AmbiguousShortNames { get; init; }

    /// <summary>
    /// The naming instruction for this pack, or empty where no two powers collide. Kept as
    /// finished text because a prohibition on its own did not work: told never to write "the
    /// Compact" the model went on writing it, because it needed *something* shorter than the
    /// full name and had not been given one. Offering the permitted short form is what changed
    /// the behaviour.
    /// </summary>
    public required string NamingNote { get; init; }

    /// <summary>
    /// What the records support, indexed by act and year — see <see cref="ClaimIndex"/>. This
    /// is what lets the checker validate a statement rather than a vocabulary.
    /// </summary>
    public required ClaimIndex Claims { get; init; }

    /// <summary>
    /// The distinguishing words of every power named in this pack — "kebarrow", "wurn" — with
    /// the kind-of-polity boilerplate stripped. Lets a check ask "does this sentence name a
    /// different power" without matching on "compact".
    /// </summary>
    public required IReadOnlySet<string> PowerWords { get; init; }

    /// <summary>What the events add up to, counted by the engine so the model need not.</summary>
    public required PackDigest Digest { get; init; }

    public required string Body { get; init; }

    public int TokenEstimate => Body.Length / 4;

    public IReadOnlyList<Tenure> Tenures() => Digest.Tenures;
}

/// <summary>
/// One person holding one seat over one stretch of years. The unit a reign scope is built on,
/// because a person is not one.
/// </summary>
public sealed record ReignSpell(EntityId Ruler, EntityId Faction, int From, int To)
{
    public int Years => Math.Max(0, To - From);
}

/// <summary>Builds packs from the log. Read-only with respect to world state, always.</summary>
public static class ContextPackBuilder
{
    /// <summary>
    /// The earliest year any passage could speak about.
    ///
    /// Not <see cref="WorldView.FirstYear"/>, which is year 1 and holds a single bookkeeping row
    /// creating the world. No pack ever contains it, so a section dating a tenure "since year 1"
    /// is citing something it was never shown — and did so twice.
    /// </summary>
    private static int FirstNarratableYear(WorldView view)
    {
        foreach (Event e in view.Log.Events)
            if (IsRenderable(e)) return e.Year;

        return view.FirstYear;
    }

    /// <summary>
    /// May this event reach a rendered passage at all?
    ///
    /// Two exclusions, for different reasons. Bookkeeping is the yearly accounting — the
    /// harvest tally, the drift of standing — which is real state but not history; letting it
    /// into a pack produced passages opening on "a harvest count at Meigate revealed a grain
    /// shortage", faithfully narrating a spreadsheet row.
    ///
    /// Secret is the more serious one. The log flags conspiracies and unattributed killings as
    /// hidden, and the renderer was narrating them as public knowledge — permanently, because
    /// accepted renders are canon. That would leave v3's epistemic layer unpicking leaks baked
    /// into the world's own text.
    /// </summary>
    public static bool IsRenderable(Event e) =>
        e.Significance >= Significance.Minor && e.Scope != Visibility.Secret;

    /// <summary>
    /// May this event be retrieved to answer a question?
    ///
    /// Narrower than <see cref="IsRenderable"/> in one direction and wider in the other, because
    /// the two rules it conflates are different rules.
    ///
    /// <b>Secrecy is absolute.</b> A query must not become a side channel around the visibility
    /// the chronicle respects: a section that declines to narrate a conspiracy is worth nothing
    /// if "who conspired against him" answers it.
    ///
    /// <b>Bookkeeping is retrievable.</b> The yearly accounts are not history and have no place
    /// in a chronicle, but they are where the causes live — roughly a third of the record, and
    /// most of the economy's influence on everything else. A query layer reading the narratable
    /// view would answer "why was there a famine" with its political consequences and none of
    /// the harvest that caused it, which is the same mistake as reading the <c>.log</c> for a
    /// measurement and has already been made three times in this project.
    /// </summary>
    public static bool IsRetrievable(Event e) => e.Scope != Visibility.Secret;

    /// <summary>
    /// The world coming into existence: a place sited, a house founded, a person born into the
    /// opening cast.
    ///
    /// <b>A stopping condition rather than an explanation.</b> These rows have no causes of their
    /// own — nothing precedes the world — so a causal walk that steps into one has reached the end
    /// of what the record can say, and reporting the genesis row as the answer dresses that up as a
    /// finding. *Because the place exists* is not why a war was declared over it.
    /// </summary>
    public static bool IsGenesis(Event e) => e.Kind
        is EventKind.GenesisWorld or EventKind.GenesisPlace
        or EventKind.GenesisFaction or EventKind.GenesisActor;

    public static ContextPack Single(WorldView view, EventId id)
    {
        Event e = view.Log.Get(id);

        List<EventId> events = [];
        foreach (EventId cause in Antecedents(view.Log, id)) events.Add(cause);
        events.Add(id);

        return Build(view, PackKind.SingleEvent, $"{EventKinds.Name(e.Kind)} in year {e.Year}", events);
    }

    /// <summary>
    /// The renderable causes of an event, seeing through hidden ones. Where a cause is
    /// bookkeeping or secret, its own causes are taken in its place — so the chain of
    /// explanation survives without the unnarratable link appearing in the prose.
    /// </summary>
    private static List<EventId> Antecedents(EventLog log, EventId id, int depth = 0)
    {
        List<EventId> found = [];
        if (depth > 4) return found;

        foreach (EventId cause in log.Get(id).Causes)
        {
            if (IsRenderable(log.Get(cause))) found.Add(cause);
            else found.AddRange(Antecedents(log, cause, depth + 1));
        }
        return found;
    }

    /// <summary>
    /// A pack over an explicit set of events. Used by the query layer, where retrieval has
    /// already decided what is relevant and the pack is simply the carrier.
    ///
    /// <b>Retrieval's work is not undone here.</b> This re-applied <see cref="IsRenderable"/> and
    /// discarded every bookkeeping row retrieval had gone to the record to find — the
    /// <c>.log</c>-versus-record error made permanent in a pipeline stage, with retrieval reading
    /// the record and the pack re-imposing the readable view over the top of it. Asked why Hadale
    /// broke away, retrieval returned the two rows recording the Compact's standing eroding away
    /// and the pack dropped both before the model saw either.
    ///
    /// So the rows are separated rather than filtered: the narratable records become the event
    /// list, and the rest become causes — what they established, not the rows themselves.
    /// </summary>
    public static ContextPack FromEvents(
        WorldView view, PackKind kind, string title, IReadOnlyList<EventId> events,
        EntityId subject = default, int from = int.MinValue, int to = int.MaxValue)
    {
        List<EventId> renderable = [];
        List<EventId> bookkeeping = [];

        foreach (EventId id in events)
        {
            Event e = view.Log.Get(id);

            // Secrecy still excludes outright, whatever else this does. A row that must not be
            // narrated must equally not be summarised into a statement of state, which would
            // leak it in a form no proper-noun check would catch.
            if (!IsRetrievable(e)) continue;

            (IsRenderable(e) ? renderable : bookkeeping).Add(id);
        }

        renderable.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        bookkeeping.Sort(static (a, b) => a.Value.CompareTo(b.Value));

        // The window spans everything retrieved, causes included. Taking it from the narratable
        // records alone dates a period by a subset of what produced it.
        List<EventId> all = [.. renderable, .. bookkeeping];
        all.Sort(static (a, b) => a.Value.CompareTo(b.Value));

        int first = from != int.MinValue ? from
            : all.Count > 0 ? view.Log.Get(all[0]).Year : view.FirstYear;
        int last = to != int.MaxValue ? to
            : all.Count > 0 ? view.Log.Get(all[^1]).Year : view.LastYear;

        return Build(view, kind, title, renderable, first, last, subject, bookkeeping, structured: true);
    }

    public static ContextPack Year(WorldView view, int year)
    {
        List<EventId> events = [];
        foreach (Event e in view.Log.Events)
            if (e.Year == year && IsRenderable(e)) events.Add(e.Id);

        return Build(view, PackKind.Year, $"the year {year}", events, year, year);
    }

    /// <summary>
    /// Every seat this person ever held, as a separate spell.
    ///
    /// A reign is not a property of a person. Heth Fal held the Kebarrow seat from 33 to 35,
    /// was cast out, took service elsewhere and held the Laehiford seat from 39 — and keyed on
    /// the actor alone, that came out as one scope which rendered the Kebarrow reign under the
    /// Laehiford title and pulled in Laehiford's plague, raids and appointments to describe it.
    /// The unit is (person, seat, from, to), and a man with two seats has two of them.
    /// </summary>
    public static List<ReignSpell> Reigns(WorldView view, EntityId ruler)
    {
        List<ReignSpell> found = [];

        foreach ((EntityId faction, List<Tenure> spells) in PackDigest.AllSeatHistories(view))
        {
            foreach (Tenure t in spells)
            {
                if (t.HolderId != ruler) continue;
                found.Add(new ReignSpell(ruler, faction, t.From, t.To));
            }
        }

        found.Sort(static (a, b) => a.From != b.From ? a.From.CompareTo(b.From) : a.Faction.CompareTo(b.Faction));
        return found;
    }

    /// <summary>One spell in one seat: everything that faction did while this person held it.</summary>
    public static ContextPack Reign(WorldView view, ReignSpell spell)
    {
        WorldState state = view.State;

        // Filtered to the faction of *this* spell. Without the faction filter the scope was
        // every event its subject appeared in, which for a man who changed houses is two
        // careers told as one.
        List<EventId> events = [];
        foreach (Event e in view.Log.Events)
        {
            if (e.Year < spell.From || e.Year > spell.To) continue;
            if (!IsRenderable(e)) continue;

            bool relevant = false;
            foreach (Participant p in e.Participants)
                if (p.Id == spell.Faction) relevant = true;

            // The ruler's own doings count only while he is acting within this seat's affairs;
            // an event of his that never touches the faction belongs to neither reign.
            if (relevant) events.Add(e.Id);
        }

        string title = $"the rule of {state.Label(spell.Ruler)} over {state.NameOf(spell.Faction)}";
        return Build(view, PackKind.Reign, title, events, spell.From, spell.To, spell.Faction);
    }

    /// <summary>Everything tagged to one arc — a war, a famine, a conspiracy.</summary>
    public static ContextPack Arc(WorldView view, EntityId arc)
    {
        Arc a = view.State.ArcOf(arc);
        List<EventId> events = [];
        foreach (Event e in view.Log.Events)
            if (e.Arc == arc && IsRenderable(e)) events.Add(e.Id);

        PackKind kind = a.Kind == ArcKind.War ? PackKind.War : PackKind.FactionArc;
        return Build(view, kind, a.Name, events, a.StartYear, a.EndYear ?? view.LastYear);
    }

    /// <summary>
    /// A faction's history, optionally narrowed to one stretch of years.
    ///
    /// The range exists because a long-lived power accumulates well over a hundred events, and
    /// asked to compress all of them at once the model returns a single unbroken block —
    /// no amount of instruction to paragraph it worked. Split into eras it produces properly
    /// shaped sections, and each passage has a scope it can actually hold.
    /// </summary>
    public static ContextPack Faction(WorldView view, EntityId faction, int fromYear = int.MinValue, int toYear = int.MaxValue)
    {
        List<EventId> events = [];
        foreach (EventId id in view.Log.ForEntity(faction))
        {
            Event e = view.Log.Get(id);
            if (e.Year < fromYear || e.Year > toYear) continue;
            if (e.Significance >= Significance.Major && IsRenderable(e)) events.Add(id);
        }

        // When a window was asked for, that window *is* the period — not the span of whichever
        // events happen to fall inside it. Deriving it from the events is why a section headed
        // 22–41 opened by calling itself seventeen years long.
        int from = fromYear != int.MinValue ? fromYear
            : events.Count > 0 ? view.Log.Get(events[0]).Year : view.FirstYear;
        int to = toYear != int.MaxValue ? toYear
            : events.Count > 0 ? view.Log.Get(events[^1]).Year : view.LastYear;

        return Build(view, PackKind.FactionArc, $"the story of {view.State.Label(faction)}", events, from, to, faction);
    }

    /// <summary>
    /// Everything that led to an event, for a question rather than for a chronicle.
    ///
    /// The same walk as <see cref="Chain"/> with one difference that matters: it keeps the
    /// bookkeeping rows instead of stepping over them. A chronicle is right to skip them — a
    /// section opening on "a harvest count at Meigate revealed a grain shortage" is narrating a
    /// spreadsheet — but "why was there a famine" is a question about the harvest, and the
    /// chronicle's walk answers it with everything except the harvest.
    ///
    /// Secret events end a branch rather than being stepped over. Seeing through a secret cause
    /// to its own causes is how a hidden thing leaks: the conclusion arrives without the link,
    /// and the reader can infer what was removed from the shape of what is left.
    ///
    /// <b>Genesis rows end a branch too, and are not included.</b> This walk keeps bookkeeping,
    /// which is what makes "why was there a famine" answerable from the harvest — but a genesis row
    /// is bookkeeping of a different sort: it is the world beginning, it has no causes of its own,
    /// and returning it as the last link presents a stopping condition as an explanation. Two of
    /// seed 42's four staged causal questions walked back to <c>GENESIS.PLACE</c> — Threi Cut coming
    /// into existence — and answered *why was war declared over it* with *because it is there*.
    ///
    /// Dropped from the retrieval rather than from the record. The edge is true: the war was fought
    /// in pursuit of a goal formed because that place existed, and <c>PerceptionPhase</c> says so
    /// deliberately. What is wrong is offering it as an answer, so the fix belongs where the chain
    /// is read and not where it is written.
    /// </summary>
    public static List<EventId> Trace(WorldView view, EventId tip, int maxDepth = 24)
    {
        List<EventId> ordered = [];
        HashSet<EventId> seen = [];
        Walk(tip);

        ordered.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        return ordered;

        void Walk(EventId id)
        {
            if (ordered.Count >= maxDepth || !seen.Add(id)) return;

            Event e = view.Log.Get(id);
            if (!IsRetrievable(e)) return;

            // The tip itself is what was asked about, so it is kept even where it is a genesis row —
            // "when did Threi Cut come into existence" is a fair question with a real answer. It is
            // only as a *cause* that a genesis row says nothing.
            if (id != tip && IsGenesis(e)) return;

            ordered.Add(id);
            foreach (EventId cause in e.Causes) Walk(cause);
        }
    }

    /// <summary>
    /// A causal chain, oldest first. The most valuable unit: the engine already knows these
    /// events belong together and in what order, which is precisely the connective tissue a
    /// flat log cannot show.
    /// </summary>
    public static ContextPack Chain(WorldView view, EventId tip, int maxDepth = 24)
    {
        List<EventId> ordered = [];
        HashSet<EventId> seen = [];
        Walk(tip);

        ordered.Sort(static (a, b) => a.Value.CompareTo(b.Value));

        int from = ordered.Count > 0 ? view.Log.Get(ordered[0]).Year : 0;
        int to = ordered.Count > 0 ? view.Log.Get(ordered[^1]).Year : 0;

        return Build(view, PackKind.CausalChain, $"the events leading to {tip}", ordered, from, to);

        void Walk(EventId id)
        {
            if (ordered.Count >= maxDepth || !seen.Add(id)) return;
            if (IsRenderable(view.Log.Get(id))) ordered.Add(id);
            foreach (EventId cause in view.Log.Get(id).Causes) Walk(cause);
        }
    }

    // ---- assembly ---------------------------------------------------------

    /// <summary>
    /// <paramref name="subject"/> is what the statistics are computed about — a faction for a
    /// faction arc, the ruler's house for a reign. Left empty where no single subject owns the
    /// period, in which case no figures are offered rather than wrong ones.
    /// </summary>
    /// <param name="causeRows">
    /// Bookkeeping the pack should carry as state rather than as records. Empty for a chronicle,
    /// which has no business printing any of it.
    /// </param>
    /// <param name="structured">
    /// Whether event lines carry their role and outcome as fields.
    ///
    /// On for a query and off for a chronicle, which is not a hedge. A chronicle's job is prose
    /// and the fields would end up in it — the pack has been copied verbatim into a passage
    /// before, brackets and all. An answer's job is to be right about which of two men did the
    /// thing, and role is not reliably recoverable from a sentence: five of the seven records
    /// about Paernmel Has have him as the target and two have him ordering the killing, and
    /// reading that off the text yields five, which is wrong and looks right.
    /// </param>
    private static ContextPack Build(
        WorldView view, PackKind kind, string title, List<EventId> events,
        int from = 0, int to = 0, EntityId subject = default,
        IReadOnlyList<EventId>? causeRows = null, bool structured = false)
    {
        WorldState state = view.State;

        List<EntityId> cast = [];
        HashSet<EntityId> castSeen = [];
        List<string> vocabulary = [];
        HashSet<string> vocabSeen = new(StringComparer.OrdinalIgnoreCase);

        foreach (EventId id in events)
        {
            Event e = view.Log.Get(id);
            if (from == 0 && to == 0) { from = e.Year; to = e.Year; }
            from = Math.Min(from, e.Year);
            to = Math.Max(to, e.Year);

            foreach (Participant p in e.Participants)
                if (castSeen.Add(p.Id)) cast.Add(p.Id);
        }

        HashSet<EventId> included = [.. events];

        StringBuilder body = new();

        // Flagged as end-state, because it is. Titles and who holds what are read from the
        // final world, not from the years these events cover — without the warning the model
        // asserts a place's present owner as though it had always held it.
        body.Append("PEOPLE, PLACES AND POWERS NAMED BELOW\n");
        foreach (EntityId id in cast) body.Append("  ").Append(Describe(state, id)).Append('\n');

        // Where two powers here end in the same word, the short form stops identifying either
        // of them. A war section used "the Compact" throughout for the Griwick Compact, in a
        // document where every other section meant Kebarrow by it — the prose was accurate and
        // unreadable at the same time, which is the worse failure of the two.
        HashSet<string> ambiguous = [];
        string? clash = Ambiguity(state, cast, ambiguous);
        if (clash is not null) body.Append(clash);

        body.Append("\nEVENTS (oldest first)\n");
        int previousYear = int.MinValue;

        foreach (EventId id in events)
        {
            Event e = view.Log.Get(id);

            // Gaps were supplied here so the model would not have to compute them. It used them
            // to write relative expressions anyway and still got them wrong — "the following
            // year" for two events in the same year. Every event carries its own absolute date
            // and the prompt now forbids relative time entirely, so the markers are gone.
            previousYear = e.Year;

            // Plain numerals. The pack used the log's own "[Y0027]" and the model copied it
            // verbatim into prose, so one section said "Y0027" while the next said "in 27".
            body.Append("  ").Append(id).Append(" [year ")
                .Append(e.Year.ToString(CultureInfo.InvariantCulture))
                .Append("] ").Append(view.Describe(id));

            // Only causes the pack actually carries. Citing one it filtered out gave the model a
            // reference it could not look up, and rather than omit it the model wrote connective
            // prose to bridge the gap — invention caused by a hole in its own input.
            List<EventId> citable = [];
            foreach (EventId cause in e.Causes)
                if (included.Contains(cause)) citable.Add(cause);

            if (citable.Count > 0)
            {
                body.Append("  (because ");
                for (int i = 0; i < citable.Count; i++)
                {
                    if (i > 0) body.Append(", ");
                    body.Append(citable[i]);
                }
                body.Append(')');
            }

            if (structured) body.Append(QueryFacts.Fields(view, e, subject));
            body.Append('\n');
        }

        // What the bookkeeping behind these records establishes, kept apart from them.
        //
        // The separation is structural rather than a matter of wording, and has to be: a
        // heading the model reads as a second event list is one prompt revision away from
        // being narrated, and a narrated measurement is how "a harvest count at Meigate
        // revealed a grain shortage" reached canon. These lines carry no record id, so there
        // is nothing here to cite, and each is a state rather than an occurrence, so there is
        // nothing here to date. Both properties are load-bearing.
        List<string> causes = causeRows is null or { Count: 0 }
            ? []
            : PackCauses.Notes(view, causeRows, Relevant(cast, subject));

        if (causes.Count > 0)
        {
            body.Append("\nHOW THINGS STOOD — the conditions behind the records above. These are\n")
                .Append("measurements, not events. Nothing here happened on a day and nothing here\n")
                .Append("can be cited. Use them to explain what the records show; never narrate one\n")
                .Append("as though it were something that occurred.\n");

            foreach (string note in causes) body.Append("  · ").Append(note).Append('\n');
        }

        // The vocabulary is the fabrication oracle, and it is derived from the finished body
        // rather than rebuilt from the parts. Assembling it separately let the two drift: the
        // dossiers named a faction the vocabulary did not contain, so the checker reported a
        // fabrication for a word the model had been handed. Checker and model see one universe.
        // Statistics are about the subject over the window, computed from the whole log — not
        // from this pack's narratable subset, which would inherit every exclusion the render
        // filter applies and report figures that are confidently wrong.
        //
        // A query pack counts over its own records instead, for two reasons. The digest has no
        // secrecy filter — it never needed one while its only caller counted for a chronicle
        // built from the same walk — and counting from the log would let a figure include the
        // very record retrieval withheld. And an answer states its figures at the scope they
        // were computed for: five records under a total of seven is a contradiction the reader
        // can see and cannot resolve.
        List<EventId> counted = [.. events, .. causeRows ?? []];
        counted.Sort(static (a, b) => a.Value.CompareTo(b.Value));

        PackDigest digest = subject.IsNone
            ? PackDigest.Empty(from, to)
            : structured
                ? PackDigest.Of(view, subject, from, to, counted)
                : PackDigest.Of(view, subject, from, to);

        // Appended before the vocabulary is derived, so every figure the model is invited to
        // state is also a figure the fabrication check will accept.
        body.Append(structured ? digest.ToQueryBlock() : digest.ToPromptBlock());
        if (structured) body.Append(QueryFacts.Block(view, events, subject));

        string bodyText = body.ToString();
        AddWords(bodyText);
        for (int y = from; y <= to; y++) AddWord(y.ToString(CultureInfo.InvariantCulture));

        return new ContextPack
        {
            Kind = kind,
            Title = title,
            Events = events,
            FromYear = from,
            ToYear = to,
            WorldFromYear = FirstNarratableYear(view),
            Cast = cast,
            Causes = causes,
            Vocabulary = vocabulary,
            Key = Key(kind, view.Log, events),
            InputHash = InputHashOf(bodyText),
            Digest = digest,
            ActorPairs = Pairs(view, events),
            SuccessionPairs = Successions(digest),
            SeatHolders = Holders(digest),
            RuleEnders = Enders(digest),
            RaidTargets = RaidedThings(view, events),
            AmbiguousShortNames = ambiguous,
            NamingNote = clash ?? "",
            Claims = ClaimIndex.Build(view, events, subject),
            PowerWords = PowersNamed(state, cast),
            Body = bodyText,
        };

        void AddWords(string text)
        {
            foreach (string word in text.Split(
                         [' ', ',', '.', ':', '(', ')', '\'', '—', '-', '?', '!', ';', '\n', '\r', '\t'],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                AddWord(word);
            }
        }

        void AddWord(string word)
        {
            if (word.Length > 0 && vocabSeen.Add(word)) vocabulary.Add(word);
        }
    }

    /// <summary>
    /// Deliberately confined to facts that do not change: a name, what kind of thing it is,
    /// and when a person lived.
    ///
    /// Titles and who holds which town are read from the *final* world, and a pack routinely
    /// spans decades before that. Including them produced exactly the anachronism you would
    /// expect — a famine in year 4 attributed to a faction that did not exist until year 49,
    /// because the town's dossier listed its eventual owner. Everything time-varying is left
    /// to the events, which carry their own dates.
    /// </summary>
    /// <summary>
    /// Which people share an event. Used to catch a relationship asserted between two names
    /// that never appear together — "Ska was murdered by Ker, who was in turn set aside by Le
    /// Vild", where Ker never held the seat and Le Vild set aside somebody else entirely.
    /// </summary>
    private static HashSet<string> Pairs(WorldView view, List<EventId> events)
    {
        HashSet<string> pairs = new(StringComparer.OrdinalIgnoreCase);

        foreach (EventId id in events)
        {
            List<string> here = Cast(view, id);

            for (int i = 0; i < here.Count; i++)
                for (int j = i + 1; j < here.Count; j++)
                    pairs.Add(Pair(here[i], here[j]));

            // Also pair across a causal edge. A ruler's death and the succession it caused are
            // two events, so the dead man and his replacement never co-occur — yet "Wul took
            // the seat after Sisrill's death" is exactly true, and flagging it was wrong.
            foreach (EventId cause in view.Log.Get(id).Causes)
            {
                if (!view.Log.TryGet(cause, out _)) continue;
                foreach (string a in here)
                    foreach (string b in Cast(view, cause))
                        if (a != b) pairs.Add(Pair(a, b));
            }
        }
        return pairs;
    }

    private static List<string> Cast(WorldView view, EventId id)
    {
        List<string> names = [];
        foreach (Participant p in view.Log.Get(id).Participants)
        {
            if (p.Id.Kind != EntityKind.Actor) continue;
            string surname = Surname(view.State.NameOf(p.Id));
            if (!names.Contains(surname)) names.Add(surname);
        }
        return names;
    }

    /// <summary>Consecutive holders of the seat, as "predecessor|successor".</summary>
    private static HashSet<string> Successions(PackDigest digest)
    {
        HashSet<string> pairs = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < digest.Tenures.Count; i++)
        {
            pairs.Add($"{Surname(digest.Tenures[i - 1].Holder)}|{Surname(digest.Tenures[i].Holder)}");
        }
        return pairs;
    }

    private static HashSet<string> Holders(PackDigest digest)
    {
        HashSet<string> holders = new(StringComparer.OrdinalIgnoreCase);
        foreach (Tenure t in digest.Tenures) holders.Add(Surname(t.Holder));
        return holders;
    }

    /// <summary>Who ended whose rule, by surname. Spells still running are not entered.</summary>
    private static Dictionary<string, string> Enders(PackDigest digest)
    {
        Dictionary<string, string> enders = new(StringComparer.OrdinalIgnoreCase);
        foreach (Tenure t in digest.Tenures)
        {
            if (t.Ended.StartsWith("still holding", StringComparison.Ordinal)) continue;
            enders[Surname(t.Holder)] = t.EndedBy.Length == 0 ? "" : Surname(t.EndedBy);
        }
        return enders;
    }

    /// <summary>
    /// What the raids in this pack were aimed at: the place itself, and the full name of the
    /// power holding it when the raid came. Both forms, because a chronicle may reasonably
    /// speak of raiding either the town or the house that owns it.
    /// </summary>
    private static HashSet<string> RaidedThings(WorldView view, List<EventId> events)
    {
        HashSet<string> targets = new(StringComparer.OrdinalIgnoreCase);

        foreach (EventId id in events)
        {
            Event e = view.Log.Get(id);
            if (e.Kind != EventKind.ConflictRaid || e.Where.IsNone) continue;

            targets.Add(Bare(view.State.NameOf(e.Where)));

            // The power the raid was actually aimed at, as recorded on the event. This is what
            // the digest names — "Laehiford in 20 against the Kebarrow Compact" — and without
            // it the checker contradicted the engine's own supplied text, reporting a passage
            // that had copied the digest correctly.
            if (!e.Object.IsNone) targets.Add(Bare(view.State.NameOf(e.Object)));

            EntityId owner = view.State.PlaceOf(e.Where).Controller;
            if (!owner.IsNone) targets.Add(Bare(view.State.NameOf(owner)));
        }
        return targets;
    }

    /// <summary>The distinguishing words of the powers this pack names.</summary>
    private static HashSet<string> PowersNamed(WorldState state, List<EntityId> cast)
    {
        HashSet<string> words = new(StringComparer.OrdinalIgnoreCase);
        foreach (EntityId id in cast)
        {
            if (id.Kind != EntityKind.Faction) continue;
            foreach (string word in Distinctive(state.NameOf(id))) words.Add(word);
        }
        return words;
    }

    /// <summary>
    /// A warning about short forms, where two powers in the cast share their last word. Null
    /// when every name is distinct on its own, so the ordinary case pays nothing.
    /// </summary>
    private static string? Ambiguity(WorldState state, List<EntityId> cast, HashSet<string> shared)
    {
        // Every power in the world, not only the ones this pack names. A section about a war
        // between the Griwick Compact and the Wurn League contains exactly one Compact, so at
        // pack scope "the Compact" looks unambiguous — and it went into a document where the
        // phrase means Kebarrow everywhere else. Ambiguity is a property of the book.
        Dictionary<string, List<string>> byLastWord = [];

        foreach (Faction f in state.Factions)
        {
            string last = f.Name[(f.Name.LastIndexOf(' ') + 1)..].ToLowerInvariant();

            if (!byLastWord.TryGetValue(last, out List<string>? sharing))
                byLastWord[last] = sharing = [];
            if (!sharing.Contains(f.Name)) sharing.Add(f.Name);
        }

        HashSet<EntityId> here = [.. cast];

        StringBuilder note = new();
        foreach ((string word, List<string> sharing) in byLastWord)
        {
            if (sharing.Count < 2) continue;

            // Only warn where this pack actually names one of them; otherwise the note is
            // advice about powers the passage will never mention.
            bool relevant = false;
            foreach (Faction f in state.Factions)
                if (here.Contains(f.Id) && sharing.Contains(f.Name)) relevant = true;
            if (!relevant) continue;

            shared.Add(word);

            note.Append("  NAMING: ").Append(sharing.Count)
                .Append(" powers here are called \"").Append(word)
                .Append("\", so \"the ").Append(word)
                .Append("\" on its own names none of them and must never appear. Use the full\n")
                .Append("  name, or the short form given here:\n");

            foreach (string name in sharing)
            {
                List<string> distinctive = Distinctive(name);
                string shortForm = distinctive.Count > 0
                    ? char.ToUpperInvariant(distinctive[0][0]) + distinctive[0][1..]
                    : name;
                note.Append("    ").Append(name).Append(" — write \"").Append(name)
                    .Append("\", or \"").Append(shortForm).Append("\" when you need it shorter\n");
            }
        }

        return note.Length == 0 ? null : note.ToString();
    }

    /// <summary>
    /// Words that describe a *kind* of polity rather than name one. Matching on these made "the
    /// Drelthorn League" — a faction that does not exist — resolve to the Wurn League on the
    /// strength of the word "league", and produce a confident answer about the wrong thing.
    /// Shared with the query layer, so the two cannot drift apart.
    /// </summary>
    public static readonly HashSet<string> GenericPolityWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "league", "compact", "crown", "commune", "republic", "covenant", "charter", "house",
        "sworn", "free", "city", "men", "assembly", "burghers", "rising", "greater", "second",
        "vale", "seat", "faction", "power", "settlement", "site", "the", "of", "a", "an",
    };

    /// <summary>
    /// The words of a name that actually pick something out — everything that is not an article
    /// or a kind-of-polity noun. Two names with no distinguishing word in common are not the
    /// same name, however much boilerplate they share.
    /// </summary>
    public static List<string> Distinctive(string phrase)
    {
        List<string> words = [];
        foreach (string word in phrase.Split([' ', '\''], StringSplitOptions.RemoveEmptyEntries))
        {
            string clean = word.Trim('.', ',', ';', ':', '’', '"').ToLowerInvariant();
            if (clean.Length <= 2 || GenericPolityWords.Contains(clean)) continue;
            words.Add(clean);
        }
        return words;
    }

    /// <summary>Lower-cased and stripped of a leading article, so "the Wurn League" matches "Wurn League".</summary>
    public static string Bare(string name)
    {
        string s = name.Trim().ToLowerInvariant();
        return s.StartsWith("the ", StringComparison.Ordinal) ? s[4..] : s;
    }

    /// <summary>
    /// What a cause statement is allowed to be about: the cast, plus the subject where it never
    /// appeared in a narratable record of its own. The yearly accounts touch every place and
    /// every power in the world, and an unfiltered reading buries the one line that explains
    /// something under thirty that explain nothing.
    /// </summary>
    private static HashSet<EntityId> Relevant(List<EntityId> cast, EntityId subject)
    {
        HashSet<EntityId> relevant = [.. cast];
        if (!subject.IsNone) relevant.Add(subject);
        return relevant;
    }

    /// <summary>Order-independent key, so "A and B" matches "B and A".</summary>
    public static string Pair(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    public static string Surname(string fullName)
    {
        int space = fullName.LastIndexOf(' ');
        return (space < 0 ? fullName : fullName[(space + 1)..]).ToLowerInvariant();
    }

    private static string Describe(WorldState state, EntityId id)
    {
        switch (id.Kind)
        {
            case EntityKind.Actor:
            {
                Actor a = state.ActorOf(id);
                string life = a.IsAlive ? $"born {a.BirthYear}" : $"{a.BirthYear}–{a.DeathYear}";
                return $"{a.Name} ({id}) — a person, {life}";
            }
            case EntityKind.Place:
            {
                Place p = state.PlaceOf(id);
                return $"{p.Name} ({id}) — a {p.Kind.ToString().ToLowerInvariant()}";
            }
            case EntityKind.Faction:
            {
                Faction f = state.FactionOf(id);
                return $"{f.Name} ({id}) — a power, succession by {f.Succession.ToString().ToLowerInvariant()}";
            }
            case EntityKind.Arc:
                return $"{state.ArcOf(id).Name} ({id})";
            default:
                return id.ToString();
        }
    }

    /// <summary>
    /// Built from each event's content-derived <see cref="Event.Key"/>, never from its position
    /// in the log.
    ///
    /// This hashed <c>EventId.Value</c> at first, which is just the row number. Any change to
    /// the engine renumbers every event, so the entire render cache was silently stranded the
    /// moment a rule changed — twelve passages describing a world whose ids had all moved. The
    /// stable identity existed for exactly this reason and was simply not being used; now a
    /// passage survives anything that does not alter the events it actually describes.
    /// </summary>
    /// <summary>
    /// The body, hashed. The body is what the prompt is built around, so hashing it covers the
    /// events, the cast, the naming note, the causes and every computed figure at once — and it
    /// cannot fall out of step with what the model saw, because it *is* what the model saw.
    ///
    /// The prompt template around it is not included, deliberately: that is what
    /// <see cref="Prompts.VersionFor"/> already keys on, and one input should be recorded once.
    /// </summary>
    public static string InputHashOf(string body)
    {
        byte[] digest = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexStringLower(digest.AsSpan(0, 8));
    }

    private static string Key(PackKind kind, EventLog log, List<EventId> events)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong h = offset ^ (ulong)kind;
        foreach (EventId id in events)
        {
            foreach (char c in log.Get(id).Key)
            {
                h ^= c;
                h *= prime;
            }
        }
        return $"{kind.ToString().ToLowerInvariant()}-{h:x16}";
    }
}
