using WorldBuilder.Core.Rendering;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// A log plus everything the query commands need derived from it: the final state, and each
/// event's sentence rendered against the state as it stood when the event happened. Built
/// once by replay so <c>why</c>, <c>who</c> and <c>stats</c> all agree with the <c>.log</c> file.
/// </summary>
public sealed class WorldView
{
    private readonly string[] _descriptions;

    private WorldView(EventLog log, ulong seed, WorldState state, string[] descriptions, Membership membership)
    {
        Log = log;
        Seed = seed;
        State = state;
        _descriptions = descriptions;
        Members = membership;
    }

    public EventLog Log { get; }
    public ulong Seed { get; }

    /// <summary>The world at the end of the log.</summary>
    public WorldState State { get; }

    /// <summary>
    /// Who belonged to which house, at each point in the log. Built in the same replay as the
    /// descriptions, so asking "was the killer one of theirs at the time" costs a lookup rather
    /// than a fold — see <see cref="Membership"/> for why it is derived rather than carried.
    /// </summary>
    public Membership Members { get; }

    public int FirstYear => Log.Count > 0 ? Log.Events[0].Year : 0;
    public int LastYear => Log.Count > 0 ? Log.Events[^1].Year : 0;

    /// <param name="board">
    /// The board this history was run on, where the caller already holds it. A measurement panel
    /// makes one board per seed and never stores it, so there is nothing on disk to look up. The
    /// log's own fingerprint still decides: offering the wrong board is refused.
    /// </param>
    public static WorldView Build(EventLog log, ulong seed, Geography.Board? board = null)
    {
        string[] descriptions = new string[log.Count + 1];
        Membership membership = new();

        WorldState state = Replay.Walk(log, seed, (s, e) =>
        {
            descriptions[e.Id.Value] = EventTemplates.Describe(s, e);
            membership.Observe(s, e);
        }, board);

        return new WorldView(log, seed, state, descriptions, membership);
    }

    public static WorldView Load(string path)
    {
        (EventLog log, ulong seed) = Serialization.JsonlIo.Read(path);
        return Build(log, seed);
    }

    public string Describe(EventId id) => _descriptions[id.Value];

    /// <summary>One compact line for use inside trees and timelines.</summary>
    public string Summarise(EventId id)
    {
        Event e = Log.Get(id);
        return $"{id} [Y{e.Year:D4}] {EventKinds.Name(e.Kind)}  {Describe(id)}";
    }
}
