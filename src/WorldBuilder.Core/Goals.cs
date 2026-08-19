namespace WorldBuilder.Core;

/// <summary>
/// What an actor or faction is currently trying to do. Goals are the difference between a
/// history and a list: they persist across ticks, so actions accumulate into arcs instead of
/// firing independently. An owner holds at most <see cref="GoalBook.MaxPerOwner"/> at once.
/// </summary>
public enum GoalKind : byte
{
    /// <summary>Actor wants their faction's leadership.</summary>
    SeizeLeadership = 0,
    /// <summary>Owner wants to make the target pay for something recorded in the log.</summary>
    Avenge = 1,
    /// <summary>Faction wants a place it does not control.</summary>
    TakePlace = 2,
    /// <summary>Faction is short of grain and will trade, raid or seize to fix it.</summary>
    SecureGrain = 3,
    /// <summary>Faction wants an ore site specifically — the scarcest thing in the region.</summary>
    ControlOre = 4,
    /// <summary>Faction wants a friend, usually because it is losing.</summary>
    FormAlliance = 5,
    /// <summary>Faction's legitimacy is low and the ruler knows it.</summary>
    RestoreLegitimacy = 6,
    /// <summary>Exiled actor wants back in. The engine's recurring-antagonist generator.</summary>
    ReturnFromExile = 7,
}

/// <summary>
/// Why a goal left the book. <b>Every removal names one, and the parameter is required</b>, so a
/// new removal path cannot be added without saying what it is.
///
/// <b>This is a label, not a record.</b> Naming the reason at the call site is what let the
/// lifecycle be counted at all — the alternative was a hand-written table of call sites, which is
/// the shape of artefact that has already been wrong in both directions here. It is also the
/// discriminator any future event would have to carry, which is why the labels are the distinctions
/// a reader would want rather than the branches the code happens to have: three separate reasons
/// were sharing one <c>if</c> in <c>RetireGoals</c>.
/// </summary>
public enum GoalEnd
{
    /// <summary>Progress reached 100 and the retirement sweep collected it.</summary>
    Completed = 0,

    /// <summary>The owner got the thing it wanted, and the action that got it removed the goal.</summary>
    Achieved = 1,

    /// <summary>
    /// Resolved against the owner, or resolved either way by a contest. The man has the seat or
    /// has been caught trying, and in both cases the wanting is over.
    /// </summary>
    Spent = 2,

    /// <summary>Not completed by <see cref="Goal.ExpiresYear"/>.</summary>
    Expired = 3,

    /// <summary>The owner died, and the reducer cleared them on the death event.</summary>
    OwnerDead = 4,

    /// <summary>The owner was expelled.</summary>
    OwnerExiled = 5,

    /// <summary>The owner took service with another house.</summary>
    OwnerDefected = 6,

    /// <summary>The owner was cast out by a <c>disown</c> delta.</summary>
    OwnerDisowned = 7,

    /// <summary>The owner is no longer in any house, so there is no seat to want.</summary>
    OwnerLeftFaction = 8,

    /// <summary>The owner became a ruler elsewhere — a secession or a partition made them one.</summary>
    OwnerTookASeat = 9,

    /// <summary>What the goal wanted is already so: the place is held, the alliance exists.</summary>
    AlreadySatisfied = 10,

    /// <summary>The target house holds no ground. You cannot be avenged on what is already gone.</summary>
    TargetDefunct = 11,

    /// <summary>The target person is dead.</summary>
    TargetDead = 12,

    /// <summary>The target is not the kind of entity this goal can act on.</summary>
    TargetInvalid = 13,

    /// <summary>
    /// The retirement sweep found a goal whose owner was dead.
    ///
    /// <b>Its own label rather than sharing <see cref="OwnerDead"/>, and the reason is the count.</b>
    /// The reducer clears an actor's goals on the death event, which lands earlier in the same year
    /// than the sweep runs, so this branch cannot be the one that catches a dead owner — and while
    /// the two shared a label the panel's 27 clears looked like evidence that both were live. Asserting
    /// two branches together makes the weaker one invisible; §4 of the project reference has the
    /// covert-coup version of the same mistake.
    /// </summary>
    OwnerDeadAtRetirement = 14,
}

/// <summary>
/// What moved a goal's progress. Same reasoning as <see cref="GoalEnd"/>: the advance is
/// unrecorded either way, and a label is what makes "unrecorded, at eleven sites, by these
/// actions" a measurement rather than an assertion.
///
/// <b>One label per call site and no spares.</b> The first draft of this enum carried an
/// <c>AssassinSent</c> that no site used, because an assassination advances no goal — it removes one
/// on a kill and leaves it untouched otherwise. A label with no emitter is worse than a dead branch,
/// and an audit whose own vocabulary reports a structural zero it invented is not an audit.
/// </summary>
public enum GoalStep
{
    GrainBought = 0,
    PactSigned = 1,
    LargesseGiven = 2,
    FavouriteElevated = 3,
    InsultGiven = 4,
    TributeDemanded = 5,
    RaidReturned = 6,
    WarDeclared = 7,
    AllianceRefused = 8,
    SupportCourted = 9,
    PlotFormed = 10,
}

/// <summary>
/// Why a goal the rules asked for was not created.
///
/// Counted rather than discarded. "The perception phase forms a goal" and "the perception phase
/// tries to form a goal and the book is full" are different claims about how much of the world's
/// intent is actually running, and only the first was ever visible.
/// </summary>
public enum GoalRefusal
{
    /// <summary>The owner already holds <see cref="GoalBook.MaxPerOwner"/>.</summary>
    BookFull = 0,

    /// <summary>The owner already holds this kind against this target.</summary>
    AlreadyHeld = 1,
}

/// <summary>
/// A sink that sees every goal transition. Null on every ordinary run, read by no rule, and
/// notified only after the mutation it reports — so attaching one cannot change the world, which
/// is asserted rather than assumed (<c>InstrumentationInvarianceTests</c>).
/// </summary>
public interface IGoalWatcher
{
    void Created(Goal goal);
    void Refused(EntityId owner, GoalKind kind, GoalRefusal why);
    void Advanced(Goal goal, int delta, GoalStep step);
    void Attached(Goal goal, EntityId arc);
    void Ended(Goal goal, GoalEnd why, EventId citation);

    /// <summary>
    /// A removal that found nothing to remove — the goal had already gone, cleared by something
    /// earlier in the same tick.
    ///
    /// Counted separately because the alternative is counting it as an ending, which is what the
    /// §1 audit did: <c>Remove</c> notified the watcher whether or not the book held the goal, so a
    /// challenger who lost and was exiled had his ambition cleared by the reducer and then
    /// <i>ended again</i> as <c>Spent</c> by the rules. The seemingly reassuring arithmetic
    /// — created − ended = live — could not detect it, because that identity holds by construction
    /// whatever the labels say.
    /// </summary>
    void Vanished(Goal goal, GoalEnd why);
}

public sealed class Goal
{
    public required int Id { get; init; }
    public required EntityId Owner { get; init; }
    public required GoalKind Kind { get; init; }
    public required int CreatedYear { get; init; }
    public required EventId Cause { get; init; }

    /// <summary>
    /// What the goal is about. <b>Set once, at creation.</b> Init-only rather than settable
    /// because nothing retargets a goal — which the compiler now says instead of a grep.
    /// </summary>
    public EntityId Target { get; init; }

    /// <summary>The storyline this goal spawned, if it has got that far (a plot, a war).</summary>
    public EntityId Arc { get; private set; }

    /// <summary>0..100. Actions advance it; reaching 100 completes the goal.</summary>
    public int Progress { get; private set; }

    /// <summary>Abandoned if not completed by this year, so dead goals do not clog the book.</summary>
    public required int ExpiresYear { get; init; }

    internal void Advance(int delta) => Progress += delta;
    internal void Attach(EntityId arc) => Arc = arc;
}

/// <summary>
/// The set of live goals, keyed by owner. Sorted so iteration order is deterministic.
///
/// <b>At ruleset 6 this was the one piece of standing state that was not a fold of the log</b> —
/// created here by the perception phase directly and removed from four phases, so a world replayed
/// from its record held none of them. Ruleset 7 closed that: the rules decide and the reducer applies,
/// and every mutating method here is called either from <see cref="EventReducer"/> or from
/// <see cref="Rules.GoalRecord"/>'s off-switch path. <c>docs/goalbook-phase-2-report.md</c>.
///
/// The labels every transition carries are older than the fix and outlived it. They were added to
/// measure the defect, and they turned out to be exactly what the record needs to name a transition —
/// <see cref="GoalEnd"/> is both the audit's vocabulary and the payload's.
/// </summary>
public sealed class GoalBook
{
    public const int MaxPerOwner = 2;

    private readonly SortedDictionary<EntityId, List<Goal>> _byOwner = [];
    private int _nextId = 1;

    /// <summary>
    /// Optional diagnostic sink. Null on an ordinary run and read by no rule.
    /// </summary>
    public IGoalWatcher? Watch { get; set; }

    public IReadOnlyList<Goal> For(EntityId owner) =>
        _byOwner.TryGetValue(owner, out List<Goal>? list) ? list : [];

    public bool Has(EntityId owner, GoalKind kind)
    {
        foreach (Goal g in For(owner))
            if (g.Kind == kind) return true;
        return false;
    }

    public Goal? Find(EntityId owner, GoalKind kind)
    {
        foreach (Goal g in For(owner))
            if (g.Kind == kind) return g;
        return null;
    }

    /// <summary>Whether this goal still sits in the book, or something already took it out.</summary>
    public bool Holds(Goal goal) => For(goal.Owner).Contains(goal);

    /// <summary>A live goal by its id, for the reducer applying a transition keyed on one.</summary>
    public Goal? ById(int id)
    {
        foreach (KeyValuePair<EntityId, List<Goal>> kv in _byOwner)
            foreach (Goal g in kv.Value)
                if (g.Id == id) return g;
        return null;
    }

    /// <summary>
    /// Whether the book would take this goal, and if not, why — the cap logic asked as a question.
    ///
    /// The rules need this separately from <see cref="Add"/> because once creation is a recorded
    /// transition, the rules decide and the reducer applies. The decision includes the cap: a
    /// perception phase that proposed goals the book would have refused would form 441 more of them
    /// across the panel and be a different simulation.
    ///
    /// Reports the refusal to the watcher, because this call <i>is</i> the moment of refusal — the
    /// rules asked and were told no, which is exactly what the old <c>Add</c> counted.
    /// </summary>
    /// <summary>
    /// Records a refusal the caller worked out for itself.
    ///
    /// The batching phase has to test against the book <i>plus</i> what it has already proposed this
    /// year, which <see cref="WouldAdmit"/> cannot see, so the count comes from there instead. It is
    /// the same event either way — the rules asked and were told no.
    /// </summary>
    public void NoteRefusal(EntityId owner, GoalKind kind, GoalRefusal why) =>
        Watch?.Refused(owner, kind, why);

    public GoalRefusal? WouldAdmit(EntityId owner, GoalKind kind, EntityId target)
    {
        IReadOnlyList<Goal> list = For(owner);

        if (list.Count >= MaxPerOwner)
        {
            Watch?.Refused(owner, kind, GoalRefusal.BookFull);
            return GoalRefusal.BookFull;
        }

        foreach (Goal g in list)
        {
            if (g.Kind == kind && g.Target == target)
            {
                Watch?.Refused(owner, kind, GoalRefusal.AlreadyHeld);
                return GoalRefusal.AlreadyHeld;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds a goal unless the owner is full or already holds one of this kind.
    ///
    /// <b>The off-switch path only.</b> With goal recording on, the rules propose and
    /// <see cref="Restore"/> applies; this is what runs when it is off, so a
    /// <c>record-no-goals</c> arm reproduces the ruleset-6 world exactly.
    /// </summary>
    public Goal? Add(EntityId owner, GoalKind kind, EntityId target, int year, int lifespan, EventId cause)
    {
        if (WouldAdmit(owner, kind, target) is not null) return null;
        return Place(owner, kind, target, year, year + lifespan, cause);
    }

    /// <summary>
    /// Recreates a goal from the record.
    ///
    /// <b>Bypasses the caps deliberately, and throws if they would have refused.</b> The live run
    /// already applied them when it decided to propose, so a fold that re-applied them would be
    /// asking the question twice — and if the answer ever differed, the honest response is a loud
    /// failure rather than a book that quietly diverges from the world it is supposed to be.
    /// </summary>
    internal Goal Restore(
        EntityId owner, GoalKind kind, EntityId target, int createdYear, int expiresYear, EventId cause)
    {
        IReadOnlyList<Goal> list = For(owner);

        if (list.Count >= MaxPerOwner)
        {
            throw new InvalidOperationException(
                $"the record forms a {kind} goal for {owner}, whose book already holds " +
                $"{list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}. The fold " +
                "has diverged from the run that wrote this log, and every later goal transition on " +
                "this world is now against the wrong book.");
        }

        foreach (Goal g in list)
        {
            if (g.Kind == kind && g.Target == target)
            {
                throw new InvalidOperationException(
                    $"the record forms a second {kind} goal for {owner} against the same target. " +
                    "The fold has diverged from the run that wrote this log.");
            }
        }

        return Place(owner, kind, target, createdYear, expiresYear, cause);
    }

    private Goal Place(
        EntityId owner, GoalKind kind, EntityId target, int createdYear, int expiresYear, EventId cause)
    {
        if (!_byOwner.TryGetValue(owner, out List<Goal>? list))
            _byOwner[owner] = list = [];

        Goal goal = new()
        {
            Id = _nextId++,
            Owner = owner,
            Kind = kind,
            Target = target,
            CreatedYear = createdYear,
            ExpiresYear = expiresYear,
            Cause = cause,
        };

        list.Add(goal);
        Watch?.Created(goal);
        return goal;
    }

    /// <summary>
    /// Moves a goal's progress, naming what moved it.
    /// </summary>
    public void Advance(Goal goal, int delta, GoalStep step)
    {
        goal.Advance(delta);
        Watch?.Advanced(goal, delta, step);
    }

    /// <summary>
    /// Binds the storyline a goal has spawned to it.
    /// </summary>
    public void Attach(Goal goal, EntityId arc)
    {
        goal.Attach(arc);
        Watch?.Attached(goal, arc);
    }

    /// <summary>
    /// Removes one goal, naming why.
    /// </summary>
    /// <param name="citation">
    /// The event the removing site had just emitted, where it had one. <see cref="EventId.None"/>
    /// means nothing in the log is adjacent to this transition at all — which is the difference
    /// between an ending a reader could infer and one that leaves no trace, and is measured rather
    /// than declared for exactly that reason.
    /// </param>
    public void Remove(Goal goal, GoalEnd why, EventId citation = default)
    {
        bool held = false;
        if (_byOwner.TryGetValue(goal.Owner, out List<Goal>? list))
        {
            held = list.Remove(goal);
            if (list.Count == 0) _byOwner.Remove(goal.Owner);
        }

        if (held) Watch?.Ended(goal, why, citation);
        else Watch?.Vanished(goal, why);
    }

    /// <summary>Removes everything one owner holds, naming why.</summary>
    public void RemoveAllFor(EntityId owner, GoalEnd why, EventId citation = default)
    {
        if (!_byOwner.TryGetValue(owner, out List<Goal>? list)) return;

        List<Goal> going = [.. list];
        _byOwner.Remove(owner);

        if (Watch is null) return;
        foreach (Goal goal in going) Watch.Ended(goal, why, citation);
    }

    /// <summary>Every live goal, ordered by owner then creation. Safe to mutate during iteration.</summary>
    public List<Goal> Snapshot()
    {
        List<Goal> all = [];
        foreach (KeyValuePair<EntityId, List<Goal>> kv in _byOwner)
            all.AddRange(kv.Value);
        return all;
    }
}
