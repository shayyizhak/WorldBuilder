namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Which house each person belonged to, at every point in the log.
///
/// The design question this settles: membership is <b>folded state, not a field on the event</b>.
/// Copying "the victim belonged to f:2" onto every event would denormalise a fact the reducer
/// already owns, and the copy would be wrong the moment v2 inserted an event that changed who
/// belonged to what — the retcon would have to rewrite payloads it did not author. So the log
/// keeps recording what happened and this index reconstructs who was where.
///
/// What makes that affordable is doing it once. Every question of the form "was the killer one
/// of theirs at the time" previously cost a full replay of the log, per faction, per scope; this
/// is built during the single replay <see cref="WorldView"/> already performs and then answers
/// in a binary search. Transitions are sparse — a person changes house a handful of times in a
/// life — so the whole index is a few hundred entries.
///
/// Positions are event ordinals rather than years, because several changes routinely happen
/// inside one year and "at the time" has to mean at the time.
/// </summary>
public sealed class Membership
{
    private readonly Dictionary<int, List<(int At, EntityId Faction)>> _byActor = [];

    /// <summary>
    /// Records any change of house caused by the event just applied. Called from the replay
    /// walk, after the reducer, so it sees state exactly as the event left it.
    /// </summary>
    internal void Observe(WorldState state, Event e)
    {
        foreach (Actor a in state.Actors)
        {
            if (!_byActor.TryGetValue(a.Id.Index, out List<(int At, EntityId Faction)>? spells))
            {
                _byActor[a.Id.Index] = [(e.Id.Value, a.Faction)];
                continue;
            }

            if (spells[^1].Faction != a.Faction) spells.Add((e.Id.Value, a.Faction));
        }
    }

    /// <summary>The house this person held entering <paramref name="at"/> — before it applied.</summary>
    public EntityId Before(EntityId actor, EventId at) => Lookup(actor, at.Value - 1);

    /// <summary>The house this person held once <paramref name="at"/> had applied.</summary>
    public EntityId After(EntityId actor, EventId at) => Lookup(actor, at.Value);

    /// <summary>
    /// Whether this person answered to that house as the event began.
    ///
    /// "As it began" is the right edge for both sides of a killing: an exile strips a house on
    /// the way through, and a victim's own death event would otherwise be read as leaving them
    /// houseless at the moment they were killed.
    /// </summary>
    public bool WasIn(EntityId actor, EntityId faction, EventId at) =>
        !faction.IsNone && actor.Kind == EntityKind.Actor && Before(actor, at) == faction;

    private EntityId Lookup(EntityId actor, int ordinal)
    {
        if (actor.IsNone || actor.Kind != EntityKind.Actor) return EntityId.None;
        if (!_byActor.TryGetValue(actor.Index, out List<(int At, EntityId Faction)>? spells)) return EntityId.None;

        // Last transition at or before the position asked about.
        int lo = 0, hi = spells.Count - 1, found = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (spells[mid].At <= ordinal) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }

        return found < 0 ? EntityId.None : spells[found].Faction;
    }
}
