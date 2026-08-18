using System.Globalization;
using System.Text;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Where two logs of the same seed stop being the same log, against where the change that was
/// meant to separate them first acted.
///
/// <b>What this replaces.</b> Step one could assert that its ruleset was additive — every event of
/// the sealed baseline still present, unchanged, insertions only. Step two changes mechanics, so
/// that property is gone and something has to stand in its place, or a ruleset bump becomes a
/// licence for anything at all to have moved. The standing-in property:
///
/// <blockquote>
/// The new log is identical to the old one up to the first termination, and divergent after.
/// </blockquote>
///
/// Divergence *before* the first termination means something other than the intended change moved
/// the world, and that is worth halting on: it is the difference between "these worlds differ
/// because trade ties now end" and "these worlds differ, and nobody knows why".
///
/// <b>The first termination is found mechanically, not judged.</b> It is the first event carrying
/// <see cref="RelationTrajectory.CauseField"/>, a payload key no event of the previous ruleset
/// writes. No reading of which severance was the interesting one is involved.
///
/// <b>"Divergent after" is the weak half and is reported as such.</b> Once one event is inserted
/// every later id shifts, so every later event's causal edges differ by renumbering alone. The
/// second figure below separates that from real movement: the first event whose *content* differs
/// once ids are set aside.
/// </summary>
public static class Divergence
{
    public sealed record Report
    {
        public required ulong Seed { get; init; }
        public required int OldCount { get; init; }
        public required int NewCount { get; init; }

        /// <summary>Index of the first event that differs at all, or −1 where the logs match.</summary>
        public required int FirstDifference { get; init; }

        /// <summary>Index of the first event that differs in content rather than in ids, or −1.</summary>
        public required int FirstContentDifference { get; init; }

        /// <summary>Index of the first event carrying an <c>endCause</c>, or −1 where none does.</summary>
        public required int FirstTermination { get; init; }

        public required int FirstDifferenceYear { get; init; }
        public required int FirstTerminationYear { get; init; }

        /// <summary>
        /// Whether nothing moved before the change did.
        ///
        /// A log with no termination at all cannot satisfy this and does not silently pass: a seed
        /// on which the mechanic never fires is a finding, not a clean run.
        /// </summary>
        public bool Holds => FirstTermination >= 0
                             && (FirstDifference < 0 || FirstDifference >= FirstTermination);

        public string Verdict => FirstTermination < 0
            ? "NO TERMINATION — the mechanic never fired on this seed; nothing to anchor divergence to"
            : Holds
                ? $"holds — first difference at {Describe(FirstDifference, FirstDifferenceYear)}, " +
                  $"first termination at index {FirstTermination.ToString(CultureInfo.InvariantCulture)} " +
                  $"(year {FirstTerminationYear.ToString(CultureInfo.InvariantCulture)})"
                : $"HALT — the logs diverge at {Describe(FirstDifference, FirstDifferenceYear)}, " +
                  $"before the first termination at index " +
                  $"{FirstTermination.ToString(CultureInfo.InvariantCulture)} " +
                  $"(year {FirstTerminationYear.ToString(CultureInfo.InvariantCulture)}). " +
                  "Something other than this change moved the world";

        private static string Describe(int index, int year) => index < 0
            ? "nowhere"
            : $"index {index.ToString(CultureInfo.InvariantCulture)} " +
              $"(year {year.ToString(CultureInfo.InvariantCulture)})";
    }

    /// <summary>
    /// Everything about an event except where it sits in the log — the same shape
    /// <c>AdditiveRecordTests</c> compares on, and for the same reason: <see cref="Event.Id"/> and
    /// <see cref="Event.Key"/> are both artefacts of position, so an insertion moves them without
    /// anything about the world having changed.
    /// </summary>
    public static string Content(Event e)
    {
        StringBuilder sb = new();
        sb.Append(e.Year).Append('|').Append(EventKinds.Name(e.Kind)).Append('|')
          .Append(e.Significance).Append('|').Append(e.Scope).Append('|')
          .Append(e.Outcome).Append('|').Append(e.Origin).Append('|').Append(e.Arc).Append('|');

        foreach (Participant p in e.Participants) sb.Append(p.Role).Append(':').Append(p.Id).Append(',');
        sb.Append('|');
        foreach (EntityId w in e.Witnesses) sb.Append(w).Append(',');
        sb.Append('|');
        foreach (KeyValuePair<string, string> kv in e.Data) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(',');

        return sb.ToString();
    }

    private static string WithCauses(Event e)
    {
        StringBuilder sb = new(Content(e));
        sb.Append('|');
        foreach (EventId c in e.Causes) sb.Append(c).Append(',');
        return sb.ToString();
    }

    public static Report Between(EventLog older, EventLog newer, ulong seed)
    {
        int firstDifference = -1, firstContent = -1, firstTermination = -1;

        int shared = Math.Min(older.Count, newer.Count);
        for (int i = 0; i < shared; i++)
        {
            if (firstDifference < 0 && WithCauses(older.Events[i]) != WithCauses(newer.Events[i]))
                firstDifference = i;
            if (firstContent < 0 && Content(older.Events[i]) != Content(newer.Events[i]))
                firstContent = i;
            if (firstDifference >= 0 && firstContent >= 0) break;
        }

        // Logs of different length with a matching prefix diverge at the end of the shorter one.
        if (firstDifference < 0 && older.Count != newer.Count) firstDifference = shared;
        if (firstContent < 0 && older.Count != newer.Count) firstContent = shared;

        for (int i = 0; i < newer.Count; i++)
        {
            if (newer.Events[i].GetString(RelationTrajectory.CauseField) is null) continue;
            firstTermination = i;
            break;
        }

        return new Report
        {
            Seed = seed,
            OldCount = older.Count,
            NewCount = newer.Count,
            FirstDifference = firstDifference,
            FirstContentDifference = firstContent,
            FirstTermination = firstTermination,
            FirstDifferenceYear = YearAt(newer, firstDifference),
            FirstTerminationYear = YearAt(newer, firstTermination),
        };

        static int YearAt(EventLog log, int index) =>
            index >= 0 && index < log.Count ? log.Events[index].Year : 0;
    }
}
