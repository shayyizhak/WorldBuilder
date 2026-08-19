using System.Globalization;

namespace WorldBuilder.Core.Rules;

/// <summary>
/// How a goal transition gets into the record. One file, for every transition, deliberately — the
/// same argument <see cref="RelationEnds"/> makes: whatever records one transition is the mechanism
/// that records any of them, and a per-transition mechanism means writing this again under a
/// different name for every label the audit turned up.
///
/// <b>The invariant.</b> A goal transition happens only inside an event that names it. There are two
/// shapes and both live here:
///
/// <list type="bullet">
/// <item><see cref="Advance"/>, <see cref="Attach"/>, <see cref="End"/> — an event is already being
/// emitted and <i>is</i> the cause, so the transition rides its payload. This is
/// <see cref="RelationEnds.Into"/>'s shape and the reason is the same: a separate event beside it
/// would record the same fact twice with an arrow between them.</item>
/// <item><see cref="Form"/>, <see cref="EndWithoutAHost"/> — nothing else in the tick says this
/// happened, so the transition gets an event of its own carrying the count, the breakdown and one key
/// per goal. This is <c>POLITY.COLLAPSE</c>'s shape.</item>
/// </list>
///
/// <b>Creation has no host, which the brief did not anticipate.</b> §1.1 routes creation onto "the
/// event the goal already cites". A goal cites an event that has already been appended — often years
/// earlier — and <see cref="Event"/> is an immutable record in an append-only log, so that event
/// cannot be amended. The perception phase emits nothing else, so creation is an orphan in exactly
/// §1.2's sense and takes §1.2's shape.
///
/// <b>The off-switch runs through here too.</b> With <see cref="Tick.RecordsGoals"/> false every
/// method mutates the book directly, which is what ruleset 6 did, so the arm reproduces those logs
/// event for event and key for key.
/// </summary>
public static class GoalRecord
{
    /// <summary>The arm name a world carries in its header when goal recording is switched off.</summary>
    public const string OffArm = "record-no-goals";

    /// <summary>Payload key naming how many goals an event formed, for the count-versus-label assertion.</summary>
    public const string FormedCount = "goalsFormed";

    /// <summary>Breakdown by kind, <c>SeizeLeadership:3,Avenge:1</c>.</summary>
    public const string FormedKinds = "goalsFormedKinds";

    /// <summary>Payload key naming how many goals an event ended.</summary>
    public const string EndedCount = "goalsEnded";

    /// <summary>Breakdown by reason, <c>Expired:2,Completed:1</c>.</summary>
    public const string EndedReasons = "goalsEndedReasons";

    /// <summary>
    /// How the record carries a goal ending.
    ///
    /// <b>Three answers, not two.</b> An earlier version returned "the event kind, or null", and null
    /// meant either "the reducer folds this already" or "a rule is emitting the causing event right
    /// now" — two entirely different arrangements sharing one value, which is the ambiguous-label
    /// defect this project keeps finding in its own record. Naming all three also lets the audit say
    /// how each ending reaches the log instead of inferring it.
    /// </summary>
    public enum GoalRoute
    {
        /// <summary>
        /// The reducer already performs it while folding the event that caused it — a death, exile,
        /// defection, secession, partition or <c>disown</c>. Six sites in <see cref="EventReducer"/>,
        /// and a rule that also recorded it would be recording it twice.
        /// </summary>
        Folded = 0,

        /// <summary>
        /// A rule is emitting the event that ends the goal, and <see cref="End"/> puts the key on that
        /// draft. The conquest, the alliance, the coup, the homecoming, the killing.
        /// </summary>
        Host = 1,

        /// <summary>
        /// Nothing else says it happened, so it gets a <see cref="EventKind.GoalsEnded"/> row of its
        /// own carrying the count and the reasons.
        /// </summary>
        OwnEvent = 2,
    }

    /// <summary>
    /// The route every ending takes into the record.
    ///
    /// <b>A switch expression with no discard arm, and that is the point.</b> Warnings are errors in
    /// this build, so adding a <see cref="GoalEnd"/> without giving it a route here fails the build
    /// rather than passing silently — the compile-time half of the guard §2 asks for, and the direct
    /// analogue of <see cref="RelationEnds.Names"/> throwing for a relation kind with no event.
    /// </summary>
    // CS8524 only — the arm for values the enum has no name for, which a GoalEnd cast from a stray
    // integer would be. Adding a discard arm to silence it would silence CS8509 with it, and CS8509
    // is the whole mechanism: it is the error a *new named* GoalEnd raises when nobody has given it a
    // route. Suppressing the one keeps the other, which is why this is a pragma and not a `_ =>`.
#pragma warning disable CS8524
    public static GoalRoute Route(GoalEnd why) => why switch
    {
        GoalEnd.OwnerDead => GoalRoute.Folded,
        GoalEnd.OwnerExiled => GoalRoute.Folded,
        GoalEnd.OwnerDefected => GoalRoute.Folded,
        GoalEnd.OwnerDisowned => GoalRoute.Folded,
        GoalEnd.OwnerTookASeat => GoalRoute.Folded,

        GoalEnd.Achieved => GoalRoute.Host,
        GoalEnd.Spent => GoalRoute.Host,

        GoalEnd.Completed => GoalRoute.OwnEvent,
        GoalEnd.Expired => GoalRoute.OwnEvent,
        GoalEnd.OwnerDeadAtRetirement => GoalRoute.OwnEvent,
        GoalEnd.AlreadySatisfied => GoalRoute.OwnEvent,
        GoalEnd.TargetDefunct => GoalRoute.OwnEvent,
        GoalEnd.TargetDead => GoalRoute.OwnEvent,
        GoalEnd.TargetInvalid => GoalRoute.OwnEvent,
        GoalEnd.OwnerLeftFaction => GoalRoute.OwnEvent,
    };
#pragma warning restore CS8524

    /// <summary>The event kind an ending gets to itself, or null where it rides something.</summary>
    public static EventKind? Names(GoalEnd why) =>
        Route(why) == GoalRoute.OwnEvent ? EventKind.GoalsEnded : null;

    // ---- hosted transitions ------------------------------------------------

    /// <summary>Moves a goal's progress, on the draft that caused it.</summary>
    public static void Advance(Tick tick, EventDraft host, Goal goal, int delta, GoalStep step)
    {
        if (tick.RecordsGoals) host.GoalStep(goal, delta, step);
        else tick.State.Goals.Advance(goal, delta, step);
    }

    /// <summary>Binds a goal to the storyline it has spawned, on the draft that opens it.</summary>
    public static void Attach(Tick tick, EventDraft host, Goal goal, EntityId arc)
    {
        if (tick.RecordsGoals) host.GoalArc(goal, arc);
        else tick.State.Goals.Attach(goal, arc);
    }

    /// <summary>
    /// Ends a goal on the draft of the event that ended it.
    ///
    /// <b>Refuses an ending that has an event of its own</b>, so an orphan reason cannot be quietly
    /// attached to a passing host and lose its count, and a folded reason cannot be recorded twice.
    ///
    /// <b>Skips a goal the book no longer holds, and says so.</b> Fifteen removals across the panel
    /// name a goal something else already cleared — a challenger who lost an open challenge is exiled
    /// by <c>SettleCoup</c>, the reducer clears his ambition, and the rules then end it again as
    /// <c>Spent</c>. Emitting a key for it would make the fold refuse the log. The §1 audit counted
    /// those fifteen as endings; they are not.
    /// </summary>
    public static void End(Tick tick, EventDraft host, Goal goal, GoalEnd why)
    {
        if (Route(why) != GoalRoute.Host)
        {
            throw new InvalidOperationException(
                $"a {why} goal ending is routed {Route(why)}, so it must not ride a host event. " +
                "Either the reducer already performs it and recording it here would record it twice, " +
                "or it needs a GOALS.ENDED row of its own — see GoalRecord.Route.");
        }

        if (!tick.State.Goals.Holds(goal))
        {
            tick.State.Goals.Remove(goal, why);   // notifies the census as Vanished; changes nothing
            return;
        }

        if (tick.RecordsGoals) host.GoalEnd(goal, why);
        else tick.State.Goals.Remove(goal, why);
    }

    // ---- orphans -----------------------------------------------------------

    /// <summary>One goal the perception phase has decided should exist.</summary>
    public readonly record struct Proposal(
        EntityId Owner, GoalKind Kind, EntityId Target, int ExpiresYear, EventId Cause);

    /// <summary>
    /// Forms the goals a phase has decided on, in one event.
    ///
    /// Emits nothing when the list is empty, so a quiet year adds no row.
    /// </summary>
    public static void Form(Tick tick, List<Proposal> proposals)
    {
        if (proposals.Count == 0) return;

        if (!tick.RecordsGoals)
        {
            foreach (Proposal p in proposals)
            {
                tick.State.Goals.Add(
                    p.Owner, p.Kind, p.Target, tick.Year, p.ExpiresYear - tick.Year, p.Cause);
            }

            return;
        }

        EventDraft draft = new EventDraft(EventKind.GoalsFormed)
            .Weight(Significance.Bookkeeping)
            .Set(FormedCount, proposals.Count)
            .Set(FormedKinds, Tally(proposals.Select(static p => p.Kind.ToString())));

        // No participants, no arc and no causes. All three are deliberate and the third is the one
        // that had to be found out.
        //
        // Participants would put these rows into Log.ForEntity, which four cooldowns and six
        // cause-finders walk; every one of them filters on EventKind first, so it would be safe today
        // and one kind-agnostic scan away from not being. An arc would be worse than unsafe:
        // CloseFinishedArcs is the one log read in the rules that does not filter by kind, and a
        // bookkeeping row carrying an arc would keep a famine alive.
        //
        // <b>Causes are the interesting omission.</b> Citing each goal's own cause here is the obvious
        // thing to write and it is wrong: it adds an edge from a real event to a bookkeeping row, and
        // the causal-variety metrics Layer 1 asserts are counts over exactly those edges. Five seeds'
        // pinned chain-shape figures moved the moment these rows carried causes. That is the defect
        // `PerceptionPhase.LatestCauseFor` already documents from the other side — a cause that is
        // technically true and manufactures "the long lifecycle-shaped chains that made the depth look
        // real". The cause is not lost: it is inside the `goalAdd` payload, where the fold reads it and
        // no traversal counts it.
        for (int i = 0; i < proposals.Count; i++)
        {
            Proposal p = proposals[i];
            draft.GoalAdd(i, p.Owner, p.Kind, p.Target, p.ExpiresYear, p.Cause);
        }

        tick.Emit(draft);
    }

    /// <summary>
    /// Ends a single goal that has nothing to name it — an action-phase guard finding its target gone.
    ///
    /// One event per occurrence rather than a batch at the end of the phase, because the goal has to
    /// leave the book at the moment the guard fires: the resolution phase runs later in the same tick
    /// and looks goals up by owner and kind. A batch would leave a goal standing that the rules had
    /// already dropped, and a fold that agreed with it would be agreeing with the wrong world.
    /// </summary>
    public static void Lapse(Tick tick, Goal goal, GoalEnd why) =>
        EndWithoutAHost(tick, [(goal, why)]);

    /// <summary>
    /// Ends goals that nothing else in the record accounts for, in one event.
    ///
    /// <b>Reads the reasons off the goals before the reducer is allowed near them</b>, the same
    /// ordering <see cref="RelationEnds.OnItsOwnEvent"/> needs: the book no longer holds them once the
    /// fold applies the endings, and the breakdown is what makes the record say what was lost rather
    /// than only that something was.
    /// </summary>
    public static void EndWithoutAHost(Tick tick, List<(Goal Goal, GoalEnd Why)> endings)
    {
        if (endings.Count == 0) return;

        foreach ((Goal goal, GoalEnd why) in endings)
        {
            if (Route(why) == GoalRoute.OwnEvent) continue;

            throw new InvalidOperationException(
                $"a {why} goal ending is routed {Route(why)}, so it must not get a " +
                $"{EventKinds.Name(EventKind.GoalsEnded)} row of its own. See GoalRecord.Route.");
        }

        if (!tick.RecordsGoals)
        {
            foreach ((Goal goal, GoalEnd why) in endings) tick.State.Goals.Remove(goal, why);
            return;
        }

        EventDraft draft = new EventDraft(EventKind.GoalsEnded)
            .Weight(Significance.Bookkeeping)
            .Set(EndedCount, endings.Count)
            .Set(EndedReasons, Tally(endings.Select(static e => e.Why.ToString())));

        // No causes, for the reason given in Form: an edge from a real event to a bookkeeping row is
        // an edge the causal-variety metrics count.
        foreach ((Goal goal, GoalEnd why) in endings) draft.GoalEnd(goal, why);

        tick.Emit(draft);
    }

    /// <summary>
    /// <c>Expired:2,Completed:1</c> — sorted, so the string is a property of the content rather than
    /// of iteration order.
    ///
    /// A bare total is an unlabelled figure, which this project treats as a fabrication vector
    /// regardless of who reads it next. The label and the total are asserted to agree.
    /// </summary>
    private static string Tally(IEnumerable<string> names)
    {
        SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (string name in names) counts[name] = counts.GetValueOrDefault(name) + 1;

        List<string> parts = [];
        foreach ((string name, int n) in counts)
            parts.Add($"{name}:{n.ToString(CultureInfo.InvariantCulture)}");

        return string.Join(",", parts);
    }
}
