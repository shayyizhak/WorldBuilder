using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>One relation kind's population over the life of a world.</summary>
/// <param name="Live">
/// Live ties at the end of each year, indexed from <paramref name="FirstYear"/>. Counted as
/// unordered pairs: a symmetric edge written in both directions is one tie, because two houses
/// trading is one arrangement and counting the directions makes every figure here double.
/// </param>
public sealed record KindTrajectory(RelationKind Kind, int FirstYear, IReadOnlyList<int> Live)
{
    public int Peak
    {
        get
        {
            int peak = 0;
            foreach (int n in Live) peak = Math.Max(peak, n);
            return peak;
        }
    }

    public int Final => Live.Count == 0 ? 0 : Live[^1];

    /// <summary>Ties ever made, whether or not they survived. The denominator peak alone cannot give.</summary>
    public int EverCreated { get; init; }

    public int Ended { get; init; }

    /// <summary>
    /// Whether this kind only ever went up.
    ///
    /// <b>Peak equalling final is the weaker statement and is not what is asserted here.</b> A kind
    /// that gained two ties and lost two in the same year has peak equal to final and is not
    /// monotonic. This reads the terminations themselves.
    /// </summary>
    public bool Monotonic => Ended == 0;
}

/// <summary>Where one tie ended, and what the record said ended it.</summary>
/// <param name="Cause">
/// The <c>endCause</c> the emitting event carried, or <see cref="Unnamed"/> where it carried none.
/// A tie that vanishes inside an event which does not say a tie ended is the defect this phase
/// repairs, so it is reported under its own label rather than skipped or given the event's kind.
/// </param>
public sealed record Termination(int Year, RelationKind Kind, EntityId From, EntityId To,
    EventId At, EventKind Via, string Cause)
{
    public const string Unnamed = "(not named by its event)";
}

/// <summary>
/// The relation graph's population over time, folded from the record.
///
/// <b>From the log, never from a re-run.</b> That is what lets this read a sealed baseline of an
/// older ruleset and produce the "before" column without the engine that wrote it still existing.
///
/// <b>Through <see cref="EventReducer"/>, never through a second reading of the payload.</b> The
/// first version of this walked <c>rel:</c> and <c>relDel:</c> keys itself, which looked like the
/// same thing and was not: <c>AtWar</c> is applied by <c>ApplyWarDeclared</c> and removed by
/// <c>ApplyPeace</c>, in code, keyed on the event kind and carried by no payload verb at all — so
/// the whole of war and peace was invisible to it and the kind simply did not appear in the table.
/// A measurement that reimplements the fold measures the reimplementation. This one replays the
/// record through the engine's own reducer and diffs the graph it produces.
///
/// <b>Directed edges are the state; unordered pairs are the unit reported.</b> The state has to be
/// directed or it is not the graph: <see cref="EventDraft.RelBoth"/> writes two keys and the
/// reducer makes two edges. What a reader counts is arrangements, not directions — two houses
/// trading is one tie — so the reported figure collapses the ends, and so does a termination: one
/// severance, however many directed keys the event needed to carry it.
/// </summary>
public static class RelationTrajectory
{
    /// <summary>The payload field a rule writes to say what ended a tie.</summary>
    public const string CauseField = "endCause";

    private readonly record struct Tie(EntityId Low, EntityId High, RelationKind Kind)
    {
        public static Tie Of(EntityId a, EntityId b, RelationKind kind) =>
            a.CompareTo(b) <= 0 ? new Tie(a, b, kind) : new Tie(b, a, kind);
    }

    public sealed record Report
    {
        public required int FirstYear { get; init; }
        public required int LastYear { get; init; }
        public required IReadOnlyList<KindTrajectory> Kinds { get; init; }

        /// <summary>
        /// Pairs of houses that both still hold ground, per year — the ties that *could* exist.
        ///
        /// <b>The denominator the first version of this file did not have, and the reason its
        /// verdict was wrong.</b> Four of the five panel worlds finish with one or two houses
        /// standing, so the number of trade ties available at the end is one or zero. A guard
        /// reading final against peak calls that "the rule empties the graph" when what emptied
        /// it is hegemony: the world ran out of houses to trade between. Worse, the same figure
        /// on the previous ruleset showed three live ties in a world with one house left — ties
        /// between houses that no longer existed, which is the defect this phase exists to
        /// repair, scoring as healthy.
        ///
        /// Fullness is relative to what is possible. A graph is uninformative when it is all of
        /// the pairs there are or none of them, and neither statement can be made without this.
        /// </summary>
        public required IReadOnlyList<int> AvailablePairs { get; init; }

        public int FinalAvailablePairs => AvailablePairs.Count == 0 ? 0 : AvailablePairs[^1];

        public int PeakAvailablePairs
        {
            get
            {
                int peak = 0;
                foreach (int n in AvailablePairs) peak = Math.Max(peak, n);
                return peak;
            }
        }

        /// <summary>Every severance, in log order.</summary>
        public required IReadOnlyList<Termination> Terminations { get; init; }

        public KindTrajectory? Of(RelationKind kind)
        {
            foreach (KindTrajectory k in Kinds)
                if (k.Kind == kind) return k;
            return null;
        }

        /// <summary>The first year in which any tie ended, or null where none ever did.</summary>
        public int? FirstTerminationYear => Terminations.Count == 0 ? null : Terminations[0].Year;

        /// <summary>The event at which the first tie ended, or none.</summary>
        public EventId FirstTerminationAt =>
            Terminations.Count == 0 ? EventId.None : Terminations[0].At;

        /// <summary>Terminations of one kind by the cause their event named, commonest first.</summary>
        public List<(string Cause, int Count)> Causes(RelationKind kind)
        {
            Dictionary<string, int> tally = new(StringComparer.Ordinal);
            foreach (Termination t in Terminations)
                if (t.Kind == kind) tally[t.Cause] = tally.GetValueOrDefault(t.Cause) + 1;

            List<(string, int)> rows = [.. tally.Select(static kv => (kv.Key, kv.Value))];
            rows.Sort(static (a, b) => a.Item2 != b.Item2
                ? b.Item2.CompareTo(a.Item2)
                : string.CompareOrdinal(a.Item1, b.Item1));
            return rows;
        }
    }

    public static Report Of(EventLog log, ulong seed = 0, Geography.Board? board = null)
    {
        int firstYear = log.Events.Count == 0 ? 0 : log.Events[0].Year;
        int lastYear = log.Events.Count == 0 ? 0 : log.Events[^1].Year;

        HashSet<Tie> live = [];
        Dictionary<RelationKind, int> created = [];
        Dictionary<RelationKind, int> ended = [];
        Dictionary<RelationKind, List<int>> perYear = [];
        List<Termination> terminations = [];
        List<int> availablePairs = [];
        int standing = 0;

        foreach (RelationKind kind in Enum.GetValues<RelationKind>()) perYear[kind] = [];

        int year = firstYear;

        // The board is passed through rather than looked up. Nothing in the fold's effect on the
        // relation graph consults geography, but the replay still refuses a world whose log names
        // a board it was not handed — and a measurement panel makes one board per seed and never
        // stores it, so "look up the repository's" is the wrong answer there and fails loudly.
        Rendering.Replay.Walk(log, seed, (state, e) =>
        {
            // Close out the years the log skipped as well as the ones it filled, so the series is
            // indexable by year rather than by "years that happened to carry an event".
            while (year < e.Year) { Record(); year++; }

            // Diffed at the tie rather than at the directed edge, so every figure here is in one
            // unit. Counting makings on edges and live ties on pairs put "80 grievance ties made"
            // above a peak of 68 — the twelve extra were second directions of grudges already
            // counted, which is a real thing to know and not a thing to put in this column.
            HashSet<Tie> now = [];
            foreach (Relation r in state.Relations.All)
                now.Add(Tie.Of(r.Key.From, r.Key.To, r.Key.Kind));

            foreach (Tie tie in now)
                if (!live.Contains(tie)) created[tie.Kind] = created.GetValueOrDefault(tie.Kind) + 1;

            foreach (Tie tie in live)
            {
                if (now.Contains(tie)) continue;

                ended[tie.Kind] = ended.GetValueOrDefault(tie.Kind) + 1;
                terminations.Add(new Termination(e.Year, tie.Kind, tie.Low, tie.High, e.Id, e.Kind,
                    e.GetString(CauseField) ?? Termination.Unnamed));
            }

            live = now;

            // Read here rather than in Record, which runs on year boundaries where no state is
            // in hand. A house that holds no ground is defunct by the engine's own definition and
            // is not somebody anyone can strike a bargain with.
            standing = 0;
            foreach (Faction f in state.Factions)
                if (!state.IsDefunct(f.Id)) standing++;
        }, board);

        while (year <= lastYear) { Record(); year++; }

        List<KindTrajectory> kinds = [];
        foreach (RelationKind kind in Enum.GetValues<RelationKind>())
        {
            if (created.GetValueOrDefault(kind) == 0) continue;
            kinds.Add(new KindTrajectory(kind, firstYear, perYear[kind])
            {
                EverCreated = created[kind],
                Ended = ended.GetValueOrDefault(kind),
            });
        }

        return new Report
        {
            FirstYear = firstYear,
            LastYear = lastYear,
            Kinds = kinds,
            Terminations = terminations,
            AvailablePairs = availablePairs,
        };

        void Record()
        {
            Dictionary<RelationKind, int> counts = [];
            foreach (Tie t in live) counts[t.Kind] = counts.GetValueOrDefault(t.Kind) + 1;

            foreach (RelationKind kind in Enum.GetValues<RelationKind>())
                perYear[kind].Add(counts.GetValueOrDefault(kind));

            availablePairs.Add(standing * (standing - 1) / 2);
        }
    }

    /// <summary>
    /// The step-two brief's §4 degeneracy guard, stated as a verdict rather than left to whoever
    /// reads the table.
    ///
    /// <b>Both ends fail.</b> A fully connected trade graph carries no information and neither does
    /// an empty one, so the rule is degenerate if final ties land near zero *or* near peak. The
    /// bands are a tenth of peak and nine tenths of it, and they are deliberately wide: this
    /// guards against a rule that does nothing or kills everything. It is not a target, and a
    /// constant moved to satisfy it would be a constant chosen by fitting.
    /// </summary>
    /// <summary>
    /// How full the graph was, against how full it could have been.
    /// </summary>
    /// <param name="Years">Years in which at least one pair of standing houses existed.</param>
    /// <param name="MeanPct">Mean of live-ties-over-available-pairs across those years, in percent.</param>
    /// <param name="ImpossibleYears">
    /// Years holding more ties than there were pairs of houses to hold them.
    ///
    /// Not a rounding artefact and not a definition quibble: a tie whose other end has collapsed
    /// is a live edge to a house that does not exist. This is the monotonic defect counted rather
    /// than asserted, and on the previous ruleset it is non-zero.
    /// </param>
    public sealed record Density(int Years, int MeanPct, int ImpossibleYears)
    {
        public override string ToString() =>
            $"mean {MeanPct.ToString(CultureInfo.InvariantCulture)}% of available pairs over " +
            $"{Years.ToString(CultureInfo.InvariantCulture)} year(s) with any pair" +
            (ImpossibleYears == 0
                ? ""
                : $"; {ImpossibleYears.ToString(CultureInfo.InvariantCulture)} year(s) held more " +
                  "ties than there were houses to hold them");
    }

    public static Density DensityOf(KindTrajectory k, IReadOnlyList<int> availablePairs)
    {
        int years = 0, impossible = 0, total = 0;

        for (int i = 0; i < k.Live.Count && i < availablePairs.Count; i++)
        {
            if (availablePairs[i] <= 0) continue;

            years++;
            total += k.Live[i] * 100 / availablePairs[i];
            if (k.Live[i] > availablePairs[i]) impossible++;
        }

        return new Density(years, years == 0 ? 0 : total / years, impossible);
    }

    public static string Degeneracy(KindTrajectory k) => Degeneracy(k.Peak, k.Final, k.Ended, $"`{k.Kind}`");

    /// <summary>
    /// The same guard over pooled figures, which is where it is asserted.
    ///
    /// <b>Per seed the bands cannot mean what they say.</b> A world with three live trade ties at
    /// its peak has a tenth-of-peak floor of 1 and a nine-tenths ceiling of 2, so the whole
    /// non-degenerate range is two values and a single tie decides the verdict. That is the
    /// granularity problem this project already has doctrine for — a rate needs enough
    /// observations to be a rate — and the doctrine's answer is the one applied here: reported per
    /// seed, asserted on the pooled panel.
    /// </summary>
    public static string Degeneracy(int peak, int final, int ended, string what)
    {
        if (peak == 0) return "no ties ever";
        if (ended == 0) return $"DEGENERATE — nothing ever ends; {what} is still monotonic";

        int floor = Math.Max(1, peak / 10);
        int ceiling = peak * 9 / 10;

        if (final < floor)
            return $"DEGENERATE — final {final} is under a tenth of peak {peak}; the rule empties the graph";
        if (final > ceiling)
            return $"DEGENERATE — final {final} is over nine tenths of peak {peak}; the rule barely fires";

        return $"ok — peak {peak}, final {final}";
    }

    public static IReadOnlyList<string> Render(Report report, ulong seed)
    {
        List<string> lines =
        [
            $"seed {seed.ToString(CultureInfo.InvariantCulture)}, years " +
            $"{report.FirstYear.ToString(CultureInfo.InvariantCulture)}–" +
            $"{report.LastYear.ToString(CultureInfo.InvariantCulture)}, " +
            $"{report.Terminations.Count} termination(s)",
            "",
            "| kind | ties made | ended | peak live | final live | monotonic |",
            "|---|---|---|---|---|---|",
        ];

        foreach (KindTrajectory k in report.Kinds)
        {
            lines.Add($"| `{k.Kind}` | {k.EverCreated} | {k.Ended} | {k.Peak} | {k.Final} | " +
                      $"{(k.Monotonic ? "**yes**" : "no")} |");
        }

        return lines;
    }
}
