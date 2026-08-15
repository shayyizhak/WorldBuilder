namespace WorldBuilder.Core.Rules;

/// <summary>
/// Cooldowns, checked against the log itself rather than against extra state.
///
/// Without these the engine happily emitted "the Laenbarrow Compact demands tribute of the
/// Fo League and is refused" once a year for thirty years. Each line was individually true and
/// collectively worthless: repetition is the main way a symbolic history turns unreadable, and
/// it is cheaper to forbid it here than to filter it out at the formatter.
/// </summary>
public static class Recent
{
    /// <summary>Did this entity do this kind of thing to that entity within the last N years?</summary>
    public static bool Did(Tick tick, EntityId actor, EventKind kind, EntityId towards, int withinYears)
    {
        IReadOnlyList<EventId> history = tick.Log.ForEntity(actor);
        int cutoff = tick.Year - withinYears;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            Event e = tick.Log.Get(history[i]);
            if (e.Year < cutoff) return false;
            if (e.Kind != kind) continue;
            if (towards.IsNone) return true;

            // Any role counts. Restricting this to subject and object silently exempted
            // everything aimed at a *place* — raids target a Role.Place participant, so the
            // raid cooldown never once fired.
            foreach (Participant p in e.Participants)
                if (p.Id == towards) return true;
        }

        return false;
    }

    /// <summary>As <see cref="Did"/>, ignoring the target — "has this happened here at all lately".</summary>
    public static bool Happened(Tick tick, EntityId entity, EventKind kind, int withinYears) =>
        Did(tick, entity, kind, EntityId.None, withinYears);

    /// <summary>
    /// The most recent event of a kind touching this entity, or nothing. Used to cite the thing
    /// that actually made an action possible — the harvest that produced the surplus being
    /// sold, the peace that made an alliance thinkable.
    /// </summary>
    public static EventId LastOfKind(Tick tick, EntityId entity, EventKind kind)
    {
        IReadOnlyList<EventId> history = tick.Log.ForEntity(entity);
        for (int i = history.Count - 1; i >= 0; i--)
            if (tick.Log.Get(history[i]).Kind == kind) return history[i];
        return EventId.None;
    }

    /// <summary>Has this ever happened to this entity, at any point in the world's history?</summary>
    public static bool Ever(Tick tick, EntityId entity, EventKind kind) => CountEver(tick, entity, kind) > 0;

    /// <summary>
    /// How many times this has happened to this entity across all of history. Used where a
    /// mechanic must get more expensive each time it is used rather than staying a free move.
    /// </summary>
    public static int CountEver(Tick tick, EntityId entity, EventKind kind, string? dataKey = null, string? dataValue = null)
    {
        int count = 0;
        foreach (EventId id in tick.Log.ForEntity(entity))
        {
            Event e = tick.Log.Get(id);
            if (e.Kind != kind) continue;
            if (dataKey is not null && e.GetString(dataKey) != dataValue) continue;
            count++;
        }
        return count;
    }
}
