using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The two properties the staging loop's conclusions rest on.
///
/// Not a test of the artefacts — those are machine-derived prompts for a human session and nobody
/// has read them, so there is nothing to assert about their content. What is assertable is that the
/// loop's two load-bearing claims are the kind of claim that could be false.
/// </summary>
public class ReferenceStagingTests
{
    private static WorldView World(ulong seed = 42)
    {
        Simulation sim = new(seed);
        sim.Run(50);
        return WorldView.Build(sim.Log, seed);
    }

    /// <summary>
    /// The §1.1 detector can fire.
    ///
    /// <b>This is the whole value of that check.</b> "No goal row reached retrieval" is worthless
    /// unless something demonstrates a goal row *can* be retrieved — otherwise it is the silent-path
    /// family, a pass produced by a probe that does not work, and the first version of the probe was
    /// exactly that: the adversarial plan tripped the world-wide cap and returned zero alongside
    /// every other path.
    /// </summary>
    [Fact]
    public void TheGoalRowProbeCanActuallyRetrieveAGoalRow()
    {
        WorldView view = World();
        QueryEngine engine = new(new CacheOnlyLlmClient("none"), view);

        List<RetrievalProbe> probes = ReferenceStaging.GoalRowReach(engine, view);

        Assert.Contains(probes, static p => p.Reached > 0);

        // And the ordinary paths reach none, which is the answer the loop reports. If this ever
        // fails it is a finding about the query layer, not about this test.
        foreach (RetrievalProbe p in probes)
        {
            if (p.Note.StartsWith("adversarial", StringComparison.Ordinal)) continue;
            Assert.Equal(0, p.Reached);
        }
    }

    /// <summary>
    /// The record split covers the record exactly once, and nothing with a party to it is filed as
    /// bookkeeping.
    ///
    /// The second half is the clause that took two attempts. Splitting on `Significance` alone put the
    /// founding successions and the genesis rows — which the facts sheet's ruler lists and Powers
    /// section are derived from — into the file the sheet is told not to draw on.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheRecordSplitPartitionsTheRecord(ulong seed)
    {
        WorldView view = World(seed);

        int book = 0, history = 0;
        foreach (Event e in view.Log.Events)
        {
            if (ReferenceStaging.IsBookkeeping(e))
            {
                book++;
                Assert.Empty(e.Participants);
            }
            else
            {
                history++;
            }
        }

        Assert.Equal(view.Log.Count, book + history);

        // Both halves are non-empty, so neither assertion above is satisfied by a split that put
        // everything on one side.
        Assert.True(book > 0);
        Assert.True(history > 0);

        // The goal rows are all on the bookkeeping side — the loop's §2 premise about them.
        foreach (Event e in view.Log.Events)
            if (ReferenceStaging.IsGoalRow(e)) Assert.True(ReferenceStaging.IsBookkeeping(e));
    }

    /// <summary>
    /// A famine or plague run is one episode, not every occurrence at a place.
    ///
    /// The derivation this replaced summed nineteen famine records spread over forty-seven years into
    /// a single claim and staged it as "how many died in the famine at Meigate". A wrong engine figure,
    /// which this project holds to be worse than a wrong model figure because nothing questions it.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void EveryDisasterRunIsOneEpisode(ulong seed)
    {
        WorldView view = World(seed);

        int runs = 0;
        foreach (EventKind kind in new[] { EventKind.EconomyFamine, EventKind.EconomyPlague })
        {
            foreach ((EntityId where, List<Event> run) in ReferenceSet.Runs(view, kind))
            {
                runs++;

                // One arc per run, or a single event that belongs to none.
                HashSet<EntityId> arcs = [.. run.Select(static e => e.Arc)];
                Assert.Single(arcs);
                if (arcs.Single().IsNone) Assert.Single(run);

                // Contiguous in the sense that matters: the arc closes when a year passes without the
                // place being touched, so a run cannot contain a gap wider than the whole arc.
                for (int i = 1; i < run.Count; i++) Assert.True(run[i].Year >= run[i - 1].Year);
                Assert.All(run, e => Assert.Equal(where, e.Where));
            }
        }

        Assert.True(runs > 0, "no famine or plague on this seed, so nothing above was exercised");
    }
}
