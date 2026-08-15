namespace WorldBuilder.Core;

/// <summary>
/// What an actor or faction is currently trying to do. Goals are the difference between a
/// history and a list: they persist across ticks, so actions accumulate into arcs instead of
/// firing independently. An owner holds at most <see cref="GoalBook.MaxPerOwner"/> at once.
/// </summary>
public enum GoalKind : byte
{
    /// <summary>Actor wants their faction's leadership.</summary>
    SeizeLeadership = 0,
    /// <summary>Owner wants to make the target pay for something recorded in the log.</summary>
    Avenge = 1,
    /// <summary>Faction wants a place it does not control.</summary>
    TakePlace = 2,
    /// <summary>Faction is short of grain and will trade, raid or seize to fix it.</summary>
    SecureGrain = 3,
    /// <summary>Faction wants an ore site specifically — the scarcest thing in the region.</summary>
    ControlOre = 4,
    /// <summary>Faction wants a friend, usually because it is losing.</summary>
    FormAlliance = 5,
    /// <summary>Faction's legitimacy is low and the ruler knows it.</summary>
    RestoreLegitimacy = 6,
    /// <summary>Exiled actor wants back in. The engine's recurring-antagonist generator.</summary>
    ReturnFromExile = 7,
}

public sealed class Goal
{
    public required int Id { get; init; }
    public required EntityId Owner { get; init; }
    public required GoalKind Kind { get; init; }
    public required int CreatedYear { get; init; }
    public required EventId Cause { get; init; }

    public EntityId Target { get; set; }

    /// <summary>The storyline this goal spawned, if it has got that far (a plot, a war).</summary>
    public EntityId Arc { get; set; }

    /// <summary>0..100. Actions advance it; reaching 100 completes the goal.</summary>
    public int Progress { get; set; }

    /// <summary>Abandoned if not completed by this year, so dead goals do not clog the book.</summary>
    public required int ExpiresYear { get; init; }
}

/// <summary>
/// The set of live goals, keyed by owner. Sorted so iteration order is deterministic.
/// </summary>
public sealed class GoalBook
{
    public const int MaxPerOwner = 2;

    private readonly SortedDictionary<EntityId, List<Goal>> _byOwner = [];
    private int _nextId = 1;

    public IReadOnlyList<Goal> For(EntityId owner) =>
        _byOwner.TryGetValue(owner, out List<Goal>? list) ? list : [];

    public bool Has(EntityId owner, GoalKind kind)
    {
        foreach (Goal g in For(owner))
            if (g.Kind == kind) return true;
        return false;
    }

    public Goal? Find(EntityId owner, GoalKind kind)
    {
        foreach (Goal g in For(owner))
            if (g.Kind == kind) return g;
        return null;
    }

    /// <summary>Adds a goal unless the owner is full or already holds one of this kind.</summary>
    public Goal? Add(EntityId owner, GoalKind kind, EntityId target, int year, int lifespan, EventId cause)
    {
        if (!_byOwner.TryGetValue(owner, out List<Goal>? list))
            _byOwner[owner] = list = [];

        if (list.Count >= MaxPerOwner) return null;
        foreach (Goal g in list)
            if (g.Kind == kind && g.Target == target) return null;

        Goal goal = new()
        {
            Id = _nextId++,
            Owner = owner,
            Kind = kind,
            Target = target,
            CreatedYear = year,
            ExpiresYear = year + lifespan,
            Cause = cause,
        };
        list.Add(goal);
        return goal;
    }

    public void Remove(Goal goal)
    {
        if (_byOwner.TryGetValue(goal.Owner, out List<Goal>? list))
        {
            list.Remove(goal);
            if (list.Count == 0) _byOwner.Remove(goal.Owner);
        }
    }

    public void RemoveAllFor(EntityId owner) => _byOwner.Remove(owner);

    /// <summary>Every live goal, ordered by owner then creation. Safe to mutate during iteration.</summary>
    public List<Goal> Snapshot()
    {
        List<Goal> all = [];
        foreach (KeyValuePair<EntityId, List<Goal>> kv in _byOwner)
            all.AddRange(kv.Value);
        return all;
    }
}
