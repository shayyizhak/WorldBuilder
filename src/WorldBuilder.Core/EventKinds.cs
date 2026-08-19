namespace WorldBuilder.Core;

/// <summary>Dotted display names for event kinds. The log is grepped by these, so they are stable.</summary>
public static class EventKinds
{
    public static string Name(EventKind kind) => kind switch
    {
        EventKind.GenesisWorld => "GENESIS.WORLD",
        EventKind.GenesisPlace => "GENESIS.PLACE",
        EventKind.GenesisFaction => "GENESIS.FACTION",
        EventKind.GenesisActor => "GENESIS.ACTOR",

        EventKind.LifeBirth => "LIFE.BIRTH",
        EventKind.LifeComingOfAge => "LIFE.COMING_OF_AGE",
        EventKind.LifeDeathNatural => "LIFE.DEATH_NATURAL",
        EventKind.LifeDeathViolent => "LIFE.DEATH_VIOLENT",
        EventKind.LifeMarriage => "LIFE.MARRIAGE",

        EventKind.EconomyYield => "ECONOMY.YIELD",
        EventKind.EconomyFamine => "ECONOMY.FAMINE",
        EventKind.EconomyFamineEnds => "ECONOMY.FAMINE_ENDS",
        EventKind.EconomyPlagueEnds => "ECONOMY.PLAGUE_ENDS",
        EventKind.EconomyBumperHarvest => "ECONOMY.BUMPER_HARVEST",
        EventKind.EconomyPlague => "ECONOMY.PLAGUE",
        EventKind.EconomyTradePact => "ECONOMY.TRADE_PACT",
        EventKind.EconomyTradeCollapse => "ECONOMY.TRADE_COLLAPSE",

        EventKind.PolitySuccession => "POLITY.SUCCESSION",
        EventKind.PolitySuccessionDisputed => "POLITY.SUCCESSION_DISPUTED",
        EventKind.PolityCoupPlotted => "POLITY.COUP_PLOTTED",
        EventKind.PolityCoupResolved => "POLITY.COUP_RESOLVED",
        EventKind.PolityChallenge => "POLITY.CHALLENGE",
        EventKind.PolityPlotDiesWithPlotter => "POLITY.PLOT_DIES_WITH_PLOTTER",
        EventKind.PolityPlotLapses => "POLITY.PLOT_LAPSES",
        EventKind.PolityCourtsSupport => "POLITY.COURTS_SUPPORT",
        EventKind.PolityExile => "POLITY.EXILE",
        EventKind.PolityExileReturn => "POLITY.EXILE_RETURN",
        EventKind.PolityAppointment => "POLITY.APPOINTMENT",
        EventKind.PolityLegitimacyCrisis => "POLITY.LEGITIMACY_CRISIS",
        EventKind.PolityRevolt => "POLITY.REVOLT",
        EventKind.PolitySecession => "POLITY.SECESSION",
        EventKind.PolityPartition => "POLITY.PARTITION",
        EventKind.PolityCollapse => "POLITY.COLLAPSE",

        EventKind.DiploInsult => "DIPLO.INSULT",
        EventKind.DiploTributeDemanded => "DIPLO.TRIBUTE_DEMANDED",
        EventKind.DiploAllianceFormed => "DIPLO.ALLIANCE_FORMED",
        EventKind.DiploAllianceBroken => "DIPLO.ALLIANCE_BROKEN",
        EventKind.DiploWarDeclared => "DIPLO.WAR_DECLARED",
        EventKind.DiploPeaceSigned => "DIPLO.PEACE_SIGNED",

        EventKind.ConflictRaid => "CONFLICT.RAID",
        EventKind.ConflictBattle => "CONFLICT.BATTLE",
        EventKind.ConflictSiege => "CONFLICT.SIEGE",
        EventKind.ConflictConquest => "CONFLICT.CONQUEST",
        EventKind.ConflictAssassination => "CONFLICT.ASSASSINATION",

        EventKind.IntrigueBetrayal => "INTRIGUE.BETRAYAL",
        EventKind.IntrigueGrievanceSettled => "INTRIGUE.GRIEVANCE_SETTLED",

        // A family of its own, because a goal belongs to a faction or to an actor indifferently and
        // filing it under POLITY or INTRIGUE would misname half of them. `GOALS.` is also the prefix
        // anybody reading the record will want to grep out: these are bookkeeping rows.
        EventKind.GoalsFormed => "GOALS.FORMED",
        EventKind.GoalsEnded => "GOALS.ENDED",

        _ => kind.ToString().ToUpperInvariant(),
    };

    private static readonly Dictionary<string, EventKind> ByName = BuildLookup();

    private static Dictionary<string, EventKind> BuildLookup()
    {
        Dictionary<string, EventKind> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (EventKind k in Enum.GetValues<EventKind>()) map[Name(k)] = k;
        return map;
    }

    public static bool TryParse(string name, out EventKind kind) => ByName.TryGetValue(name, out kind);
}
