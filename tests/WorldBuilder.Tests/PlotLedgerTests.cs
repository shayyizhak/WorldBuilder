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
    /// Defect 2, characterised and left alone: no code path can produce a coup win.
    ///
    /// The single emitter of <c>POLITY.COUP_RESOLVED</c> hard-codes <c>mode=exposed</c> and
    /// <c>Outcome.Failed</c>, and the audit only increments its win counter for a mode that is
    /// neither exposed nor abandoned. So the 0% is not a rate that never comes up — it is a
    /// branch that does not exist. "No path exists" and "a path exists and never wins" are
    /// different findings, and this test records which one this is.
    ///
    /// It will fail the moment a win becomes reachable, which is exactly when it should be read
    /// again and deleted.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void NoCoupIsEverWonBecauseNoPathCanWinOne(ulong seed)
    {
        (_, Simulation sim) = Run(seed);

        List<Event> resolutions = [.. sim.Log.Events.Where(e => e.Kind == EventKind.PolityCoupResolved)];

        Assert.NotEmpty(resolutions);
        Assert.All(resolutions, e =>
        {
            Assert.Equal("exposed", e.GetString("mode"));
            Assert.Equal(Outcome.Failed, e.Outcome);
        });
    }

    /// <summary>
    /// And the gate that actually consumes the plots, on every seed: the target dies first.
    ///
    /// This is the reason distribution's head everywhere, by a wide margin, and it is the thing
    /// the next round has to decide about. Recorded as an assertion so a change that moves it
    /// cannot pass unnoticed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void TheCommonestEndingIsThatTheTargetDiedFirst(ulong seed)
    {
        (PlotLedger ledger, Simulation sim) = Run(seed);
        PlotAccounting account = ledger.Account(sim.Log);

        Assert.NotEmpty(account.Ranked);
        Assert.Equal("its target is already dead", account.Ranked[0].Reason);
    }
}
