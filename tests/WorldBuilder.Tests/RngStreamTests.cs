using WorldBuilder.Core;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Guards the single most consequential decision in v0: random draws are derived per
/// (seed, year, entity, purpose) rather than pulled from one shared sequence.
///
/// v2 back-propagates authored facts by inserting events into the past. With a shared
/// generator, inserting anything would re-roll every draw after it and dissolve all subsequent
/// history. These tests exist to make that property hard to break by accident later.
/// </summary>
public class RngStreamTests
{
    private static ulong[] Take(Rng rng, int count)
    {
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++) values[i] = rng.NextUInt64();
        return values;
    }

    [Fact]
    public void SameCoordinatesGiveTheSameStream()
    {
        Rng a = Rng.For(42, 30, EntityId.Actor(5), RngPurpose.Mortality);
        Rng b = Rng.For(42, 30, EntityId.Actor(5), RngPurpose.Mortality);
        Assert.Equal(Take(a, 16), Take(b, 16));
    }

    [Theory]
    [InlineData(43UL, 30, 5, RngPurpose.Mortality)]
    [InlineData(42UL, 31, 5, RngPurpose.Mortality)]
    [InlineData(42UL, 30, 6, RngPurpose.Mortality)]
    [InlineData(42UL, 30, 5, RngPurpose.Battle)]
    public void ChangingAnyCoordinateChangesTheStream(ulong seed, int year, int actor, RngPurpose purpose)
    {
        ulong[] baseline = Take(Rng.For(42, 30, EntityId.Actor(5), RngPurpose.Mortality), 8);
        ulong[] altered = Take(Rng.For(seed, year, EntityId.Actor(actor), purpose), 8);
        Assert.NotEqual(baseline, altered);
    }

    [Fact]
    public void DrawingFromOneStreamDoesNotDisturbAnother()
    {
        // The heart of it. One entity taking a different number of draws this year — because a
        // rule changed, or because history was retconned upstream — must leave every other
        // entity's draws untouched.
        Rng untouched = Rng.For(42, 30, EntityId.Actor(6), RngPurpose.Mortality);
        ulong[] expected = Take(untouched, 8);

        Rng noisy = Rng.For(42, 30, EntityId.Actor(5), RngPurpose.Mortality);
        Take(noisy, 1000);

        Rng again = Rng.For(42, 30, EntityId.Actor(6), RngPurpose.Mortality);
        Assert.Equal(expected, Take(again, 8));
    }

    [Fact]
    public void BranchesAreIndependentAndRepeatable()
    {
        Rng parent = Rng.For(42, 30, EntityId.Faction(1), RngPurpose.Succession);

        Assert.Equal(Take(parent.Branch(3), 8), Take(parent.Branch(3), 8));
        Assert.NotEqual(Take(parent.Branch(3), 8), Take(parent.Branch(4), 8));
    }

    [Fact]
    public void NextIsUniformAndInRange()
    {
        Rng rng = Rng.For(42, 1, EntityId.None, RngPurpose.Genesis);
        int[] buckets = new int[10];

        for (int i = 0; i < 100_000; i++)
        {
            int roll = rng.Next(10);
            Assert.InRange(roll, 0, 9);
            buckets[roll]++;
        }

        // Rejection sampling, so no modulo bias: every bucket should sit near 10,000.
        foreach (int count in buckets) Assert.InRange(count, 9_000, 11_000);
    }

    [Fact]
    public void WeightedChoiceRespectsWeightsAndHandlesEmpty()
    {
        Rng rng = Rng.For(42, 1, EntityId.None, RngPurpose.ActionChoice);

        Assert.Equal(-1, rng.PickIndexWeighted([0, 0, 0]));

        int[] hits = new int[3];
        for (int i = 0; i < 30_000; i++) hits[rng.PickIndexWeighted([1, 0, 9])]++;

        Assert.Equal(0, hits[1]);
        Assert.InRange(hits[0], 2_400, 3_600);
        Assert.InRange(hits[2], 26_400, 27_600);
    }
}
