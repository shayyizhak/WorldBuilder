namespace WorldBuilder.Core;

/// <summary>
/// The edge types of the relation graph. This — not the entity schema — is where richness
/// is allowed to accumulate. When the world feels thin, add a relation kind or a rule that
/// reads one; do not add a field to <see cref="Actor"/>.
/// </summary>
public enum RelationKind : byte
{
    /// <summary>Blood or marriage. Drives succession and makes it messy.</summary>
    Kin = 0,
    Marriage = 1,
    /// <summary>Accumulated harm done by the target. The engine's memory, and the reason
    /// a betrayal in year 12 can legibly cause a war in year 38.</summary>
    Grievance = 2,
    Alliance = 3,
    Trade = 4,
    Rivalry = 5,
    /// <summary>Personal loyalty of an actor to another actor, distinct from faction membership.</summary>
    Fealty = 6,
    /// <summary>Faction subordinate to faction.</summary>
    Vassal = 7,
    /// <summary>Currently at war. Directed both ways when a war opens.</summary>
    AtWar = 8,
}

public readonly record struct RelationKey(EntityId From, EntityId To, RelationKind Kind)
    : IComparable<RelationKey>
{
    public int CompareTo(RelationKey other)
    {
        int c = From.CompareTo(other.From);
        if (c != 0) return c;
        c = To.CompareTo(other.To);
        if (c != 0) return c;
        return Kind.CompareTo(other.Kind);
    }
}

/// <summary>
/// A weighted, dated, *sourced* edge. <see cref="Cause"/> is the point: every relation can
/// name the event that created it, so "why does Ironmark hate the Corr League" resolves to
/// an event id rather than an opinion.
/// </summary>
public sealed class Relation
{
    public required RelationKey Key { get; init; }
    public required int CreatedYear { get; init; }
    public required EventId Cause { get; init; }

    public int Value { get; set; }
    public int LastChangedYear { get; set; }

    /// <summary>The most recent event to move this value, for causal tracing of *changes*.</summary>
    public EventId LastCause { get; set; }
}

/// <summary>
/// The relation graph. Backed by a sorted dictionary so enumeration order is a property of
/// the data and not of insertion history — iteration order is part of determinism here.
/// </summary>
public sealed class RelationGraph
{
    private readonly SortedDictionary<RelationKey, Relation> _edges = [];

    // Adjacency indexes. The sorted dictionary stays the canonical, deterministically ordered
    // store; these only make lookups cheap. Without them every "who is sworn to this actor"
    // scanned the whole graph, which turned a 300-year run into an O(actors x edges) crawl.
    private readonly Dictionary<EntityId, List<Relation>> _from = [];
    private readonly Dictionary<EntityId, List<Relation>> _to = [];

    public IEnumerable<Relation> All => _edges.Values;
    public int Count => _edges.Count;

    public Relation? Find(EntityId from, EntityId to, RelationKind kind) =>
        _edges.TryGetValue(new RelationKey(from, to, kind), out Relation? r) ? r : null;

    public int ValueOf(EntityId from, EntityId to, RelationKind kind) =>
        Find(from, to, kind)?.Value ?? 0;

    public bool Has(EntityId from, EntityId to, RelationKind kind) =>
        _edges.ContainsKey(new RelationKey(from, to, kind));

    /// <summary>Adds <paramref name="delta"/> to the edge, creating it if absent. Returns the edge.</summary>
    public Relation Adjust(EntityId from, EntityId to, RelationKind kind, int delta, int year, EventId cause)
    {
        RelationKey key = new(from, to, kind);
        if (!_edges.TryGetValue(key, out Relation? r))
        {
            r = new Relation { Key = key, CreatedYear = year, Cause = cause, LastCause = cause };
            _edges[key] = r;
            Index(_from, from, r);
            Index(_to, to, r);
        }
        r.Value += delta;
        r.LastChangedYear = year;
        r.LastCause = cause;
        return r;
    }

    public void Remove(EntityId from, EntityId to, RelationKind kind)
    {
        RelationKey key = new(from, to, kind);
        if (!_edges.Remove(key, out Relation? r)) return;

        _from.TryGetValue(from, out List<Relation>? outgoing);
        outgoing?.Remove(r);
        _to.TryGetValue(to, out List<Relation>? incoming);
        incoming?.Remove(r);
    }

    /// <summary>Outgoing edges of one kind, in key order.</summary>
    public List<Relation> From(EntityId from, RelationKind kind) => Filter(_from, from, kind);

    /// <summary>Incoming edges of one kind — "who is sworn to this actor", and the like.</summary>
    public List<Relation> To(EntityId to, RelationKind kind) => Filter(_to, to, kind);

    /// <summary>Sums the value of every incoming edge of a kind. The common case, without allocating.</summary>
    public int IncomingTotal(EntityId to, RelationKind kind)
    {
        if (!_to.TryGetValue(to, out List<Relation>? edges)) return 0;

        int total = 0;
        foreach (Relation r in edges)
            if (r.Key.Kind == kind) total += r.Value;
        return total;
    }

    /// <summary>All edges touching an entity in either direction, for the <c>who</c> view.</summary>
    public List<Relation> Touching(EntityId id)
    {
        List<Relation> result = [];
        if (_from.TryGetValue(id, out List<Relation>? outgoing)) result.AddRange(outgoing);
        if (_to.TryGetValue(id, out List<Relation>? incoming))
            foreach (Relation r in incoming)
                if (r.Key.From != id) result.Add(r);

        result.Sort(static (a, b) => a.Key.CompareTo(b.Key));
        return result;
    }

    private static List<Relation> Filter(Dictionary<EntityId, List<Relation>> index, EntityId id, RelationKind kind)
    {
        List<Relation> result = [];
        if (!index.TryGetValue(id, out List<Relation>? edges)) return result;

        foreach (Relation r in edges)
            if (r.Key.Kind == kind) result.Add(r);
        return result;
    }

    /// <summary>Keeps each adjacency list in key order, so iteration never depends on insertion history.</summary>
    private static void Index(Dictionary<EntityId, List<Relation>> index, EntityId id, Relation edge)
    {
        if (!index.TryGetValue(id, out List<Relation>? edges)) index[id] = edges = [];

        int at = edges.Count;
        while (at > 0 && edges[at - 1].Key.CompareTo(edge.Key) > 0) at--;
        edges.Insert(at, edge);
    }
}
