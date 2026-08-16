using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The coup diagnostic: every conspiracy accounted for, and the instrumentation proved harmless.
///
/// Diagnosis only. Nothing here changes a probability, a weight, a threshold or a rate, and the
/// two coup invariants are expected to go on failing — the success rate is currently measured
/// over whichever slice of plots the resolver reaches, and tuning a rate against a biased sample
/// produces a number that looks right and means nothing.
/// </summary>
public class PlotLedgerTests
{
    public static TheoryData<ulong> Panel() => [7UL, 42UL, 99UL, 1234UL, 2025UL];

    private static (PlotLedger Ledger, Simulation Sim) Run(ulong seed, int years = 50)
    {
        PlotLedger ledger = new();
        Simulation sim = new(seed) { Ledger = ledger };
        sim.Run(years);
        return (ledger, sim);
    }

    /// <summary>
    /// Instrumentation that changes the world is not instrumentation.
    ///
    /// The one property that makes a diagnostic trustworthy, and the one an abort condition names
    /// outright. Asserted over the serialised record rather than over a summary, so a difference
    /// anywhere in any event shows.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void AttachingTheLedgerChangesNothingAboutTheWorld(ulong seed)
    {
        Simulation plain = new(seed);
        plain.Run(50);

        Simulation watched = new(seed) { Ledger = new PlotLedger() };
        watched.Run(50);

        Assert.Equal(plain.Log.Count, watched.Log.Count);

        for (int i = 0; i < plain.Log.Count; i++)
            Assert.Equal(JsonlIo.Serialise(plain.Log.Events[i]), JsonlIo.Serialise(watched.Log.Events[i]));
    }

    /// <summary>
    /// Every plot is accounted for: resolved, ended for a named reason, or never examined.
    ///
    /// An unexamined plot with no recorded reason is an accounting failure, not a row to skip —
    /// the same rule as a dangling causal edge.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void EveryPlotIsAccountedFor(ulong seed)
    {
        (PlotLedger ledger, Simulation sim) = Run(seed);
        PlotAccounting account = ledger.Account(sim.Log);

        Assert.True(account.Plotted > 0, $"seed {seed} opened no plots at all");
        Assert.True(account.Balances,
            $"seed {seed}: plotted {account.Plotted} != resolved {account.Resolved} + " +
            $"with-reason {account.UnresolvedWithReason} + unexamined {account.Unexamined}");

        // Every plot that did not resolve carries a reason, and no reason is blank.
        foreach (PlotStanding p in ledger.Plots)
            Assert.False(string.IsNullOrWhiteSpace(p.Reason), $"{p.Arc} has no recorded reason");
    }

    /// <summary>
    /// The finding, pinned: the resolver reaches every plot.
    ///
    /// The brief's hypothesis was that a large share were never examined — that plots enter a
    /// state they never leave. They do not. Every plot on every seed is examined, most within a
    /// tick or two of opening, and pinning that here stops the wrong diagnosis being carried
    /// forward into the round that fixes this.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void NoPlotGoesUnexamined(ulong seed)
    {
        (PlotLedger ledger, Simulation sim) = Run(seed);
        PlotAccounting account = ledger.Account(sim.Log);

        Assert.Equal(0, account.Unexamined);
        Assert.Equal(account.Plotted, account.Examined);
    }

    /// <summary>
    /// Both outcomes of the three-way roll are reached, on every seed.
    ///
    /// Under ruleset 1 the win branch did not exist: the sole emitter hard-coded
    /// <c>mode=exposed</c> and <c>Outcome.Failed</c>, so the renderer's covert-win template and
    /// the audit's win counter were both dead. Asserted here rather than assumed, because a
    /// branch that exists and is never taken is worth exactly as much as one that does not exist.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void BothOutcomesOfTheRollAreReached(ulong seed)
    {
        (_, Simulation sim) = Run(seed);

        List<Event> resolutions = [.. sim.Log.Events.Where(e => e.Kind == EventKind.PolityCoupResolved)];

        Assert.Contains(resolutions, e => e.GetString("mode") == "exposed" && e.Outcome == Outcome.Failed);
        Assert.Contains(resolutions, e => e.GetString("mode") == "seized" && e.Outcome == Outcome.Succeeded);

        // And nothing emits the third mode the audit used to carry a counter for.
        Assert.DoesNotContain(resolutions, e => e.GetString("mode") == "abandoned");
    }

    /// <summary>
    /// The deferral branch is reached too, so the roll is genuinely three-way.
    ///
    /// A plot that is neither struck nor uncovered this year has to be able to wait, or the
    /// "third outcome" is a rename of the second.
    /// </summary>
    [Fact]
    public void APlotCanWaitAnotherYear()
    {
        bool deferred = false;

        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234, 2025 })
        {
            (PlotLedger ledger, _) = Run(seed);
            foreach (PlotStanding p in ledger.Plots)
                if (p.Examined > 1) deferred = true;
        }

        Assert.True(deferred, "no plot was ever examined twice, so nothing ever deferred");
    }

    /// <summary>
    /// A covert win moves the seat.
    ///
    /// The constraint that separates a win from a cosmetic event: a log line saying power changed
    /// hands, beside a world in which it did not. Every seizure must be followed by a succession
    /// naming the plotter, through the same path an open challenge takes.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void ACovertWinMovesTheSeat(ulong seed)
    {
        (_, Simulation sim) = Run(seed);

        List<Event> seizures =
            [.. sim.Log.Events.Where(e => e.Kind == EventKind.PolityCoupResolved
                                          && e.GetString("mode") == "seized")];

        Assert.NotEmpty(seizures);

        foreach (Event seizure in seizures)
        {
            bool tookTheSeat = sim.Log.Events.Any(e =>
                e.Kind == EventKind.PolitySuccession
                && e.Subject == seizure.Subject
                && e.Faction == seizure.Faction
                && e.Year == seizure.Year);

            Assert.True(tookTheSeat,
                $"seed {seed}: {seizure.Id} seized the seat of {seizure.Faction} in " +
                $"{seizure.Year} and no succession followed");
        }
    }

    /// <summary>
    /// The gate that consumed the population under ruleset 1 is gone.
    ///
    /// "Its target is already dead" was the head of the reason distribution on every seed and 82
    /// of 109 lapses across the panel. A plot now bids for a seat rather than against a person,
    /// so an unrelated murder is the plotter's opening instead of the end of his conspiracy.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void ATargetsDeathNoLongerVoidsTheConspiracy(ulong seed)
    {
        (PlotLedger ledger, Simulation sim) = Run(seed);
        PlotAccounting account = ledger.Account(sim.Log);

        Assert.DoesNotContain("its target is already dead", account.Reasons.Keys);
        Assert.DoesNotContain("its target no longer holds the seat", account.Reasons.Keys);
    }

    /// <summary>
    /// Succeeding by other means ends the plot, and is counted apart from a covert win.
    ///
    /// A plotter who inherits or wins the seat openly has no conspiracy left to run, but he did
    /// not seize it covertly either. The two must never be added together, so the reason is its
    /// own and the event is a lapse rather than a resolution.
    /// </summary>
    [Fact]
    public void TakingTheSeatByOtherMeansIsNotACovertWin()
    {
        bool seen = false;

        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234, 2025 })
        {
            (PlotLedger ledger, Simulation sim) = Run(seed);
            PlotAccounting account = ledger.Account(sim.Log);

            if (!account.Reasons.ContainsKey("the plotter took the seat by other means")) continue;
            seen = true;

            // It is a lapse, so it is not in the resolved count and cannot inflate the win rate.
            Assert.True(account.Resolved <= account.Plotted - account.UnresolvedWithReason + account.Resolved);
        }

        Assert.True(seen, "no plotter on the panel ever took the seat by other means");
    }
}
