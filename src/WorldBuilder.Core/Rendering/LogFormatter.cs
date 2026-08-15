using System.Globalization;
using System.Text;

namespace WorldBuilder.Core.Rendering;

/// <summary>
/// The readable log: one greppable line per event, grouped by year.
///
/// Lines are rendered during a replay rather than against the finished world, so a ruler who
/// dies in year 12 is described as a ruler and not as whatever the entity became by year 50.
/// </summary>
public static class LogFormatter
{
    private const int KindWidth = 26;
    private const int ArcWidth = 5;

    public static IReadOnlyList<string> Render(
        EventLog log, ulong seed, Significance minimum = Significance.Minor, bool showCauses = true)
    {
        List<string> lines = [];
        int lastYear = int.MinValue;

        // Which events this view actually shows. A reference to an event the reader cannot see
        // is a broken reference as far as they are concerned — the causal graph in the JSONL is
        // complete, but printing "<= e:150" beside a line when e:150 was filtered out as
        // bookkeeping makes the readable log look like it has holes in it, and reads as a bug.
        HashSet<EventId> visible = [];
        foreach (Event e in log.Events)
            if (e.Significance >= minimum) visible.Add(e.Id);

        // Say what this view leaves out, at the top, where it is measured.
        //
        // The hidden rows are the yearly accounts, and they are where economy causation starts,
        // so any edge count taken from this file understates it by a wide margin. That figure
        // has been read off this file and reported as a dynamics regression three times.
        int hidden = log.Count - visible.Count;
        if (hidden > 0)
        {
            lines.Add($"# {visible.Count} of {log.Count} events. The other " +
                      $"{hidden} are bookkeeping — the yearly accounts — and are hidden here.");
            lines.Add("# They are in the .jsonl, they carry causes and effects, and much of the");
            lines.Add("# economy's influence on everything else runs through them. Counting causal");
            lines.Add("# edges in this file measures the view, not the world: use `wb audit`.");
            lines.Add(string.Empty);
        }

        Replay.Walk(log, seed, (state, e) =>
        {
            if (e.Significance < minimum) return;

            if (e.Year != lastYear)
            {
                if (lastYear != int.MinValue) lines.Add(string.Empty);
                lastYear = e.Year;
            }

            lines.Add(Line(state, e, showCauses, visible));
        });

        return lines;
    }

    public static string Line(
        WorldState state, Event e, bool showCauses = true, HashSet<EventId>? visible = null)
    {
        StringBuilder sb = new();

        sb.Append('[').Append('Y').Append(e.Year.ToString("D4", CultureInfo.InvariantCulture)).Append("] ");
        sb.Append((e.Arc.IsNone ? "" : e.Arc.ToString()).PadRight(ArcWidth)).Append(' ');
        sb.Append(EventKinds.Name(e.Kind).PadRight(KindWidth)).Append("  ");
        sb.Append(EventTemplates.Describe(state, e));

        if (e.Scope == Visibility.Secret) sb.Append("  [secret]");

        if (showCauses && e.Causes.Count > 0)
        {
            List<EventId> shown = [];
            foreach (EventId cause in e.Causes)
                if (visible is null || visible.Contains(cause)) shown.Add(cause);

            if (shown.Count > 0)
            {
                sb.Append("  <= ");
                for (int i = 0; i < shown.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(shown[i]);
                }
            }
        }

        sb.Append("  ").Append(e.Id);
        return sb.ToString();
    }
}
