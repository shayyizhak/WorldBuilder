using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WorldBuilder.Core.Geography;

/// <summary>
/// The board's stored form.
///
/// A map generator is not reproducible across its own versions — the same class of problem as
/// the model's run-to-run variance, and settled the same way: generation happens once, the
/// result becomes an artefact, and the artefact is what travels with the world. Nothing here
/// takes a seed, because nothing here regenerates anything.
///
/// Written by hand for the same reason the event log is: the file is hashed into the world
/// header and compared byte-for-byte, so field order, number formatting and line endings must be
/// properties of the writer rather than of a serialiser's defaults or of the machine.
/// </summary>
public static class BoardIo
{
    public const string Format = "wb-board/1";

    public static Board Read(string path) => Parse(File.ReadAllText(path));

    public static Board Parse(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string format = Text(root, "format");
        if (format.Length == 0)
            throw new FormatException("this file declares no board format; it is not a board artefact.");

        // Refuses rather than guesses. A later format may add a field this build would drop in
        // silence, and a board silently missing part of itself is a world whose distances are
        // wrong by an amount nobody can see — the same asymmetry the world header already makes,
        // for the same reason.
        if (!string.Equals(format, Format, StringComparison.Ordinal))
            throw new FormatException($"board format '{format}' is not '{Format}'; this build cannot read it.");

        if (!root.TryGetProperty("cells", out JsonElement cells) || cells.ValueKind != JsonValueKind.Array)
            throw new FormatException("board artefact has no 'cells' array.");

        List<BoardCell> parsed = [];
        foreach (JsonElement cell in cells.EnumerateArray())
        {
            List<int> neighbours = [];
            if (cell.TryGetProperty("adj", out JsonElement adj))
                foreach (JsonElement n in adj.EnumerateArray()) neighbours.Add(n.GetInt32());
            neighbours.Sort();

            parsed.Add(new BoardCell
            {
                Index = Int(cell, "i", -1),
                X = Int(cell, "x", 0),
                Y = Int(cell, "y", 0),
                Height = Int(cell, "h", 0),
                Terrain = Enum.Parse<Terrain>(Text(cell, "terrain"), ignoreCase: true),
                Biome = Text(cell, "biome"),
                MoveCost = Int(cell, "cost", 0),
                Neighbours = neighbours,
            });
        }

        return new Board(parsed, format, Text(root, "source"), Text(root, "generator"));
    }

    /// <summary>
    /// The artefact, as bytes. LF throughout and no trailing whitespace, so the hash in the world
    /// header is a property of the board rather than of the platform that wrote it.
    /// </summary>
    public static string Serialise(Board board)
    {
        StringBuilder sb = new();
        sb.Append("{\n");
        sb.Append("  \"format\": ").Append(JsonSerializer.Serialize(board.Format)).Append(",\n");
        sb.Append("  \"source\": ").Append(JsonSerializer.Serialize(board.Source)).Append(",\n");
        sb.Append("  \"generator\": ").Append(JsonSerializer.Serialize(board.Generator)).Append(",\n");
        sb.Append("  \"cells\": [\n");

        for (int i = 0; i < board.Count; i++)
        {
            BoardCell c = board[i];
            sb.Append("    {\"i\": ").Append(N(c.Index))
              .Append(", \"x\": ").Append(N(c.X))
              .Append(", \"y\": ").Append(N(c.Y))
              .Append(", \"h\": ").Append(N(c.Height))
              .Append(", \"terrain\": ").Append(JsonSerializer.Serialize(c.Terrain.ToString()))
              .Append(", \"biome\": ").Append(JsonSerializer.Serialize(c.Biome))
              .Append(", \"cost\": ").Append(N(c.MoveCost))
              .Append(", \"adj\": [");

            for (int j = 0; j < c.Neighbours.Count; j++)
            {
                if (j > 0) sb.Append(", ");
                sb.Append(N(c.Neighbours[j]));
            }

            sb.Append("]}").Append(i == board.Count - 1 ? "\n" : ",\n");
        }

        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    public static void Write(string path, Board board)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);

        // WriteAllText with a UTF-8 encoding that emits no BOM, and no newline translation: the
        // world header records this file's sha256, and a BOM or a CRLF makes that hash a fact
        // about the writing machine.
        File.WriteAllText(path, Serialise(board), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String
            ? e.GetString() ?? "" : "";

    private static int Int(JsonElement root, string name, int fallback) =>
        root.TryGetProperty(name, out JsonElement e) && e.TryGetInt32(out int v) ? v : fallback;
}

/// <summary>
/// An Azgaar Fantasy Map Generator export, read into a <see cref="Board"/>.
///
/// Azgaar is the primary source for the region layer because it hands over a cell adjacency
/// graph directly, which is the one thing the political layer actually needs and the one thing
/// most generators do not export. Its output is consumed, never embedded — no generator code is
/// linked, compiled in or vendored here, which is what keeps the licence question closed.
///
/// The conversion is deliberately shallow. Azgaar knows a great deal about its world — states,
/// cultures, religions, burgs, routes — and every one of those is a *history*, which §2 settles
/// is simulated rather than imported. What is taken is the ground: where the cells are, what
/// they are made of, and what adjoins what.
/// </summary>
public static class AzgaarImport
{
    /// <summary>
    /// Azgaar's biome names, in its own index order, with what each costs to cross.
    ///
    /// The costs are the board's, not Azgaar's — it has no notion of travel cost — so they are
    /// stated here once, on import, and thereafter live on the cell. That is the honest place
    /// for them: a judgement made while converting somebody else's map, recorded in the artefact
    /// it produced, rather than a table in the engine that silently re-prices every board.
    /// </summary>
    private static readonly (string Biome, Terrain Terrain, int Cost)[] Biomes =
    [
        ("marine", Terrain.Water, 9),
        ("hot desert", Terrain.Plains, 4),
        ("cold desert", Terrain.Plains, 4),
        ("savanna", Terrain.Plains, 2),
        ("grassland", Terrain.Plains, 2),
        ("tropical seasonal forest", Terrain.Forest, 3),
        ("temperate deciduous forest", Terrain.Forest, 3),
        ("tropical rainforest", Terrain.Forest, 5),
        ("temperate rainforest", Terrain.Forest, 4),
        ("taiga", Terrain.Forest, 4),
        ("tundra", Terrain.Plains, 3),
        ("glacier", Terrain.Mountains, 8),
        ("wetland", Terrain.Marsh, 6),
    ];

    /// <summary>
    /// Reads the <c>cells</c> block of an Azgaar JSON export.
    ///
    /// Fails loudly on anything it cannot map rather than substituting a default. A cell quietly
    /// defaulted to grassland is a cell whose distance is wrong by an amount that never surfaces,
    /// and the whole point of importing a board is that the distances are somebody else's fact
    /// rather than this engine's guess.
    /// </summary>
    public static Board Parse(string json, string source)
    {
        using JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("cells", out JsonElement cells)
            || cells.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException(
                "this does not look like an Azgaar cell export: no 'cells' array at the root. " +
                "Export from the generator with Save/Load → Export → JSON, including cell data.");
        }

        List<BoardCell> parsed = [];
        int index = 0;

        foreach (JsonElement cell in cells.EnumerateArray())
        {
            int biome = cell.TryGetProperty("biome", out JsonElement b) && b.TryGetInt32(out int bi) ? bi : -1;
            if (biome < 0 || biome >= Biomes.Length)
                throw new FormatException($"cell {index} has biome {biome}, which this build has no terrain for.");

            int height = cell.TryGetProperty("h", out JsonElement h) && h.TryGetInt32(out int hv) ? hv : 0;

            List<int> neighbours = [];
            if (cell.TryGetProperty("c", out JsonElement c) && c.ValueKind == JsonValueKind.Array)
                foreach (JsonElement n in c.EnumerateArray()) neighbours.Add(n.GetInt32());
            neighbours.Sort();

            (int x, int y) = Point(cell);
            (string name, Terrain terrain, int cost) = Biomes[biome];

            // Azgaar's heights run 0–100 with 20 as sea level. Above 60 the map is drawn as
            // highland whatever the biome says, and highland is the thing that makes an ore site
            // hard to reach — so it overrides, and says so.
            if (height >= 60 && terrain != Terrain.Water)
            {
                terrain = Terrain.Mountains;
                cost = Math.Max(cost, 7);
            }
            else if (height >= 45 && terrain is Terrain.Plains or Terrain.Forest)
            {
                terrain = Terrain.Hills;
                cost = Math.Max(cost, 4);
            }

            parsed.Add(new BoardCell
            {
                Index = index++,
                X = x,
                Y = y,
                Height = height,
                Terrain = terrain,
                Biome = name,
                MoveCost = cost,
                Neighbours = neighbours,
            });
        }

        return new Board(parsed, BoardIo.Format, source, "azgaar-fmg (cell export)");
    }

    /// <summary>Azgaar carries cell centres as <c>p: [x, y]</c>, in map units.</summary>
    private static (int X, int Y) Point(JsonElement cell)
    {
        if (!cell.TryGetProperty("p", out JsonElement p) || p.ValueKind != JsonValueKind.Array)
            return (0, 0);

        int[] coords = [.. p.EnumerateArray().Select(static e => (int)e.GetDouble())];
        return coords.Length >= 2 ? (coords[0], coords[1]) : (0, 0);
    }
}
