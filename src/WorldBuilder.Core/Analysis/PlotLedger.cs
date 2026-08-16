using System.Globalization;
using System.Text;

namespace WorldBuilder.Core.Analysis;

/// <summary>Where a plot stood the last time the resolver had anything to say about it.</summary>
/// <param name="Arc">The conspiracy.</param>
/// <param name="Opened">The year it was plotted.</param>
/// <param name="Examined">How many ticks the resolver actually looked at it.</param>
/// <param name="LastYear">The last year it was looked at, or the year it was closed unexamined.</param>
/// <param name="Reason">The gate it fell out of, or why it was never examined.</param>
/// <param name="Terminal">Whether that reason ended it rather than merely deferred it.</param>
public sealed record PlotStanding(
    EntityId Arc, int Opened, int Examined, int LastYear, string Reason, bool Terminal);

/// <summary>
/// Diagnostic accounting for conspiracies: what the resolver did with each one, and why.
///
/// <b>This is not world history and never enters the event log.</b> Events record what happened
/// in the world; this records what happened in the engine, and the two must not be confused —
/// a log that carried its own instrumentation would make the world's record depend on how it was
/// being watched.
///
/// The distinction it exists for is <b>examined versus not examined</b>. A plot the resolver
/// never looked at and a plot it looked at and declined to advance are presently indistinguishable
/// from outside, which is the same conflation as <c>unresolvable</c> in the checker and the single
/// empty-result sentence in the query layer: not-checked and not-true reported identically. Third
/// venue, same defect class.
///
/// Purely observational. Nothing here is read by any rule, so attaching it cannot change what the
/// simulation does — which is the only property that makes it safe to attach at all.
/// </summary>
public sealed class PlotLedger
{
    private readonly Dictionary<EntityId, PlotStanding> _plots = [];

    /// <summary>Every plot the ledger has heard of, oldest first.</summary>
    public IReadOnlyList<PlotStanding> Plots
    {
        get
        {
            List<PlotStanding> ordered = [.. _plots.Values];
            ordered.Sort(static (a, b) => a.Opened != b.Opened
                ? a.Opened.CompareTo(b.Opened)
                : a.Arc.CompareTo(b.Arc));
            return ordered;
        }
    }

    /// <summary>A plot was opened. Recorded before anything has had a chance to look at it.</summary>
    public void Opened(EntityId arc, int year) =>
        _plots[arc] = new PlotStanding(arc, year, 0, year, "never examined", Terminal: false);

    /// <summary>
    /// The resolver reached this plot and stopped at a gate.
    ///
    /// <paramref name="terminal"/> separates "this ended it" from "not this year". Both are
    /// reasons; only one is an ending, and a ledger that blurred them would answer the question
    /// it was built to ask with the same word for both.
    /// </summary>
    public void Examined(EntityId arc, int year, string gate, bool terminal = false)
    {
        if (!_plots.TryGetValue(arc, out PlotStanding? standing)) return;

        _plots[arc] = standing with
        {
            Examined = standing.Examined + 1,
            LastYear = year,
            Reason = gate,
            Terminal = terminal,
        };
    }

    /// <summary>
    /// The resolver did not reach this plot, and this is why.
    ///
    /// The bucket the whole exercise is about. An unexamined plot with no recorded reason is an
    /// accounting failure, not a row to skip — the same rule as a dangling causal edge.
    /// </summary>
    public void NotExamined(EntityId arc, int year, string why)
    {
        if (!_plots.TryGetValue(arc, out PlotStanding? standing)) return;
        if (standing.Terminal) return;

        _plots[arc] = standing with { LastYear = year, Reason = why };
    }

    /// <summary>The counts, in the shape the coverage block already uses.</summary>
    public PlotAccounting Account(EventLog log)
    {
        HashSet<EntityId> resolved = [];

        foreach (Event e in log.Events)
            if (e.Kind == EventKind.PolityCoupResolved && !e.Arc.IsNone) resolved.Add(e.Arc);

        int examined = 0, withReason = 0, unexamined = 0;
        Dictionary<string, int> reasons = new(StringComparer.Ordinal);

        foreach (PlotStanding p in Plots)
        {
            if (p.Examined > 0) examined++;

            if (resolved.Contains(p.Arc)) continue;

            if (p.Examined == 0) unexamined++;
            else withReason++;

            reasons[p.Reason] = reasons.GetValueOrDefault(p.Reason) + 1;
        }

        return new PlotAccounting(_plots.Count, examined, resolved.Count, withReason, unexamined, reasons);
    }
}

/// <summary>
/// The per-run block. <c>Plotted == Resolved + UnresolvedWithReason + Unexamined</c>, asserted
/// rather than hoped for: a plot that falls out of all three buckets is exactly the thing this
/// was built to find.
/// </summary>
public sealed record PlotAccounting(
    int Plotted,
    int Examined,
    int Resolved,
    int UnresolvedWithReason,
    int Unexamined,
    IReadOnlyDictionary<string, int> Reasons)
{
    public bool Balances => Plotted == Resolved + UnresolvedWithReason + Unexamined;

    public IReadOnlyList<(string Reason, int Count)> Ranked
    {
        get
        {
            List<(string, int)> ordered = [.. Reasons.Select(r => (r.Key, r.Value))];
            ordered.Sort(static (a, b) => b.Item2 != a.Item2
                ? b.Item2.CompareTo(a.Item2)
                : string.CompareOrdinal(a.Item1, b.Item1));
            return ordered;
        }
    }

    public IReadOnlyList<string> Report()
    {
        List<string> lines =
        [
            $"  plotted {Plotted}   examined {Examined}   resolved {Resolved}   " +
            $"unresolved-with-reason {UnresolvedWithReason}   unexamined {Unexamined}" +
            (Balances ? "" : "   ACCOUNTING FAILURE"),
        ];

        foreach ((string reason, int count) in Ranked)
            lines.Add($"      {count.ToString(CultureInfo.InvariantCulture),4}  {reason}");

        return lines;
    }

    public string ToText()
    {
        StringBuilder sb = new();
        foreach (string line in Report()) sb.Append(line).Append('\n');
        return sb.ToString();
    }
}
