using System.Globalization;

namespace WorldBuilder.Core;

/// <summary>
/// Fluent builder for an event. Rules describe *what happened and what it moved*; the
/// numeric changes ride along as declarative payload deltas that
/// <see cref="EventReducer"/> applies uniformly.
/// </summary>
public sealed class EventDraft(EventKind kind)
{
    internal EventKind Kind { get; } = kind;
    internal List<Participant> Participants { get; } = [];
    internal List<EventId> Causes { get; } = [];
    internal List<EntityId> Witnesses { get; } = [];
    internal List<KeyValuePair<string, string>> Data { get; } = [];
    internal Visibility Scope { get; private set; } = Visibility.Public;
    internal Significance Significance { get; private set; } = Significance.Minor;
    internal Outcome Outcome { get; private set; } = Outcome.NotApplicable;
    internal EntityId Arc { get; private set; } = EntityId.None;

    public EventDraft Subject(EntityId id) => Part(Role.Subject, id);
    public EventDraft Object(EntityId id) => Part(Role.Object, id);
    public EventDraft At(EntityId place) => Part(Role.Place, place);
    public EventDraft By(EntityId faction) => Part(Role.Faction, faction);
    public EventDraft Bystander(EntityId id) => Part(Role.Bystander, id);

    private EventDraft Part(Role role, EntityId id)
    {
        if (!id.IsNone) Participants.Add(new Participant(role, id));
        return this;
    }

    /// <summary>Records a causal parent. Everything but genesis must have at least one.</summary>
    public EventDraft Because(EventId cause)
    {
        if (!cause.IsNone && !Causes.Contains(cause)) Causes.Add(cause);
        return this;
    }

    public EventDraft Because(IEnumerable<EventId> causes)
    {
        foreach (EventId c in causes) Because(c);
        return this;
    }

    public EventDraft Seen(EntityId witness)
    {
        if (!witness.IsNone && !Witnesses.Contains(witness)) Witnesses.Add(witness);
        return this;
    }

    public EventDraft Hidden(Visibility scope) { Scope = scope; return this; }
    public EventDraft Weight(Significance significance) { Significance = significance; return this; }
    public EventDraft Resolved(Outcome outcome) { Outcome = outcome; return this; }
    public EventDraft InArc(EntityId arc) { Arc = arc; return this; }

    public EventDraft Set(string key, string value)
    {
        Data.Add(new KeyValuePair<string, string>(key, value));
        return this;
    }

    public EventDraft Set(string key, int value) =>
        Set(key, value.ToString(CultureInfo.InvariantCulture));

    public EventDraft Set(string key, EntityId value) => Set(key, value.ToString());

    public EventDraft Set<TEnum>(string key, TEnum value) where TEnum : struct, Enum =>
        Set(key, value.ToString());

    /// <summary>
    /// Sets a key only when it applies, so the ordinary case writes no key at all rather than an
    /// empty one — the same rule the world header follows, and for the same reason: a field
    /// present and blank is not distinguishable from a field that was lost.
    /// </summary>
    public EventDraft SetIf(bool when, string key, string value) => when ? Set(key, value) : this;

    // Declarative state deltas — the reducer reads these generically.

    public EventDraft Pop(EntityId place, int delta) => Delta($"pop:{place}", delta);
    public EventDraft Stock(EntityId place, Resource r, int delta) => Delta($"stock:{place}:{r}", delta);
    public EventDraft Leg(EntityId faction, int delta) => Delta($"leg:{faction}", delta);
    public EventDraft Treas(EntityId faction, int delta) => Delta($"treas:{faction}", delta);

    public EventDraft Rel(EntityId from, EntityId to, RelationKind kind, int delta) =>
        Delta($"rel:{from}:{to}:{kind}", delta);

    /// <summary>Symmetric relation change, for edges that are meaningless one-way (kin, alliance).</summary>
    public EventDraft RelBoth(EntityId a, EntityId b, RelationKind kind, int delta) =>
        Rel(a, b, kind, delta).Rel(b, a, kind, delta);

    /// <summary>
    /// Closes a storyline. Routed through the payload rather than set on the arc directly, so
    /// that replaying the log reproduces it — an arc quietly ended in the rules layer is a
    /// state change the log never saw, and the fold stops matching the live world.
    /// </summary>
    public EventDraft EndArc(EntityId arc) => arc.IsNone ? this : Set($"arcEnd:{arc}", 1);

    public EventDraft RelDel(EntityId from, EntityId to, RelationKind kind) =>
        Set($"relDel:{from}:{to}:{kind}", 1);

    // ---- goals ------------------------------------------------------------
    //
    // Goals travel as payload deltas for the same reason arcs do, and the comment on EndArc already
    // states it: state changed in the rules layer is a change the log never saw, and the fold stops
    // matching the live world. Goals were the last thing in WorldState still being changed that way.
    //
    // Fields are packed into the value rather than spread across the key because an EntityId prints
    // as `a:14` — a key carrying an owner, a kind and a target would split into a variable number of
    // tokens, and ApplyDeltas splits keys on ':'. The index in the key is an ordinal within the
    // event and carries no meaning beyond keeping the keys distinct; the reducer assigns ids.

    /// <summary>Forms a goal on the fold. The ordinal only has to be unique within this event.</summary>
    public EventDraft GoalAdd(
        int ordinal, EntityId owner, GoalKind kind, EntityId target, int expiresYear, EventId cause) =>
        Set($"goalAdd:{ordinal}", $"{owner}|{kind}|{target}|{expiresYear}|{cause}");

    /// <summary>Moves a goal's progress on the fold, naming what moved it.</summary>
    public EventDraft GoalStep(Goal goal, int delta, GoalStep step) =>
        Set($"goalStep:{goal.Id}", $"{delta}|{step}");

    /// <summary>Binds a goal to the storyline it has just spawned.</summary>
    public EventDraft GoalArc(Goal goal, EntityId arc) => Set($"goalArc:{goal.Id}", arc.ToString());

    /// <summary>Ends a goal on the fold, naming why.</summary>
    public EventDraft GoalEnd(Goal goal, GoalEnd why) => Set($"goalEnd:{goal.Id}", why.ToString());

    /// <summary>
    /// Writes a state delta, and never writes one of zero.
    ///
    /// The principle the world header follows: a field present and empty is not distinguishable from
    /// a field that was lost.
    ///
    /// <b>A delta naming the null entity is not guarded here, and that is a known defect.</b> A
    /// violent death writes <c>leg:-</c> where the victim belonged to no house — the site checks
    /// <c>IsNone</c> two lines earlier for the grievance edge and for whether he was a leader, and not
    /// for this — and the reducer drops it in silence, because <c>leg:-</c> splits into two tokens
    /// where its case wants three and so matches nothing at all. Twice across the reference panel.
    ///
    /// It is the same emitting-and-ignoring shape ruleset 8 repaired for severances, and it is
    /// deliberately *not* repaired with it: guarding here changes the log for every rule that writes
    /// a delta, so reproducing ruleset 7 byte for byte would need this switchable too — and the
    /// severance off-switch reaches the one rule that emits severances, not the eight that emit
    /// deltas. Repairing it means putting the switch somewhere every draft passes, which is a wider
    /// change than the one-site repair it was found during. Counted by
    /// <see cref="Analysis.MutationAudit"/> under <see cref="Analysis.MutationVerdict.NoEntity"/> and
    /// bounded by a test, so it cannot grow quietly while it waits.
    /// </summary>
    private EventDraft Delta(string key, int delta) => delta == 0 ? this : Set(key, delta);
}

/// <summary>
/// Binds the log and the state together: an event is appended and folded in one step, so the
/// two can never drift. All simulation rules write through here and nowhere else.
/// </summary>
public sealed class Chronicle
{
    private int _year;
    private int _sequenceInYear;

    public Chronicle(WorldState state, EventLog log)
    {
        State = state;
        Log = log;
        _year = state.Year;
    }

    public WorldState State { get; }
    public EventLog Log { get; }

    /// <summary>Id the next arc-opening event should claim. Reserved before emitting.</summary>
    public EntityId ReserveArc() => State.NextArcId;

    public EntityId ReserveActor() => State.NextActorId;
    public EntityId ReserveFaction() => State.NextFactionId;
    public EntityId ReservePlace() => State.NextPlaceId;

    public void BeginYear(int year)
    {
        _year = year;
        _sequenceInYear = 0;
    }

    public Event Emit(EventDraft draft)
    {
        IReadOnlyList<EntityId> witnesses = draft.Witnesses.Count > 0
            ? draft.Witnesses
            : DeriveWitnesses(draft);

        Event e = EventFactory.Create(
            id: Log.NextId,
            year: _year,
            kind: draft.Kind,
            participants: draft.Participants,
            causes: draft.Causes,
            witnesses: witnesses,
            data: draft.Data,
            scope: draft.Scope,
            significance: draft.Significance,
            outcome: draft.Outcome,
            arc: draft.Arc,
            sequence: _sequenceInYear++);

        Log.Append(e);
        EventReducer.Apply(State, e);
        return e;
    }

    /// <summary>
    /// Who was positioned to perceive this. v0 never reads the result — v3's rumour
    /// propagation does, and it cannot be reconstructed after the fact, which is the whole
    /// reason it is computed now.
    /// </summary>
    private List<EntityId> DeriveWitnesses(EventDraft draft)
    {
        List<EntityId> witnesses = [];

        foreach (Participant p in draft.Participants)
            if (p.Id.Kind == EntityKind.Actor) Add(p.Id);

        EntityId place = EntityId.None;
        EntityId faction = EntityId.None;
        foreach (Participant p in draft.Participants)
        {
            if (p.Role == Role.Place) place = p.Id;
            if (p.Role == Role.Faction) faction = p.Id;
        }

        switch (draft.Scope)
        {
            case Visibility.Secret:
                break;

            case Visibility.FactionInternal:
                if (!faction.IsNone)
                    foreach (Actor a in State.MembersOf(faction))
                        if (a.Title != Title.Commoner) Add(a.Id);
                break;

            case Visibility.PlaceLocal:
                AddThoseAt(place);
                break;

            default:
                AddThoseAt(place);
                foreach (Faction f in State.Factions)
                    if (!f.Leader.IsNone && State.ActorOf(f.Leader).IsAlive) Add(f.Leader);
                break;
        }

        return witnesses;

        void AddThoseAt(EntityId where)
        {
            if (where.IsNone) return;
            foreach (Actor a in State.LivingActors())
                if (a.Place == where) Add(a.Id);
        }

        void Add(EntityId id)
        {
            if (!witnesses.Contains(id)) witnesses.Add(id);
        }
    }
}
