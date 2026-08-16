namespace WorldBuilder.Core.Geography;

/// <summary>
/// The world's view of the board: where its places sit, and how far apart they are.
///
/// This is the single distance function §2 pre-committed to. Four mechanics consume it and none
/// of them holds a notion of nearness of its own — a raid, a war, a conquest and a betrothal all
/// ask the same object the same question, so if the map says two houses are neighbours they are
/// neighbours to all four. The alternative, which is what happens by default, is four mechanics
/// each with a private idea of "close" that agree until somebody compares them.
///
/// Everything it returns is a <b>proximity percentage</b> rather than a raw cost, for a reason
/// worth stating: a cost is in the board's units and a rule that multiplies by one is a rule
/// that has to be re-tuned for every map. A proximity is 100 at the board's own median
/// separation whatever the map, so a score that was calibrated before geography existed still
/// means what it meant at a typical distance, and only near and far move it.
/// </summary>
public sealed class Geography
{
    /// <summary>
    /// What a rule sees where the world has no board at all.
    ///
    /// A world folded from a ruleset-3 log has no positions, and its rules must behave exactly as
    /// they did when they were written — which is what 100 gives, since every consumer multiplies
    /// by this and divides by a hundred. It is a real answer for a real case, not a fallback that
    /// papers over a missing map: <c>wb run</c> refuses to simulate without a board, so the only
    /// worlds that see this are the ones that legitimately predate one.
    ///
    /// It is nonetheless the shape of thing this project keeps getting caught by — a neutral
    /// value that presents as a working rule — so Layer 1 asserts that proximity actually varies
    /// in a world that has a board, rather than trusting that it must.
    /// </summary>
    public const int Neutral = 100;

    private readonly Board _board;
    private readonly WorldState _state;
    private int _reference = -1;

    internal Geography(Board board, WorldState state)
    {
        _board = board;
        _state = state;
    }

    public Board Board => _board;

    /// <summary>
    /// The separation a rule should treat as ordinary: the median travel cost between the places
    /// this world actually has.
    ///
    /// <b>Not the board's own median, and the difference was a real defect rather than a nicety.</b>
    /// <see cref="Board.ReferenceCost"/> is the median over every pair of land cells, and the
    /// first version of this class used it — on the reasoning that a board should calibrate
    /// itself. But places are not scattered over the board at random: <see cref="Siting"/> spreads
    /// them deliberately, choosing each new site as far as possible from everything already
    /// placed. So every pair of places sat well beyond the board's median, every proximity came
    /// out below 100, and four mechanics that were supposed to be centred on "ordinary distance"
    /// were in fact discounted at every distance that ever occurred.
    ///
    /// It was invisible in the code and obvious in the metric: the near/far split reported war
    /// declaration at 0 near and 29 far across the whole panel, which is a branch that cannot
    /// fire wearing a percentage. The claim "at a typical separation a rule scores exactly what it
    /// scored before geography existed" was true of a distance no world contained.
    ///
    /// Places are fixed at genesis and never move, so this is stable for the life of the world —
    /// which it must be, or the same log would score differently depending on when it was read.
    /// </summary>
    public int ReferenceCost
    {
        get
        {
            if (_reference >= 0) return _reference;

            List<int> costs = [];
            List<Place> sited = [];
            foreach (Place place in _state.Places)
                if (place.IsSited) sited.Add(place);

            for (int a = 0; a < sited.Count; a++)
                for (int b = a + 1; b < sited.Count; b++)
                    costs.Add(_board.Cost(sited[a].Cell, sited[b].Cell));

            // A world with fewer than two sited places has no separation to be typical of, and
            // falls back to the board's own figure rather than to a constant. It cannot arise
            // from worldgen, which sites seven; it can arise from a fixture.
            if (costs.Count == 0) return _reference = _board.ReferenceCost;

            costs.Sort();
            return _reference = Math.Max(1, costs[costs.Count / 2]);
        }
    }

    /// <summary>How near two places are, as a percentage where 100 is a typical separation.</summary>
    public int BetweenPlaces(EntityId a, EntityId b)
    {
        int from = CellOf(a);
        int to = CellOf(b);
        return from < 0 || to < 0 ? Neutral : Proximity(_board.Cost(from, to));
    }

    /// <summary>
    /// How near a place is to a faction: the nearest of its holdings.
    ///
    /// Nearest rather than the seat, because reach is a property of where a house actually is.
    /// A compact whose capital is on the far coast but which holds the mine next door can raid
    /// that mine, and measuring from the seat would say otherwise — which is the sort of answer
    /// that is defensible in the code and obviously wrong in the prose.
    /// </summary>
    public int FromFactionToPlace(EntityId faction, EntityId place)
    {
        int to = CellOf(place);
        if (to < 0) return Neutral;

        int best = -1;
        foreach (Place held in _state.HoldingsOf(faction))
        {
            if (held.Cell < 0) continue;
            int cost = _board.Cost(held.Cell, to);
            if (best < 0 || cost < best) best = cost;
        }

        // A house that holds nothing has nowhere to march from. It is also, by
        // WorldState.IsDefunct, a house no rule should be considering in the first place — so
        // this is the neutral answer rather than a distant one, to avoid a defunct faction being
        // quietly ranked as merely far away.
        return best < 0 ? Neutral : Proximity(best);
    }

    /// <summary>
    /// How near two houses are: the closest their holdings come to each other.
    ///
    /// This is the one every conflict rule wants. Two realms are neighbours if any of their
    /// ground touches, however far apart their capitals are, and that is what decides whether one
    /// can plausibly march on the other.
    /// </summary>
    public int BetweenFactions(EntityId a, EntityId b)
    {
        int best = -1;

        foreach (Place ours in _state.HoldingsOf(a))
        {
            if (ours.Cell < 0) continue;
            foreach (Place theirs in _state.HoldingsOf(b))
            {
                if (theirs.Cell < 0) continue;
                int cost = _board.Cost(ours.Cell, theirs.Cell);
                if (best < 0 || cost < best) best = cost;
            }
        }

        return best < 0 ? Neutral : Proximity(best);
    }

    /// <summary>How near two people are, by the ground they stand on.</summary>
    public int BetweenActors(EntityId a, EntityId b)
    {
        Actor one = _state.ActorOf(a);
        Actor other = _state.ActorOf(b);
        return one.Place.IsNone || other.Place.IsNone
            ? Neutral
            : BetweenPlaces(one.Place, other.Place);
    }

    /// <summary>The raw travel cost between two places, for reporting rather than for scoring.</summary>
    public int CostBetween(EntityId a, EntityId b)
    {
        int from = CellOf(a);
        int to = CellOf(b);
        return from < 0 || to < 0 ? 0 : _board.Cost(from, to);
    }

    /// <summary>The terrain a place stands on, for reporting rather than for scoring.</summary>
    public Terrain TerrainOf(EntityId place)
    {
        int cell = CellOf(place);
        return cell < 0 ? Terrain.Plains : _board[cell].Terrain;
    }

    /// <summary>
    /// A cost as a proximity percentage.
    ///
    /// <code>
    /// same place            200
    /// typical separation    100
    /// far away            →   0
    /// </code>
    ///
    /// Every consumer multiplies by this and divides by a hundred, so a pair at an ordinary
    /// separation scores exactly what it scored before geography existed. That is what let four
    /// mechanics gain a distance term without a single threshold in <see cref="SimConfig"/>
    /// moving — and it only holds because <see cref="ReferenceCost"/> is a separation worlds
    /// actually contain.
    /// </summary>
    private int Proximity(int cost)
    {
        int reference = ReferenceCost;
        return 200 * reference / (reference + cost);
    }

    private int CellOf(EntityId place) =>
        place.IsNone || place.Kind != EntityKind.Place ? -1 : _state.PlaceOf(place).Cell;
}
