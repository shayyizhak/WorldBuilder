namespace WorldBuilder.Core;

/// <summary>
/// The three things anyone fights over. Kept at three deliberately: grain is consumed and
/// so produces famine, ore is concentrated in two sites and so produces war, silver buys
/// the other two and so produces politics.
/// </summary>
public enum Resource : byte
{
    Grain = 0,
    Ore = 1,
    Silver = 2,
}

public static class Resources
{
    public const int Count = 3;
    public static readonly Resource[] All = [Resource.Grain, Resource.Ore, Resource.Silver];
    public static string Name(Resource r) => r switch
    {
        Resource.Grain => "grain",
        Resource.Ore => "ore",
        _ => "silver",
    };
}

/// <summary>
/// The four dials an actor is made of, each 0..100. Every decision the actor makes is a
/// weighted roll over these plus the situation — which is what stops the cast reading as dice.
/// </summary>
public readonly record struct Traits(int Ambition, int Guile, int Martial, int Loyalty);

public enum Title : byte
{
    Commoner = 0,
    Retainer = 1,
    Captain = 2,
    Steward = 3,
    Heir = 4,
    Ruler = 5,
    Exile = 6,
}

public sealed class Actor
{
    public required EntityId Id { get; init; }
    public required string Name { get; init; }
    public required int BirthYear { get; init; }
    public required Traits Traits { get; init; }

    public int? DeathYear { get; set; }
    public EntityId Place { get; set; }
    public EntityId Faction { get; set; }
    public Title Title { get; set; }

    public bool IsAlive => DeathYear is null;
    public int AgeAt(int year) => year - BirthYear;
}

public enum PlaceKind : byte
{
    Region = 0,
    Settlement = 1,
    Site = 2,
}

public sealed class Place
{
    public required EntityId Id { get; init; }
    public required string Name { get; init; }
    public required PlaceKind Kind { get; init; }
    public EntityId Parent { get; init; }

    public int Population { get; set; }

    /// <summary>
    /// Where on the imported board this place stands, or −1 for a place that is not sited.
    ///
    /// One cell, never a list: a place is somewhere. The <see cref="PlaceKind.Region"/> is the
    /// exception and carries −1 deliberately — a region is the ground the board is made of rather
    /// than a point on it, it is never marched on, raided, held or fought over, and giving it a
    /// cell would put a spurious position into every distance the engine measures.
    ///
    /// The value comes off the place's genesis event, so a position is part of the record rather
    /// than something recomputed at load time. The board it indexes into travels with the world
    /// as a hashed artefact; the log alone carries the positions, and the two are checked against
    /// each other when a bundle is opened.
    /// </summary>
    public int Cell { get; init; } = -1;

    public bool IsSited => Cell >= 0;

    /// <summary>Base annual production per resource. What makes one place worth taking and another not.</summary>
    public required int[] Yield { get; init; }

    public int[] Stockpile { get; } = new int[Resources.Count];

    public EntityId Controller { get; set; }

    public int YieldOf(Resource r) => Yield[(int)r];
}

public enum SuccessionRule : byte
{
    /// <summary>Eldest child of the ruler; failing that, the eldest kin.</summary>
    Primogeniture = 0,
    /// <summary>The faction's titled actors choose, weighted by their own loyalty and the candidate's standing.</summary>
    Election = 1,
    /// <summary>Highest martial score takes it, and dares anyone to object.</summary>
    Strongest = 2,
}

public sealed class Faction
{
    public required EntityId Id { get; init; }
    public required string Name { get; init; }
    public required SuccessionRule Succession { get; init; }
    public required EntityId Seat { get; init; }

    public EntityId Leader { get; set; }

    /// <summary>0..100. Below the crisis threshold the faction starts shedding places and actors.</summary>
    public int Legitimacy { get; set; } = 60;

    public int Treasury { get; set; }
}

public enum ArcKind : byte
{
    War = 0,
    Famine = 1,
    Feud = 2,
    Plot = 3,
    Succession = 4,
    Plague = 5,
}

/// <summary>
/// A named, multi-event storyline. Arcs exist purely for legibility: they let the log say
/// "the Salt War (w:3)" across twenty years instead of emitting twenty unrelated battles.
/// </summary>
public sealed class Arc
{
    public required EntityId Id { get; init; }
    public required ArcKind Kind { get; init; }
    public required string Name { get; init; }
    public required int StartYear { get; init; }
    public required EventId Origin { get; init; }

    public int? EndYear { get; set; }
    public List<EntityId> Sides { get; } = [];

    public bool IsOpen => EndYear is null;
}
