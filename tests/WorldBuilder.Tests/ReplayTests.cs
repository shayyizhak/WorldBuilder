using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Rendering;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// "World state is a fold over the log" has to be literally true, not a slogan. If replaying
/// the log does not reproduce the world exactly, then time travel is a lie and v2's re-fold
/// after a retcon would silently produce a different world.
/// </summary>
public class ReplayTests
{
    /// <summary>Everything about the world that a fold is supposed to reproduce.</summary>
    private static string Fingerprint(WorldState state)
    {
        StringBuilder sb = new();

        foreach (Actor a in state.Actors)
            sb.Append($"{a.Id}|{a.Name}|{a.BirthYear}|{a.DeathYear}|{a.Title}|{a.Faction}|{a.Place}\n");

        foreach (Place p in state.Places)
            sb.Append($"{p.Id}|{p.Name}|{p.Population}|{p.Controller}|" +
                      $"{p.Stockpile[0]},{p.Stockpile[1]},{p.Stockpile[2]}\n");

        foreach (Faction f in state.Factions)
            sb.Append($"{f.Id}|{f.Name}|{f.Leader}|{f.Legitimacy}|{f.Treasury}\n");

        foreach (Arc arc in state.Arcs)
            sb.Append($"{arc.Id}|{arc.Kind}|{arc.Name}|{arc.StartYear}|{arc.EndYear}\n");

        foreach (Relation r in state.Relations.All)
            sb.Append($"{r.Key.From}->{r.Key.To}:{r.Key.Kind}={r.Value}\n");

        return sb.ToString();
    }

    [Fact]
    public void FoldingTheLogReproducesTheSimulatedWorld()
    {
        Simulation sim = new(42);
        sim.Run(50);

        WorldState replayed = Replay.Fold(sim.Log, 42);
        Assert.Equal(Fingerprint(sim.State), Fingerprint(replayed));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(40)]
    public void ReplayStopsCleanlyAtAnyYear(int year)
    {
        Simulation sim = new(42);
        sim.Run(50);

        WorldState past = Replay.Fold(sim.Log, 42, year);

        // Nobody may have died in a year the replay has not reached yet.
        foreach (Actor a in past.Actors)
            if (a.DeathYear is int died) Assert.True(died <= year);

        Assert.True(past.Actors.Count <= sim.State.Actors.Count);
        Assert.True(past.Arcs.Count <= sim.State.Arcs.Count);
    }

    [Fact]
    public void JsonlRoundTripsExactly()
    {
        Simulation sim = new(42);
        sim.Run(50);

        string path = Path.Combine(Path.GetTempPath(), $"wb-test-{Guid.CreateVersion7()}.jsonl");
        try
        {
            JsonlIo.Write(path, sim.Log, 42);
            (EventLog reloaded, ulong seed) = JsonlIo.Read(path);

            Assert.Equal(42UL, seed);
            Assert.Equal(sim.Log.Count, reloaded.Count);

            for (int i = 0; i < sim.Log.Count; i++)
                Assert.Equal(JsonlIo.Serialise(sim.Log.Events[i]), JsonlIo.Serialise(reloaded.Events[i]));

            // The point of the round trip: the file alone rebuilds the world.
            Assert.Equal(Fingerprint(sim.State), Fingerprint(Replay.Fold(reloaded, seed)));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void EveryReadableEventRendersToANonEmptySentence()
    {
        Simulation sim = new(42);
        sim.Run(50);

        IReadOnlyList<string> lines = LogFormatter.Render(sim.Log, 42);
        Assert.NotEmpty(lines);

        foreach (string line in lines)
        {
            if (line.Length == 0) continue;
            Assert.DoesNotContain("someone someone", line, StringComparison.Ordinal);
            Assert.DoesNotContain("  -  ", line, StringComparison.Ordinal);
        }
    }
}
