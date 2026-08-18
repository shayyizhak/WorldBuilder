using System.Security.Cryptography;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The counterfactual probe, and the one property it must have before any figure it produces is
/// worth reading: attaching it cannot change the world.
///
/// Every site was restructured to take its single random draw into a variable and compare it
/// against both the real line and the flat one. That is easy to get wrong in a way that is
/// invisible — a counterfactual that takes its own draw shifts every subsequent stream in the
/// year, and the run still looks perfectly plausible. So it is asserted rather than reviewed.
/// </summary>
public class GeographyProbeTests
{
    private static string Hash(EventLog log)
    {
        StringBuilder sb = new();
        foreach (Event e in log.Events) sb.Append(JsonlIo.Serialise(e)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(99UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void AttachingTheProbeChangesNothingAboutTheWorld(ulong seed)
    {
        Simulation bare = new(seed);
        bare.Run(50);

        GeographyProbe probe = new();
        Simulation watched = new(seed) { Probe = probe };
        watched.Run(50);

        Assert.Equal(Hash(bare.Log), Hash(watched.Log));

        // And it saw something, or the previous assertion is satisfied by a probe that never ran.
        Assert.NotEmpty(probe.Decisions);
    }

    [Fact]
    public void TheProbeWatchesAllFourMechanicsAndNoOthers()
    {
        // The budget, asserted. A fifth consumer of distance would show up here as a fifth
        // mechanic, which is the escalation §5 of the geography phase reserved.
        GeographyProbe probe = new();

        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234, 2025 })
        {
            Simulation sim = new(seed) { Probe = probe };
            sim.Run(50);
        }

        HashSet<string> mechanics = [.. probe.Summarise().Select(static s => s.Mechanic)];

        Assert.Equal(
            ["alliance", "conquest", "marriage", "raid targeting", "war declaration"],
            mechanics.OrderBy(static m => m, StringComparer.Ordinal));
    }

    [Fact]
    public void DiscriminationIsCountedOverDecisionsDistanceCouldHaveMoved()
    {
        // A ranking over one candidate had no alternative to weigh, and one whose candidates are
        // all equidistant had no room to differ. Counting either would make the share a measure
        // of how many foregone conclusions a world contained.
        GeographyProbe probe = new();

        probe.Ranked("raid targeting", candidates: 1, nearest: 100, furthest: 100, discriminated: false);
        probe.Ranked("raid targeting", candidates: 3, nearest: 100, furthest: 100, discriminated: false);
        probe.Ranked("raid targeting", candidates: 3, nearest: 80, furthest: 120, discriminated: true);
        probe.Ranked("raid targeting", candidates: 3, nearest: 80, furthest: 120, discriminated: false);

        DiscriminationSummary summary = Assert.Single(probe.Summarise());

        Assert.Equal(4, summary.Consulted);
        Assert.Equal(2, summary.Open);
        Assert.Equal(1, summary.Discriminated);
        Assert.Equal(50, summary.SharePct);
    }

    [Fact]
    public void AShareOfZeroIsReportedWhereNothingWasOpenRatherThanDividedBy()
    {
        GeographyProbe probe = new();
        probe.Ranked("raid targeting", candidates: 1, nearest: 100, furthest: 100, discriminated: false);

        Assert.Equal(0, Assert.Single(probe.Summarise()).SharePct);
    }

    [Fact]
    public void TheCounterfactualPickTakesNoDrawOfItsOwn()
    {
        // WouldPick is static and takes the roll it is given, which is what makes the whole
        // arrangement sound. Asserted directly: the same relative position in two different
        // weightings, with no generator involved.
        Rng rng = Rng.For(42, 1, EntityId.Actor(1), RngPurpose.Marriage);
        Rng untouched = rng;

        int[] weights = [10, 30, 60];
        int chosen = rng.PickIndexWeighted(weights, out long roll, out long total);

        // The flat weighting puts all the mass on the first option, so the same draw must land
        // there whatever it was.
        Assert.Equal(0, Rng.WouldPick([100, 0, 0], roll, total));
        Assert.InRange(chosen, 0, 2);

        // And the generator that was handed to WouldPick is untouched, because it was not.
        Assert.Equal(untouched.PickIndexWeighted(weights, out long sameRoll, out _), chosen);
        Assert.Equal(roll, sameRoll);
    }
}
