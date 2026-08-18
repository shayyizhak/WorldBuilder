using WorldBuilder.Core.Geography;
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

    /// <summary>
    /// Optional counterfactual sink for the distance-consuming decisions. Null on an ordinary
    /// run, read by no rule, and — the part that matters — no site consults it before drawing.
    /// Every counterfactual reuses the draw the real decision already took.
    /// </summary>
    public Analysis.GeographyProbe? Probe { get; set; }

    /// <summary>
    /// Which termination rules this run may use. <see cref="TerminationArm.All"/> on any real
    /// world; anything else is a diagnostic arm and the header says so.
    /// </summary>
    public TerminationArm Arm { get; init; } = TerminationArm.All;

    /// <summary>The random arm's schedule, or null on every run that is not that arm.</summary>
    public RandomTieSchedule? RandomTies { get; init; }

    /// <summary>Whether one termination rule is switched on for this run.</summary>
    public bool Allows(TerminationArm rule) => (Arm & rule) != 0;

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

    /// <summary>
    /// A new world on a board.
    ///
    /// <paramref name="board"/> defaults to the repository's stored artefact rather than to
    /// nothing, and there is no path here that makes one: §2 settles that a map is imported once
    /// and thereafter carried, and prohibition 5 restates it. A world with no board would be a
    /// world in which every distance came out exactly typical, which is the sort of quiet,
    /// plausible uniformity this project has learned to distrust.
    /// </summary>
    public Simulation(
        ulong seed,
        SimConfig? config = null,
        int startYear = 1,
        Board? board = null,
        ProximityControlKind control = ProximityControlKind.None,
        TerminationArm arm = TerminationArm.All)
    {
        _config = config ?? SimConfig.Default;
        _forge = new NameForge(seed);

        State = new WorldState { Seed = seed };
        Log = new EventLog();
        Chronicle = new Chronicle(State, Log);
        StartYear = startYear;
        Control = control;
        Arm = arm;

        Board playing = board ?? Boards.Stored();
        State.Attach(playing);

        WorldGen.Generate(Chronicle, _forge, _config, startYear, playing, control, arm);

        // Attached after worldgen, because the empirical distribution it draws from is the set of
        // proximities this world's places present — which does not exist until they are sited.
        // That ordering is the point of the control: same distribution, same clamp exposure, and
        // only the origin of the values changed.
        if (control != ProximityControlKind.None) State.UseControl(control);
    }

    /// <summary>
    /// Which synthetic distance model this world ran under, if any.
    ///
    /// Anything but <see cref="ProximityControlKind.None"/> makes this a diagnostic artefact
    /// rather than a world: it is marked in the file header and in the genesis event, and
    /// <c>wb baseline cut</c> refuses it.
    /// </summary>
    public ProximityControlKind Control { get; }

    public WorldState State { get; }
    public EventLog Log { get; }
    public Chronicle Chronicle { get; }
    public int StartYear { get; }

    /// <summary>Attach before running to account for every conspiracy. Off by default.</summary>
    public Analysis.PlotLedger? Ledger { get; set; }

    /// <summary>Attach before running to measure what distance actually decided. Off by default.</summary>
    public Analysis.GeographyProbe? Probe { get; set; }

    /// <summary>
    /// Which of ruleset 6's termination rules this run is allowed to use.
    ///
    /// Anything but <see cref="TerminationArm.All"/> makes this a diagnostic artefact rather than a
    /// world, on exactly the same footing as a proximity control: it is marked in the file header
    /// and in the genesis event, and <c>wb baseline cut</c> refuses it.
    /// </summary>
    public TerminationArm Arm { get; }

    /// <summary>
    /// The schedule of random trade-tie removals, for the discriminating arm of the war-rule
    /// experiment. Set before <see cref="Run"/>, like <see cref="Ledger"/> and <see cref="Probe"/>.
    ///
    /// Required by <see cref="TerminationArm.RandomTrade"/> and refused without it: an arm that
    /// silently removed nothing would publish the collapse arm's figures under the random arm's
    /// name, which is the quietest way an experiment can lie.
    /// </summary>
    public RandomTieSchedule? RandomTies { get; set; }

    public void Run(int years)
    {
        if ((Arm & TerminationArm.RandomTrade) != 0 && RandomTies is null)
        {
            throw new InvalidOperationException(
                "the random arm needs a schedule. Set Simulation.RandomTies from the war arm's " +
                "own removals on this seed and board — an unmatched random arm measures a " +
                "different treatment, and an empty one measures the collapse arm.");
        }

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

        Tick tick = new(Chronicle, _config, _forge, year)
        {
            Ledger = Ledger, Probe = Probe, Arm = Arm, RandomTies = RandomTies,
        };

        LifePhase.Run(tick);
        EconomyPhase.Run(tick);
        PerceptionPhase.Run(tick);
        ActionPhase.Run(tick);
        ResolutionPhase.Run(tick);
        ConsequencePhase.Run(tick);
    }
}
