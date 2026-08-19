using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
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
    /// <summary>
    /// Components the fold is not expected to reproduce. <b>Empty, as of ruleset 7.</b>
    ///
    /// <b>Kept as an empty array rather than deleted, because the mechanism is the point.</b> At
    /// ruleset 6 this held <c>goals.identity</c>, <c>goals.progress</c> and <c>goals.arc</c>, and the
    /// theory below asserted that exactly those three differed — so that when the record started
    /// carrying goals the test went red and said so, instead of the repair having to be remembered.
    /// It did, and this is the other half of that change.
    ///
    /// <b>And the exclusion was named because it used to be invisible.</b> This file's fingerprint was
    /// hand-written and read actors, places, factions, arcs and relation values: not goals, and also
    /// not traits, yields, cells, succession rules, seats, arc sides or a relation's provenance. It
    /// asserted "state is a fold over the log" while being unable to fail on two thirds of the state.
    /// <see cref="WorldFingerprint"/> is exhaustive instead, so the next thing to fall out of the fold
    /// fails a test rather than going unnoticed for a ruleset or two.
    /// </summary>
    private static readonly string[] NotFolded = [];

    private static string Fingerprint(WorldState state)
    {
        StringBuilder sb = new();
        foreach ((string name, string value) in WorldFingerprint.Of(state))
        {
            if (Array.IndexOf(NotFolded, name) >= 0) continue;
            sb.Append(name).Append('\n').Append(value);
        }

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

    /// <summary>
    /// A fold reproduces every component of the world, goals included. <b>The ruleset-7 deliverable.</b>
    ///
    /// This is the property <see cref="WorldState"/> has claimed since v1 — "the fold of the event log"
    /// — and that nothing enforced. At ruleset 6 it was false for three components out of twenty-seven,
    /// and the version of this theory that ran then asserted that <i>exactly</i> those three differed,
    /// so that closing the gap would fail a test rather than needing to be remembered. It did.
    ///
    /// It keeps the same shape now, comparing against an empty exclusion list, because the mechanism is
    /// worth more than the current answer: whatever falls out of the fold next fails here.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void AFoldReproducesEveryComponentOfTheWorld(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);

        WorldState replayed = Replay.Fold(sim.Log, seed, board: sim.State.Board);

        Assert.Equal(
            [.. NotFolded.Order(StringComparer.Ordinal)],
            WorldFingerprint.Differences(sim.State, replayed));

        // And the world actually has goals in it, so the assertion above is not satisfied by a panel
        // that never formed one. This is the line that would have failed at ruleset 6 for a different
        // reason than the one being fixed.
        Assert.NotEmpty(replayed.Goals.Snapshot());
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
