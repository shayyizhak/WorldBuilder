using System.Text;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Walks the stored causal edges. "Why is this duchy poor?" is a graph traversal, not an
/// opinion — which is only true because every event recorded its parents at the moment it
/// was emitted rather than having them inferred afterwards.
/// </summary>
public static class CausalTrace
{
    /// <summary>Renders the ancestry of an event as an indented tree, oldest causes deepest.</summary>
    public static IReadOnlyList<string> Why(WorldView view, EventId id, int maxDepth = 8)
    {
        List<string> lines = [];
        HashSet<EventId> seen = [];

        lines.Add(view.Summarise(id));
        Walk(view, id, prefix: "", depth: 0, maxDepth, seen, lines);
        return lines;
    }

    private static void Walk(
        WorldView view, EventId id, string prefix, int depth, int maxDepth,
        HashSet<EventId> seen, List<string> lines)
    {
        if (depth >= maxDepth) return;
        if (!seen.Add(id)) return;

        IReadOnlyList<EventId> causes = view.Log.Get(id).Causes;
        for (int i = 0; i < causes.Count; i++)
        {
            bool last = i == causes.Count - 1;
            lines.Add($"{prefix}{(last ? " +- " : " +- ")}{view.Summarise(causes[i])}");
            Walk(view, causes[i], prefix + (last ? "    " : " |  "), depth + 1, maxDepth, seen, lines);
        }
    }

    /// <summary>Everything this event went on to cause, downstream.</summary>
    public static IReadOnlyList<string> What(WorldView view, EventId id, int maxDepth = 4)
    {
        List<string> lines = [view.Summarise(id)];
        HashSet<EventId> seen = [];
        WalkDown(view, id, "", 0, maxDepth, seen, lines);
        return lines;
    }

    private static void WalkDown(
        WorldView view, EventId id, string prefix, int depth, int maxDepth,
        HashSet<EventId> seen, List<string> lines)
    {
        if (depth >= maxDepth || !seen.Add(id)) return;

        IReadOnlyList<EventId> effects = view.Log.EffectsOf(id);
        for (int i = 0; i < effects.Count; i++)
        {
            bool last = i == effects.Count - 1;
            lines.Add($"{prefix} -> {view.Summarise(effects[i])}");
            WalkDown(view, effects[i], prefix + (last ? "    " : " |  "), depth + 1, maxDepth, seen, lines);
        }
    }

    /// <summary>
    /// The earliest event this one can be traced back to, and how many years separate them.
    /// The span is the objective form of "does this history have long causality?".
    /// </summary>
    public static (EventId Root, int Span) Roots(EventLog log, EventId id)
    {
        HashSet<EventId> seen = [];
        Queue<EventId> queue = new();
        queue.Enqueue(id);

        EventId earliest = id;
        int earliestYear = log.Get(id).Year;

        while (queue.Count > 0)
        {
            EventId current = queue.Dequeue();
            if (!seen.Add(current)) continue;

            Event e = log.Get(current);
            if (e.Year < earliestYear) { earliestYear = e.Year; earliest = current; }

            foreach (EventId cause in e.Causes) queue.Enqueue(cause);
        }

        return (earliest, log.Get(id).Year - earliestYear);
    }

    /// <summary>
    /// The longest ancestral paths in the log, oldest event first, deepest chain first. Only
    /// chains taken at their tip are returned, so one long history is not reported once per link.
    /// </summary>
    public static List<IReadOnlyList<EventId>> DeepestChains(EventLog log, int count)
    {
        List<EventId>?[] longest = new List<EventId>?[log.Count + 1];
        List<List<EventId>> tips = [];

        for (int i = 1; i <= log.Count; i++) Longest(new EventId(i));

        foreach (Event e in log.Events)
        {
            if (log.EffectsOf(e.Id).Count > 0) continue;
            List<EventId> path = longest[e.Id.Value]!;
            if (path.Count >= 2) tips.Add(path);
        }

        tips.Sort(static (a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : a[0].Value.CompareTo(b[0].Value));

        List<IReadOnlyList<EventId>> result = [];
        foreach (List<EventId> path in tips)
        {
            if (result.Count >= count) break;
            List<EventId> oldestFirst = [.. path];
            oldestFirst.Reverse();
            result.Add(oldestFirst);
        }
        return result;

        List<EventId> Longest(EventId id)
        {
            if (longest[id.Value] is { } cached) return cached;

            longest[id.Value] = [id];
            List<EventId> best = [];

            foreach (EventId cause in log.Get(id).Causes)
            {
                if (cause.Value >= id.Value) continue;
                List<EventId> candidate = Longest(cause);
                if (candidate.Count > best.Count) best = candidate;
            }

            List<EventId> path = [id, .. best];
            longest[id.Value] = path;
            return path;
        }
    }

    public static string Render(IReadOnlyList<string> lines)
    {
        StringBuilder sb = new();
        foreach (string line in lines) sb.AppendLine(line);
        return sb.ToString();
    }
}
