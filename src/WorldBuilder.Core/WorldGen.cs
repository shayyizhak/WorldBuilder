using WorldBuilder.Core.Geography;

namespace WorldBuilder.Core;

/// <summary>
/// Builds the starting world. Everything — every place, faction, actor and kin edge — is
/// emitted as a genesis event, so the log alone is sufficient to reconstruct the world. That
/// is not ceremony: the <c>why</c>/<c>who</c>/<c>stats</c> commands re-fold from the log file,
/// which means every run silently re-verifies that the log is complete.
///
/// The board is the one thing not in the log, and cannot be: it is hundreds of cells of imported
/// data with a licence of its own. What the log records is which cell each place stands on and
/// the fingerprint of the board those indices mean something in — so the record still says
/// everything about the world, and can still refuse a map that is not the one it was run on.
/// </summary>
public static class WorldGen
{
    public static void Generate(
        Chronicle chronicle, NameForge forge, SimConfig config, int startYear, Board board,
        ProximityControlKind control = ProximityControlKind.None,
        Rules.TerminationArm arm = Rules.TerminationArm.All)
    {
        WorldState state = chronicle.State;
        chronicle.BeginYear(startYear);

        chronicle.Emit(new EventDraft(EventKind.GenesisWorld)
            .Set("seed", state.Seed.ToString())
            .Set("startYear", startYear)
            // Which map this history happened on. The bundle header hashes the file beside the
            // world; this names the board inside the record, so a log carried away from its
            // bundle still knows what its cell indices refer to.
            .Set("board", board.Fingerprint)
            .Set("boardCells", board.Count)
            // In the record as well as in the header. A control world is a diagnostic artefact
            // and a log carried away from its file would otherwise look exactly like a history.
            .SetIf(control != ProximityControlKind.None, "control", ProximityControl.NameOf(control))
            // Likewise for a world run under a subset of its own ruleset. An arm world is
            // internally consistent and about nowhere, and without this it would be
            // indistinguishable from a history by anything but its length.
            .SetIf(arm != Rules.TerminationArm.All, "arm", Rules.TerminationArms.NameOf(arm))
            .Weight(Significance.Bookkeeping));

        // Sited in one pass so every place is spread against every other, whatever kind it is.
        // Doing settlements and mines separately let a mine land on top of a town, because
        // neither pass could see the other's choices.
        List<int> taken = [];

        EntityId region = GenerateRegion(chronicle, forge);
        List<EntityId> settlements = GenerateSettlements(chronicle, forge, config, region, startYear, board, taken);
        List<EntityId> oreSites = GenerateOreSites(chronicle, forge, config, region, startYear, board, taken);
        GenerateFactions(chronicle, forge, config, settlements, oreSites, startYear);
        GenerateActors(chronicle, forge, config, settlements, startYear);
    }

    private static EntityId GenerateRegion(Chronicle chronicle, NameForge forge)
    {
        EntityId id = chronicle.ReservePlace();
        chronicle.Emit(new EventDraft(EventKind.GenesisPlace)
            .Subject(id)
            .Set("name", forge.RegionName(0))
            .Set("placeKind", PlaceKind.Region)
            .Weight(Significance.Bookkeeping));
        return id;
    }

    private static List<EntityId> GenerateSettlements(
        Chronicle chronicle, NameForge forge, SimConfig config, EntityId region, int startYear,
        Board board, List<int> taken)
    {
        List<EntityId> ids = [];
        WorldState state = chronicle.State;

        for (int i = 0; i < config.Settlements; i++)
        {
            EntityId id = chronicle.ReservePlace();
            Rng rng = Rng.For(state.Seed, startYear, id, RngPurpose.Genesis);

            // A stream of its own, so siting the world consumes nothing the population and yield
            // draws below were consuming. Their values are unchanged by geography arriving.
            Rng placing = Rng.For(state.Seed, startYear, id, RngPurpose.Placement);
            int cell = Siting.Choose(board, taken, PlaceKind.Settlement, ref placing);
            taken.Add(cell);

            int population = rng.Range(700, 2400);
            int grain = population / 17;
            int silver = rng.Range(3, 13);

            // Two years of grain in store: enough that a single bad harvest is a scare
            // rather than a catastrophe, so famine reads as an accumulation.
            int stockGrain = (population / config.ConsumptionDivisor) * 2;

            chronicle.Emit(new EventDraft(EventKind.GenesisPlace)
                .Subject(id)
                .Set("name", forge.PlaceName(i, PlaceKind.Settlement))
                .Set("placeKind", PlaceKind.Settlement)
                .Set("parent", region)
                .Set("cell", cell)
                .Set("population", population)
                .Set("yieldGrain", grain)
                .Set("yieldOre", 0)
                .Set("yieldSilver", silver)
                .Set("stockGrain", stockGrain)
                .Set("stockSilver", rng.Range(10, 40))
                .Set("controller", EntityId.Faction(1 + i % config.Factions))
                .Weight(Significance.Bookkeeping));

            ids.Add(id);
        }
        return ids;
    }

    private static List<EntityId> GenerateOreSites(
        Chronicle chronicle, NameForge forge, SimConfig config, EntityId region, int startYear,
        Board board, List<int> taken)
    {
        List<EntityId> ids = [];
        WorldState state = chronicle.State;

        for (int i = 0; i < config.OreSites; i++)
        {
            EntityId id = chronicle.ReservePlace();
            Rng rng = Rng.For(state.Seed, startYear, id, RngPurpose.Genesis);

            Rng placing = Rng.For(state.Seed, startYear, id, RngPurpose.Placement);
            int cell = Siting.Choose(board, taken, PlaceKind.Site, ref placing);
            taken.Add(cell);

            int population = rng.Range(150, 420);

            // Mines cannot feed themselves. Holding one means importing grain, which makes
            // the richest places on the map also the most fragile — and worth fighting over.
            chronicle.Emit(new EventDraft(EventKind.GenesisPlace)
                .Subject(id)
                .Set("name", forge.PlaceName(100 + i, PlaceKind.Site))
                .Set("placeKind", PlaceKind.Site)
                .Set("parent", region)
                .Set("cell", cell)
                .Set("population", population)
                .Set("yieldGrain", population / 60)
                .Set("yieldOre", rng.Range(28, 62))
                .Set("yieldSilver", 0)
                .Set("stockGrain", population / config.ConsumptionDivisor)
                .Set("stockOre", rng.Range(20, 60))
                // The first site is held; the last is unclaimed. An unowned prize on the map
                // from year one gives every faction a reason to move before anything provokes them.
                .Set("controller", i == 0 ? EntityId.Faction(config.Factions) : EntityId.None)
                .Weight(Significance.Bookkeeping));

            ids.Add(id);
        }
        return ids;
    }

    private static void GenerateFactions(
        Chronicle chronicle, NameForge forge, SimConfig config,
        List<EntityId> settlements, List<EntityId> oreSites, int startYear)
    {
        WorldState state = chronicle.State;

        // Each faction gets a different succession rule so all three paths are exercised
        // within a 50-year run rather than one of them lying dead in the code.
        SuccessionRule[] rules =
        [
            SuccessionRule.Primogeniture,
            SuccessionRule.Election,
            SuccessionRule.Strongest,
        ];

        for (int i = 0; i < config.Factions; i++)
        {
            EntityId id = chronicle.ReserveFaction();
            EntityId seat = settlements[i % settlements.Count];
            Rng rng = Rng.For(state.Seed, startYear, id, RngPurpose.Genesis);

            string seatName = state.PlaceOf(seat).Name;
            string founder = forge.PersonName(900 + i).Split(' ')[^1];

            chronicle.Emit(new EventDraft(EventKind.GenesisFaction)
                .Subject(id)
                .At(seat)
                .Set("name", forge.FactionName(i, seatName, founder))
                .Set("succession", rules[i % rules.Length])
                .Set("seat", seat)
                .Set("legitimacy", rng.Range(52, 72))
                .Set("treasury", rng.Range(90, 260))
                .Weight(Significance.Bookkeeping));

            _ = oreSites;
        }
    }

    private static void GenerateActors(
        Chronicle chronicle, NameForge forge, SimConfig config,
        List<EntityId> settlements, int startYear)
    {
        WorldState state = chronicle.State;
        int perFaction = config.StartingActors / config.Factions;
        int slot = 0;

        for (int f = 0; f < config.Factions; f++)
        {
            EntityId faction = EntityId.Faction(f + 1);
            EntityId seat = state.FactionOf(faction).Seat;
            EntityId rulerId = EntityId.None;

            for (int i = 0; i < perFaction; i++)
            {
                EntityId id = chronicle.ReserveActor();
                Rng rng = Rng.For(state.Seed, startYear, id, RngPurpose.Traits);

                bool isRuler = i == 0;
                bool isHeir = i == 1;

                Title title = isRuler ? Title.Ruler
                    : isHeir ? Title.Heir
                    : rng.Next(3) switch
                    {
                        0 => Title.Captain,
                        1 => Title.Steward,
                        _ => Title.Retainer,
                    };

                int age = isRuler ? rng.Range(34, 56)
                    : isHeir ? rng.Range(14, 30)
                    : rng.Range(19, 58);

                EventDraft draft = new EventDraft(EventKind.GenesisActor)
                    .Subject(id)
                    .By(faction)
                    .At(seat)
                    .Set("name", forge.PersonName(slot++))
                    .Set("birthYear", startYear - age)
                    .Set("ambition", isRuler ? rng.Range(40, 92) : rng.Range(12, 95))
                    .Set("guile", rng.Range(10, 92))
                    .Set("martial", rng.Range(10, 92))
                    .Set("loyalty", rng.Range(15, 90))
                    .Set("place", seat)
                    .Set("faction", faction)
                    .Set("title", title)
                    .Weight(Significance.Bookkeeping);

                if (isRuler)
                {
                    rulerId = id;
                }
                else if (isHeir && !rulerId.IsNone)
                {
                    // The heir is kin to the ruler. Everything succession does later hangs
                    // off this one edge existing at genesis.
                    draft.RelBoth(id, rulerId, RelationKind.Kin, 1)
                         .Rel(id, rulerId, RelationKind.Fealty, 40);
                }
                else if (!rulerId.IsNone)
                {
                    draft.Rel(id, rulerId, RelationKind.Fealty, rng.Range(10, 60));
                }

                chronicle.Emit(draft);
            }

            if (!rulerId.IsNone)
            {
                chronicle.Emit(new EventDraft(EventKind.PolitySuccession)
                    .Subject(rulerId)
                    .By(faction)
                    .At(seat)
                    .Set("reason", "founding")
                    .Weight(Significance.Bookkeeping));
            }
        }

        // Whatever is left over goes to the unclaimed places as unaffiliated locals — the
        // pool that breakaway polities later draw their leaders from.
        int remaining = config.StartingActors - perFaction * config.Factions;
        for (int i = 0; i < remaining; i++)
        {
            EntityId id = chronicle.ReserveActor();
            Rng rng = Rng.For(state.Seed, startYear, id, RngPurpose.Traits);
            EntityId home = settlements[rng.Next(settlements.Count)];

            chronicle.Emit(new EventDraft(EventKind.GenesisActor)
                .Subject(id)
                .At(home)
                .Set("name", forge.PersonName(slot++))
                .Set("birthYear", startYear - rng.Range(20, 50))
                .Set("ambition", rng.Range(30, 95))
                .Set("guile", rng.Range(20, 95))
                .Set("martial", rng.Range(20, 95))
                .Set("loyalty", rng.Range(5, 45))
                .Set("place", home)
                .Set("title", Title.Commoner)
                .Weight(Significance.Bookkeeping));
        }
    }
}
