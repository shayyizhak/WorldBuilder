using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Geography;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Geography as the rules meet it: where the world's places are, and what the one distance
/// function says about them.
///
/// Every test enters through a simulated world rather than through a hand-built board, because
/// the defect this file exists to pin was invisible on a hand-built one. The proximity function
/// was calibrated against the board's median separation and was perfectly correct about it; what
/// was wrong is that no world ever contained two places at that distance, so a rule "centred on
/// ordinary" was discounted at every distance that occurred. Only a real world shows that.
/// </summary>
public class GeographyTests
{
    private static WorldView World(ulong seed = 42, int years = 50)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return WorldView.Build(sim.Log, seed);
    }

    [Fact]
    public void EveryPlaceThatCanBeTravelledToHasExactlyOnePositionOnTheBoard()
    {
        // §9's exit criterion, with the one exception stated rather than assumed: a region is the
        // ground the board is made of rather than a point on it. It is never marched on, held,
        // raided or fought over, and a cell for it would put a spurious position into every
        // distance the engine measures.
        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234, 2025 })
        {
            WorldState state = World(seed, 20).State;

            Assert.Empty(GeographyAudit.Positions(state));
            Assert.True(state.HasBoard);

            foreach (Place place in state.Places)
            {
                if (place.Kind == PlaceKind.Region) { Assert.False(place.IsSited); continue; }

                Assert.True(place.IsSited, $"{place.Id} has no position");
                Assert.True(state.Board!.Has(place.Cell), $"{place.Id} is off the board");
            }
        }
    }

    [Fact]
    public void NoTwoPlacesShareACell()
    {
        WorldState state = World().State;

        List<int> cells = [.. state.Places.Where(static p => p.IsSited).Select(static p => p.Cell)];
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    [Fact]
    public void MinesStandOnHighGroundAndTownsDoNot()
    {
        // Terrain deciding what may go where is not decoration: it is what makes the richest
        // places on the map also the hardest to reach, which was already true of them
        // economically — a mine cannot feed itself, and now it is also up a mountain.
        WorldState state = World().State;

        foreach (Place place in state.Places)
        {
            if (!place.IsSited) continue;
            Terrain terrain = state.Board![place.Cell].Terrain;

            if (place.Kind == PlaceKind.Site)
                Assert.True(terrain is Terrain.Hills or Terrain.Mountains, $"{place.Id} is a mine on {terrain}");
            else
                Assert.NotEqual(Terrain.Water, terrain);
        }
    }

    [Fact]
    public void ProximityIsCentredOnASeparationTheWorldActuallyContains()
    {
        // The defect, pinned. If the reference is the board's median rather than the world's,
        // every pair of places reads below 100 and four mechanics are quietly discounted
        // everywhere. The assertion is that real place pairs straddle 100 — some nearer, some
        // further — which is what "centred" has to mean to be worth anything.
        WorldState state = World().State;
        Geography geo = state.Geo!;

        List<Place> sited = [.. state.Places.Where(static p => p.IsSited)];
        List<int> proximities = [];

        for (int a = 0; a < sited.Count; a++)
            for (int b = a + 1; b < sited.Count; b++)
                proximities.Add(geo.BetweenPlaces(sited[a].Id, sited[b].Id));

        Assert.Contains(proximities, p => p > Geography.Neutral);
        Assert.Contains(proximities, p => p < Geography.Neutral);
    }

    [Fact]
    public void DistanceCanVary()
    {
        // The reachability guard, asserted the way the ratio metrics are. A board returning one
        // proximity for every pair would have four mechanics multiplying by a constant and
        // reporting that geography had been consulted — the same defect class as a ratio whose
        // numerator no path can move.
        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234, 2025 })
        {
            (int pairs, int lowest, int highest) = GeographyAudit.ProximitySpread(World(seed, 20).State);

            Assert.True(pairs > 0, $"seed {seed} has no pairs of sited places");
            Assert.True(highest > lowest, $"seed {seed} gives every pair of places the same proximity");
        }
    }

    [Fact]
    public void DistanceIsSymmetricAndNearerThanAverageAtHome()
    {
        WorldState state = World().State;
        Geography geo = state.Geo!;

        List<Place> sited = [.. state.Places.Where(static p => p.IsSited)];
        Place one = sited[0];
        Place other = sited[^1];

        Assert.Equal(geo.BetweenPlaces(one.Id, other.Id), geo.BetweenPlaces(other.Id, one.Id));
        Assert.Equal(200, geo.BetweenPlaces(one.Id, one.Id));
    }

    [Fact]
    public void AWorldWithNoBoardReadsNeutralRatherThanGuessing()
    {
        // Every world folded from a log written before geography existed. Neutral is a real
        // answer for a real case — multiplying by 100 and dividing by 100 leaves those rules
        // behaving exactly as they did when they were written — and it is not a fallback that
        // papers over a missing map, because wb run refuses to simulate without one.
        WorldState bare = new() { Seed = 1 };

        Assert.False(bare.HasBoard);
        Assert.Null(bare.Geo);
        Assert.Empty(GeographyAudit.Positions(bare));
        Assert.Empty(GeographyAudit.Reach(bare, new EventLog()));
    }

    [Fact]
    public void TheLogNamesTheBoardTheHistoryHappenedOn()
    {
        Simulation sim = new(42);
        sim.Run(2);

        Event genesis = sim.Log.Events[0];
        Assert.Equal(EventKind.GenesisWorld, genesis.Kind);
        Assert.Equal(Boards.Stored().Fingerprint, genesis.GetString("board"));

        // And a fold of that log finds the same board and attaches it, so a replayed world can
        // answer the same distance questions the live one could.
        WorldState folded = WorldBuilder.Core.Rendering.Replay.Fold(sim.Log, 42);
        Assert.True(folded.HasBoard);
    }

    [Fact]
    public void AWorldFoldedAgainstTheWrongBoardIsRefused()
    {
        // Not a note. A history read beside a map it never happened on is internally consistent
        // and about somewhere else, and no downstream check would see anything unusual.
        Simulation sim = new(42);
        sim.Run(2);

        EventLog tampered = new();
        foreach (Event e in sim.Log.Events)
        {
            if (e.Kind != EventKind.GenesisWorld) { tampered.Append(e); continue; }

            List<KeyValuePair<string, string>> data = [.. e.Data];
            for (int i = 0; i < data.Count; i++)
                if (data[i].Key == "board") data[i] = new KeyValuePair<string, string>("board", new string('0', 64));

            tampered.Append(e with { Data = data });
        }

        FormatException thrown = Assert.Throws<FormatException>(
            () => WorldBuilder.Core.Rendering.Replay.Fold(tampered, 42));

        Assert.Contains("stored board is", thrown.Message, StringComparison.Ordinal);
    }
}
