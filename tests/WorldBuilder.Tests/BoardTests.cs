using WorldBuilder.Core.Geography;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The imported physical layer, on its own terms: what a board must be for a distance taken from
/// it to mean anything.
///
/// The board is inert as far as these tests are concerned — no rule is consulted, no world is
/// simulated. That is queue item 4.2's whole claim: geography present and checkable before
/// anything reads it.
/// </summary>
public class BoardTests
{
    /// <summary>A line of five cells, each adjoining the next. Distances on it are arithmetic,
    /// so a wrong one is obvious rather than merely surprising.</summary>
    private static Board Line(params int[] costs)
    {
        List<BoardCell> cells = [];
        for (int i = 0; i < costs.Length; i++)
        {
            List<int> neighbours = [];
            if (i > 0) neighbours.Add(i - 1);
            if (i < costs.Length - 1) neighbours.Add(i + 1);

            cells.Add(new BoardCell
            {
                Index = i,
                X = i,
                Y = 0,
                Height = 40,
                Terrain = Terrain.Plains,
                Biome = "grassland",
                MoveCost = costs[i],
                Neighbours = neighbours,
            });
        }

        return new Board(cells, BoardIo.Format, "test", "test");
    }

    [Fact]
    public void DistanceIsTheCheapestRouteAndIsTheSameBothWays()
    {
        Board board = Line(2, 2, 2, 2, 2);

        Assert.Equal(0, board.Cost(0, 0));
        Assert.Equal(2, board.Cost(0, 1));
        Assert.Equal(8, board.Cost(0, 4));
        Assert.Equal(board.Cost(0, 4), board.Cost(4, 0));
    }

    [Fact]
    public void ARouteThroughExpensiveGroundCostsMore()
    {
        // Each step is priced by both of its ends, so a mountain in the middle makes the two
        // crossings that touch it dearer without making the whole line dearer.
        Board cheap = Line(2, 2, 2);
        Board dear = Line(2, 8, 2);

        Assert.True(dear.Cost(0, 2) > cheap.Cost(0, 2));
    }

    [Fact]
    public void AsymmetricAdjacencyIsRefusedAtConstruction()
    {
        // The specific defect worth naming: both directions return a plausible number, so nothing
        // downstream looks wrong. A gap that presents as a pass.
        List<BoardCell> cells =
        [
            Cell(0, [1]),
            Cell(1, []),
        ];

        FormatException thrown = Assert.Throws<FormatException>(
            () => new Board(cells, BoardIo.Format, "test", "test"));

        Assert.Contains("not symmetric", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADisconnectedBoardIsRefusedRatherThanReturningAnInfinity()
    {
        List<BoardCell> cells = [Cell(0, []), Cell(1, [])];
        Board board = new(cells, BoardIo.Format, "test", "test");

        FormatException thrown = Assert.Throws<FormatException>(() => board.Cost(0, 1));
        Assert.Contains("not connected", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBoardsOwnMedianIsAPropertyOfTheBoardAndNotTheScaleRulesUse()
    {
        // Kept as a described property rather than a scoring one. Calibrating rules against it
        // was a real defect: places are sited deliberately far apart, so every separation a world
        // actually contains sits beyond this figure, and every proximity came out below 100. The
        // scale rules use is Geography.ReferenceCost — see GeographyTests.
        Board board = Line(2, 2, 2, 2, 2);

        Assert.Equal(4, board.ReferenceCost);
    }

    [Fact]
    public void TheStoredBoardRoundTripsByteForByte()
    {
        // The artefact is hashed into the world header, so reading and rewriting it must not
        // change a byte — otherwise the hash is a property of whoever last opened it.
        string path = Boards.Locate();
        string stored = File.ReadAllText(path);

        Assert.Equal(stored, BoardIo.Serialise(BoardIo.Parse(stored)));
    }

    [Fact]
    public void TheStoredBoardIsWholeAndHasGroundOfEveryKind()
    {
        Board board = Boards.Stored();

        Assert.Equal(BoardIo.Format, board.Format);
        Assert.True(board.Count > 0);

        // Connectivity, asserted by asking for a distance the whole way across: AllPairs throws
        // on an unreachable cell, so this is the assertion that the board is one place.
        Assert.True(board.Cost(0, board.Count - 1) > 0);

        HashSet<Terrain> kinds = [.. board.Cells.Select(static c => c.Terrain)];
        Assert.Contains(Terrain.Water, kinds);
        Assert.Contains(Terrain.Mountains, kinds);
        Assert.True(kinds.Count >= 4, "a board with two kinds of ground gives terrain nothing to say");
    }

    [Fact]
    public void ABoardFromAnotherFormatIsRefusedRatherThanReadPartly()
    {
        FormatException thrown = Assert.Throws<FormatException>(
            () => BoardIo.Parse("{\"format\":\"wb-board/2\",\"cells\":[]}"));

        Assert.Contains("wb-board/2", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAzgaarExportBecomesABoard()
    {
        // The shape Azgaar's JSON export actually has: cells with a point, a height, a biome
        // index and a neighbour list. Nothing else is taken — states, cultures and burgs are
        // histories, and §2 settles that histories are simulated rather than imported.
        const string export = """
            {"cells":[
              {"i":0,"p":[10,10],"h":30,"biome":4,"c":[1]},
              {"i":1,"p":[20,10],"h":65,"biome":4,"c":[0,2]},
              {"i":2,"p":[30,10],"h":10,"biome":0,"c":[1]}
            ]}
            """;

        Board board = AzgaarImport.Parse(export, "test export");

        Assert.Equal(3, board.Count);
        Assert.Equal(Terrain.Plains, board[0].Terrain);
        Assert.Equal(Terrain.Mountains, board[1].Terrain);       // height overrides the biome
        Assert.Equal(Terrain.Water, board[2].Terrain);
        Assert.Equal("test export", board.Source);
        Assert.True(board.Cost(0, 2) > 0);
    }

    [Fact]
    public void AnAzgaarExportWithABiomeThisBuildCannotPriceIsRefused()
    {
        // Not defaulted to grassland. A cell quietly given the wrong cost is a distance that is
        // wrong by an amount nothing ever surfaces, and the point of importing a board is that
        // the distances are somebody else's fact rather than this engine's guess.
        Assert.Throws<FormatException>(
            () => AzgaarImport.Parse("""{"cells":[{"i":0,"p":[0,0],"h":30,"biome":97,"c":[]}]}""", "test"));
    }

    [Fact]
    public void SomethingThatIsNotAnAzgaarExportSaysSo()
    {
        FormatException thrown = Assert.Throws<FormatException>(
            () => AzgaarImport.Parse("""{"info":{"version":"1.9"}}""", "test"));

        Assert.Contains("Export", thrown.Message, StringComparison.Ordinal);
    }

    private static BoardCell Cell(int index, IReadOnlyList<int> neighbours) => new()
    {
        Index = index,
        X = index,
        Y = 0,
        Height = 40,
        Terrain = Terrain.Plains,
        Biome = "grassland",
        MoveCost = 2,
        Neighbours = neighbours,
    };
}
