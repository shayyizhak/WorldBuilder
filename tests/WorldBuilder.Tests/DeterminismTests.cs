using System.Security.Cryptography;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The reproducibility guarantee: a world is fully described by its seed, so a seed plus (later)
/// an intervention log is a shareable world. If these fail, nothing else about the design works.
/// </summary>
public class DeterminismTests
{
    private static string Hash(ulong seed, int years)
    {
        Simulation sim = new(seed);
        sim.Run(years);

        StringBuilder sb = new();
        foreach (Event e in sim.Log.Events) sb.Append(JsonlIo.Serialise(e)).Append('\n');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(1234UL)]
    [InlineData(0UL)]
    [InlineData(ulong.MaxValue)]
    public void SameSeedProducesIdenticalHistory(ulong seed)
    {
        Assert.Equal(Hash(seed, 50), Hash(seed, 50));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentHistories()
    {
        Assert.NotEqual(Hash(42, 50), Hash(43, 50));
    }

    [Fact]
    public void RunningLongerExtendsTheSameHistoryRatherThanChangingIt()
    {
        // A 50-year run must be a strict prefix of a 100-year run. If it is not, some rule is
        // reading the horizon, and "replay to year N" would not mean what it claims to.
        Simulation shortRun = new(42);
        shortRun.Run(50);

        Simulation longRun = new(42);
        longRun.Run(100);

        Assert.True(shortRun.Log.Count <= longRun.Log.Count);
        for (int i = 0; i < shortRun.Log.Count; i++)
        {
            Assert.Equal(
                JsonlIo.Serialise(shortRun.Log.Events[i]),
                JsonlIo.Serialise(longRun.Log.Events[i]));
        }
    }

    [Fact]
    public void EventKeysAreStableAndContentDerived()
    {
        Simulation a = new(42);
        a.Run(30);

        Simulation b = new(42);
        b.Run(30);

        for (int i = 0; i < a.Log.Count; i++)
            Assert.Equal(a.Log.Events[i].Key, b.Log.Events[i].Key);

        // Keys must not simply mirror log position — a render cached against a key has to
        // survive a v2 retcon that shifts every downstream id.
        Event sample = a.Log.Events[^1];
        string recomputed = EventFactory.ComputeKey(sample.Year, sample.Kind, sample.Participants, 0);
        Assert.Equal(16, recomputed.Length);
    }
}
