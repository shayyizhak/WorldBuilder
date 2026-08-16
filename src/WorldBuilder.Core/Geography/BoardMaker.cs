namespace WorldBuilder.Core.Geography;

/// <summary>
/// A board maker of last resort, for when no Azgaar export is to hand.
///
/// <b>This is not part of the simulation and must never be reached from it.</b> It exists so
/// that a board artefact can be brought into existence on a machine with no browser and no
/// hosted generator, and it is used exactly once, by <c>wb map make</c>, whose output is then
/// stored, hashed and read back like any other import. <see cref="AzgaarImport"/> is the real
/// path; a board produced here says so in its own <see cref="Board.Source"/>, so a world can
/// always name which kind of map it was run against.
///
/// The distinction matters more than the quality of the terrain does. §2 settles that a map is
/// a stored artefact and never a function of <c>world_seed</c>, and the way that stops being
/// true is not by anybody deciding otherwise — it is by a generator sitting in the engine, being
/// convenient, and getting called from worldgen one day because it was there. So the seed here
/// is an argument to a command-line tool, not a world seed, and nothing in <c>Rules/</c> can
/// reach this class at all.
/// </summary>
public static class BoardMaker
{
    /// <summary>
    /// Twenty by fourteen, which is 280 cells.
    ///
    /// Sized against what it has to hold rather than against realism: seven sited places on a
    /// board of 280 leaves room for two houses to be genuinely far apart, and it keeps the
    /// all-pairs distance table small enough to build in milliseconds — which it must be,
    /// because the test suite builds a world several hundred times.
    /// </summary>
    public const int DefaultWidth = 20;
    public const int DefaultHeight = 14;

    /// <summary>
    /// An offset hex lattice with a height field over it.
    ///
    /// Hexes rather than squares because a square grid has to choose between four neighbours,
    /// where a diagonal is a step that costs the same as a side and distances come out wrong, and
    /// eight neighbours, where they come out wrong the other way. Six neighbours all one step
    /// apart is the cheapest arrangement in which "how far" has one answer.
    /// </summary>
    public static Board Make(int width, int height, ulong seed)
    {
        BoardCell[] cells = new BoardCell[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                int h = HeightAt(x, y, width, height, seed);
                int wet = Field(x * 7 + 13, y * 7 + 29, seed ^ 0x9E3779B97F4A7C15UL);

                (Terrain terrain, string biome, int cost) = Describe(h, wet);

                cells[i] = new BoardCell
                {
                    Index = i,
                    X = x,
                    Y = y,
                    Height = h,
                    Terrain = terrain,
                    Biome = biome,
                    MoveCost = cost,
                    Neighbours = Neighbours(x, y, width, height),
                };
            }
        }

        return new Board(cells, BoardIo.Format, "wb map make (no Azgaar export available)",
            $"wb-boardmaker/1 {width}x{height} seed {seed}");
    }

    /// <summary>
    /// What a height and a wetness make.
    ///
    /// The bands and the costs are the one place in this file where a judgement is recorded, and
    /// it is recorded in the artefact rather than in the engine: once written, a cell carries its
    /// own cost and nothing here is consulted again. An Azgaar import makes the same judgement in
    /// its own table, which is why two boards from different sources can sit side by side without
    /// the engine having to hold an opinion about what a marsh is worth.
    /// </summary>
    private static (Terrain, string, int) Describe(int h, int wet) => h switch
    {
        < 30 => (Terrain.Water, "marine", 9),
        < 38 when wet > 50 => (Terrain.Marsh, "wetland", 6),
        < 58 when wet > 60 => (Terrain.Forest, "temperate deciduous forest", 3),
        < 58 => (Terrain.Plains, wet > 35 ? "grassland" : "savanna", 2),
        < 74 => (Terrain.Hills, wet > 50 ? "taiga" : "cold desert", 4),
        _ => (Terrain.Mountains, "glacier", 7),
    };

    /// <summary>
    /// A height field: three octaves of value noise, pulled down towards the edges so the map
    /// has a coast rather than running off the side of itself.
    /// </summary>
    private static int HeightAt(int x, int y, int width, int height, ulong seed)
    {
        int value = Field(x, y, seed) * 5 / 10
                    + Field(x / 2, y / 2, seed ^ 0xD1B54A32D192ED03UL) * 3 / 10
                    + Field(x / 4, y / 4, seed ^ 0xA24BAED4963EE407UL) * 2 / 10;

        // Distance from the nearest edge, as a percentage of the largest it could be.
        int edge = Math.Min(Math.Min(x, width - 1 - x), Math.Min(y, height - 1 - y));
        int reach = Math.Max(1, Math.Min(width, height) / 2);
        int inland = Math.Min(100, edge * 100 / reach);

        // The 145 is a range stretch, not a tuned constant: three octaves of noise average to the
        // middle of their range, so an unstretched field produces a board that is almost entirely
        // one band and has no highland at all. It is chosen against the terrain histogram this
        // makes, and that histogram is reported by `wb map show` — which is the only defence a
        // number like this can have.
        return Math.Clamp(value * (35 + inland * 65 / 100) * 145 / 10000, 0, 100);
    }

    /// <summary>Integer value noise, 0..100. Hashed rather than interpolated: cheap, and the
    /// only property required of it is that it is the same every time.</summary>
    private static int Field(int x, int y, ulong seed)
    {
        ulong h = seed;
        h ^= (ulong)(uint)x * 0x9E3779B97F4A7C15UL;
        h = (h ^ (h >> 29)) * 0xBF58476D1CE4E5B9UL;
        h ^= (ulong)(uint)y * 0xD1B54A32D192ED03UL;
        h = (h ^ (h >> 32)) * 0x94D049BB133111EBUL;
        return (int)((h >> 33) % 101);
    }

    /// <summary>The six neighbours of an odd-row-offset hex cell, clipped at the board's edge.</summary>
    private static List<int> Neighbours(int x, int y, int width, int height)
    {
        int shift = (y & 1) == 1 ? 1 : 0;

        (int dx, int dy)[] steps =
        [
            (-1, 0), (1, 0),
            (shift - 1, -1), (shift, -1),
            (shift - 1, 1), (shift, 1),
        ];

        List<int> found = [];
        foreach ((int dx, int dy) in steps)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
            found.Add(ny * width + nx);
        }

        found.Sort();
        return found;
    }
}
