namespace WorldBuilder.Core;

/// <summary>
/// The fold of the event log. Every mutation lives behind an <c>internal</c> surface and is
/// only ever called from <see cref="EventReducer"/>: rules read this and propose events, the
/// reducer applies them. That discipline is what makes "state is a fold over the log" true
/// rather than aspirational, and it is what lets v2 re-fold history after a retcon.
/// </summary>
public sealed class WorldState
{
    private readonly List<Actor> _actors = [];
    private readonly List<Place> _places = [];
    private readonly List<Faction> _factions = [];
    private readonly List<Arc> _arcs = [];

    public required ulong Seed { get; init; }
    public int Year { get; internal set; }

    public IReadOnlyList<Actor> Actors => _actors;
    public IReadOnlyList<Place> Places => _places;
    public IReadOnlyList<Faction> Factions => _factions;
    public IReadOnlyList<Arc> Arcs => _arcs;

    public RelationGraph Relations { get; } = new();
    public GoalBook Goals { get; } = new();

    // ---- lookup -----------------------------------------------------------

    public Actor ActorOf(EntityId id) => _actors[id.Index - 1];
    public Place PlaceOf(EntityId id) => _places[id.Index - 1];
    public Faction FactionOf(EntityId id) => _factions[id.Index - 1];
    public Arc ArcOf(EntityId id) => _arcs[id.Index - 1];

    public string NameOf(EntityId id) => id.Kind switch
    {
        EntityKind.Actor => ActorOf(id).Name,
        EntityKind.Place => PlaceOf(id).Name,
        EntityKind.Faction => FactionOf(id).Name,
        EntityKind.Arc => ArcOf(id).Name,
        _ => "-",
    };

    /// <summary>Name plus short id, the form every log line uses: <c>Harn Vell (a:112)</c>.</summary>
    public string Label(EntityId id) => id.IsNone ? "-" : $"{NameOf(id)} ({id})";

    // ---- derived queries --------------------------------------------------

    public IEnumerable<Actor> LivingActors()
    {
        foreach (Actor a in _actors)
            if (a.IsAlive) yield return a;
    }

    public List<Actor> MembersOf(EntityId faction)
    {
        List<Actor> result = [];
        foreach (Actor a in _actors)
            if (a.IsAlive && a.Faction == faction) result.Add(a);
        return result;
    }

    public List<Place> HoldingsOf(EntityId faction)
    {
        List<Place> result = [];
        foreach (Place p in _places)
            if (p.Controller == faction && p.Kind != PlaceKind.Region) result.Add(p);
        return result;
    }

    public int HoldingCount(EntityId faction)
    {
        int n = 0;
        foreach (Place p in _places)
            if (p.Controller == faction && p.Kind != PlaceKind.Region) n++;
        return n;
    }

    /// <summary>
    /// A polity that holds no ground is finished, and stays finished. It keeps its identity so
    /// the log can still name it, but it must never again be chosen as a target, a host, a
    /// trading partner or an ally — a dead faction that kept receiving defectors and returning
    /// exiles would come back to life just far enough to collapse all over again.
    /// </summary>
    public bool IsDefunct(EntityId faction) => faction.IsNone || HoldingCount(faction) == 0;

    /// <summary>
    /// The factions that still exist, in id order. Every selection pool is built from this
    /// rather than from <see cref="Factions"/>, so exclusion happens once, at the source.
    /// </summary>
    public List<Faction> ActiveFactions()
    {
        List<Faction> result = [];
        foreach (Faction f in _factions)
            if (HoldingCount(f.Id) > 0) result.Add(f);
        return result;
    }

    /// <summary>
    /// A single integer standing for "how much this faction can throw at a problem". Used for
    /// war opportunity, battle resolution and the runaway-leader diagnostic. Integers only.
    /// </summary>
    public int PowerOf(EntityId faction)
    {
        Faction f = FactionOf(faction);
        int power = 0;
        foreach (Place p in HoldingsOf(faction))
            power += p.Population / 100 + p.Stockpile[(int)Resource.Ore] / 40;

        power += f.Treasury / 50;
        power += f.Legitimacy / 8;
        if (!f.Leader.IsNone && ActorOf(f.Leader).IsAlive)
            power += ActorOf(f.Leader).Traits.Martial / 8;

        return Math.Max(1, power);
    }

    public int StockOf(EntityId faction, Resource r)
    {
        int total = 0;
        foreach (Place p in HoldingsOf(faction)) total += p.Stockpile[(int)r];
        return total;
    }

    public int PopulationOf(EntityId faction)
    {
        int total = 0;
        foreach (Place p in HoldingsOf(faction)) total += p.Population;
        return total;
    }

    /// <summary>Living kin of an actor, sorted eldest first — the raw material of succession.</summary>
    public List<Actor> KinOf(EntityId actor)
    {
        List<Actor> kin = [];
        foreach (Relation r in Relations.From(actor, RelationKind.Kin))
            if (r.Key.To.Kind == EntityKind.Actor && ActorOf(r.Key.To).IsAlive)
                kin.Add(ActorOf(r.Key.To));

        kin.Sort(static (x, y) =>
        {
            int c = x.BirthYear.CompareTo(y.BirthYear);
            return c != 0 ? c : x.Id.CompareTo(y.Id);
        });
        return kin;
    }

    public IEnumerable<Arc> OpenArcs()
    {
        foreach (Arc a in _arcs)
            if (a.IsOpen) yield return a;
    }

    public Arc? OpenWarBetween(EntityId a, EntityId b)
    {
        foreach (Arc arc in _arcs)
            if (arc.IsOpen && arc.Kind == ArcKind.War && arc.Sides.Contains(a) && arc.Sides.Contains(b))
                return arc;
        return null;
    }

    public bool AtWar(EntityId faction)
    {
        foreach (Arc arc in _arcs)
            if (arc.IsOpen && arc.Kind == ArcKind.War && arc.Sides.Contains(faction)) return true;
        return false;
    }

    // ---- mutation (reducer only) -----------------------------------------

    internal EntityId NextActorId => EntityId.Actor(_actors.Count + 1);
    internal EntityId NextPlaceId => EntityId.Place(_places.Count + 1);
    internal EntityId NextFactionId => EntityId.Faction(_factions.Count + 1);
    internal EntityId NextArcId => EntityId.Arc(_arcs.Count + 1);

    internal void AddActor(Actor a) => _actors.Add(a);
    internal void AddPlace(Place p) => _places.Add(p);
    internal void AddFaction(Faction f) => _factions.Add(f);
    internal void AddArc(Arc a) => _arcs.Add(a);
}
