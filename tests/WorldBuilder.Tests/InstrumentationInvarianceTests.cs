using System.Security.Cryptography;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// <b>Standing rule: attaching a measurement must not change the world.</b>
///
/// Promoted out of the geography probe, because it is larger than the phase that produced it.
/// Every future probe adopts it, and the assertion is always the same one — hash the full event
/// log with the instrumentation and without it, across the whole seed panel, and require
/// equality. Nothing weaker detects the failure: a run with a perturbed stream is a perfectly
/// plausible run, every existing test stays green, and the only symptom is that the figures
/// describe a world nobody else will ever produce.
///
/// <b>Standing rule: RNG draw order is load-bearing.</b>
///
/// This is the sharper half and it is a constraint on Stage 3's determinism guarantee rather
/// than on instrumentation. Reproducibility is not a property of the rules alone; it is a
/// property of the rules <i>and the order in which they consume the stream</i>. A pure
/// refactor — one that changes no logic, no threshold and no branch — can change every world
/// from that year on, if it moves a draw across a short-circuit.
///
/// The worked example, kept because it is the only one anybody will believe: the conquest site
/// reads
/// <code>
/// attackerWon &amp;&amp; margin &gt; … &amp;&amp; rng.Chance(…) &amp;&amp; field.Controller == defender.Id
/// </code>
/// so the die is thrown <i>before</i> the holder is checked, and a battle on ground the defender
/// had already lost still consumes its draw. Hoisting the holder check into the guard — which
/// reads as obviously equivalent, and is what anybody would write — silently stops drawing in
/// those cases and re-sequences every stream after it. Every test in the suite stayed green.
///
/// So the check runs over the whole mechanic set rather than only over instrumented sites: each
/// available sink is attached alone and together, and all of them must leave the log identical.
/// The sinks between them touch conspiracies and all four distance-consuming mechanics, which is
/// the coverage that makes this a detector rather than a spot check.
/// </summary>
public class InstrumentationInvarianceTests
{
    private static readonly ulong[] Panel = [7, 42, 99, 1234, 2025];

    private static string Hash(EventLog log)
    {
        StringBuilder sb = new();
        foreach (Event e in log.Events) sb.Append(JsonlIo.Serialise(e)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Run(ulong seed, bool ledger, bool probe)
    {
        Simulation sim = new(seed)
        {
            Ledger = ledger ? new PlotLedger() : null,
            Probe = probe ? new GeographyProbe() : null,
        };

        sim.Run(50);
        return Hash(sim.Log);
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(99UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void NoCombinationOfSinksChangesTheWorld(ulong seed)
    {
        string bare = Run(seed, ledger: false, probe: false);

        Assert.Equal(bare, Run(seed, ledger: true, probe: false));
        Assert.Equal(bare, Run(seed, ledger: false, probe: true));
        Assert.Equal(bare, Run(seed, ledger: true, probe: true));
    }

    [Fact]
    public void TheSinksBetweenThemActuallyObserveSomething()
    {
        // Without this the theory above is satisfied by two sinks that never fire, which is the
        // shape of a test that reports coverage it does not have.
        PlotLedger ledger = new();
        GeographyProbe probe = new();

        Simulation sim = new(42) { Ledger = ledger, Probe = probe };
        sim.Run(50);

        Assert.NotEmpty(ledger.Plots);
        Assert.NotEmpty(probe.Decisions);
    }
}
