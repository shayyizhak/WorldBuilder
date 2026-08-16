namespace WorldBuilder.Core.Geography;

/// <summary>
/// Where the world's places stand on the imported board.
///
/// <b>The decision, recorded because §4.3 asked for it explicitly.</b> Three options were open:
/// derive positions from the map, assign them at worldgen, or seed them from adjacency already
/// implicit in the world. The third is not available — a <see cref="Place"/> has a parent and
/// nothing else, so there is no existing adjacency to recover, and inventing one from who traded
/// with whom would be reading a history back into the ground it happened on, which is exactly
/// the direction §2 forbids.
///
/// Between the other two: <b>assigned at worldgen, and recorded in the log</b>. Deriving at load
/// time would make a position a function of whatever code was running when the world was opened,
/// so two builds could disagree about where a town is while both read the same record. Assigning
/// at worldgen puts the cell on the genesis event, which makes it a fact about the world rather
/// than an opinion about it — the same reasoning that puts every other genesis value there.
///
/// <b>Terrain decides what may go where.</b> Settlements take habitable ground and mines take
/// highland, which is not decoration: it means the richest places on the map are also the hardest
/// to reach and the most awkward to hold, and that was already true of them economically. A mine
/// cannot feed itself; now it is also up a mountain.
/// </summary>
public static class Siting
{
    /// <summary>Ground a town will stand on. Anything but open water and bare rock.</summary>
    private static readonly Terrain[] Habitable =
        [Terrain.Plains, Terrain.Forest, Terrain.Hills, Terrain.Marsh];

    /// <summary>Ground that has ore under it.</summary>
    private static readonly Terrain[] Mineral = [Terrain.Hills, Terrain.Mountains];

    /// <summary>
    /// Chooses a cell for the next place, given what has already been sited.
    ///
    /// <b>Spread, not scatter.</b> The cell chosen is the candidate furthest from everything
    /// already placed, which puts the world's towns across the board rather than in a heap. The
    /// alternative — an independent random cell each time — produces worlds where two of five
    /// settlements are neighbours and the rest are a fortnight apart, and a distance rule reading
    /// that map produces a history about an accident of sampling.
    ///
    /// The seed enters once, at the first place, and everything after it follows from the board.
    /// So two seeds lay their world out differently, and neither lays it out badly.
    /// </summary>
    public static int Choose(Board board, IReadOnlyList<int> taken, PlaceKind kind, ref Rng rng)
    {
        Terrain[] wanted = kind == PlaceKind.Site ? Mineral : Habitable;

        List<int> candidates = [];
        for (int i = 0; i < board.Count; i++)
            if (Array.IndexOf(wanted, board[i].Terrain) >= 0 && !taken.Contains(i)) candidates.Add(i);

        // A board with nowhere to put a mine is a board this world cannot be run on, and saying
        // so beats putting the mine in the sea. `wb map show` reports the terrain histogram
        // precisely so this is visible before a run rather than during one.
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"the board has no free {string.Join(" or ", wanted)} cell for a {kind}. " +
                "Check `wb map show` — a board needs ground of every kind the world is made of.");
        }

        if (taken.Count == 0) return candidates[rng.Next(candidates.Count)];

        int best = candidates[0], bestDistance = -1;
        foreach (int candidate in candidates)
        {
            int nearest = int.MaxValue;
            foreach (int already in taken) nearest = Math.Min(nearest, board.Cost(candidate, already));

            // Ties break on the lower cell index, so the choice is a function of the board and
            // the seed and of nothing else — not of the order a list happened to come out in.
            if (nearest <= bestDistance) continue;
            bestDistance = nearest;
            best = candidate;
        }

        return best;
    }
}
