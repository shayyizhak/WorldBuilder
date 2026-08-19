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
    public static WorldState Fold(EventLog log, ulong seed, int? untilYear = null, Board? board = null)
    {
        WorldState state = new() { Seed = seed };
        FoldInto(state, log, board, untilYear);
        return state;
    }

    /// <summary>
    /// Folds a log into a state the caller already holds.
    ///
    /// Exists so a caller can attach a sink to the state <i>before</i> the first event is applied.
    /// The goal audit needs to see the transitions the reducer performs, and by the time
    /// <see cref="Fold"/> has returned every one of them is in the past. Same loop, same reducer.
    /// </summary>
    public static void FoldInto(WorldState state, EventLog log, Board? board = null, int? untilYear = null)
    {
        Attach(state, log, board);

        foreach (Event e in log.Events)
        {
            if (untilYear is int limit && e.Year > limit) break;
            state.Year = e.Year;
            EventReducer.Apply(state, e);
        }
    }

    /// <summary>
    /// Replays the log, handing each event to <paramref name="visit"/> immediately after it is
    /// applied. Used by the formatter so every line is rendered against the state as it stood
    /// at that moment rather than against the end of history.
    /// </summary>
    /// <param name="before">
    /// Called with the same state <i>immediately before</i> the event is applied, where a caller
    /// needs both sides of one event.
    ///
    /// Asking whether a payload key changed anything is a question about the difference, and the
    /// difference is not recoverable from either side alone — a <c>relDel</c> naming no live edge
    /// and one naming an edge look identical once the fold has run. Nothing here mutates: the
    /// reducer is still the only writer, and the instrumentation-invariance tests hold because a
    /// reader attached to this cannot move the world.
    /// </param>
    public static WorldState Walk(EventLog log, ulong seed, Action<WorldState, Event> visit,
        Board? board = null, Action<WorldState, Event>? before = null)
    {
        WorldState state = new() { Seed = seed };
        WalkInto(state, log, visit, board, before);
        return state;
    }

    /// <summary>
    /// Walks a log into a state the caller already holds, for the same reason as
    /// <see cref="FoldInto"/>: a sink has to be attached before the first event is applied.
    /// </summary>
    public static void WalkInto(
        WorldState state, EventLog log, Action<WorldState, Event> visit, Board? board = null,
        Action<WorldState, Event>? before = null)
    {
        Attach(state, log, board);

        foreach (Event e in log.Events)
        {
            state.Year = e.Year;
            before?.Invoke(state, e);
            EventReducer.Apply(state, e);
            visit(state, e);
        }
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
    /// <param name="offered">
    /// A board the caller already holds — a measurement panel builds one per seed and never
    /// stores it. Null means look up the repository's. Either way the log's fingerprint decides:
    /// offering the wrong board is refused exactly as loudly as finding the wrong one.
    /// </param>
    private static void Attach(WorldState state, EventLog log, Board? offered)
    {
        if (log.Count == 0) return;

        Event genesis = log.Events[0];
        if (genesis.Kind != EventKind.GenesisWorld) return;

        string named = genesis.GetString("board") ?? "";
        if (named.Length == 0) return;

        Board board = offered ?? Boards.Stored();
        if (string.Equals(board.Fingerprint, named, StringComparison.OrdinalIgnoreCase))
        {
            state.Attach(board);
            return;
        }

        throw new FormatException(
            $"this world was simulated on board {named[..12]} and the board offered for it is " +
            $"{board.Fingerprint[..12]}. A map that is not the one a history happened on gives " +
            "every distance in it a different answer, and nothing downstream would look wrong.");
    }
}
