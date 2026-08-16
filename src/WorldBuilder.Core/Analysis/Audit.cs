using System.Globalization;
using System.Text;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Measures the specific pathologies that make a symbolic history unreadable: events that
/// repeat verbatim, mechanics that fire without changing anything, and causal depth that turns
/// out to be one actor's lifecycle rather than genuine cross-domain consequence.
///
/// Separate from <see cref="WorldStats"/> on purpose. Stats answers "what shape is this world";
/// this answers "is the log honest and worth reading", and it needs to stay comparable run to
/// run so a fix can be shown to have worked rather than asserted to have worked.
/// </summary>
public sealed record Audit
{
    public required int ReadableEvents { get; init; }
    public required int RepeatedEvents { get; init; }
    public required IReadOnlyList<(string Text, int Count)> WorstRepeats { get; init; }

    public required int MaxCollapsesPerFaction { get; init; }
    public required int TotalCollapses { get; init; }

    public required int Famines { get; init; }
    public required int FamineEnds { get; init; }

    public required int DeepChains { get; init; }
    public required int DistinctChainShapes { get; init; }
    public required int LifecycleChains { get; init; }
    public required IReadOnlyList<(string Shape, int Count)> CommonShapes { get; init; }

    public required int CoupsResolved { get; init; }
    public required int CoupsWon { get; init; }
    public required int CoupsLost { get; init; }
    public required int CoupsExposed { get; init; }

    public required int Assassinations { get; init; }
    public required int AssassinationsFatal { get; init; }

    /// <summary>Every conspiracy must end in exactly one recorded way.</summary>
    public required int PlotsOpened { get; init; }
    public required int PlotsTerminated { get; init; }
    public required int Challenges { get; init; }

    public required int Plagues { get; init; }
    public required int PlagueEnds { get; init; }
    public required int MedianFamineDeaths { get; init; }
    public required int MedianPlagueDeaths { get; init; }

    /// <summary>
    /// Deaths as a share of the stricken settlement, in tenths of a percent. Absolute death
    /// counts compare the disasters *and* the places they happen to strike; this compares only
    /// how hard each one hits, which is the thing the two mechanics actually control.
    /// </summary>
    public required int FamineSeverityPerMille { get; init; }
    public required int PlagueSeverityPerMille { get; init; }

    public int SeverityRatioPct =>
        FamineSeverityPerMille == 0 ? 0 : PlagueSeverityPerMille * 100 / FamineSeverityPerMille;

    /// <summary>
    /// Termination over conspiracies that had time to conclude.
    ///
    /// <b>Redefined 2026-08-16, ruleset 2. Predecessor: <c>PlotsTerminated / PlotsOpened</c>,
    /// asserted at ≥ 85%.</b>
    ///
    /// The old definition was written when a plot was voided by its target's death, which made a
    /// high termination rate close to automatic — nearly every conspiracy was closed within one
    /// to three years of opening, and what the metric actually policed was that the engine does
    /// not silently abandon them. Under seat-attachment a plot persists until it is struck,
    /// uncovered, its author dies, or its lifespan runs out, so a plot opened at Y49 in a run
    /// that stops at Y51 is an unfinished conspiracy rather than an abandoned one. Counting it
    /// as a failure measures where the observer stopped watching.
    ///
    /// So the denominator is plots that had a full <see cref="SimConfig.PlotLifespan"/> in which
    /// to end. <b>This is not invented for the occasion:</b> the engine's own
    /// <c>AssertEveryPlotTerminated</c> has always applied the same exemption, for the reason its
    /// comment gives — inventing an ending for a plot because the observer looked away is not
    /// truthful. The metric simply had not caught up.
    ///
    /// The threshold is unchanged at 85%.
    /// </summary>
    public int PlotTerminationPct => PlotsMatured == 0 ? 100 : PlotsMaturedTerminated * 100 / PlotsMatured;

    /// <summary>The old figure, kept so the redefinition can be seen rather than taken on trust.</summary>
    public int PlotTerminationPctOfAllOpened =>
        PlotsOpened == 0 ? 100 : PlotsTerminated * 100 / PlotsOpened;

    /// <summary>Plots opened early enough to have had their full lifespan before the run stopped.</summary>
    public required int PlotsMatured { get; init; }
    public required int PlotsMaturedTerminated { get; init; }

    /// <summary>
    /// Raids that were not beaten off, whatever they carried away.
    ///
    /// The numerator behind the raid-spread invariant, named so the reachability guard can assert
    /// that a raid getting through is a thing this engine can do at all.
    /// </summary>
    public required int RaidsThatGotThrough { get; init; }

    /// <summary>How much deadlier a typical plague is than a typical famine, as a percentage.</summary>
    public int PlagueToFaminePct =>
        MedianFamineDeaths == 0 ? 0 : MedianPlagueDeaths * 100 / MedianFamineDeaths;

    public required int EventsWithCauses { get; init; }
    public required int CauseEdges { get; init; }

    /// <summary>
    /// Causal edges whose two ends sit in different families — LIFE to POLITY, ECONOMY to
    /// CONFLICT. A world where every consequence stays inside its own mechanic is several
    /// simulations running side by side rather than one.
    /// </summary>
    public required int CrossDomainEdges { get; init; }

    /// <summary>
    /// Edges running from an ECONOMY event to something that is not one. Economy participation
    /// was a v0 acceptance criterion and it is the coupling most easily lost, because the
    /// harvest ticks along changing numbers whether or not anything ever reads them.
    /// </summary>
    public required int EconomyDrivenEdges { get; init; }

    /// <summary>Every edge that starts at an ECONOMY event, whatever it points at.</summary>
    public required int EconomyEdges { get; init; }

    /// <summary>
    /// Event kinds whose outcome is lopsided, worst first, as "KIND outcome N of M (P%)".
    ///
    /// A latent fabrication list. Where one outcome dominates, a renderer can be right most of
    /// the time by ignoring the clause and writing the common case — which is exactly how a
    /// rare upheld claim came out as a set-aside one, and how a lost challenge came out as a
    /// won one before that. These are the labels that most need to name their outcome
    /// explicitly rather than distinguish it by a trailing word.
    /// </summary>
    public required IReadOnlyList<string> SkewedOutcomes { get; init; }

    /// <summary>
    /// The same counts restricted to edges both of whose ends appear in the readable
    /// <c>.log</c> — that is, with the Bookkeeping rows filtered out.
    ///
    /// Reported because the gap between the two is large and has now been read twice as a
    /// dynamics regression. Economy causation begins in the yearly accounts, which are
    /// Bookkeeping by design and therefore invisible in the view most likely to be measured:
    /// the same run shows 142 economy-driven edges in the record and 18 in the log.
    /// </summary>
    public required int VisibleEdges { get; init; }
    public required int VisibleEconomyDrivenEdges { get; init; }

    /// <summary>
    /// Events nothing ever cites. A high share means the log is full of things that happen and
    /// then change nothing — the failure mode that survives every repetition fix, because an
    /// event with no consequence has no reason not to happen again next year.
    /// </summary>
    public required int DeadEndEvents { get; init; }
    public required int TotalEvents { get; init; }
    public required IReadOnlyList<(string Kind, int Count)> DeadEndsByKind { get; init; }

    /// <summary>
    /// Events belonging to a kind that *never once* causes anything, anywhere in the run. This
    /// is the stricter and more useful reading: an individual battle that happened to lead
    /// nowhere is ordinary, but a mechanic that has never in fifty years produced a consequence
    /// is decoration.
    /// </summary>
    public required int InertKindEvents { get; init; }
    public required IReadOnlyList<string> InertKinds { get; init; }

    public int DeadEndPct => TotalEvents == 0 ? 0 : DeadEndEvents * 100 / TotalEvents;
    public int InertPct => TotalEvents == 0 ? 0 : InertKindEvents * 100 / TotalEvents;

    public int RepeatRatePct => ReadableEvents == 0 ? 0 : RepeatedEvents * 100 / ReadableEvents;
    /// <summary>
    /// <b>The invariant's measure: wins over every conspiracy that was ever opened.</b>
    ///
    /// The denominator is the load-bearing choice, and it was ambiguous for months. Over decided
    /// plots the panel has 21; over plotted it has 130, and a fix could reach 15% of decided
    /// while covert seizure remained irrelevant to the world. The v0 intent was a world in which
    /// power can be taken covertly; a plot that lapses is a plot that did not take power, and
    /// excluding it measures the resolver's own bookkeeping rather than the world's dynamics.
    /// </summary>
    public int CoupSuccessPctOfPlotted => PlotsOpened == 0 ? 0 : CoupsWon * 100 / PlotsOpened;

    /// <summary>
    /// Diagnostic sub-population: wins over conspiracies that reached any recorded resolution.
    /// Not the invariant. Reported beside the real figure so the two can be compared, never
    /// instead of it.
    /// </summary>
    public int CoupSuccessPctOfResolved => CoupsResolved == 0 ? 0 : CoupsWon * 100 / CoupsResolved;

    /// <summary>
    /// Diagnostic sub-population: wins among coups that reached a decision rather than lapsing.
    ///
    /// <b>Not the invariant, and the reason the invariant needed one stated.</b> This is the
    /// figure that reported a plausible number for months while being structurally incapable of
    /// any other value, because <c>CoupsWon</c> had no reachable emitter. Kept because the
    /// comparison against the plotted figure says how much of the population never gets a
    /// verdict at all — which is a real thing to know, as long as nobody mistakes it for the
    /// success rate.
    /// </summary>
    public int CoupDecidedPct
    {
        get
        {
            int decided = CoupsWon + CoupsLost + CoupsExposed;
            return decided == 0 ? 0 : CoupsWon * 100 / decided;
        }
    }
    public int AssassinationFatalPct => Assassinations == 0 ? 0 : AssassinationsFatal * 100 / Assassinations;
    public int LifecycleChainPct => DeepChains == 0 ? 0 : LifecycleChains * 100 / DeepChains;

    public static Audit Compute(
        WorldView view, int minChainDepth = 5, int skewThreshold = 70, int plotLifespan = 0)
    {
        EventLog log = view.Log;

        Dictionary<string, int> seen = [];
        Dictionary<EntityId, int> collapses = [];
        int repeats = 0, readable = 0;
        int famines = 0, famineEnds = 0;
        int coups = 0, won = 0, lost = 0, exposed = 0;
        int assassinations = 0, fatal = 0;
        int withCauses = 0, edges = 0;
        int crossDomain = 0, economyEdges = 0, economyDriven = 0;
        int visibleEdges = 0, visibleEconomyDriven = 0;
        int challenges = 0, plagues = 0, plagueEnds = 0;

        HashSet<EventId> plotsOpened = [];
        HashSet<EventId> plotsTerminated = [];
        List<int> famineDeaths = [];
        List<int> plagueDeaths = [];
        List<int> famineSeverity = [];
        List<int> plagueSeverity = [];

        foreach (Event e in log.Events)
        {
            if (e.Causes.Count > 0) { withCauses++; edges += e.Causes.Count; }

            string here = Family(e.Kind);
            foreach (EventId cause in e.Causes)
            {
                if (!log.TryGet(cause, out Event parent)) continue;
                string there = Family(parent.Kind);

                bool bothReadable = e.Significance >= Significance.Minor
                                    && parent.Significance >= Significance.Minor;
                if (bothReadable) visibleEdges++;

                if (there != here) crossDomain++;
                if (there != "ECONOMY") continue;

                economyEdges++;
                if (here == "ECONOMY") continue;

                economyDriven++;
                if (bothReadable) visibleEconomyDriven++;
            }

            switch (e.Kind)
            {
                case EventKind.PolityCollapse:
                    collapses[e.Faction] = collapses.GetValueOrDefault(e.Faction) + 1;
                    break;
                // Readable disasters only. Attrition at a near-abandoned mining site still moves
                // the world, but it is not what a reader would call a famine and counting it
                // here is what made the two disasters look like different units.
                case EventKind.EconomyFamine:
                    if (e.GetInt("years") <= 1) famines++;
                    if (e.Significance >= Significance.Minor)
                    {
                        famineDeaths.Add(e.GetInt("deaths"));
                        famineSeverity.Add(Severity(e));
                    }
                    break;
                case EventKind.EconomyFamineEnds: famineEnds++; break;
                case EventKind.EconomyPlague:
                    // Distinct outbreaks, not plague-years: a plague that runs three years emits
                    // three events, and counting those against the number that ended made a
                    // resolving mechanic look like a leaking one.
                    if (e.GetInt("years") <= 1) plagues++;
                    if (e.Significance >= Significance.Minor)
                    {
                        plagueDeaths.Add(e.GetInt("deaths"));
                        plagueSeverity.Add(Severity(e));
                    }
                    break;
                case EventKind.EconomyPlagueEnds: plagueEnds++; break;
                case EventKind.PolityChallenge: challenges++; break;
                case EventKind.PolityCoupPlotted: plotsOpened.Add(e.Id); break;
                case EventKind.PolityPlotDiesWithPlotter:
                case EventKind.PolityPlotLapses:
                    foreach (EventId cause in e.Causes) plotsTerminated.Add(cause);
                    break;
                case EventKind.ConflictAssassination:
                    assassinations++;
                    if (e.Outcome == Outcome.Succeeded) fatal++;
                    break;
                case EventKind.PolityCoupResolved:
                    coups++;
                    foreach (EventId cause in e.Causes) plotsTerminated.Add(cause);
                    // "abandoned" was a third case with no emitter — an unreachable branch in a
                    // switch is a claim about the world that nothing can make true, and this one
                    // sat beside the win counter that was unreachable for the same reason.
                    // Removed rather than given an emitter: a plot nobody pursued lapses, and a
                    // lapse is POLITY_PLOT_LAPSES, which is a different event and already counted.
                    if (e.GetString("mode") == "exposed") exposed++;
                    else if (e.Outcome == Outcome.Succeeded) won++;
                    else lost++;
                    break;
                default: break;
            }

            if (e.Significance < Significance.Minor) continue;
            readable++;

            string signature = $"{EventKinds.Name(e.Kind)}|{Normalise(view.Describe(e.Id))}";
            int count = seen.GetValueOrDefault(signature) + 1;
            seen[signature] = count;
            if (count > 1) repeats++;
        }

        // Conspiracies that had their full lifespan before the records stop. A plot opened two
        // years before the end is pending, not abandoned, and the engine's own termination
        // assertion has always exempted them for exactly that reason.
        int lifespan = plotLifespan > 0 ? plotLifespan : SimConfig.Default.PlotLifespan;
        int lastYear = log.Count == 0 ? 0 : log.Events[^1].Year;
        HashSet<EventId> matured = [];

        foreach (Event e in log.Events)
            if (e.Kind == EventKind.PolityCoupPlotted && e.Year + lifespan <= lastYear)
                matured.Add(e.Id);

        int gotThrough = 0;
        foreach (Event e in log.Events)
            if (e.Kind == EventKind.ConflictRaid && e.Outcome == Outcome.Succeeded) gotThrough++;

        List<(string, int)> worst = [];
        foreach ((string sig, int count) in seen)
            if (count > 2) worst.Add((sig[(sig.IndexOf('|', StringComparison.Ordinal) + 1)..], count));
        worst.Sort(static (a, b) => b.Item2 != a.Item2 ? b.Item2.CompareTo(a.Item2) : string.CompareOrdinal(a.Item1, b.Item1));

        // Anything readable that nothing ever names as a cause. Bookkeeping is excluded: the
        // yearly accounts are not meant to be pointed at, and counting them would swamp the
        // measure with rows that were never intended to have consequences.
        int deadEnds = 0;
        int considered = 0;
        Dictionary<EventKind, int> deadEndByKind = [];
        foreach (Event e in log.Events)
        {
            if (e.Significance < Significance.Minor) continue;
            considered++;
            if (log.EffectsOf(e.Id).Count > 0) continue;
            deadEnds++;
            deadEndByKind[e.Kind] = deadEndByKind.GetValueOrDefault(e.Kind) + 1;
        }

        // Kinds that never cause anything at all, and how many events they account for.
        Dictionary<EventKind, int> perKind = [];
        Dictionary<EventKind, int> causingPerKind = [];
        foreach (Event e in log.Events)
        {
            if (e.Significance < Significance.Minor) continue;
            perKind[e.Kind] = perKind.GetValueOrDefault(e.Kind) + 1;
            if (log.EffectsOf(e.Id).Count > 0)
                causingPerKind[e.Kind] = causingPerKind.GetValueOrDefault(e.Kind) + 1;
        }

        int inertEvents = 0;
        List<string> inertKinds = [];
        foreach ((EventKind kind, int count) in perKind)
        {
            if (causingPerKind.GetValueOrDefault(kind) > 0) continue;
            if (IsClosure(kind)) continue;
            inertEvents += count;
            inertKinds.Add($"{EventKinds.Name(kind)} ({count})");
        }
        inertKinds.Sort(StringComparer.Ordinal);

        List<(string, int)> deadEndKinds = [];
        foreach (KeyValuePair<EventKind, int> kv in deadEndByKind)
            deadEndKinds.Add((EventKinds.Name(kv.Key), kv.Value));
        deadEndKinds.Sort(static (a, b) => b.Item2 != a.Item2
            ? b.Item2.CompareTo(a.Item2) : string.CompareOrdinal(a.Item1, b.Item1));
        if (deadEndKinds.Count > 12) deadEndKinds = deadEndKinds[..12];

        (int deep, int lifecycle, Dictionary<string, int> shapes) = Chains(log, minChainDepth);

        List<(string, int)> common = [];
        foreach ((string shape, int count) in shapes) common.Add((shape, count));
        common.Sort(static (a, b) => b.Item2 != a.Item2 ? b.Item2.CompareTo(a.Item2) : string.CompareOrdinal(a.Item1, b.Item1));

        int maxCollapse = 0;
        int totalCollapse = 0;
        foreach (int n in collapses.Values) { maxCollapse = Math.Max(maxCollapse, n); totalCollapse += n; }

        return new Audit
        {
            ReadableEvents = readable,
            RepeatedEvents = repeats,
            WorstRepeats = worst.Count > 12 ? worst[..12] : worst,
            MaxCollapsesPerFaction = maxCollapse,
            TotalCollapses = totalCollapse,
            Famines = famines,
            FamineEnds = famineEnds,
            DeepChains = deep,
            DistinctChainShapes = shapes.Count,
            LifecycleChains = lifecycle,
            CommonShapes = common.Count > 8 ? common[..8] : common,
            CoupsResolved = coups,
            CoupsWon = won,
            CoupsLost = lost,
            CoupsExposed = exposed,
            Assassinations = assassinations,
            AssassinationsFatal = fatal,
            EventsWithCauses = withCauses,
            CauseEdges = edges,
            CrossDomainEdges = crossDomain,
            EconomyEdges = economyEdges,
            EconomyDrivenEdges = economyDriven,
            VisibleEdges = visibleEdges,
            VisibleEconomyDrivenEdges = visibleEconomyDriven,
            SkewedOutcomes = Skew(log, skewThreshold),
            PlotsOpened = plotsOpened.Count,
            PlotsTerminated = CountIntersection(plotsOpened, plotsTerminated),
            PlotsMatured = matured.Count,
            PlotsMaturedTerminated = CountIntersection(matured, plotsTerminated),
            RaidsThatGotThrough = gotThrough,
            Challenges = challenges,
            Plagues = plagues,
            PlagueEnds = plagueEnds,
            MedianFamineDeaths = MedianOf(famineDeaths),
            MedianPlagueDeaths = MedianOf(plagueDeaths),
            FamineSeverityPerMille = MedianOf(famineSeverity),
            PlagueSeverityPerMille = MedianOf(plagueSeverity),
            DeadEndEvents = deadEnds,
            TotalEvents = considered,
            DeadEndsByKind = deadEndKinds,
            InertKindEvents = inertEvents,
            InertKinds = inertKinds,
        };
    }

    /// <summary>
    /// Deaths as tenths of a percent of the settlement. The base population is stamped on the
    /// event when it is emitted, because it cannot be recovered afterwards — by the end of the
    /// run the place has grown, shrunk or been abandoned.
    /// </summary>
    /// <summary>
    /// How lopsided each event kind's outcome is.
    ///
    /// "Outcome" is read from whichever field carries it: the <see cref="Outcome"/> enum where
    /// the kind uses one, otherwise the <c>reason</c> or <c>mode</c> payload — those are the
    /// three places this engine records how a thing came out. Kinds with fewer than five
    /// readable instances are skipped; a skew over three of them means nothing.
    /// </summary>
    private static List<string> Skew(EventLog log, int threshold = 70)
    {
        Dictionary<string, Dictionary<string, int>> byDecision = [];

        foreach (Event e in log.Events)
        {
            if (e.Significance < Significance.Minor) continue;

            string? outcome = e.Outcome != Outcome.NotApplicable
                ? e.Outcome.ToString()
                : e.GetString("reason") ?? e.GetString("mode");
            if (outcome is null) continue;

            // Grouped by the decision, where the emit site names one. A kind can record
            // several unrelated choices — how a seat passed is not one decision but four —
            // and pooling them averages a lopsided call into an even-looking one.
            string decision = e.GetString("decision") is { } named
                ? $"{EventKinds.Name(e.Kind)} ({named})"
                : EventKinds.Name(e.Kind);

            if (!byDecision.TryGetValue(decision, out Dictionary<string, int>? counts))
                byDecision[decision] = counts = [];
            counts[outcome] = counts.GetValueOrDefault(outcome) + 1;
        }

        List<(string Line, int Pct)> skewed = [];

        foreach ((string kind, Dictionary<string, int> counts) in byDecision)
        {
            int total = 0, most = 0;
            string commonest = "";
            foreach ((string outcome, int n) in counts)
            {
                total += n;
                if (n <= most && (n != most || string.CompareOrdinal(outcome, commonest) >= 0)) continue;
                most = n;
                commonest = outcome;
            }

            if (total < 5 || counts.Count < 2) continue;

            int pct = most * 100 / total;
            if (pct < threshold) continue;

            skewed.Add(($"{kind} — \"{commonest}\" in {most} of {total} ({pct}%)", pct));
        }

        skewed.Sort(static (a, b) => a.Pct != b.Pct
            ? b.Pct.CompareTo(a.Pct)
            : string.CompareOrdinal(a.Line, b.Line));

        List<string> lines = [];
        foreach ((string line, int _) in skewed) lines.Add(line);
        return lines;
    }

    /// <summary>The family an event belongs to — the part of its name before the dot.</summary>
    private static string Family(EventKind kind)
    {
        string name = EventKinds.Name(kind);
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? name : name[..dot];
    }

    private static int Severity(Event e)
    {
        int before = e.GetInt("ofPop");
        int deaths = e.GetInt("deaths");
        return before <= 0 ? 0 : deaths * 1000 / before;
    }

    private static int MedianOf(List<int> values)
    {
        if (values.Count == 0) return 0;
        values.Sort();
        return values[values.Count / 2];
    }

    private static int CountIntersection(HashSet<EventId> opened, HashSet<EventId> terminated)
    {
        int n = 0;
        foreach (EventId id in opened)
            if (terminated.Contains(id)) n++;
        return n;
    }

    /// <summary>
    /// Events whose whole job is to record that something ended.
    ///
    /// Exempt from the inert-kind measure, because holding an ending to account for causing a
    /// further event is the wrong test — a plot that lapsed exists to answer "what became of
    /// it", not to start something. Without this exemption the round-2 requirement to make
    /// plots and plagues terminate explicitly would have *worsened* the consequence-free share
    /// it was measured against, which says more about the metric than about the world.
    /// </summary>
    private static bool IsClosure(EventKind kind) => kind is
        EventKind.PolityPlotLapses or EventKind.PolityPlotDiesWithPlotter
        or EventKind.EconomyFamineEnds or EventKind.EconomyPlagueEnds
        or EventKind.DiploPeaceSigned or EventKind.PolityCollapse;

    /// <summary>Digits collapse to <c>#</c>, so "420 dead" and "37 dead" count as the same sentence.</summary>
    private static string Normalise(string description)
    {
        StringBuilder sb = new(description.Length);
        bool inNumber = false;

        foreach (char c in description)
        {
            if (char.IsDigit(c)) { if (!inNumber) { sb.Append('#'); inNumber = true; } }
            else { sb.Append(c); inNumber = false; }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Walks the longest ancestral path from every event and records its shape as a sequence of
    /// event kinds. A "lifecycle" chain is one where a single actor appears at every step, or
    /// where every step is a birth/marriage/death — depth that comes from one person ageing
    /// rather than from anything in the world acting on anything else.
    /// </summary>
    private static (int Deep, int Lifecycle, Dictionary<string, int> Shapes) Chains(EventLog log, int minDepth)
    {
        List<EventId>?[] longest = new List<EventId>?[log.Count + 1];
        Dictionary<string, int> shapes = [];
        int deep = 0, lifecycle = 0;

        for (int i = 1; i <= log.Count; i++) Longest(new EventId(i));

        foreach (Event e in log.Events)
        {
            List<EventId> path = longest[e.Id.Value]!;
            if (path.Count < minDepth) continue;

            // Only count a chain at its tip, so one long history is not counted once per link.
            if (log.EffectsOf(e.Id).Count > 0) continue;

            deep++;
            StringBuilder sb = new();
            for (int i = path.Count - 1; i >= 0; i--)
            {
                if (sb.Length > 0) sb.Append(" > ");
                sb.Append(EventKinds.Name(log.Get(path[i]).Kind));
            }

            string shape = sb.ToString();
            shapes[shape] = shapes.GetValueOrDefault(shape) + 1;
            if (IsLifecycle(log, path)) lifecycle++;
        }

        return (deep, lifecycle, shapes);

        List<EventId> Longest(EventId id)
        {
            if (longest[id.Value] is { } cached) return cached;

            longest[id.Value] = [id]; // guards against revisiting while recursing
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

    private static bool IsLifecycle(EventLog log, List<EventId> path)
    {
        bool allLife = true;
        Dictionary<EntityId, int> appearances = [];

        foreach (EventId id in path)
        {
            Event e = log.Get(id);
            if (e.Kind is not (EventKind.LifeBirth or EventKind.LifeMarriage or EventKind.LifeComingOfAge
                or EventKind.LifeDeathNatural or EventKind.LifeDeathViolent or EventKind.GenesisActor))
            {
                allLife = false;
            }

            HashSet<EntityId> here = [];
            foreach (Participant p in e.Participants)
                if (p.Id.Kind == EntityKind.Actor) here.Add(p.Id);
            foreach (EntityId id2 in here) appearances[id2] = appearances.GetValueOrDefault(id2) + 1;
        }

        if (allLife) return true;

        foreach (int n in appearances.Values)
            if (n == path.Count) return true;
        return false;
    }

    // ---- reporting --------------------------------------------------------

    public IReadOnlyList<string> Report()
    {
        List<string> lines =
        [
            $"readable events        {ReadableEvents}",
            $"verbatim repeats       {RepeatedEvents} ({RepeatRatePct}%)",
            $"collapses              {TotalCollapses} total, worst faction {MaxCollapsesPerFaction}x",
            $"famine : famine_ends   {Famines} : {FamineEnds}" +
                (FamineEnds == 0 ? "" : $"  ({(Famines * 10 / Math.Max(1, FamineEnds)) / 10.0:0.0} : 1)"),
            $"deep chains (>=5)      {DeepChains}, {DistinctChainShapes} distinct shapes, " +
                $"{LifecycleChains} lifecycle-only ({LifecycleChainPct}%)",
            $"coups resolved         {CoupsResolved}: {CoupsWon} won, {CoupsLost} lost, " +
                $"{CoupsExposed} exposed (hidden)  " +
                $"(success {CoupSuccessPctOfPlotted}% of {PlotsOpened} plotted — the invariant; " +
                $"{CoupSuccessPctOfResolved}% of resolved, {CoupDecidedPct}% of decided)",
            $"assassinations         {Assassinations}: {AssassinationsFatal} fatal ({AssassinationFatalPct}%)",
            $"plots (matured)        {PlotsMatured} had their full lifespan, " +
                $"{PlotsMaturedTerminated} terminated ({PlotTerminationPct}%)",
            $"plots                  {PlotsOpened} opened, {PlotsTerminated} terminated " +
                $"({PlotTerminationPctOfAllOpened}% of all opened, the pre-ruleset-2 figure); " +
                $"{Challenges} open challenges",
            $"plague                 {Plagues} outbreaks, {PlagueEnds} ended; " +
                $"median deaths plague {MedianPlagueDeaths} vs famine {MedianFamineDeaths} " +
                $"({PlagueToFaminePct / 100.0:0.0}x)",
            $"disaster severity      plague {PlagueSeverityPerMille / 10.0:0.0}% of a settlement " +
                $"vs famine {FamineSeverityPerMille / 10.0:0.0}% ({SeverityRatioPct / 100.0:0.0}x)",
            $"causal edges           {CauseEdges} across {EventsWithCauses} events; " +
                $"{CrossDomainEdges} cross a family boundary",
            $"economy coupling       {EconomyDrivenEdges} of {CauseEdges} edges run from an " +
                $"ECONOMY event to something that is not one ({EconomyEdges} start there in all)",
            $"  the same, in the .log  {VisibleEconomyDrivenEdges} of {VisibleEdges} — the readable log " +
                "hides Bookkeeping rows, and economy",
            "                         causation starts in them. Measure the record, not the view.",
            $"dead ends              {DeadEndEvents} of {TotalEvents} readable events cause nothing ({DeadEndPct}%)",
            $"inert kinds            {InertKinds.Count} kinds never cause anything, " +
                $"{InertKindEvents} events ({InertPct}%)" +
                (InertKinds.Count == 0 ? "" : ": " + string.Join(", ", InertKinds)),
        ];

        if (WorstRepeats.Count > 0)
        {
            lines.Add("");
            lines.Add("most repeated lines");
            foreach ((string text, int count) in WorstRepeats)
                lines.Add($"  {count,4}x  {Truncate(text, 96)}");
        }

        if (SkewedOutcomes.Count > 0)
        {
            lines.Add("");
            lines.Add("lopsided outcomes — where guessing the common case scores well");
            foreach (string line in SkewedOutcomes) lines.Add("  " + line);
        }

        if (DeadEndsByKind.Count > 0)
        {
            lines.Add("");
            lines.Add("dead ends by kind");
            foreach ((string kind, int count) in DeadEndsByKind)
                lines.Add($"  {count,5}  {kind}");
        }

        if (CommonShapes.Count > 0)
        {
            lines.Add("");
            lines.Add("most common deep-chain shapes");
            foreach ((string shape, int count) in CommonShapes)
                lines.Add($"  {count,4}x  {Truncate(shape, 110)}");
        }

        return lines;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
