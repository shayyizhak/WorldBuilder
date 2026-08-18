using System.Globalization;
using WorldBuilder.Core.Rendering;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// The objective half of the "is this interesting?" gate.
///
/// Reading five logs catches boring history but not the failure modes that hide behind a good
/// first twenty years: one faction quietly eating the map, a cast that never repeats, causal
/// chains that are all two events long. Those are measurable, so they are measured.
/// </summary>
public sealed record WorldStats
{
    public required int Years { get; init; }
    public required int TotalEvents { get; init; }
    public required int ReadableEvents { get; init; }

    public required int MinPerYear { get; init; }
    public required int MedianPerYear { get; init; }
    public required int MaxPerYear { get; init; }

    /// <summary>Largest share of total faction power held by one faction, by year.</summary>
    public required IReadOnlyList<(int Year, int SharePct, string Faction)> Concentration { get; init; }

    public required int RunawayYear { get; init; }

    public required int ActorsTotal { get; init; }
    public required int ActorsRecurring { get; init; }

    public required int LongestCausalSpan { get; init; }
    public required EventId LongestCausalEvent { get; init; }

    /// <summary>
    /// Whether domination is an ending or a phase. A world that consolidates once and then
    /// sits there has stopped producing history; a world that consolidates and breaks up
    /// again has a shape. These count the breakups.
    /// </summary>
    public required int LeadershipChanges { get; init; }
    public required int DistinctLeaders { get; init; }
    public required int HegemonyCollapses { get; init; }
    public required int PeakSharePct { get; init; }
    public required int FinalSharePct { get; init; }
    public required int MedianPolities { get; init; }

    public required int MedianOpenArcs { get; init; }
    public required int DistinctKinds { get; init; }
    public required IReadOnlyList<(string Kind, int Count)> KindCounts { get; init; }

    public static WorldStats Compute(WorldView view, SimConfig? config = null)
    {
        _ = config;
        EventLog log = view.Log;

        Dictionary<int, int> readablePerYear = [];
        Dictionary<EntityId, int> actorAppearances = [];
        Dictionary<EventKind, int> kindCounts = [];
        List<(int Year, int SharePct, string Faction)> concentration = [];
        List<int> openArcCounts = [];
        List<int> polities = [];

        int firstYear = view.FirstYear;
        int lastYear = view.LastYear;
        int runawayYear = 0;

        int currentYear = int.MinValue;

        WorldState final = Replay.Walk(log, view.Seed, (state, e) =>
        {
            if (e.Year != currentYear)
            {
                if (currentYear != int.MinValue) Snapshot(state, currentYear);
                currentYear = e.Year;
                readablePerYear.TryAdd(e.Year, 0);
            }

            kindCounts[e.Kind] = kindCounts.GetValueOrDefault(e.Kind) + 1;

            if (e.Significance < Significance.Minor) return;
            readablePerYear[e.Year] = readablePerYear.GetValueOrDefault(e.Year) + 1;

            foreach (Participant p in e.Participants)
                if (p.Id.Kind == EntityKind.Actor)
                    actorAppearances[p.Id] = actorAppearances.GetValueOrDefault(p.Id) + 1;
        }, view.Board);

        if (currentYear != int.MinValue) Snapshot(final, currentYear);

        List<int> perYear = [.. readablePerYear.Values];
        perYear.Sort();

        // Measured over actors who reached adulthood, not over everyone ever born. Infants who
        // died young are not the cast, and counting them only makes the denominator lie.
        int adults = 0;
        int recurring = 0;
        foreach (Actor a in final.Actors)
        {
            int lived = (a.DeathYear ?? lastYear) - a.BirthYear;
            if (lived < 16) continue;
            adults++;
            if (actorAppearances.GetValueOrDefault(a.Id) >= 3) recurring++;
        }

        (EventId longestEvent, int longestSpan) = LongestChain(log);

        List<(string, int)> kinds = [];
        foreach (KeyValuePair<EventKind, int> kv in kindCounts) kinds.Add((EventKinds.Name(kv.Key), kv.Value));
        kinds.Sort(static (a, b) => b.Item2 != a.Item2 ? b.Item2.CompareTo(a.Item2) : string.CompareOrdinal(a.Item1, b.Item1));

        openArcCounts.Sort();

        // Walk the concentration curve: count how often the top spot changed hands, and how
        // often a faction that had passed 70% was later pushed back below 55%.
        int leadershipChanges = 0;
        int hegemonyCollapses = 0;
        int peak = 0;
        bool hegemonic = false;
        string? previousLeader = null;
        HashSet<string> leaders = [];

        foreach ((int _, int share, string faction) in concentration)
        {
            if (faction != "-")
            {
                leaders.Add(faction);
                if (previousLeader is not null && faction != previousLeader) leadershipChanges++;
                previousLeader = faction;
            }

            peak = Math.Max(peak, share);
            if (share >= 70) hegemonic = true;
            else if (hegemonic && share < 55) { hegemonyCollapses++; hegemonic = false; }
        }

        List<int> polityCounts = [.. polities];
        polityCounts.Sort();

        return new WorldStats
        {
            Years = lastYear - firstYear,
            TotalEvents = log.Count,
            ReadableEvents = Sum(perYear),
            MinPerYear = perYear.Count > 0 ? perYear[0] : 0,
            MedianPerYear = Median(perYear),
            MaxPerYear = perYear.Count > 0 ? perYear[^1] : 0,
            Concentration = concentration,
            RunawayYear = runawayYear,
            ActorsTotal = adults,
            ActorsRecurring = recurring,
            LongestCausalSpan = longestSpan,
            LongestCausalEvent = longestEvent,
            LeadershipChanges = leadershipChanges,
            DistinctLeaders = leaders.Count,
            HegemonyCollapses = hegemonyCollapses,
            PeakSharePct = peak,
            FinalSharePct = concentration.Count > 0 ? concentration[^1].SharePct : 0,
            MedianPolities = Median(polityCounts),
            MedianOpenArcs = Median(openArcCounts),
            DistinctKinds = kindCounts.Count,
            KindCounts = kinds,
        };

        void Snapshot(WorldState state, int year)
        {
            // Share of the region's settled population, not of the internal "power" scalar.
            // Power folds in treasury and a leader's martial score, so a landless rump with a
            // full purse counted towards the denominator and quietly flattered the numbers.
            // Who feeds and taxes how many people is what domination actually means here.
            int total = 0;
            int best = 0;
            int standing = 0;
            string bestName = "-";

            foreach (Faction f in state.Factions)
            {
                int held = state.PopulationOf(f.Id);
                total += held;
                if (held > 0) standing++;
                if (held > best) { best = held; bestName = f.Name; }
            }

            polities.Add(standing);
            int share = total == 0 ? 0 : best * 100 / total;
            concentration.Add((year, share, bestName));

            if (runawayYear == 0 && share >= 70) runawayYear = year;

            int open = 0;
            foreach (Arc _ in state.OpenArcs()) open++;
            openArcCounts.Add(open);
        }
    }

    /// <summary>
    /// The longest gap in years between an event and the oldest thing it can be traced to.
    /// A world where this number is 3 has no memory, whatever its event count looks like.
    /// </summary>
    private static (EventId Event, int Span) LongestChain(EventLog log)
    {
        EventId best = EventId.None;
        int bestSpan = 0;

        foreach (Event e in log.Events)
        {
            if (e.Significance < Significance.Major) continue;
            (EventId _, int span) = CausalTrace.Roots(log, e.Id);
            if (span > bestSpan) { bestSpan = span; best = e.Id; }
        }

        return (best, bestSpan);
    }

    private static int Median(List<int> sorted) =>
        sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];

    private static int Sum(List<int> values)
    {
        int total = 0;
        foreach (int v in values) total += v;
        return total;
    }

    // ---- reporting --------------------------------------------------------

    public IReadOnlyList<string> Report(WorldView view)
    {
        List<string> lines =
        [
            $"years {Years}   events {TotalEvents} total, {ReadableEvents} readable",
            $"density   min {MinPerYear}/yr   median {MedianPerYear}/yr   max {MaxPerYear}/yr",
            "",
            "concentration (largest faction's share of settled population)",
        ];

        foreach ((int year, int share, string faction) in Sample(Concentration, 10))
            lines.Add($"  Y{year:D4}  {Bar(share)} {share,3}%  {faction}");

        lines.Add("");
        lines.Add($"cycles        peak {PeakSharePct}%, ended at {FinalSharePct}%   " +
                  $"{HegemonyCollapses} hegemony collapse(s)");
        lines.Add($"              top spot changed {LeadershipChanges}x between {DistinctLeaders} factions; " +
                  $"median {MedianPolities} polities standing");
        lines.Add($"cast          {ActorsRecurring} of {ActorsTotal} actors appear 3+ times " +
                  $"({Dossier.Percent(ActorsRecurring, ActorsTotal)})");
        lines.Add($"causality     longest chain spans {LongestCausalSpan} years " +
                  $"({(LongestCausalEvent.IsNone ? "-" : LongestCausalEvent.ToString())})");
        lines.Add($"plots         median {MedianOpenArcs} arcs open at once");
        lines.Add($"vocabulary    {DistinctKinds} distinct event kinds used");

        lines.Add("");
        lines.Add("gate criteria");
        lines.Add(Check("no runaway faction before Y40", RunawayYear == 0 || RunawayYear >= 40,
            RunawayYear == 0 ? "never exceeded 70%" : $"hit 70% in Y{RunawayYear}"));
        lines.Add(Check("cast recurrence >= 60%", ActorsTotal > 0 && ActorsRecurring * 100 / ActorsTotal >= 60,
            Dossier.Percent(ActorsRecurring, ActorsTotal)));
        lines.Add(Check("a grudge spanning >= 15 years", LongestCausalSpan >= 15,
            $"{LongestCausalSpan} years"));
        lines.Add(Check("density 10-40 readable events/yr", MedianPerYear is >= 10 and <= 40,
            $"median {MedianPerYear}"));
        lines.Add(Check("3+ arcs typically live", MedianOpenArcs >= 3, $"median {MedianOpenArcs}"));

        lines.Add("");
        lines.Add("event mix");
        foreach ((string kind, int count) in KindCounts)
            lines.Add($"  {count,5}  {kind}");

        _ = view;
        return lines;
    }

    private static string Check(string label, bool passed, string detail) =>
        $"  [{(passed ? "PASS" : "FAIL")}] {label,-34} {detail}";

    private static string Bar(int pct)
    {
        int filled = Math.Clamp(pct / 5, 0, 20);
        return new string('#', filled) + new string('.', 20 - filled);
    }

    private static List<T> Sample<T>(IReadOnlyList<T> source, int count)
    {
        List<T> result = [];
        if (source.Count == 0) return result;

        int step = Math.Max(1, source.Count / count);
        for (int i = 0; i < source.Count; i += step) result.Add(source[i]);
        if (result.Count > 0 && !ReferenceEquals(result[^1], source[^1])) result.Add(source[^1]);
        return result;
    }

    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
