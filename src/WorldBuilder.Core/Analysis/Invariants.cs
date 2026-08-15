namespace WorldBuilder.Core.Analysis;

/// <summary>One invariant, its measured value, and whether it held.</summary>
public sealed record Invariant(string Name, string Measured, bool Held, string Expected);

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

        Add("verbatim repeat rate", $"{audit.RepeatRatePct}%", audit.RepeatRatePct < 10, "< 10%");
        Add("single-actor causal chains", $"{audit.LifecycleChainPct}%", audit.LifecycleChainPct == 0, "0%");
        Add("distinct deep-chain shapes", audit.DistinctChainShapes, audit.DistinctChainShapes >= 60, ">= 60");
        Add("collapses per faction", audit.MaxCollapsesPerFaction, audit.MaxCollapsesPerFaction <= 1, "<= 1");

        // Coups are counted among those that reached a decision. Plots overtaken by the target
        // dying never reach a reader, so counting them in the denominator understates the world.
        Add("coup success rate", $"{audit.CoupDecidedPct}%", audit.CoupDecidedPct > 15, "> 15%");
        Add("covert coup path", audit.CoupsWon + audit.CoupsExposed,
            audit.CoupsWon + audit.CoupsExposed > 0, "> 0 resolved conspiracies");

        int economyPct = audit.CauseEdges == 0 ? 0 : audit.EconomyDrivenEdges * 100 / audit.CauseEdges;
        int crossPct = audit.CauseEdges == 0 ? 0 : audit.CrossDomainEdges * 100 / audit.CauseEdges;

        Add("economy-driven edges", $"{economyPct}%", economyPct >= 10, ">= 10% of all edges");
        Add("cross-domain edges", $"{crossPct}%", crossPct >= 25, ">= 25% of all edges");

        // Maximum causal depth, from the deepest chain the audit found.
        int depth = 0;
        foreach (IReadOnlyList<EventId> chain in CausalTrace.DeepestChains(view.Log, 1))
            depth = Math.Max(depth, chain.Count);
        Add("maximum causal depth", depth, depth >= 8, ">= 8");

        // Every conspiracy ends in exactly one recorded way, or the log has loose ends.
        Add("plots terminated", $"{audit.PlotTerminationPct}%", audit.PlotTerminationPct >= 85, ">= 85%");

        return results;

        void Add(string name, object measured, bool held, string expected) =>
            results.Add(new Invariant(name, measured.ToString() ?? "", held, expected));
    }
}
