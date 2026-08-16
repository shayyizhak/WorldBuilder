namespace WorldBuilder.Core.Geography;

/// <summary>
/// What the ground is made of, as far as moving across it is concerned.
///
/// Deliberately short. Terrain exists here to price a step, not to describe scenery — the
/// descriptive layer is <see cref="BoardCell.Biome"/>, which is a free string carried through
/// from whatever generated the map and which no rule reads. A rule that wants to know how hard
/// a place is to reach asks for a cost; a renderer that wants to say what it looks like asks for
/// a biome. Keeping those apart is what stops a travel rule quietly becoming a scenery rule.
/// </summary>
public enum Terrain : byte
{
    Water = 0,
    Plains = 1,
    Forest = 2,
    Hills = 3,
    Mountains = 4,
    Marsh = 5,
}

/// <summary>
/// One cell of the imported board.
///
/// <see cref="MoveCost"/> is carried on the cell rather than derived from <see cref="Terrain"/>
/// by a table in the engine, because §2 of the phase brief settles that travel cost is a
/// property of the board. A generator that knows its own terrain better than this enum does can
/// say so, and the engine does not have to grow an opinion about what a marsh costs.
/// </summary>
public sealed record BoardCell
{
    public required int Index { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Height { get; init; }
    public required Terrain Terrain { get; init; }

    /// <summary>The generator's own label. Carried, never read by a rule.</summary>
    public required string Biome { get; init; }

    /// <summary>What it costs to be in this cell, in the board's own units. Always positive.</summary>
    public required int MoveCost { get; init; }

    /// <summary>Cell indices sharing an edge with this one. Symmetric — verified on load.</summary>
    public required IReadOnlyList<int> Neighbours { get; init; }

    public bool IsLand => Terrain != Terrain.Water;
}

/// <summary>
/// The imported physical layer: cells, what they are made of, what adjoins what, and what it
/// costs to cross. A board, never a history — who holds these cells and who fights over them is
/// simulated on top and is not recorded here.
///
/// <b>One distance function.</b> Every rule that needs to know how far apart two things are
/// calls <see cref="Cost"/>, and nothing anywhere keeps its own notion of nearness. That was a
/// pre-committed decision rather than a discovered one: a per-mechanic idea of "close" is four
/// mechanics that disagree about the same map, and the disagreement is invisible for exactly as
/// long as no two of them are compared.
///
/// <b>Water is dear, not forbidden.</b> Making open water impassable is the obvious modelling
/// choice and it manufactures an unreachable branch: every rule would grow an "and if it cannot
/// be reached at all" path which, on a board whose settled cells are all in one land mass, no
/// world would ever take. This project has spent a whole phase on branches that could not fire,
/// so a crossing is priced instead of prohibited and every pair of cells has a finite distance.
/// An island is far away. It is not another universe.
/// </summary>
public sealed class Board
{
    private readonly BoardCell[] _cells;
    private int[][]? _costs;
    private int _referenceCost = -1;
    private string? _fingerprint;

    public Board(IReadOnlyList<BoardCell> cells, string format, string source, string generator)
    {
        _cells = [.. cells];
        Format = format;
        Source = source;
        Generator = generator;
        Verify();
    }

    /// <summary>The artefact format this board was read from, e.g. <c>wb-board/1</c>.</summary>
    public string Format { get; }

    /// <summary>Where the board came from — an Azgaar export, or this repository's own maker.</summary>
    public string Source { get; }

    /// <summary>What produced it, in enough detail to tell two of them apart.</summary>
    public string Generator { get; }

    public IReadOnlyList<BoardCell> Cells => _cells;
    public int Count => _cells.Length;

    /// <summary>
    /// This board's identity: the sha256 of its own canonical bytes.
    ///
    /// Computed from the serialised form rather than from the file it was read out of, so it is a
    /// property of the board and not of where it happened to be sitting. Serialisation round-trips
    /// byte-for-byte — which is a test, not an assumption — so this equals the sha256 of a stored
    /// artefact, and a world can record which board it was run against in the log itself.
    ///
    /// That is the point. The bundle header hashes the file; the genesis event names the board.
    /// One catches a file that changed under a world, the other catches a world opened beside the
    /// wrong map entirely.
    /// </summary>
    public string Fingerprint => _fingerprint ??= WorldBuilder.Core.Serialization.WorldBundle.HashOfBytes(
        System.Text.Encoding.UTF8.GetBytes(BoardIo.Serialise(this)));

    public BoardCell this[int index] => _cells[index];

    public bool Has(int index) => index >= 0 && index < _cells.Length;

    /// <summary>
    /// The cheapest route between two cells, in the board's own cost units.
    ///
    /// All-pairs, computed once on first use and held. The board is a few hundred cells and the
    /// simulation asks this question thousands of times a run, so paying for it once is both
    /// faster and — more to the point — makes the figure a stable property of the board rather
    /// than something that could differ between two callers who reached it by different routes.
    /// </summary>
    public int Cost(int from, int to)
    {
        if (!Has(from) || !Has(to))
            throw new ArgumentOutOfRangeException(nameof(from), $"no such cell: {from} → {to} on a board of {Count}.");

        _costs ??= AllPairs();
        return _costs[from][to];
    }

    /// <summary>
    /// The median cost over every pair of land cells: what a typical separation is on this board,
    /// considered on its own.
    ///
    /// <b>Reported, and only a fallback for scoring.</b> This looks like the natural scale for a
    /// distance rule and is not one, because a world's places are not scattered over the board at
    /// random — they are spread deliberately, so every pair of them sits well beyond this figure.
    /// Calibrating rules against it put every proximity that ever occurred below 100, which is a
    /// systematic discount dressed as a centring. The scale rules actually use is
    /// <see cref="Geography.ReferenceCost"/>, which is the median between the places a world has.
    ///
    /// Kept because it is the right number for describing a map — <c>wb map show</c> prints it —
    /// and because a world with fewer than two sited places has no separation of its own.
    /// </summary>
    public int ReferenceCost
    {
        get
        {
            if (_referenceCost >= 0) return _referenceCost;

            _costs ??= AllPairs();

            List<int> sample = [];
            for (int a = 0; a < _cells.Length; a++)
            {
                if (!_cells[a].IsLand) continue;
                for (int b = a + 1; b < _cells.Length; b++)
                {
                    if (!_cells[b].IsLand) continue;
                    sample.Add(_costs[a][b]);
                }
            }

            if (sample.Count == 0) return _referenceCost = 1;

            sample.Sort();
            return _referenceCost = Math.Max(1, sample[sample.Count / 2]);
        }
    }

    /// <summary>
    /// Structural checks, run at construction so a malformed board cannot reach a rule.
    ///
    /// Asymmetric adjacency is the specific failure worth naming: a board where A lists B and B
    /// does not list A produces a distance that depends on which way it is asked, which is
    /// exactly the class of defect this project keeps finding — a gap that presents as a pass,
    /// since both directions return a plausible number.
    /// </summary>
    private void Verify()
    {
        if (_cells.Length == 0) throw new FormatException("a board with no cells is not a board.");

        for (int i = 0; i < _cells.Length; i++)
        {
            BoardCell cell = _cells[i];

            if (cell.Index != i)
                throw new FormatException($"cell {i} is indexed {cell.Index}; cells must be in index order.");

            if (cell.MoveCost <= 0)
                throw new FormatException($"cell {i} has move cost {cell.MoveCost}; a step must cost something.");

            foreach (int n in cell.Neighbours)
            {
                if (!Has(n))
                    throw new FormatException($"cell {i} adjoins {n}, which is not on a board of {Count}.");

                if (n == i)
                    throw new FormatException($"cell {i} adjoins itself.");

                if (!_cells[n].Neighbours.Contains(i))
                    throw new FormatException(
                        $"adjacency is not symmetric: cell {i} adjoins {n} and {n} does not adjoin {i}.");
            }
        }
    }

    /// <summary>
    /// Dijkstra from every cell. A binary heap would be faster and this is a few hundred cells;
    /// the simple form is kept because it is obviously correct and runs once.
    /// </summary>
    private int[][] AllPairs()
    {
        int n = _cells.Length;
        int[][] all = new int[n][];

        for (int start = 0; start < n; start++)
        {
            int[] dist = new int[n];
            bool[] settled = new bool[n];
            Array.Fill(dist, int.MaxValue);
            dist[start] = 0;

            for (int step = 0; step < n; step++)
            {
                int at = -1, best = int.MaxValue;
                for (int i = 0; i < n; i++)
                    if (!settled[i] && dist[i] < best) { best = dist[i]; at = i; }

                if (at < 0) break;
                settled[at] = true;

                foreach (int next in _cells[at].Neighbours)
                {
                    // A step is priced by both ends, so crossing into mountains costs more than
                    // crossing out of them costs less — and the graph stays undirected, which is
                    // what makes a route the same length in both directions.
                    int step_ = (_cells[at].MoveCost + _cells[next].MoveCost + 1) / 2;
                    if (dist[at] + step_ >= dist[next]) continue;
                    dist[next] = dist[at] + step_;
                }
            }

            // A cell nothing reaches would give a rule an infinity to reason about. The board is
            // verified connected on load, so this cannot fire; it is here because "cannot fire"
            // has been wrong before and a silent int.MaxValue would flow into a score.
            for (int i = 0; i < n; i++)
                if (dist[i] == int.MaxValue)
                    throw new FormatException($"cell {i} cannot be reached from cell {start}; the board is not connected.");

            all[start] = dist;
        }

        return all;
    }
}
