using System.Globalization;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Core.Rules;

/// <summary>
/// How a relation ends. One file, for every kind, deliberately.
///
/// <b>The defect this exists to close.</b> `RelationKind.Trade` was created by two rules and
/// removed by none — peak equalled final on all five panel seeds — and `POLITY.COLLAPSE` removed
/// nothing at all, so a destroyed house kept its allies and its trading partners for the rest of
/// the world's history. State that only ever goes up trends toward everything connected to
/// everything, and a fully connected relation graph carries no information. That is a correctness
/// defect whether or not anything renders it.
///
/// <b>Why a capability rather than a trade rule.</b> Whatever ends a trade tie is the mechanism
/// that ends any tie. The monotonic sweep will almost certainly find more of these, and a
/// per-kind mechanism means writing this again with a different name each time.
///
/// <b>The invariant.</b> A relation ends only inside an event that names its ending. There are two
/// shapes of that and both live here:
///
/// <list type="bullet">
/// <item><see cref="OnItsOwnEvent"/> — nothing in the log yet says this tie ended, so the ending
/// gets the event <see cref="Names"/> gives it, and that event carries the severance.</item>
/// <item><see cref="Into"/> — the event already being emitted *is* the ending. A house that has
/// just been recorded as finished does not need a second event to say its obligations are
/// finished too; it needs its own event to say how many and of what kinds.</item>
/// </list>
///
/// <b>Termination is distinct from never-having-existed, and the distinction lives in the log.</b>
/// <see cref="RelationGraph"/> keeps live edges only. A tombstone there would make every one of
/// the forty-odd `Has`/`ValueOf`/`From`/`To` call sites ambiguous between "is there a live edge"
/// and "is there an edge", which is the absent-versus-unknown conflation arriving through a
/// different door and into the subsystem that was supposed to be the clean one. What makes the
/// record a sufficient answer is the invariant above: if every ending is named by an event, then
/// "were these two ever allied" is answerable from the log with no inference at all.
/// </summary>
public static class RelationEnds
{
    /// <summary>A border closed by a declaration of war.</summary>
    public const string War = "war";

    /// <summary>Nothing has moved this tie for <see cref="DisusedAfterYears"/> years.</summary>
    public const string Disuse = "disuse";

    /// <summary>One of the two houses no longer exists.</summary>
    public const string Collapse = "collapse";

    /// <summary>
    /// Taken at random, on a schedule matched to the war arm. Never emitted by a real world —
    /// see <see cref="TerminationArm.RandomTrade"/>.
    /// </summary>
    public const string Random = "random";

    /// <summary>
    /// Years without a single dealing before a trade tie is considered abandoned.
    ///
    /// <b>Twenty, argued from the cadence of use and the length of a reign</b>, and the argument
    /// was written down and committed before the first ruleset-6 world existed
    /// (`docs/brief-step-two-design.md`). The recency guard forbids the same pair repeating a
    /// pact inside five years, so an active relationship refreshes on a five-year-or-longer
    /// cadence and twenty is four consecutive missed opportunities — a tie in ordinary use never
    /// reaches it. And an arrangement between two houses should outlive a gap without outliving a
    /// generation: <see cref="SimConfig.OldAge"/> is 55 and a reign runs about two decades, so a
    /// tie whose maker is dead and unremembered is exactly what §0 of the parent phase objects to.
    ///
    /// <b>A timeout rather than a decay, and that is not a detail.</b> Decaying the edge value
    /// would move what every consumer of it sees in every year — `ProposeAlliance` scores an
    /// approach partly as `Trade / 2` — which is a large diffuse behavioural change riding inside
    /// a step whose whole subject is whether a tie exists. It would also have written into the
    /// yearly drift row from the first year a tie existed, diverging the log a decade before the
    /// first termination and spending the only mechanical guard this step has.
    /// </summary>
    public const int DisusedAfterYears = 20;

    /// <summary>
    /// The event that names the end of a relation of this kind, or null where no such event
    /// exists yet.
    ///
    /// <b>A kind with no entry cannot be terminated.</b> That is the point of the table rather
    /// than an omission in it: the way this defect comes back is a rule that deletes an edge
    /// inline because no event existed to carry it, which is precisely how a `RelDel` on the war
    /// declaration came to be the only trace of fifteen broken alliances.
    /// </summary>
    public static EventKind? Names(RelationKind kind) => kind switch
    {
        RelationKind.Trade => EventKind.EconomyTradeCollapse,
        RelationKind.Alliance => EventKind.DiploAllianceBroken,
        RelationKind.AtWar => EventKind.DiploPeaceSigned,
        _ => null,
    };

    /// <summary>
    /// The kinds a house's ending takes with it: obligations between houses, not memory of what
    /// they did.
    ///
    /// An obligation needs two parties to hold it and one of them is gone. A fact does not stop
    /// being a fact, and memory is the engine's whole reason for having a grievance edge — a
    /// grudge against a house that no longer exists is exactly the sort of thing this world is
    /// supposed to remember. So <c>Kin</c> and <c>Marriage</c> stay (they are facts about people,
    /// who outlive their house), <c>Fealty</c> stays (actor to actor, and both actors are alive),
    /// <c>Grievance</c> stays, and <c>Rivalry</c> stays because no rule reads or writes it at all.
    /// </summary>
    public static readonly RelationKind[] Obligations =
    [
        RelationKind.Alliance,
        RelationKind.Trade,
        RelationKind.Vassal,
        RelationKind.AtWar,
    ];

    /// <summary>
    /// Ends a live tie in its own event, in both directions, and returns that event.
    ///
    /// Returns null and emits nothing where the tie is not live — a caller may reasonably not
    /// know, and a rule that has to check first is a rule that will one day forget to.
    ///
    /// The edge is read before the reducer is allowed near it. <see cref="Relation.CreatedYear"/>
    /// and <see cref="Relation.Value"/> are gone the moment the fold applies the severance, and
    /// they are the two things that make the record say what was lost rather than only that
    /// something was.
    /// </summary>
    public static Event? OnItsOwnEvent(
        Tick tick, EntityId a, EntityId b, RelationKind kind, string cause, EventId because)
    {
        if (Names(kind) is not { } names)
        {
            throw new InvalidOperationException(
                $"no event names the end of a {kind} relation. Add one to RelationEnds.Names " +
                "before writing a rule that ends this kind — a tie that ends with nothing saying " +
                "so is the defect this file exists to close.");
        }

        WorldState state = tick.State;
        Relation? tie = state.Relations.Find(a, b, kind) ?? state.Relations.Find(b, a, kind);
        if (tie is null) return null;

        EventDraft draft = new EventDraft(names)
            .By(a)
            .Object(b)
            .At(b.Kind == EntityKind.Faction ? state.FactionOf(b).Seat : EntityId.None)
            .Set(RelationTrajectory.CauseField, cause)
            .Set("made", tie.CreatedYear)
            .Set("held", tick.Year - tie.CreatedYear)
            .Set("worth", tie.Value)
            .RelDel(a, b, kind)
            .RelDel(b, a, kind)
            .Because(because)
            // What made the tie, not what last touched it. Relation.Cause is set once, at
            // creation, so an ending points at its own beginning — the same design step one used
            // for alliance breaks, and for the same reason: an origin that does not resolve in
            // the log is left off entirely rather than replaced with a plausible one.
            .Because(tie.Cause)
            .Weight(Significance.Minor);

        return tick.Emit(draft);
    }

    /// <summary>
    /// Carries every obligation a house held into the event that is already recording its end.
    ///
    /// <b>One event, and it is the collapse itself.</b> Not per-relation events: a house dying
    /// with twelve edges would emit twelve, three of them a trade collapse between a dead house
    /// and somebody who decided nothing, and a collapse year would go from one readable line to
    /// thirteen. Not a separate cleanup event either: `POLITY.COLLAPSE` already says this house
    /// is finished and already disposes of its ground and its people in its own payload, and a
    /// second event beside it is a bookkeeping row wearing a history event's clothes.
    ///
    /// <b>The count and the kinds, never a bare total.</b> A collapse that silently drops twelve
    /// edges is the invisible-transition defect this phase exists to repair, and "relations
    /// cleared" is an unlabelled figure — which is the same defect with a number in front of it.
    /// </summary>
    public static void Into(EventDraft draft, WorldState state, EntityId faction)
    {
        SortedDictionary<RelationKind, int> counts = [];

        foreach (Relation r in state.Relations.Touching(faction))
        {
            if (Array.IndexOf(Obligations, r.Key.Kind) < 0) continue;

            draft.RelDel(r.Key.From, r.Key.To, r.Key.Kind);
            counts[r.Key.Kind] = counts.GetValueOrDefault(r.Key.Kind) + 1;
        }

        if (counts.Count == 0) return;

        int total = 0;
        List<string> kinds = [];
        foreach ((RelationKind kind, int n) in counts)
        {
            total += n;
            kinds.Add($"{kind}:{n.ToString(CultureInfo.InvariantCulture)}");
        }

        draft.Set(RelationTrajectory.CauseField, Collapse)
             .Set("tiesEnded", total)
             .Set("tiesEndedKinds", string.Join(",", kinds));
    }

    /// <summary>
    /// Ends every trade tie nothing has moved for <see cref="DisusedAfterYears"/> years.
    ///
    /// Draws nothing from the stream and reads no roll, so a world with no abandoned tie in it is
    /// bit-identical to the world the previous ruleset produced. That is what makes §5's
    /// first-divergence check say something.
    /// </summary>
    public static void EndDisusedTrade(Tick tick)
    {
        WorldState state = tick.State;

        // Snapshot: ending a tie removes edges from the graph, and the adjacency lists this walks
        // are the ones the removal edits.
        List<Relation> ties = [];
        foreach (Relation r in state.Relations.All)
        {
            if (r.Key.Kind != RelationKind.Trade) continue;
            if (tick.Year - r.LastChangedYear < DisusedAfterYears) continue;

            // One severance per pair. The edge is written in both directions and both sides of it
            // fall out of use together.
            if (r.Key.From.CompareTo(r.Key.To) > 0) continue;
            ties.Add(r);
        }

        foreach (Relation tie in ties)
        {
            // A house that no longer holds ground has already had its obligations ended by its
            // collapse; anything left pointing at it is not a live arrangement to abandon.
            if (state.IsDefunct(tie.Key.From) || state.IsDefunct(tie.Key.To)) continue;

            OnItsOwnEvent(tick, tie.Key.From, tie.Key.To, RelationKind.Trade, Disuse, tie.LastCause);
        }
    }

    /// <summary>
    /// Removes the trade ties this year's schedule calls for, chosen uniformly at random.
    ///
    /// <b>The discriminating arm, and not a rule.</b> It exists so that "the war rule damages
    /// histories" can be told apart from "this world is knife-edge on losing trade ties at all" —
    /// two explanations that make the same prediction about a war-versus-null contrast and have
    /// entirely different fixes.
    ///
    /// Drawn on <see cref="RngPurpose.Control"/>, which no rule may read, so the substitution
    /// consumes nothing the rules are consuming. Candidates are taken in the graph's own key
    /// order, which is a property of the data rather than of insertion history, so the choice is
    /// reproducible from (seed, year) alone.
    /// </summary>
    public static void RemoveScheduledAtRandom(Tick tick)
    {
        RandomTieSchedule? schedule = tick.RandomTies;
        if (schedule is null) return;

        int due = schedule.DueIn(tick.Year);
        if (due == 0) return;

        Rng rng = tick.Rng(RngPurpose.Control).Branch(tick.Year);

        for (int i = 0; i < due; i++)
        {
            List<Relation> live = [];
            foreach (Relation r in tick.State.Relations.All)
            {
                if (r.Key.Kind != RelationKind.Trade) continue;
                if (r.Key.From.CompareTo(r.Key.To) > 0) continue;   // one entry per tie
                if (tick.State.IsDefunct(r.Key.From) || tick.State.IsDefunct(r.Key.To)) continue;
                live.Add(r);
            }

            // A scheduled removal with nothing to remove means the arms are no longer matched.
            // Recorded rather than skipped: the run has to be able to say the treatment was not
            // delivered, because a random arm that removed fewer ties is a different experiment.
            if (live.Count == 0) { schedule.Note(removed: false); continue; }

            Relation chosen = live[rng.Next(live.Count)];
            Event? ended = OnItsOwnEvent(tick, chosen.Key.From, chosen.Key.To, RelationKind.Trade,
                Random, chosen.LastCause);

            schedule.Note(removed: ended is not null);
        }
    }
}
