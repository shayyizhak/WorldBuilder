using WorldBuilder.Core.Rules;

namespace WorldBuilder.Core;

/// <summary>Per-year working set handed to each phase. Nothing here survives the tick.</summary>
public sealed class Tick(Chronicle chronicle, SimConfig config, NameForge forge, int year)
{
    public Chronicle Chronicle { get; } = chronicle;
    public WorldState State { get; } = chronicle.State;
    public EventLog Log { get; } = chronicle.Log;
    public SimConfig Config { get; } = config;
    public NameForge Forge { get; } = forge;
    public int Year { get; } = year;

    /// <summary>Thrones that fell empty this year, with the event that emptied them.</summary>
    public List<(EntityId Faction, EventId Cause)> PendingSuccessions { get; } = [];

    /// <summary>
    /// This year's harvest accounting. Carried on the tick so that a faction acting because it
    /// is short of grain can cite the harvest that made it short — the yield event is
    /// world-level and has no participants, so it cannot be found by entity lookup.
    /// </summary>
    public EventId YieldEvent { get; set; }

    /// <summary>
    /// Optional diagnostic sink. Null on an ordinary run, and nothing in the rules reads it, so
    /// attaching one cannot change what the simulation does — which is the only thing that makes
    /// instrumentation trustworthy. Instrumentation that changes the world is not instrumentation.
    /// </summary>
    public Analysis.PlotLedger? Ledger { get; set; }

    public Rng Rng(EntityId entity, RngPurpose purpose) =>
        Core.Rng.For(State.Seed, Year, entity, purpose);

    public Rng Rng(RngPurpose purpose) =>
        Core.Rng.For(State.Seed, Year, EntityId.None, purpose);

    public Event Emit(EventDraft draft) => Chronicle.Emit(draft);
}

/// <summary>
/// The tick loop. Phases run in a fixed order and each visits entities in ascending id, so
/// the whole year is a pure function of (seed, year, state). Order matters for legibility as
/// much as for correctness: pressure builds in Environment, is noticed in Perception, is
/// acted on in Action, and only bites in Consequence — which is why the log reads as cause
/// and effect rather than as a shuffled pile of incidents.
/// </summary>
public sealed class Simulation
{
    private readonly SimConfig _config;
    private readonly NameForge _forge;

    public Simulation(ulong seed, SimConfig? config = null, int startYear = 1)
    {
        _config = config ?? SimConfig.Default;
        _forge = new NameForge(seed);

        State = new WorldState { Seed = seed };
        Log = new EventLog();
        Chronicle = new Chronicle(State, Log);
        StartYear = startYear;

        WorldGen.Generate(Chronicle, _forge, _config, startYear);
    }

    public WorldState State { get; }
    public EventLog Log { get; }
    public Chronicle Chronicle { get; }
    public int StartYear { get; }

    /// <summary>Attach before running to account for every conspiracy. Off by default.</summary>
    public Analysis.PlotLedger? Ledger { get; set; }

    public void Run(int years)
    {
        for (int i = 1; i <= years; i++) Step(StartYear + i);

        AssertEveryPlotTerminated();
    }

    /// <summary>
    /// Every conspiracy that has had time to end must have ended, in exactly one recorded way.
    ///
    /// Deliberately *not* "no plot may be open when the run stops". Sweeping the stragglers into
    /// closure events at the end of a run made the log depend on how long the run was, so a
    /// 50-year history stopped being a prefix of the 100-year one and "replay to year N" no
    /// longer meant anything. A plot opened two years before the records end is genuinely still
    /// pending, and saying so is truthful; inventing an ending for it because the observer
    /// looked away is not.
    /// </summary>
    private void AssertEveryPlotTerminated()
    {
        Dictionary<EventId, int> terminators = [];
        int lastYear = State.Year;
        int pending = 0;

        foreach (Event e in Log.Events)
        {
            // Young plots are exempt: they have not had their allotted years yet.
            if (e.Kind == EventKind.PolityCoupPlotted)
            {
                if (lastYear - e.Year >= _config.PlotLifespan) terminators.TryAdd(e.Id, 0);
                else pending++;
                continue;
            }

            bool ends = e.Kind is EventKind.PolityCoupResolved
                or EventKind.PolityPlotDiesWithPlotter or EventKind.PolityPlotLapses;
            if (!ends) continue;

            foreach (EventId cause in e.Causes)
                if (terminators.ContainsKey(cause)) terminators[cause]++;
        }

        List<string> broken = [];
        foreach ((EventId plot, int count) in terminators)
            if (count != 1) broken.Add($"{plot} has {count} terminating events");

        if (broken.Count > 0)
        {
            throw new InvalidOperationException(
                $"{broken.Count} of {terminators.Count} matured plots did not terminate exactly " +
                $"once ({pending} still within their lifespan): {string.Join(", ", broken.Take(5))}");
        }
    }

    public void Step(int year)
    {
        State.Year = year;
        Chronicle.BeginYear(year);

        Tick tick = new(Chronicle, _config, _forge, year) { Ledger = Ledger };

        LifePhase.Run(tick);
        EconomyPhase.Run(tick);
        PerceptionPhase.Run(tick);
        ActionPhase.Run(tick);
        ResolutionPhase.Run(tick);
        ConsequencePhase.Run(tick);
    }
}
