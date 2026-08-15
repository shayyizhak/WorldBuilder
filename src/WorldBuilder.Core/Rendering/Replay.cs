namespace WorldBuilder.Core.Rendering;

/// <summary>
/// Folds a log back into world state. This is the same reducer the live simulation uses, so
/// replaying a log is not a second implementation that can drift — and because it can stop at
/// any year, time travel is a parameter rather than a feature.
/// </summary>
public static class Replay
{
    /// <summary>Rebuilds state by applying every event up to and including <paramref name="untilYear"/>.</summary>
    public static WorldState Fold(EventLog log, ulong seed, int? untilYear = null)
    {
        WorldState state = new() { Seed = seed };

        foreach (Event e in log.Events)
        {
            if (untilYear is int limit && e.Year > limit) break;
            state.Year = e.Year;
            EventReducer.Apply(state, e);
        }

        return state;
    }

    /// <summary>
    /// Replays the log, handing each event to <paramref name="visit"/> immediately after it is
    /// applied. Used by the formatter so every line is rendered against the state as it stood
    /// at that moment rather than against the end of history.
    /// </summary>
    public static WorldState Walk(EventLog log, ulong seed, Action<WorldState, Event> visit)
    {
        WorldState state = new() { Seed = seed };

        foreach (Event e in log.Events)
        {
            state.Year = e.Year;
            EventReducer.Apply(state, e);
            visit(state, e);
        }

        return state;
    }
}
