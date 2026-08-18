using WorldBuilder.Core.Geography;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// How far apart a world's places are: the shape the rules are actually calibrated against.
/// </summary>
/// <param name="Range">
/// Lowest and highest separation, as an interval that states both ends and its width. It was a
/// width of this kind, written down as "spread" and read as a standard deviation, that carried a
/// verdict in the controls phase.
/// </param>
/// <param name="Cv">
/// Standard deviation as a percentage of the mean. Dimensionless on purpose, so two boards of
/// different scales can be compared on how unequal their separations are rather than how large.
/// </param>
public sealed record SeparationProfile(int Pairs, int Median, Dispersion Range, Dispersion Cv);

/// <summary>How a mechanic's acts fell out across the board: near, far, and how many of each.</summary>
/// <param name="Near">Acts whose two ends were closer together than the board's median separation.</param>
/// <param name="Far">Acts whose two ends were further apart than that.</param>
public sealed record ReachSplit(string Mechanic, int Near, int Far)
{
    public int Total => Near + Far;

    /// <summary>The share of the commoner side, in the same units the outcome-skew bar uses.</summary>
    public int SkewPct => Total == 0 ? 0 : Math.Max(Near, Far) * 100 / Total;
}

/// <summary>
/// What geography did, measured rather than assumed.
///
/// Two jobs, and they are different in kind.
///
/// <b>Structure.</b> Every place that can be travelled to has exactly one position, and every
/// position is on the board. Asserted rather than trusted, because a place that quietly lost its
/// cell would read as one that is exactly averagely far from everything — plausible, uniform, and
/// wrong, which is the failure profile this project keeps meeting.
///
/// <b>Reach.</b> For each mechanic that consumes distance, the near/far split of what it actually
/// did. This is the Layer 1 metric §7 asks for, and it is deliberately the *same* metric for all
/// four rather than a bespoke figure each: one shape, one bar, and four numbers that can be read
/// against each other.
///
/// The bar is the established outcome-skew bar — no more than 90% one way — and not a new one.
/// A mechanic that only ever acts nearby has had distance turned into a gate, which is as
/// decorative as no distance at all: the far branch exists and never fires. A mechanic that acts
/// near and far in the same proportions the map offers has not consumed distance at all. Both
/// failures are visible in one number.
/// </summary>
public static class GeographyAudit
{
    /// <summary>
    /// Everything structurally wrong with where this world's places are. Empty is the good case.
    /// </summary>
    public static List<string> Positions(WorldState state)
    {
        List<string> complaints = [];

        if (state.Board is not Board board)
        {
            // Not a complaint on its own. A world folded from a log written before geography
            // existed legitimately has no board, and saying it is broken would make every
            // ruleset-3 artefact fail a check about a feature it predates.
            foreach (Place place in state.Places)
                if (place.IsSited) complaints.Add($"{place.Id} carries cell {place.Cell} and the world has no board");

            return complaints;
        }

        HashSet<int> seen = [];

        foreach (Place place in state.Places)
        {
            if (place.Kind == PlaceKind.Region)
            {
                // The region is the ground the board is made of rather than a point on it. It is
                // never marched on, held, raided or fought over, and a cell for it would put a
                // spurious position into every distance the engine measures.
                if (place.IsSited) complaints.Add($"{place.Id} is a region and should carry no cell, but has {place.Cell}");
                continue;
            }

            if (!place.IsSited) { complaints.Add($"{place.Id} has no position"); continue; }
            if (!board.Has(place.Cell)) { complaints.Add($"{place.Id} is at cell {place.Cell}, off a board of {board.Count}"); continue; }
            if (!board[place.Cell].IsLand) complaints.Add($"{place.Id} is at cell {place.Cell}, which is open water");
            if (!seen.Add(place.Cell)) complaints.Add($"cell {place.Cell} holds more than one place");
        }

        return complaints;
    }

    /// <summary>
    /// The range of proximities between this world's places: the reachability guard for every
    /// rule that multiplies by one.
    ///
    /// <b>An invariant that cannot vary is not an invariant.</b> If every pair of places came out
    /// at the same proximity, four mechanics would be multiplying by a constant and reporting that
    /// geography was consulted. That is precisely the shape <c>CoupDecidedPct</c> had — a
    /// plausible number a threshold was tuned against, from a numerator no path could move — and
    /// it is why this returns the range rather than an average.
    ///
    /// A <see cref="Dispersion"/> rather than two ints, so the invariant that prints it cannot
    /// print a width where a reader expects a standard deviation. It used to be called
    /// <c>ProximitySpread</c>, and "spread" is the word that meant three different things.
    /// </summary>
    public static (int Pairs, Dispersion Range) ProximityRange(WorldState state)
    {
        if (state.Geo is not Geography.Geography geo) return (0, Dispersion.Range(0, 0));

        List<Place> sited = [.. state.Places.Where(static p => p.IsSited)];

        int pairs = 0, lowest = int.MaxValue, highest = int.MinValue;

        for (int a = 0; a < sited.Count; a++)
        {
            for (int b = a + 1; b < sited.Count; b++)
            {
                int near = geo.BetweenPlaces(sited[a].Id, sited[b].Id);
                pairs++;
                lowest = Math.Min(lowest, near);
                highest = Math.Max(highest, near);
            }
        }

        return pairs == 0
            ? (0, Dispersion.Range(0, 0))
            : (pairs, Dispersion.Range(lowest, highest, pairs));
    }

    /// <summary>
    /// How far apart this world's places actually are, in the board's own cost units.
    ///
    /// The board's terrain histogram says what a map is made of; this says what a *world* on it
    /// is shaped like, which is the figure the rules are calibrated against and the one a claim
    /// about "the board is too uniform" is a claim about.
    ///
    /// <b>Dispersion is reported as a coefficient of variation</b> — the standard deviation as a
    /// percentage of the mean — rather than as an interquartile range, and the choice matters
    /// because this figure is compared across boards. An IQR is in cost units, so a board whose
    /// costs are simply larger looks more varied; a coefficient of variation is dimensionless and
    /// says what it is meant to say, which is how *unequal* the separations are rather than how
    /// big.
    ///
    /// Both figures are returned, each carrying its kind. Reporting only one of a range and a
    /// coefficient of variation is how the two came to be confused: they were both called the
    /// spread, in different documents, and the word is now retired from every emitted figure.
    /// </summary>
    public static SeparationProfile Separations(WorldState state)
    {
        List<Place> sited = [.. state.Places.Where(static p => p.IsSited)];

        if (state.Board is not Board board || sited.Count < 2)
            return new SeparationProfile(0, 0, Dispersion.Range(0, 0), Dispersion.Cv(0));

        List<int> costs = [];
        for (int a = 0; a < sited.Count; a++)
            for (int b = a + 1; b < sited.Count; b++)
                costs.Add(board.Cost(sited[a].Cell, sited[b].Cell));

        costs.Sort();

        return new SeparationProfile(
            costs.Count,
            costs[costs.Count / 2],
            Dispersion.Range(costs[0], costs[^1], costs.Count),
            Dispersion.Cv(costs));
    }

    /// <summary>
    /// The near/far split of every mechanic that consumes distance.
    ///
    /// Measured between fixed ground — a place's cell and a house's seat, neither of which ever
    /// moves — so the figure is a fact about the act rather than about the state the world
    /// happened to be in when it was counted. Measuring against holdings, which change hands,
    /// would make the same log produce different numbers depending on when it was read.
    /// </summary>
    public static List<ReachSplit> Reach(WorldState state, EventLog log)
    {
        if (state.Geo is not Geography.Geography geo) return [];

        Dictionary<string, (int Near, int Far)> tally = new(StringComparer.Ordinal);

        foreach (Event e in log.Events)
        {
            (string mechanic, EntityId from, EntityId to) = Ends(state, e);
            if (mechanic.Length == 0 || from.IsNone || to.IsNone) continue;

            int near = geo.BetweenPlaces(from, to);
            if (near == Geography.Geography.Neutral) continue;   // exactly at the median: neither

            (int wasNear, int wasFar) = tally.GetValueOrDefault(mechanic);
            tally[mechanic] = near > Geography.Geography.Neutral ? (wasNear + 1, wasFar) : (wasNear, wasFar + 1);
        }

        List<ReachSplit> splits = [];
        foreach ((string mechanic, (int near, int far)) in tally) splits.Add(new ReachSplit(mechanic, near, far));

        splits.Sort(static (a, b) => string.CompareOrdinal(a.Mechanic, b.Mechanic));
        return splits;
    }

    /// <summary>
    /// The two pieces of ground an act happened between, for the four mechanics that weigh
    /// distance. Everything else returns an empty name and is not counted.
    /// </summary>
    private static (string Mechanic, EntityId From, EntityId To) Ends(WorldState state, Event e)
    {
        switch (e.Kind)
        {
            case EventKind.ConflictRaid:
                return ("raid targeting", SeatOf(state, e.Faction), e.Where);

            case EventKind.DiploWarDeclared:
                return ("war declaration", SeatOf(state, e.Faction), SeatOf(state, e.GetEntity("against")));

            case EventKind.ConflictConquest:
                return ("conquest", SeatOf(state, e.Faction), e.Where);

            case EventKind.DiploAllianceFormed:
                return ("alliance", SeatOf(state, e.Faction), SeatOf(state, e.Object));

            case EventKind.LifeMarriage when e.GetInt("crossFaction") == 1:
                // Only the cross-house matches. A marriage inside one household happens where
                // everybody already is, so counting it would measure the cast's address rather
                // than the mechanic's reach.
                return ("marriage", PlaceOf(state, e.Subject), PlaceOf(state, e.Object));

            default:
                return ("", EntityId.None, EntityId.None);
        }
    }

    private static EntityId SeatOf(WorldState state, EntityId faction) =>
        faction.IsNone || faction.Kind != EntityKind.Faction ? EntityId.None : state.FactionOf(faction).Seat;

    private static EntityId PlaceOf(WorldState state, EntityId actor) =>
        actor.IsNone || actor.Kind != EntityKind.Actor ? EntityId.None : state.ActorOf(actor).Place;
}
