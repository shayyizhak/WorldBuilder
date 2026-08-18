using System.Globalization;
using WorldBuilder.Core.Geography;
using WorldBuilder.Core.Rules;

namespace WorldBuilder.Core.Analysis;

/// <summary>One world under one arm, reduced to the figures the experiment reads.</summary>
public sealed record ArmResult(
    string Arm, ulong Seed, int RunawayYear, int Shapes, int Events, int TiesEnded, bool ScheduleMatched)
{
    /// <summary>True where this world never reached 70% and its runaway year is censored.</summary>
    public bool Censored => RunawayYear == 0;

    /// <summary>Removals the schedule called for, on the random arm; 0 on every other.</summary>
    public int Scheduled { get; init; }

    /// <summary>Scheduled removals that found no live tie to take.</summary>
    public int Missed { get; init; }
}

/// <summary>
/// The war-rule experiment: four arms, paired on the same seeds and the same boards.
///
/// <b>What it is for.</b> Ruleset 6's three termination rules were taken apart by
/// <c>wb discriminate</c> and the whole regression landed on the war rule — but on two seeds of
/// five, with three untouched. n=5 cannot tell "systematically damaging" from "high-variance, and
/// this panel caught the tail", and those have different dispositions.
///
/// <b>The random arm is the one that makes it an experiment.</b> Without it, a war-versus-null
/// difference is confounded with "trade ties came down": a world knife-edge sensitive to losing
/// ties at all would produce the same contrast, and the fix for that is a world-design problem
/// rather than a rule defect. The random arm removes the same number of ties in the same years as
/// the war arm did on that very seed and board, chosen uniformly at random on a stream no rule
/// reads. Matched per world, never on average — an arm that removed a different count is a
/// different treatment.
///
/// Every figure here is folded from logs. No inference, no model, no render cache.
/// </summary>
public static class WarRulePanel
{
    public const string Null = "null";
    public const string Collapse = "collapse";
    public const string War = "war";
    public const string Random = "random";

    public static readonly string[] Arms = [Null, Collapse, War, Random];

    /// <summary>
    /// The censoring rule, fixed in <c>docs/brief-ruleset-6-disposition-prereg.md</c> before any
    /// panel seed was run.
    ///
    /// A world that never reaches 70% concentration is recorded as <c>&gt; last year</c> and enters
    /// the paired contrast at <c>lastYear + 1</c>. Retaining the pair rather than dropping it is
    /// the conservative choice in the direction that matters: where the null arm is censored and
    /// the treated arm is not, the true shift is at least as large as the measured one, so the
    /// rule can only understate the war rule's effect and never inflate it.
    /// </summary>
    public static int Censor(int runawayYear, int lastYear) =>
        runawayYear == 0 ? lastYear + 1 : runawayYear;

    /// <summary>Runs one seed through all four arms on one board.</summary>
    public static List<ArmResult> RunSeed(ulong seed, Board board, int years)
    {
        List<ArmResult> results = [];

        // The war arm runs before the random arm because the random arm's schedule is taken from
        // it. That ordering is the experiment's design and not an implementation convenience.
        (ArmResult war, List<int> schedule) = RunWar(seed, board, years);

        results.Add(Measure(Null, seed, board, years, TerminationArm.None, null));
        results.Add(Measure(Collapse, seed, board, years, TerminationArm.Collapse, null));
        results.Add(war);
        results.Add(Measure(Random, seed, board, years,
            TerminationArm.Collapse | TerminationArm.RandomTrade, new RandomTieSchedule(schedule)));

        return results;
    }

    private static (ArmResult Result, List<int> Schedule) RunWar(ulong seed, Board board, int years)
    {
        Simulation sim = new(seed, board: board, arm: TerminationArm.War | TerminationArm.Collapse);
        sim.Run(years);

        // One entry per war-caused removal, carrying the year. Read off the record rather than
        // counted in the rule, so the schedule is exactly what the arm actually did.
        List<int> schedule = [];
        foreach (Event e in sim.Log.Events)
        {
            if (e.Kind != EventKind.EconomyTradeCollapse) continue;
            if (e.GetString(RelationTrajectory.CauseField) != RelationEnds.War) continue;
            schedule.Add(e.Year);
        }

        return (Reduce(War, seed, sim, board, matched: true), schedule);
    }

    private static ArmResult Measure(
        string arm, ulong seed, Board board, int years, TerminationArm rules, RandomTieSchedule? schedule)
    {
        Simulation sim = new(seed, board: board, arm: rules) { RandomTies = schedule };
        sim.Run(years);

        return Reduce(arm, seed, sim, board, schedule?.Matched ?? true) with
        {
            Scheduled = schedule?.Years.Count ?? 0,
            Missed = schedule?.Missed ?? 0,
        };
    }

    private static ArmResult Reduce(string arm, ulong seed, Simulation sim, Board board, bool matched)
    {
        WorldView view = WorldView.Build(sim.Log, seed, board);
        WorldStats stats = WorldStats.Compute(view);
        Audit audit = Audit.Compute(view);

        int ended = 0;
        foreach (KindTrajectory k in RelationTrajectory.Of(sim.Log, seed, board).Kinds)
            if (k.Kind == RelationKind.Trade) ended = k.Ended;

        return new ArmResult(arm, seed, stats.RunawayYear, audit.DistinctChainShapes,
            sim.Log.Count, ended, matched);
    }

    /// <summary>
    /// The degeneracy guard, fixed before the run.
    ///
    /// A contrast on a measure that barely varies is a contrast on granularity. The null arm's
    /// runaway year must have a standard deviation of at least <see cref="MinimumYearsOfVariation"/> years
    /// across the panel, and no more than half the panel may be censored — a panel mostly made of
    /// worlds where the metric never fires is measuring censoring, not hegemony.
    /// </summary>
    public const double MinimumYearsOfVariation = 3.0;

    public static string Degeneracy(IReadOnlyList<int> nullArmYears, int censored)
    {
        if (nullArmYears.Count < 2) return "VOID — fewer than two worlds";

        Dispersion sd = Dispersion.Sd(nullArmYears);
        int censoredPct = censored * 100 / nullArmYears.Count;

        string censoredText = censoredPct.ToString(CultureInfo.InvariantCulture);

        if (sd.Figure < MinimumYearsOfVariation)
        {
            return $"VOID — the null arm's runaway year has {sd}, under the " +
                   $"{MinimumYearsOfVariation.ToString("0", CultureInfo.InvariantCulture)} year minimum. " +
                   "The measure cannot express the effect being looked for";
        }

        if (censoredPct > 50)
        {
            return $"VOID — {censoredText}% of the null panel never reaches 70%. The contrast " +
                   "would be measuring censoring rather than hegemony";
        }

        return $"ok — null arm {sd}, {censoredText}% censored";
    }
}
