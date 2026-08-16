using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>How one decision resolved, across a run or a panel.</summary>
public sealed record OutcomeSpread(string Decision, IReadOnlyDictionary<string, int> Branches)
{
    public int Total => Branches.Values.Sum();

    /// <summary>The share of the commonest branch, which is what "skew" means here.</summary>
    public int SkewPct => Total == 0 ? 0 : Branches.Values.Max() * 100 / Total;

    public string Commonest
    {
        get
        {
            string best = "";
            int most = -1;
            foreach ((string branch, int n) in Branches)
                if (n > most || (n == most && string.CompareOrdinal(branch, best) < 0)) { most = n; best = branch; }
            return best;
        }
    }

    /// <summary>Branches in descending order, ties broken by name so the report is stable.</summary>
    public IReadOnlyList<(string Branch, int Count, int Pct)> Ranked
    {
        get
        {
            List<(string, int, int)> ordered = [];
            foreach ((string branch, int n) in Branches)
                ordered.Add((branch, n, Total == 0 ? 0 : n * 100 / Total));

            ordered.Sort(static (a, b) => b.Item2 != a.Item2
                ? b.Item2.CompareTo(a.Item2)
                : string.CompareOrdinal(a.Item1, b.Item1));

            return ordered;
        }
    }

    /// <summary>
    /// True where only one branch was ever seen.
    ///
    /// Either the other branches are unreachable or they are rare enough that five worlds cannot
    /// tell the difference, and both need saying. This is the shape the coup defect had — the
    /// audit's <c>abandoned</c> case had no emitter at all, and nothing noticed for months
    /// because a switch with an unreachable arm looks exactly like one whose arm is rare.
    /// </summary>
    public bool SingleBranch => Branches.Count < 2;
}

/// <summary>
/// Every outcome-bearing decision in the log, with its distribution.
///
/// <b>A skewed outcome distribution is a simulation defect, not only a rendering risk.</b> The
/// project already had the lesson filed under rendering — a model scores well guessing the
/// majority case and gets the rare case confidently wrong — and two instances now say it is also
/// about the world. Coups were 100% exposed because no other branch existed. Raids are beaten off
/// four times in five, which is not structural but fills the log with near-identical lines.
///
/// <b>No threshold here, deliberately.</b> There is no principled bar for "too skewed" yet, and
/// inventing one to sort the output would be a constant chosen by fitting. This ranks and reports;
/// judging is a human act performed once per finding and written down.
/// </summary>
public static class OutcomeAudit
{
    /// <summary>
    /// The decisions a log records, keyed as the emit site names them.
    ///
    /// A kind can record several unrelated choices — how a seat passed is not one decision but
    /// four — so where an emit site names a <c>decision</c>, it groups by that. Pooling them
    /// averages a lopsided call into an even-looking one.
    /// </summary>
    public static Dictionary<string, Dictionary<string, int>> Distributions(EventLog log)
    {
        Dictionary<string, Dictionary<string, int>> byDecision = new(StringComparer.Ordinal);

        foreach (Event e in log.Events)
        {
            if (e.Significance < Significance.Minor) continue;

            string? branch = e.Outcome != Outcome.NotApplicable
                ? e.Outcome.ToString()
                : e.GetString("reason") ?? e.GetString("mode");

            if (branch is null) continue;

            string decision = e.GetString("decision") is { } named
                ? $"{EventKinds.Name(e.Kind)} ({named})"
                : EventKinds.Name(e.Kind);

            if (!byDecision.TryGetValue(decision, out Dictionary<string, int>? counts))
                byDecision[decision] = counts = new Dictionary<string, int>(StringComparer.Ordinal);

            counts[branch] = counts.GetValueOrDefault(branch) + 1;
        }

        return byDecision;
    }

    /// <summary>The panel's decisions pooled, most skewed first.</summary>
    public static List<OutcomeSpread> Pool(IEnumerable<EventLog> logs)
    {
        Dictionary<string, Dictionary<string, int>> pooled = new(StringComparer.Ordinal);

        foreach (EventLog log in logs)
            foreach ((string decision, Dictionary<string, int> counts) in Distributions(log))
            {
                if (!pooled.TryGetValue(decision, out Dictionary<string, int>? into))
                    pooled[decision] = into = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach ((string branch, int n) in counts)
                    into[branch] = into.GetValueOrDefault(branch) + n;
            }

        List<OutcomeSpread> spreads = [];
        foreach ((string decision, Dictionary<string, int> counts) in pooled)
            spreads.Add(new OutcomeSpread(decision, counts));

        // Most skewed first; a single-branch decision sorts to the top because it is the extreme
        // case of the thing being ranked, not a separate category.
        spreads.Sort(static (a, b) => a.SkewPct != b.SkewPct
            ? b.SkewPct.CompareTo(a.SkewPct)
            : string.CompareOrdinal(a.Decision, b.Decision));

        return spreads;
    }

    /// <summary>
    /// Raids split the three ways a reader distinguishes: beaten off, through with a haul,
    /// through and empty-handed.
    ///
    /// The engine's own <see cref="Outcome"/> field only separates the first from the other two.
    /// "Got through and took nothing" is a success in the record and a failure to the house that
    /// rode out, and it is the case corpus row 16 is about — prose describing plunder where the
    /// haul was zero. It is distinguishable without reading prose: the <c>loot</c> field says so.
    /// </summary>
    public static (int BeatenOff, int TookAHaul, int TookNothing) Raids(EventLog log)
    {
        int beaten = 0, haul = 0, empty = 0;

        foreach (Event e in log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid) continue;

            if (e.Outcome != Outcome.Succeeded) { beaten++; continue; }
            (e.GetInt("loot") > 0 ? ref haul : ref empty)++;
        }

        return (beaten, haul, empty);
    }

    /// <summary>
    /// How often a raider returns to a place that has already beaten it off.
    ///
    /// The distinction the target-selection rule does not currently draw: its cooldown counts
    /// attempts, so a place that repelled you three times and a place you looted three times are
    /// the same place to it.
    /// </summary>
    public static (int Repeats, int RepeatsAfterAFailure) RaidRepeats(EventLog log)
    {
        Dictionary<string, int> attempts = new(StringComparer.Ordinal);
        Dictionary<string, int> failures = new(StringComparer.Ordinal);
        int repeats = 0, afterFailure = 0;

        foreach (Event e in log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid) continue;

            string pair = $"{e.Faction}->{e.Where}";

            if (attempts.GetValueOrDefault(pair) > 0)
            {
                repeats++;
                if (failures.GetValueOrDefault(pair) > 0) afterFailure++;
            }

            attempts[pair] = attempts.GetValueOrDefault(pair) + 1;
            if (e.Outcome != Outcome.Succeeded) failures[pair] = failures.GetValueOrDefault(pair) + 1;
        }

        return (repeats, afterFailure);
    }

    /// <summary>Raids that nothing ever cites — a raid that happened and changed nothing.</summary>
    public static (int Total, int DeadEnds) RaidConsequences(EventLog log)
    {
        int total = 0, dead = 0;

        foreach (Event e in log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid) continue;
            total++;
            if (log.EffectsOf(e.Id).Count == 0) dead++;
        }

        return (total, dead);
    }

    public static IReadOnlyList<string> Report(IReadOnlyList<OutcomeSpread> spreads)
    {
        List<string> lines = [];

        foreach (OutcomeSpread s in spreads)
        {
            lines.Add($"  {s.Decision,-38} n={s.Total,-5} skew {s.SkewPct,3}%" +
                      (s.SingleBranch ? "   ONE BRANCH ONLY" : ""));

            foreach ((string branch, int count, int pct) in s.Ranked)
                lines.Add($"      {pct,3}%  {count,5}  {branch}");
        }

        return lines;
    }

    public static string Pct(int part, int whole) =>
        whole == 0 ? "0%" : $"{part * 100 / whole}%".PadLeft(4);

    public static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);
}
