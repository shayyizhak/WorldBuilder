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
    private static readonly ulong[] Panel = ReferencePanel.Current;

    private static string Hash(EventLog log)
    {
        StringBuilder sb = new();
        foreach (Event e in log.Events) sb.Append(JsonlIo.Serialise(e)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Run(ulong seed, bool ledger, bool probe, bool goals = false)
    {
        Simulation sim = new(seed)
        {
            Ledger = ledger ? new PlotLedger() : null,
            Probe = probe ? new GeographyProbe() : null,
        };

        // Attached here rather than through a property on Simulation because the book lives on the
        // state, and it is attachable before the first tick because worldgen forms no goals.
        if (goals) sim.State.Goals.Watch = new GoalCensus();

        sim.Run(50);
        return Hash(sim.Log);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void NoCombinationOfSinksChangesTheWorld(ulong seed)
    {
        string bare = Run(seed, ledger: false, probe: false);

        Assert.Equal(bare, Run(seed, ledger: true, probe: false));
        Assert.Equal(bare, Run(seed, ledger: false, probe: true));
        Assert.Equal(bare, Run(seed, ledger: true, probe: true));

        // The goal census, alone and beside the others. It observes the phase that decides what
        // every actor does, so it is the sink with the most opportunity to disturb a stream.
        Assert.Equal(bare, Run(seed, ledger: false, probe: false, goals: true));
        Assert.Equal(bare, Run(seed, ledger: true, probe: true, goals: true));
    }

    /// <summary>
    /// The engine still produces the worlds the sealed ruleset-4 baselines were cut from, event
    /// for event.
    ///
    /// <b>The strong form of the standing rule.</b> The theory above proves that attaching a
    /// known sink changes nothing; this proves that nothing at all has changed the worlds since
    /// they were archived — which is the claim every measurement taken against those baselines
    /// silently rests on, and which no test made. A phase that adds instruments elsewhere in the
    /// tree can satisfy the theory above while having moved a draw somewhere it does not reach.
    ///
    /// <b>The header is excluded and only the header.</b> A world file opens with a provenance
    /// line carrying the engine commit and the artefact manifest, and both move for reasons that
    /// are not the world — a commit is not a ruleset. Every event line after it is compared
    /// verbatim.
    ///
    /// This fails on a genuine ruleset change, and correctly: at that point the worlds are
    /// different histories and the baselines are recut. It is a detector for a world that moved
    /// while everyone believed the ruleset had not.
    ///
    /// <b>Now pointed at ruleset 5, and failing until that set is cut.</b> Recording
    /// <c>DIPLO.ALLIANCE_BROKEN</c> renumbers every id after the first insertion and rekeys the
    /// rest of each affected year, so a verbatim replay of the ruleset-4 files cannot pass and
    /// should not be made to. The world itself did not move, and that claim is carried by
    /// <see cref="AdditiveRecordTests"/> against those same ruleset-4 files — which is the
    /// stronger statement and the one worth keeping. This theory resumes its own job the moment
    /// <c>baselines/ruleset-5/</c> exists, and until then its failure names the work that is owed
    /// rather than a defect.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheEngineStillReproducesTheSealedBaselines(ulong seed)
    {
        string path = WorldBuilder.Inference.Corpus.SealedWorld($"ruleset-{Ruleset.Version}", seed,
                          AppContext.BaseDirectory, Directory.GetCurrentDirectory())
                      ?? throw new FileNotFoundException(
                          $"no sealed baselines/ruleset-{Ruleset.Version}/seed-{seed} — the ruleset " +
                          "bumped and the set it owes has not been cut");

        (EventLog archived, ulong archivedSeed) = JsonlIo.Read(path);
        Assert.Equal(seed, archivedSeed);

        Simulation sim = new(seed);
        sim.Run(50);

        Assert.Equal(archived.Count, sim.Log.Count);
        Assert.Equal(Hash(archived), Hash(sim.Log));
    }

    [Fact]
    public void TheSinksBetweenThemActuallyObserveSomething()
    {
        // Without this the theory above is satisfied by two sinks that never fire, which is the
        // shape of a test that reports coverage it does not have.
        PlotLedger ledger = new();
        GeographyProbe probe = new();
        GoalCensus goals = new();

        Simulation sim = new(42) { Ledger = ledger, Probe = probe };
        sim.State.Goals.Watch = goals;
        sim.Run(50);

        Assert.NotEmpty(ledger.Plots);
        Assert.NotEmpty(probe.Decisions);

        // Every arm of the census, so an invariance theory satisfied by a sink that sees nothing
        // cannot pass. Creation, advance and ending are three separate paths through the book.
        Assert.NotEmpty(goals.Created);
        Assert.NotEmpty(goals.Advanced);
        Assert.NotEmpty(goals.Ended);
        Assert.NotEmpty(goals.Refused);
    }
}
