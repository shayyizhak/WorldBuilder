namespace WorldBuilder.Core;

/// <summary>
/// The append-only log. World state is a fold over this; nothing here is ever mutated or
/// removed. Indexes are maintained on append so <c>why</c>/<c>who</c> are traversals rather
/// than scans, but they are pure derivations of the sequence and can be rebuilt from it.
/// </summary>
public sealed class EventLog
{
    private readonly List<Event> _events = [];
    private readonly Dictionary<EntityId, List<EventId>> _byEntity = [];
    private readonly Dictionary<EventId, List<EventId>> _effects = [];

    public IReadOnlyList<Event> Events => _events;
    public int Count => _events.Count;

    /// <summary>Id the next appended event will receive. Ids are 1-based.</summary>
    public EventId NextId => new(_events.Count + 1);

    public Event Append(Event e)
    {
        if (e.Id.Value != _events.Count + 1)
            throw new InvalidOperationException($"Event id {e.Id} out of sequence; expected {NextId}.");

        _events.Add(e);

        foreach (Participant p in e.Participants)
        {
            if (p.Id.IsNone) continue;
            if (!_byEntity.TryGetValue(p.Id, out List<EventId>? list))
                _byEntity[p.Id] = list = [];
            if (list.Count == 0 || list[^1] != e.Id) list.Add(e.Id);
        }

        foreach (EventId cause in e.Causes)
        {
            if (cause.IsNone) continue;
            if (!_effects.TryGetValue(cause, out List<EventId>? list))
                _effects[cause] = list = [];
            list.Add(e.Id);
        }

        return e;
    }

    public Event Get(EventId id)
    {
        if (id.IsNone || id.Value > _events.Count)
            throw new ArgumentOutOfRangeException(nameof(id), $"No such event {id}.");
        return _events[id.Value - 1];
    }

    public bool TryGet(EventId id, out Event e)
    {
        if (id.IsNone || id.Value > _events.Count) { e = null!; return false; }
        e = _events[id.Value - 1];
        return true;
    }

    /// <summary>Events this entity participated in, in chronological order.</summary>
    public IReadOnlyList<EventId> ForEntity(EntityId id) =>
        _byEntity.TryGetValue(id, out List<EventId>? list) ? list : [];

    /// <summary>
    /// The event that brought this entity into the world. Used as the causal parent for
    /// things that have no other cause — an actor dying of old age is caused by having
    /// been born, which is both true and the only honest edge to draw.
    /// </summary>
    public EventId OriginOf(EntityId id)
    {
        IReadOnlyList<EventId> events = ForEntity(id);
        return events.Count > 0 ? events[0] : EventId.None;
    }

    /// <summary>Events that name this one as a cause — the downstream half of the causal graph.</summary>
    public IReadOnlyList<EventId> EffectsOf(EventId id) =>
        _effects.TryGetValue(id, out List<EventId>? list) ? list : [];

    public IEnumerable<Event> InYear(int year)
    {
        foreach (Event e in _events)
            if (e.Year == year) yield return e;
    }
}
