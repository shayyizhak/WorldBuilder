namespace WorldBuilder.Core;

/// <summary>
/// Every tuning dial in one place. The gate question ("is this history interesting?") is
/// answered by turning these, not by adding fields to entities — so they live together,
/// named for what they do to the *story* rather than to the arithmetic.
/// </summary>
public sealed record SimConfig
{
    public static readonly SimConfig Default = new();

    // ---- world shape ------------------------------------------------------

    public int Settlements { get; init; } = 5;
    public int OreSites { get; init; } = 2;
    public int Factions { get; init; } = 3;
    public int StartingActors { get; init; } = 20;

    // ---- life -------------------------------------------------------------

    public int AdultAge { get; init; } = 16;
    public int OldAge { get; init; } = 55;

    /// <summary>Annual death chance in basis points for an adult below <see cref="OldAge"/>.</summary>
    public int BaseMortalityBp { get; init; } = 120;

    /// <summary>Extra annual death chance per year of age beyond <see cref="OldAge"/>.</summary>
    public int MortalityPerYearOverBp { get; init; } = 260;

    /// <summary>
    /// Chance a married couple of childbearing age produces a child in a year. Tuned so the
    /// named cast sustains itself: the first run of this engine ended with 8 living actors out
    /// of 32 ever, and a world that runs out of people runs out of history a decade early.
    /// </summary>
    public int BirthChanceBp { get; init; } = 3000;

    public int MarriageChanceBp { get; init; } = 2400;

    public int MarriageMinAge { get; init; } = 17;
    public int MarriageMaxAge { get; init; } = 46;

    /// <summary>
    /// Ceiling on the living *named* cast. The engine tracks notables, not the census — towns
    /// carry thousands of people in <see cref="Place.Population"/> and only a few of them ever
    /// get names. Left uncapped the cast grew without bound, which made a 300-year run take
    /// four minutes and would have produced a world with more characters than any reader could
    /// hold. Vacancies are refilled from a town's population when a faction needs a claimant.
    /// </summary>
    public int NamedCastCap { get; init; } = 70;

    // ---- economy ----------------------------------------------------------

    /// <summary>Grain eaten per head per year, expressed as a divisor of population.</summary>
    public int ConsumptionDivisor { get; init; } = 20;

    /// <summary>
    /// Harvest multipliers are the product of a regional roll and a local one, so the low tail
    /// is correlated across the map. Retained for reference and for the bumper-harvest cutoff.
    /// </summary>
    public int HarvestMinPct { get; init; } = 56;
    public int HarvestMaxPct { get; init; } = 151;

    /// <summary>A place with less than this many years of grain in store is starving.</summary>
    public int FamineStockYears { get; init; } = 0;

    /// <summary>
    /// Percent of stored grain lost each year to rot and vermin. Without spoilage, surpluses
    /// compound forever and famine becomes arithmetically impossible after about a decade —
    /// the engine ran sixty years with a permanent granary and never went hungry once.
    /// </summary>
    public int GrainSpoilagePct { get; init; } = 28;

    public int BumperHarvestPct { get; init; } = 128;

    /// <summary>
    /// Deaths from famine and from plague are both a percentage of the settlement's people, so
    /// the two disasters are measured on the same scale. They were not: famine deaths came from
    /// the grain shortfall and plague deaths from raw population, which made the worst plague on
    /// record twenty-seven times deadlier than the worst famine and the economy incoherent.
    /// </summary>
    public int FamineDeathsMinPct { get; init; } = 2;
    public int FamineDeathsMaxPct { get; init; } = 9;
    public int PlagueDeathsMinPct { get; init; } = 5;
    public int PlagueDeathsMaxPct { get; init; } = 18;

    /// <summary>Chance a running plague continues into another year rather than burning out.</summary>
    public int PlaguePersistsPct { get; init; } = 45;

    /// <summary>
    /// A settlement smaller than this does not get its misfortunes announced. Mining sites
    /// shrink to a few dozen people and would otherwise report a famine most years.
    /// </summary>
    public int DisasterReportingFloor { get; init; } = 150;

    /// <summary>Above this, a good harvest is remarkable enough to be worth a line of the log.</summary>
    public int NotableHarvestPct { get; init; } = 142;
    public int PlagueChanceBp { get; init; } = 110;

    // ---- politics ---------------------------------------------------------

    /// <summary>Below this, a faction starts shedding places and attracting plots.</summary>
    public int LegitimacyCrisisThreshold { get; init; } = 30;

    public int RevoltThreshold { get; init; } = 22;
    public int SecessionThreshold { get; init; } = 14;

    /// <summary>Grievance needed before revenge becomes a goal at all.</summary>
    public int GrievanceGoalThreshold { get; init; } = 25;

    /// <summary>Grievance × opportunity needed to actually declare a war.</summary>
    public int WarDeclarationThreshold { get; init; } = 70;

    /// <summary>Percent of grievance that survives each year. Slow decay is what lets a
    /// betrayal in year 12 still be the reason for a war in year 38.</summary>
    public int GrievanceRetentionPct { get; init; } = 96;

    /// <summary>Minimum years a war runs before either side will consider peace.</summary>
    public int MinWarYears { get; init; } = 2;

    // ---- decisions --------------------------------------------------------

    /// <summary>
    /// Weight multiplier applied when an actor considers a target it already has history with.
    /// The single highest-leverage readability dial: without it the cast never recurs and the
    /// reader never forms attachments.
    /// </summary>
    public int ReincorporationBonusPct { get; init; } = 180;

    /// <summary>Years a goal survives before being abandoned.</summary>
    public int GoalLifespan { get; init; } = 12;

    /// <summary>
    /// Years a conspiracy can stay open before it simply comes to nothing. Every plot must end
    /// in a recorded way; this is the backstop for one whose circumstances never change.
    /// </summary>
    public int PlotLifespan { get; init; } = 10;

    /// <summary>Chance per year that an actor with no goal and high ambition invents one.</summary>
    public int AmbitionSparkBp { get; init; } = 1000;
}
