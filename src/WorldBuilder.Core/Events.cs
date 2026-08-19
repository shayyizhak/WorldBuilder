using System.Globalization;

namespace WorldBuilder.Core;

/// <summary>
/// The closed set of things that can happen. Names are dotted so the log is greppable by
/// family (<c>CONFLICT.</c>, <c>POLITY.</c>). Adding a kind is the sanctioned way to make
/// history richer; adding a field to an entity is not.
/// </summary>
public enum EventKind
{
    GenesisWorld,
    GenesisPlace,
    GenesisFaction,
    GenesisActor,

    LifeBirth,
    LifeComingOfAge,
    LifeDeathNatural,
    LifeDeathViolent,
    LifeMarriage,

    EconomyYield,
    EconomyFamine,
    EconomyFamineEnds,
    EconomyBumperHarvest,
    EconomyPlague,
    EconomyPlagueEnds,
    EconomyTradePact,
    EconomyTradeCollapse,

    PolitySuccession,
    PolitySuccessionDisputed,
    PolityCoupPlotted,
    PolityCoupResolved,
    /// <summary>An open bid for the seat, with no conspiracy behind it. Distinct from
    /// <see cref="PolityCoupResolved"/>, which always resolves a specific plot.</summary>
    PolityChallenge,
    /// <summary>A conspiracy that ended because its author did.</summary>
    PolityPlotDiesWithPlotter,
    /// <summary>A conspiracy that ran out of time or of purpose.</summary>
    PolityPlotLapses,
    PolityCourtsSupport,
    PolityExile,
    PolityExileReturn,
    PolityAppointment,
    PolityLegitimacyCrisis,
    PolityRevolt,
    PolitySecession,
    PolityPartition,
    PolityCollapse,

    DiploInsult,
    DiploTributeDemanded,
    DiploAllianceFormed,
    DiploAllianceBroken,
    DiploWarDeclared,
    DiploPeaceSigned,

    ConflictRaid,
    ConflictBattle,
    ConflictSiege,
    ConflictConquest,
    ConflictAssassination,

    IntrigueBetrayal,
    IntrigueGrievanceSettled,

    /// <summary>
    /// The goals formed this year, so a fold can form them too. Bookkeeping.
    ///
    /// <b>Its own event rather than a key on the event each goal cites</b>, which is what the phase-2
    /// brief's §1.1 assumed. A goal cites an event that already happened — often years earlier — and
    /// the log is append-only with immutable records, so there is no host to ride. Perception emits
    /// nothing else, so creation is an orphan in exactly the sense §1.2 defines and takes §1.2's
    /// shape: one event per occurrence, carrying the count, the breakdown by kind, and one key per
    /// goal.
    /// </summary>
    GoalsFormed,

    /// <summary>
    /// Goals that ended with nothing else to say so. Bookkeeping.
    ///
    /// Two sites reach this: the yearly retirement sweep, and the guards in the action phase that
    /// drop a goal whose target has become unreachable. <b>The brief attributes all 189 silent
    /// endings to the sweep; 35 of them are the action-phase guards</b> — `AlreadySatisfied` 18,
    /// `TargetDefunct` 13, `TargetDead` 4 — which have no host event either and would otherwise have
    /// been left out of the record by a design that only covered the sweep.
    /// </summary>
    GoalsEnded,
}

/// <summary>How loudly an event should be reported. The readable log prints Minor and above.</summary>
public enum Significance : byte
{
    /// <summary>State-carrying but not worth reading: yearly economy arithmetic, genesis rows.</summary>
    Bookkeeping = 0,
    Minor = 1,
    Major = 2,
}

/// <summary>Who could have perceived this. Unused by v0 rules; consumed by v3 rumour propagation.</summary>
public enum Visibility : byte
{
    Public = 0,
    PlaceLocal = 1,
    FactionInternal = 2,
    Secret = 3,
}

/// <summary>Where an event came from. v0 only ever writes Simulated; v2 writes the other two.</summary>
public enum Provenance : byte
{
    Simulated = 0,
    Authored = 1,
    Reconciled = 2,
}

/// <summary>How an entity participates. Templates read these to build a sentence.</summary>
public enum Role : byte
{
    Subject = 0,
    Object = 1,
    Place = 2,
    Faction = 3,
    Bystander = 4,
}

public readonly record struct Participant(Role Role, EntityId Id);

/// <summary>Outcome of an attempted action, where the attempt itself is worth recording.</summary>
public enum Outcome : byte
{
    NotApplicable = 0,
    Succeeded = 1,
    Failed = 2,
    Pending = 3,
}

/// <summary>
/// An immutable fact in the log. The engine appends these and folds them into
/// <see cref="WorldState"/>; nothing else may mutate state.
/// </summary>
public sealed record Event
{
    public required EventId Id { get; init; }

    /// <summary>
    /// Content-derived identity, stable across re-simulation and independent of position in
    /// the log. Renders are keyed on this so a cached render survives a v2 retcon that shifts
    /// every downstream <see cref="Id"/>.
    /// </summary>
    public required string Key { get; init; }

    public required int Year { get; init; }
    public required EventKind Kind { get; init; }

    public IReadOnlyList<Participant> Participants { get; init; } = [];

    /// <summary>
    /// The events that made this one happen. Required for everything but genesis: causality
    /// is a stored edge, not something inferred after the fact.
    /// </summary>
    public IReadOnlyList<EventId> Causes { get; init; } = [];

    /// <summary>
    /// Everyone positioned to perceive this at the moment it happened. Populated by v0,
    /// read by v3 — who *could* have known something is unrecoverable retrospectively.
    /// </summary>
    public IReadOnlyList<EntityId> Witnesses { get; init; } = [];

    public Visibility Scope { get; init; } = Visibility.Public;
    public Significance Significance { get; init; } = Significance.Minor;
    public Outcome Outcome { get; init; } = Outcome.NotApplicable;
    public Provenance Origin { get; init; } = Provenance.Simulated;

    /// <summary>The arc (war, famine, feud, plot) this event belongs to, if any.</summary>
    public EntityId Arc { get; init; } = EntityId.None;

    /// <summary>Ordered key/value payload. Ordered, not a dictionary, so serialisation is deterministic.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Data { get; init; } = [];

    public EntityId Subject => Find(Role.Subject);
    public EntityId Object => Find(Role.Object);
    public EntityId Where => Find(Role.Place);
    public EntityId Faction => Find(Role.Faction);

    private EntityId Find(Role role)
    {
        foreach (Participant p in Participants)
            if (p.Role == role) return p.Id;
        return EntityId.None;
    }

    /// <summary>
    /// A payload field, or null where this event does not carry one.
    ///
    /// The single choke point every payload read goes through — <see cref="GetInt"/>,
    /// <see cref="GetLong"/> and <see cref="GetEntity"/> all land here — which is why
    /// <see cref="EventFieldReadLog"/> observes it. A field name the emitter never writes returns
    /// null forever and reads downstream as a zero, an empty string or "no such entity", none of
    /// which fails. See that type for the eighth instance of it.
    /// </summary>
    public string? GetString(string key)
    {
        EventFieldReadLog.Note(Kind, key);

        foreach (KeyValuePair<string, string> kv in Data)
            if (kv.Key == key) return kv.Value;
        return null;
    }

    public long GetLong(string key, long fallback = 0) =>
        long.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : fallback;

    public int GetInt(string key, int fallback = 0) => (int)GetLong(key, fallback);

    public EntityId GetEntity(string key) =>
        EntityId.TryParse(GetString(key), out EntityId id) ? id : EntityId.None;
}

/// <summary>Builds <see cref="Event"/> records and assigns their content-derived keys.</summary>
public static class EventFactory
{
    public static Event Create(
        EventId id,
        int year,
        EventKind kind,
        IReadOnlyList<Participant> participants,
        IReadOnlyList<EventId>? causes = null,
        IReadOnlyList<EntityId>? witnesses = null,
        IReadOnlyList<KeyValuePair<string, string>>? data = null,
        Visibility scope = Visibility.Public,
        Significance significance = Significance.Minor,
        Outcome outcome = Outcome.NotApplicable,
        EntityId arc = default,
        int sequence = 0)
    {
        return new Event
        {
            Id = id,
            Key = ComputeKey(year, kind, participants, sequence),
            Year = year,
            Kind = kind,
            Participants = participants,
            Causes = causes ?? [],
            Witnesses = witnesses ?? [],
            Data = data ?? [],
            Scope = scope,
            Significance = significance,
            Outcome = outcome,
            Arc = arc,
        };
    }

    /// <summary>
    /// FNV-1a over (year, kind, participants, sequence), rendered as 16 hex chars. Deliberately
    /// excludes the log position, so the key is stable if history is later re-folded.
    /// </summary>
    public static string ComputeKey(int year, EventKind kind, IReadOnlyList<Participant> participants, int sequence)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong h = offset;
        h = Absorb(h, (ulong)(uint)year);
        h = Absorb(h, (ulong)(uint)kind);
        foreach (Participant p in participants)
        {
            h = Absorb(h, (ulong)(uint)p.Role);
            h = Absorb(h, p.Id.Bits);
        }
        h = Absorb(h, (ulong)(uint)sequence);
        return h.ToString("x16", CultureInfo.InvariantCulture);

        static ulong Absorb(ulong h, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (byte)(value >> (i * 8));
                h *= prime;
            }
            return h;
        }
    }
}
