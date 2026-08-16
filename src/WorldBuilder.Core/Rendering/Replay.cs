using WorldBuilder.Core.Geography;

namespace WorldBuilder.Core.Rendering;

/// <summary>
/// Folds a log back into world state. This is the same reducer the live simulation uses, so
/// replaying a log is not a second implementation that can drift — and because it can stop at
/// any year, time travel is a parameter rather than a feature.
///
/// A world whose genesis event names a board gets that board attached, so a replayed world can
/// answer the same distance questions the live one could. It is looked up rather than guessed
/// at: the genesis event carries the board's fingerprint, so the one on disk is either the one
/// this history happened on or it is refused.
/// </summary>
public static class Replay
{
    /// <summary>Rebuilds state by applying every event up to and including <paramref name="untilYear"/>.</summary>
    public static WorldState Fold(EventLog log, ulong seed, int? untilYear = null)
    {
        WorldState state = new() { Seed = seed };
        Attach(state, log);

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
        Attach(state, log);

        foreach (Event e in log.Events)
        {
            state.Year = e.Year;
            EventReducer.Apply(state, e);
            visit(state, e);
        }

        return state;
    }

    /// <summary>
    /// Attaches the board this history was run on, where the log names one.
    ///
    /// A log written before geography existed names none, and gets none — its places carry no
    /// cell, and every rule that consults distance reads the neutral value, which is exactly the
    /// behaviour those worlds were simulated under. That is a real answer for a real case rather
    /// than a fallback: the alternative, quietly attaching today's board to a world that was
    /// never on it, would answer distance questions about a map that history never saw.
    ///
    /// A log that names a board this build cannot produce is refused. A world folded against the
    /// wrong map is internally consistent and about somewhere else, and no downstream check would
    /// see anything unusual.
    /// </summary>
    private static void Attach(WorldState state, EventLog log)
    {
        if (log.Count == 0) return;

        Event genesis = log.Events[0];
        if (genesis.Kind != EventKind.GenesisWorld) return;

        string named = genesis.GetString("board") ?? "";
        if (named.Length == 0) return;

        Board board = Boards.Stored();
        if (string.Equals(board.Fingerprint, named, StringComparison.OrdinalIgnoreCase))
        {
            state.Attach(board);
            return;
        }

        throw new FormatException(
            $"this world was simulated on board {named[..12]} and the stored board is " +
            $"{board.Fingerprint[..12]}. A map that is not the one a history happened on gives " +
            "every distance in it a different answer, and nothing downstream would look wrong.");
    }
}
