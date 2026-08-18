namespace WorldBuilder.Core.Analysis;

/// <summary>One invariant, its measured value, and whether it held.</summary>
/// <param name="Sample">
/// How many observations the figure is computed over, or 0 where it is not a rate.
///
/// A rate without its denominator hides its own resolution. Seed 7 opens fourteen plots, so its
/// success rate can be 14.3% or 21.4% and nothing in between: a "&gt; 15%" bar there is a
/// "≥ 3 of 14" bar wearing a percentage, and reading it as a percentage invites tuning against
/// a granularity rather than against a world.
/// </param>
public sealed record Invariant(string Name, string Measured, bool Held, string Expected, int Sample = 0)
{
    /// <summary>
    /// Whether the sample is small enough that the achievable values are visibly coarse.
    ///
    /// Twenty observations puts five percentage points between neighbouring outcomes, which is
    /// a third of the coup bar. Said in the output rather than left to be rediscovered.
    /// </summary>
    public bool Coarse => Sample is > 0 and <= 20;

    public string PointsPerObservation =>
        Sample <= 0 ? "" : $"{100.0 / Sample:0.#} points per observation";
}

/// <summary>
/// Layer 1: the v0 acceptance criteria, made permanent.
///
/// Every threshold here was computed by hand during v0 review and then forgotten about, which
/// is how a covert-coup path that had worked regressed to zero successes and stayed there for a
/// round. A metric measured once is an anecdote; a metric asserted on every build is a contract.
///
/// Two rules, both learned from getting them wrong:
///
/// <b>Read the record, never the view.</b> Every figure here comes from the full event list.
/// The readable <c>.log</c> hides bookkeeping rows, and a measurement taken over it reported
/// 18 economy edges where the world has 142 — three times, in three separate reviews.
///
/// <b>A filter that drops rows fails loudly.</b> If a causal edge points at an event that is
/// not there, that is a dangling reference to be reported, not a row to skip. Silent skipping
/// is what made a broken graph look like a healthy one.
///
/// <b>A metric that encodes a superseded model will pull the world back toward the model it
/// encoded.</b> Every stage from here changes rules. A dynamics bar written against the old
/// rules is not neutral — it is a constraint on the new ones, and satisfying it may mean undoing
/// a deliberate improvement. <c>plots terminated ≥ 85%</c> was written when a conspiracy was
/// voided by its target's death; after the plot was attached to the seat instead, the same bar
/// was quietly asking for conspiracies to keep being thrown away.
///
/// Therefore, on every ruleset change: <b>each failing metric is adjudicated before it is
/// satisfied.</b> The question is not "how do we make this pass" but "does this still measure
/// what it was for". Each falls into exactly one of three categories — it encodes a superseded
/// model and should be redefined with its predecessor and reason recorded; it measures something
/// real that got worse, and the engine is what needs fixing; or it measures the right thing badly
/// and the measurement is what needs fixing.
///
/// <b>The order of operations is the whole safeguard.</b> Redefining a metric to make it pass is
/// indistinguishable from lowering a threshold unless the rationale is written down before the
/// new number is seen. A rationale formed after the measurement is a rationalisation, and this
/// project has already learned what constants chosen by fitting are worth.
/// </summary>
public static class Invariants
{
    public static List<Invariant> Check(WorldView view)
    {
        Audit audit = Audit.Compute(view);
        List<Invariant> results = [];

        // Every cause must resolve. Counted here rather than trusted from the audit, because
        // this is the invariant that a silent filter would hide.
        int dangling = 0, edges = 0;
        foreach (Event e in view.Log.Events)
            foreach (EventId cause in e.Causes)
            {
                edges++;
                if (!view.Log.TryGet(cause, out _)) dangling++;
            }

        Add("dangling causal references", dangling, dangling == 0, "0");
        Add("causal edges read", edges, edges == audit.CauseEdges,
            $"{audit.CauseEdges} (the audit's count)");

        Add("verbatim repeat rate", $"{audit.RepeatRatePct}%", audit.RepeatRatePct < 10, "< 10%", audit.ReadableEvents);
        // Asserted on the count, not on the rounded percentage.
        //
        // Found by its own positive control, 2026-08-16, ruleset 2. The percentage is integer,
        // so one lifecycle chain among a hundred and fifty divides to 0% and passed — a world
        // containing exactly the construction this metric exists to forbid reported clean. The
        // bar is unchanged at zero; what changed is that zero now means none rather than
        // "few enough to round away". Category three: the right thing, measured badly.
        Add("single-actor causal chains",
            $"{audit.LifecycleChains} ({audit.LifecycleChainPct}%)",
            audit.LifecycleChains == 0, "0 chains", audit.DeepChains);
        Add("distinct deep-chain shapes", audit.DistinctChainShapes, audit.DistinctChainShapes >= 60, ">= 60");
        Add("collapses per faction", audit.MaxCollapsesPerFaction, audit.MaxCollapsesPerFaction <= 1, "<= 1");

        // Coups are counted among those that reached a decision. Plots overtaken by the target
        // dying never reach a reader, so counting them in the denominator understates the world.
        // Per seed this asserts only that the covert path works at all. The rate itself is
        // asserted on the pooled panel by CheckPanel, because fourteen plots cannot support a
        // percentage: two seizures is 14.3% and three is 21.4%, so "> 15%" on that seed is a
        // "at least three of fourteen" bar in disguise. The threshold did not move; where it is
        // asserted did.
        Add("coup success rate (of plotted)", $"{audit.CoupSuccessPctOfPlotted}%",
            audit.CoupsWon > 0, "> 0 per seed; the rate is asserted pooled", audit.PlotsOpened);

        // Wins, not resolutions.
        //
        // This counted won + exposed and so reported a healthy covert path while nothing on it
        // ever succeeded — the exact regression it was written for, v0 run 3's "127 plotted, 0
        // won", passing its own guard. An invariant that cannot detect the defect it exists for
        // is worse than no invariant, because it occupies the place where one would be noticed
        // to be missing.
        Add("covert coup path", audit.CoupsWon, audit.CoupsWon > 0, "> 0 coups won");

        int economyPct = audit.CauseEdges == 0 ? 0 : audit.EconomyDrivenEdges * 100 / audit.CauseEdges;
        int crossPct = audit.CauseEdges == 0 ? 0 : audit.CrossDomainEdges * 100 / audit.CauseEdges;

        Add("economy-driven edges", $"{economyPct}%", economyPct >= 10, ">= 10% of all edges", audit.CauseEdges);
        Add("cross-domain edges", $"{crossPct}%", crossPct >= 25, ">= 25% of all edges", audit.CauseEdges);

        // Maximum causal depth, from the deepest chain the audit found.
        int depth = 0;
        foreach (IReadOnlyList<EventId> chain in CausalTrace.DeepestChains(view.Log, 1))
            depth = Math.Max(depth, chain.Count);
        Add("maximum causal depth", depth, depth >= 8, ">= 8");

        // Raids resolve more than one way.
        //
        // Added 2026-08-16, ruleset 3. It earned its place by being invisible: raids failed four
        // times in five and nothing in Layer 1 could see it — the verbatim-repeat metric pointed
        // here sideways, by noticing that the same house raided the same four places three times
        // each. A mechanic whose outcome is effectively constant is a mechanic that happens
        // constantly and changes nothing, and that is a simulation defect as much as a rendering
        // risk.
        //
        // Asserted on the share of the commonest branch rather than on the failure rate, so the
        // metric says what it means: a raid should be able to go either way. The bar is 90%,
        // which is loose on purpose — it forbids a decorative branch without prescribing odds,
        // and the round that set it had raids at 58%.
        (int beatenOff, int tookAHaul, int tookNothing) = OutcomeAudit.Raids(view.Log);
        int raids = beatenOff + tookAHaul + tookNothing;
        int raidSkew = raids == 0 ? 100 : Math.Max(beatenOff, tookAHaul + tookNothing) * 100 / raids;

        Add("raid outcome skew", $"{raidSkew}% one way ({beatenOff} beaten, {tookAHaul} haul, " +
            $"{tookNothing} empty)", raids > 0 && raidSkew <= 90, "<= 90% one way", raids);

        // The two mechanics this phase changed after raids, on the same bar rather than a new
        // one. §6 requires a metric for any mechanic whose distribution moved; the bar is the
        // established outcome-skew bar, not a value invented for these.
        //
        // Both currently sit near 80%, which passes. That is deliberate: they are watched rather
        // than asserted into compliance, and the residual skew on each is a recorded finding with
        // its cause named — target selection for tribute, and legitimacy being lowest exactly
        // when a disputed succession fires.
        // Reported per seed, asserted pooled.
        //
        // Per-seed n runs from 3 to 15, which cannot carry a percentage: at n=5 the achievable
        // values are 20 points apart and a single event moves the figure across the bar. §4's
        // pre-committed rule applies — assert on the pooled panel, where n is 36 and 42. A
        // per-seed floor of "> 0" is not used here either: with a 20% base rate and seven
        // demands, a world where no tribute is ever paid is one in five by chance, and a floor
        // that fails on chance is not a floor. Reachability across the panel is asserted
        // separately, which is the right home for "does this ever happen at all".
        foreach ((string name, string decision) in new[]
                 {
                     ("tribute outcome skew", "DIPLO.TRIBUTE_DEMANDED"),
                     ("heir claim outcome skew", "POLITY.SUCCESSION (claim)"),
                 })
        {
            (int n, int skew) = OutcomeAudit.SkewOf(view.Log, decision);
            Add(name, $"{skew}% one way", true, "reported here; asserted pooled", n);
        }

        // Every conspiracy ends in exactly one recorded way, or the log has loose ends.
        Add("plots terminated", $"{audit.PlotTerminationPct}%", audit.PlotTerminationPct >= 85, ">= 85%");

        // ---- geography ----------------------------------------------------
        //
        // Added at Stage 6, ruleset 4. Three things, in the order they can fail: the world is on
        // the board, the board can tell places apart, and the mechanics that consult it act both
        // near and far.

        List<string> misplaced = GeographyAudit.Positions(view.State);
        Add("places on the board", misplaced.Count == 0 ? "every sited place has one cell on the board"
            : string.Join("; ", misplaced.Take(3)), misplaced.Count == 0, "no complaints");

        // The reachability guard for every rule that multiplies by a proximity.
        //
        // A board that returned one proximity for every pair would have four mechanics
        // multiplying by a constant and reporting that geography had been consulted — the same
        // defect class as a ratio whose numerator no path can reach, arriving through a different
        // door. Asserted on the range, so it fails at definition time rather than after a phase
        // of tuning against it.
        // Emitted as a labelled range rather than as "38–76", which is the form that was read as a
        // spread once already. The figure says what kind it is at the point it is made.
        (int pairs, Dispersion proximity) = GeographyAudit.ProximityRange(view.State);

        if (view.State.HasBoard)
        {
            Add("distance can vary", $"{proximity} across {pairs} pairs of places",
                pairs > 0 && proximity.Width > 0, "a range, not a single value", pairs);
        }

        // Each distance-consuming mechanic's near/far split, on the established outcome-skew
        // bar rather than a new one. A mechanic that only ever acts nearby has had distance
        // turned into a gate, and a gate is as decorative as no distance at all.
        //
        // Reported per seed and asserted pooled, for the reason every rate here is: n runs from
        // two to thirty on a single world, and a percentage over four observations is a
        // granularity wearing a percent sign.
        foreach (ReachSplit split in GeographyAudit.Reach(view.State, view.Log))
        {
            Add($"{split.Mechanic} reach", $"{split.SkewPct}% one way ({split.Near} near, {split.Far} far)",
                true, "reported here; asserted pooled", split.Total);
        }

        return results;

        void Add(string name, object measured, bool held, string expected, int sample = 0) =>
            results.Add(new Invariant(name, measured.ToString() ?? "", held, expected, sample));
    }

    /// <summary>
    /// The invariants that are only meaningful over the whole panel.
    ///
    /// A rate needs enough observations to be a rate. Seed 7 opens fourteen plots, so its success
    /// figure moves in steps of 7.1 points and can never land between 14.3% and 21.4%; asserting
    /// a 15% bar there tests the granularity, not the world. Pooled, the panel has enough
    /// observations for the bar to mean what it was written to mean.
    ///
    /// The per-seed assertion is not dropped, it is narrowed to the thing fourteen samples *can*
    /// support: that the covert path works at all in every world.
    /// </summary>
    public static List<Invariant> CheckPanel(IReadOnlyList<Audit> panel) =>
        CheckPanel(panel, []);

    /// <summary>
    /// The panel-level invariants. <paramref name="logs"/> may be empty, in which case only the
    /// metrics computable from the audits are returned.
    /// </summary>
    public static List<Invariant> CheckPanel(IReadOnlyList<Audit> panel, IReadOnlyList<EventLog> logs) =>
        CheckPanel(panel, logs, []);

    /// <summary>
    /// The panel-level invariants, including the ones that need the world rather than the log.
    ///
    /// <paramref name="views"/> carries the geography metrics: a near/far split needs to know
    /// where things are, and that is state rather than record. Empty where a caller has only logs,
    /// in which case the geography rows are simply absent — reported by their absence rather than
    /// by a zero, since a zero here would read as "no mechanic ever acted far away".
    /// </summary>
    public static List<Invariant> CheckPanel(
        IReadOnlyList<Audit> panel, IReadOnlyList<EventLog> logs, IReadOnlyList<WorldView> views)
    {
        int won = 0, plotted = 0;
        foreach (Audit a in panel) { won += a.CoupsWon; plotted += a.PlotsOpened; }

        int pct = plotted == 0 ? 0 : won * 100 / plotted;

        List<Invariant> results =
        [
            new Invariant("coup success rate (of plotted), pooled", $"{pct}%", pct > 15,
                "> 15% of plots opened, across the panel", plotted),
        ];

        foreach ((string name, string decision) in new[]
                 {
                     ("tribute outcome skew, pooled", "DIPLO.TRIBUTE_DEMANDED"),
                     ("heir claim outcome skew, pooled", "POLITY.SUCCESSION (claim)"),
                 })
        {
            Dictionary<string, int> pooled = new(StringComparer.Ordinal);

            foreach (EventLog log in logs)
                if (OutcomeAudit.Distributions(log).TryGetValue(decision, out Dictionary<string, int>? counts))
                    foreach ((string branch, int n) in counts)
                        pooled[branch] = pooled.GetValueOrDefault(branch) + n;

            if (pooled.Count == 0) continue;

            OutcomeSkew skew = new(decision, pooled);
            results.Add(new Invariant(name, $"{skew.SkewPct}% one way",
                skew.SkewPct <= 90, "<= 90% one way, across the panel", skew.Total));
        }

        // Geography, pooled. Each distance-consuming mechanic must act both near and far across
        // the panel, on the established outcome-skew bar.
        //
        // Both branches being observed somewhere is the reachability half of this, and it is
        // asserted by the bar itself: a mechanic that only ever acted nearby reads 100% one way
        // and fails. That is the same guard the ratio metrics carry, stated as a distribution
        // rather than as a separate check — the near case and the far case are the two branches.
        Dictionary<string, (int Near, int Far)> reach = new(StringComparer.Ordinal);

        foreach (WorldView view in views)
            foreach (ReachSplit split in GeographyAudit.Reach(view.State, view.Log))
            {
                (int near, int far) = reach.GetValueOrDefault(split.Mechanic);
                reach[split.Mechanic] = (near + split.Near, far + split.Far);
            }

        List<string> mechanics = [.. reach.Keys];
        mechanics.Sort(StringComparer.Ordinal);

        foreach (string mechanic in mechanics)
        {
            (int near, int far) = reach[mechanic];
            ReachSplit pooledSplit = new(mechanic, near, far);

            results.Add(new Invariant($"{mechanic} reach, pooled",
                $"{pooledSplit.SkewPct}% one way ({near} near, {far} far)",
                pooledSplit.Total > 0 && pooledSplit.SkewPct <= 90,
                "<= 90% one way, across the panel", pooledSplit.Total));
        }

        return results;
    }

    /// <summary>
    /// The ratio metrics, each with the numerator it depends on being able to move.
    ///
    /// <b>An invariant that cannot vary is not an invariant.</b> <c>CoupDecidedPct</c> reported a
    /// plausible number for months while being structurally incapable of any other value: its
    /// numerator counted a branch that no code path could emit. The threshold was tuned against
    /// it and the invariant reported green against it, which is a defect class rather than an
    /// incident — zero and impossible are different, the same absent-versus-withheld distinction
    /// this project has now met in the checker, the query layer, the plot ledger and here.
    ///
    /// So every rate that has to clear a bar names the thing that must happen for it to be
    /// non-zero, and the suite asserts that it happens somewhere on the panel. A metric whose
    /// numerator has no reachable emitter fails loudly instead of reporting zero for a year.
    ///
    /// <b>Only metrics that must be non-zero to pass are listed, and the exclusion is a real
    /// limit rather than an oversight.</b> <c>single-actor causal chains</c> passes at exactly
    /// zero: a zero numerator there is the desired state, so a quiet panel cannot distinguish
    /// "the engine no longer produces these" from "the engine could not produce one if it tried".
    /// That class needs a constructed counter-example rather than a survey, and it does not have
    /// one yet.
    /// </summary>
    public static IReadOnlyList<(string Metric, string Numerator, Func<Audit, int> Count)> RatioMetrics =>
    [
        ("verbatim repeat rate", "a verbatim repeat", a => a.RepeatedEvents),
        ("coup success rate (of plotted)", "a coup won", a => a.CoupsWon),
        ("covert coup path", "a coup won", a => a.CoupsWon),
        ("economy-driven edges", "an ECONOMY edge into another domain", a => a.EconomyDrivenEdges),
        ("cross-domain edges", "a cross-domain edge", a => a.CrossDomainEdges),
        ("plots terminated", "a terminated plot", a => a.PlotsTerminated),
        ("raid outcome skew", "a raid that got through", a => a.RaidsThatGotThrough),
    ];
}
