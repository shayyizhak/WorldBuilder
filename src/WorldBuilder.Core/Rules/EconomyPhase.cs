namespace WorldBuilder.Core.Rules;

/// <summary>
/// Phase 2 — harvest, consumption, and the threshold events that scarcity produces.
///
/// The routine arithmetic is deliberately collapsed into a single bookkeeping event per year:
/// the reducer still sees every grain of it, so the fold stays honest, but the readable log
/// only ever shows the moments where a number crossed a line and became a story.
/// </summary>
public static class EconomyPhase
{
    public static void Run(Tick tick)
    {
        WorldState state = tick.State;

        EventDraft yield = new EventDraft(EventKind.EconomyYield)
            .Weight(Significance.Bookkeeping)
            .Hidden(Visibility.Public);

        List<(Place Place, int Shortfall, int HarvestPct)> outcomes = [];
        Dictionary<EntityId, int> projected = [];
        List<Place> worked = [];

        // Weather is regional. Rolling each place independently meant a faction holding four
        // settlements essentially never went hungry once it could move grain between them, and
        // famine — the engine's main non-political pressure — stopped firing at all. One shared
        // roll per year makes a bad harvest a thing that happens to everybody, which is both
        // how it works and far more readable: a lean year is a world event, not a local one.
        Rng seasons = tick.Rng(RngPurpose.Harvest);
        int regionPct = seasons.Range(70, 126);

        foreach (Place place in state.Places)
        {
            if (place.Kind == PlaceKind.Region) continue;

            Rng rng = tick.Rng(place.Id, RngPurpose.Harvest);
            int harvestPct = regionPct * rng.Range(80, 121) / 100;

            int grown = place.YieldOf(Resource.Grain) * harvestPct / 100;
            int eaten = place.Population / tick.Config.ConsumptionDivisor;
            int spoiled = place.Stockpile[(int)Resource.Grain] * tick.Config.GrainSpoilagePct / 100;
            int net = grown - eaten - spoiled;

            yield.Stock(place.Id, Resource.Grain, net)
                 .Stock(place.Id, Resource.Ore, place.YieldOf(Resource.Ore))
                 .Stock(place.Id, Resource.Silver, place.YieldOf(Resource.Silver))
                 .Set($"harvest:{place.Id}", harvestPct);

            projected[place.Id] = place.Stockpile[(int)Resource.Grain] + net;
            worked.Add(place);
            outcomes.Add((place, 0, harvestPct));
        }

        Redistribute(tick, yield, projected);

        for (int i = 0; i < outcomes.Count; i++)
        {
            (Place place, _, int harvestPct) = outcomes[i];
            outcomes[i] = (place, Math.Max(0, -projected[place.Id]), harvestPct);
        }

        Event yieldEvent = tick.Emit(yield);
        tick.YieldEvent = yieldEvent.Id;

        foreach ((Place place, int shortfall, int harvestPct) in outcomes)
        {
            if (shortfall > 0) Famine(tick, place, shortfall, yieldEvent.Id);
            else if (harvestPct >= tick.Config.BumperHarvestPct) Bumper(tick, place, harvestPct, yieldEvent.Id);

            Plague(tick, place, yieldEvent.Id);
        }

        CollectTaxes(tick, yieldEvent.Id);
    }

    /// <summary>
    /// A faction feeds its own. Mines produce almost no grain by design — that is what makes
    /// them expensive to hold — but without this the sites simply starved on a loop, which
    /// filled the log with a famine every year at the same two places and told the reader
    /// nothing. Now a faction only starves once its whole territory is short, which is a fact
    /// worth reading.
    /// </summary>
    private static void Redistribute(Tick tick, EventDraft yield, Dictionary<EntityId, int> projected)
    {
        WorldState state = tick.State;

        foreach (Faction faction in state.Factions)
        {
            List<Place> holdings = state.HoldingsOf(faction.Id);
            if (holdings.Count < 2) continue;

            foreach (Place hungry in holdings)
            {
                int deficit = -projected[hungry.Id];
                if (deficit <= 0) continue;

                foreach (Place spare in holdings)
                {
                    if (deficit <= 0) break;
                    if (spare.Id == hungry.Id) continue;

                    // A place keeps one year's eating before anything is shipped out.
                    int reserve = spare.Population / tick.Config.ConsumptionDivisor;
                    int available = projected[spare.Id] - reserve;
                    if (available <= 0) continue;

                    int moved = Math.Min(available, deficit);
                    projected[spare.Id] -= moved;
                    projected[hungry.Id] += moved;
                    deficit -= moved;

                    yield.Stock(spare.Id, Resource.Grain, -moved)
                         .Stock(hungry.Id, Resource.Grain, moved);
                }
            }
        }
    }

    private static void Famine(Tick tick, Place place, int shortfall, EventId cause)
    {
        // A share of the settlement's people, scaled by how deep the shortfall runs. Deriving
        // this from the grain figure instead produced famines that killed eight people out of a
        // thousand — arithmetically consistent, and absurd to read.
        int severity = Math.Clamp(
            tick.Config.FamineDeathsMinPct + shortfall / 2,
            tick.Config.FamineDeathsMinPct,
            tick.Config.FamineDeathsMaxPct);
        int deaths = Math.Max(1, place.Population * severity / 100);

        // A hamlet of forty souls having a bad year is not a famine, and reporting it as one
        // filled the log with three-death disasters that dragged the median so far below
        // plague's that the two read as different units. The people are still lost; the event
        // simply is not announced.
        bool worthReporting = place.Population >= tick.Config.DisasterReportingFloor;

        Arc? running = FindOpenFamine(tick, place.Id);
        EntityId arc = running?.Id ?? tick.Chronicle.ReserveArc();
        int yearsHungry = running is null ? 0 : tick.Year - running.StartYear;

        Rng rng = tick.Rng(place.Id, RngPurpose.Harvest).Branch(7);

        EventDraft draft = new EventDraft(EventKind.EconomyFamine)
            .At(place.Id)
            .By(place.Controller)
            .Set("shortfall", shortfall)
            .Set("deaths", deaths)
            .Set("ofPop", place.Population)
            .Set("years", yearsHungry + 1)
            .Pop(place.Id, -deaths)
            .Because(cause)
            .InArc(arc)
            .Weight(worthReporting ? Significance.Major : Significance.Bookkeeping);

        if (running is null)
            draft.Set("arcName", ArcNames.Famine(ref rng, place.Name, tick.Year));
        else
            draft.Because(running.Origin);

        if (!place.Controller.IsNone) draft.Leg(place.Controller, -6);

        // From the second year onward people stop waiting and leave. This is what turns a
        // famine from a number that repeats into a state that changes: the place shrinks until
        // it can feed itself, so a settlement cannot starve identically five years running.
        if (yearsHungry >= 1) Migrate(tick, place, draft, ref rng);

        tick.Emit(draft);
    }

    /// <summary>
    /// Moves a share of a starving town's people to the best-fed place under the same flag,
    /// or simply out of the world if their rulers have nowhere better. Self-limiting by
    /// construction: fewer mouths means less consumed, so the famine ends on its own.
    /// </summary>
    private static void Migrate(Tick tick, Place place, EventDraft draft, ref Rng rng)
    {
        int leaving = Math.Max(20, place.Population / rng.Range(5, 9));
        leaving = Math.Min(leaving, place.Population - 20);
        if (leaving <= 0) return;

        Place? refuge = null;
        foreach (Place candidate in tick.State.HoldingsOf(place.Controller))
        {
            if (candidate.Id == place.Id) continue;
            int spare = candidate.Stockpile[(int)Resource.Grain] - candidate.Population / tick.Config.ConsumptionDivisor;
            if (spare <= 0) continue;
            if (refuge is null || spare > refuge.Stockpile[(int)Resource.Grain]) refuge = candidate;
        }

        draft.Pop(place.Id, -leaving).Set("left", leaving);

        if (refuge is not null) draft.Pop(refuge.Id, leaving).Set("refuge", refuge.Id);
    }

    private static Arc? FindOpenFamine(Tick tick, EntityId place) =>
        FindOpenArcAt(tick, place, ArcKind.Famine);

    /// <summary>A disaster that recurs at the same place year on year is the same disaster.</summary>
    private static Arc? FindOpenArcAt(Tick tick, EntityId place, ArcKind kind)
    {
        foreach (Arc arc in tick.State.OpenArcs())
            if (arc.Kind == kind && arc.Sides.Contains(place)) return arc;
        return null;
    }

    private static void Bumper(Tick tick, Place place, int harvestPct, EventId cause)
    {
        // A good year is only worth reading if it is exceptional, or if it is the year a
        // hunger broke. Routine abundance still feeds people — the state change is applied
        // either way — but at one line per good harvest per place it was the single largest
        // source of verbatim repetition in the log and it carried no information.
        bool exceptional = harvestPct >= tick.Config.NotableHarvestPct;
        bool endsHunger = Recent.Did(tick, place.Id, EventKind.EconomyFamine, place.Id, 2);

        EventDraft draft = new EventDraft(EventKind.EconomyBumperHarvest)
            .At(place.Id)
            .By(place.Controller)
            .Set("harvestPct", harvestPct)
            .Pop(place.Id, place.Population / 60)
            .Because(cause) // the yield roll genuinely produced this surplus
            .Weight(exceptional || endsHunger ? Significance.Minor : Significance.Bookkeeping);

        if (!place.Controller.IsNone) draft.Leg(place.Controller, 2);

        tick.Emit(draft);
    }

    /// <summary>
    /// Plague, which now behaves like famine: it runs over years, it drives people out, and it
    /// ends in a recorded way. Previously it fired as a one-year spike that never resolved.
    /// </summary>
    private static void Plague(Tick tick, Place place, EventId cause)
    {
        Rng rng = tick.Rng(place.Id, RngPurpose.Disease);
        Arc? running = FindOpenArcAt(tick, place.Id, ArcKind.Plague);

        bool struck = running is not null
            ? rng.Chance(tick.Config.PlaguePersistsPct)
            : rng.ChanceBp(tick.Config.PlagueChanceBp);

        if (!struck) return;

        EntityId arc = running?.Id ?? tick.Chronicle.ReserveArc();
        int yearsRunning = running is null ? 0 : tick.Year - running.StartYear;

        int severity = rng.Range(tick.Config.PlagueDeathsMinPct, tick.Config.PlagueDeathsMaxPct + 1);
        int deaths = Math.Max(1, place.Population * severity / 100);

        // No cause: a plague is not brought on by the harvest. It merely arrives in the same
        // year, and recording the yield as its parent was co-occurrence dressed as causation.
        EventDraft draft = new EventDraft(EventKind.EconomyPlague)
            .At(place.Id)
            .By(place.Controller)
            .Set("deaths", deaths)
            .Set("ofPop", place.Population)
            .Set("years", yearsRunning + 1)
            .Pop(place.Id, -deaths)
            .InArc(arc)
            .Weight(place.Population >= tick.Config.DisasterReportingFloor
                ? Significance.Major : Significance.Bookkeeping);

        if (running is null) draft.Set("arcName", $"the Pestilence of {place.Name}");
        else draft.Because(running.Origin);

        if (!place.Controller.IsNone) draft.Leg(place.Controller, -3);
        if (yearsRunning >= 1) Migrate(tick, place, draft, ref rng);

        tick.Emit(draft);
        _ = cause;
    }

    /// <summary>
    /// Silver flows from places to the faction that holds them. Folded into the yearly
    /// bookkeeping event rather than emitted separately — nobody wants to read a tax return.
    /// </summary>
    private static void CollectTaxes(Tick tick, EventId cause)
    {
        WorldState state = tick.State;

        EventDraft draft = new EventDraft(EventKind.EconomyYield)
            .Because(cause)
            .Weight(Significance.Bookkeeping)
            .Set("kind", "taxes");

        bool any = false;
        foreach (Faction faction in state.Factions)
        {
            int taxed = 0;
            foreach (Place place in state.HoldingsOf(faction.Id))
            {
                int take = place.Stockpile[(int)Resource.Silver] / 3;
                if (take <= 0) continue;
                draft.Stock(place.Id, Resource.Silver, -take);
                taxed += take;
            }

            if (taxed > 0)
            {
                draft.Treas(faction.Id, taxed);
                any = true;
            }
        }

        if (any) tick.Emit(draft);
    }
}
