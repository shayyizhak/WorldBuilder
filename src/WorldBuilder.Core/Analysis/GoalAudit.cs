using System.Globalization;
using WorldBuilder.Core.Geography;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Every goal transition a world underwent, counted.
///
/// Null on every ordinary run and notified only after the mutation it reports, so attaching it
/// cannot move the world — asserted by log hash across the panel, not assumed.
/// </summary>
public sealed class GoalCensus : IGoalWatcher
{
    public SortedDictionary<GoalKind, int> Created { get; } = [];
    public SortedDictionary<GoalRefusal, int> Refused { get; } = [];
    public SortedDictionary<GoalStep, int> Advanced { get; } = [];
    public SortedDictionary<GoalEnd, int> Ended { get; } = [];

    /// <summary>Endings whose site had an event to point at. The rest leave no trace whatsoever.</summary>
    public SortedDictionary<GoalEnd, int> Cited { get; } = [];

    /// <summary>Removals that found nothing to remove. See <see cref="IGoalWatcher.Vanished"/>.</summary>
    public SortedDictionary<GoalEnd, int> Vanished { get; } = [];

    public int TotalVanished => Sum(Vanished);

    public int Attachments { get; private set; }

    /// <summary>The most goals alive at once. Zero for a fold, which is the finding.</summary>
    public int Peak { get; private set; }

    /// <summary>How many times the live count fell — the monotonic sweep's question, for goals.</summary>
    public int Decreases { get; private set; }

    public int Live { get; private set; }

    public int TotalCreated => Sum(Created);
    public int TotalEnded => Sum(Ended);
    public int TotalAdvanced => Sum(Advanced);
    public int TotalRefused => Sum(Refused);
    public int TotalCited => Sum(Cited);

    void IGoalWatcher.Created(Goal goal)
    {
        Bump(Created, goal.Kind);
        Live++;
        if (Live > Peak) Peak = Live;
    }

    void IGoalWatcher.Refused(EntityId owner, GoalKind kind, GoalRefusal why) => Bump(Refused, why);

    void IGoalWatcher.Advanced(Goal goal, int delta, GoalStep step) => Bump(Advanced, step);

    void IGoalWatcher.Attached(Goal goal, EntityId arc) => Attachments++;

    void IGoalWatcher.Ended(Goal goal, GoalEnd why, EventId citation)
    {
        Bump(Ended, why);
        if (!citation.IsNone) Bump(Cited, why);
        Live--;
        Decreases++;
    }

    void IGoalWatcher.Vanished(Goal goal, GoalEnd why) => Bump(Vanished, why);

    private static void Bump<TKey>(SortedDictionary<TKey, int> into, TKey key) where TKey : notnull =>
        into[key] = into.GetValueOrDefault(key) + 1;

    private static int Sum<TKey>(SortedDictionary<TKey, int> of) where TKey : notnull
    {
        int total = 0;
        foreach (int n in of.Values) total += n;
        return total;
    }
}

/// <summary>What one world's goal lifecycle did, live and on replay of its own record.</summary>
/// <param name="Folded">
/// What a fold of the record performed.
///
/// <b>At ruleset 6 this was zero of everything, and that was the finding.</b> It is now the same
/// census as <paramref name="Live"/> when the record is complete, which is the deliverable stated as a
/// pair of columns: every transition the rules made, the record made too.
///
/// A ruleset-6 audit needed a third pass to make this column able to vary at all — a fold with the
/// book topped up between events, because with creation outside the fold the reducer's clears had
/// nothing to clear and could only read zero. That scaffolding is gone: the column varies on its own
/// now, which is what fixing the defect means.
/// </param>
/// <param name="Divergent">Components of <see cref="WorldState"/> the fold failed to reproduce.</param>
public sealed record GoalAuditSeed(
    ulong Seed,
    int Events,
    GoalCensus Live,
    GoalCensus Folded,
    IReadOnlyList<string> Divergent,
    IReadOnlyList<string> Checked);

/// <summary>
/// Every transition a goal undergoes, counted live and counted again on a fold of the same world's
/// own record.
///
/// <b>Two passes, and the second one is the instrument.</b> The panel is simulated with a census
/// attached, then each world's log is folded from empty with a second census attached. Equal columns
/// mean the record carries the lifecycle; a shortfall names what it is missing. At ruleset 6 the
/// folded column read zero for all of it, which is the defect this measured; at ruleset 7 the two
/// agree, which is what closing it looks like.
///
/// That is deliberately not a hand-written table of call sites. §4 of the project reference records
/// what happened the last time a measurable property of the code was written out by hand: the list
/// of rules lacking floor protection named two that were protected and omitted one that was not —
/// wrong in both directions, which is the signature of a list reasoned out rather than measured. The
/// same trap was live here and the brief's own framing walked into it twice: the reducer was said to
/// touch goals "at exactly one point" and it touches them at six, and the 189 silent endings were all
/// attributed to the retirement sweep when 35 of them are action-phase guards.
/// </summary>
public static class GoalAudit
{
    public static GoalAuditSeed Run(ulong seed, int years)
    {
        GoalCensus live = new();

        Simulation sim = new(seed);
        sim.State.Goals.Watch = live;
        sim.Run(years);

        // Folded against this world's own board, never the repository's. Five separate sites once
        // looked up the stored board here and were all correct only because the reference panel
        // shares one; see §4 of the project reference.
        GoalCensus folded = new();
        WorldState replayed = Fold(sim.Log, seed, folded, sim.State.Board);

        return new GoalAuditSeed(
            seed,
            sim.Log.Count,
            live,
            folded,
            WorldFingerprint.Differences(sim.State, replayed),
            WorldFingerprint.Components(sim.State));
    }

    private static WorldState Fold(EventLog log, ulong seed, IGoalWatcher watch, Board? board)
    {
        WorldState state = new() { Seed = seed };
        state.Goals.Watch = watch;
        Rendering.Replay.FoldInto(state, log, board);
        return state;
    }

    // ---- reporting --------------------------------------------------------

    public static IReadOnlyList<string> Render(IReadOnlyList<GoalAuditSeed> panel)
    {
        List<string> lines = [];

        lines.Add("## Goal lifecycle, measured across the panel");
        lines.Add("");
        lines.Add($"{panel.Count.ToString(CultureInfo.InvariantCulture)} world(s): " +
                  string.Join(", ", Names(panel)) + ".");
        lines.Add("");

        // ---- creation
        lines.Add("### Created — by the perception phase, and by nothing else");
        lines.Add("");
        lines.Add("| goal kind | created (live) | created (on replay) |");
        lines.Add("|---|---|---|");

        foreach (GoalKind kind in Enum.GetValues<GoalKind>())
        {
            lines.Add($"| `{kind}` | {Total(panel, s => s.Live.Created.GetValueOrDefault(kind))} " +
                      $"| {Total(panel, s => s.Folded.Created.GetValueOrDefault(kind))} |");
        }

        lines.Add($"| **all kinds** | **{Total(panel, s => s.Live.TotalCreated)}** " +
                  $"| **{Total(panel, s => s.Folded.TotalCreated)}** |");
        lines.Add("");
        int madeLive = Sum(panel, s => s.Live.TotalCreated);
        int madeFold = Sum(panel, s => s.Folded.TotalCreated);
        lines.Add(madeLive == madeFold
            ? "Formed by `GOALS.FORMED`, one row per year. The two columns agreeing is the point: the " +
              "record forms every goal the rules did, so a world folded from its own log wants what " +
              "the live world wanted."
            : $"**{N(madeLive - madeFold)} creations do not survive a fold.** The record is incomplete.");
        lines.Add("");

        int takePlace = Sum(panel, s => s.Live.Created.GetValueOrDefault(GoalKind.TakePlace));
        if (takePlace == 0)
        {
            lines.Add("**`GoalKind.TakePlace` is created by nothing.** `ActionPhase` handles it beside " +
                      "`ControlOre` and no rule anywhere adds one, so the branch is reachable only " +
                      "through a kind that does not exist — the same structural-zero family as the four " +
                      "event kinds that are declared and never emitted.");
            lines.Add("");
        }

        lines.Add("Creations the book refused, which no counter saw before:");
        lines.Add("");
        lines.Add("| refusal | live |");
        lines.Add("|---|---|");
        foreach (GoalRefusal why in Enum.GetValues<GoalRefusal>())
            lines.Add($"| `{why}` | {Total(panel, s => s.Live.Refused.GetValueOrDefault(why))} |");
        lines.Add("");

        // ---- mutation
        lines.Add("### Mutated — progress only. Nothing retargets a goal.");
        lines.Add("");
        lines.Add("`Goal.Target` is `init`-only and the tree compiles, which is the compiler saying " +
                  "what a grep would only have suggested. `Goal.Arc` is set once, when a goal spawns " +
                  "a storyline. Progress is the one quantity that moves repeatedly.");
        lines.Add("");
        lines.Add("| what moved it | advances (live) | advances (on replay) |");
        lines.Add("|---|---|---|");

        foreach (GoalStep step in Enum.GetValues<GoalStep>())
        {
            lines.Add($"| `{step}` | {Total(panel, s => s.Live.Advanced.GetValueOrDefault(step))} " +
                      $"| {Total(panel, s => s.Folded.Advanced.GetValueOrDefault(step))} |");
        }

        lines.Add($"| **all steps** | **{Total(panel, s => s.Live.TotalAdvanced)}** " +
                  $"| **{Total(panel, s => s.Folded.TotalAdvanced)}** |");
        lines.Add($"| arc attachments | {Total(panel, s => s.Live.Attachments)} " +
                  $"| {Total(panel, s => s.Folded.Attachments)} |");
        lines.Add("");

        // ---- endings
        lines.Add("### Resolved and abandoned — fifteen distinct endings");
        lines.Add("");
        lines.Add("`on a fold` is what replaying the record performed. Equal to `live` means the " +
                  "record carries the transition; short of it means the fold is missing endings the " +
                  "rules made. `route` is read off `GoalRecord.Route` rather than described here, so " +
                  "the table cannot drift from the table the build enforces.");
        lines.Add("");
        lines.Add("| ending | live | on a fold | route | in the record |");
        lines.Add("|---|---|---|---|---|");

        foreach (GoalEnd why in Enum.GetValues<GoalEnd>())
        {
            int liveN = Sum(panel, s => s.Live.Ended.GetValueOrDefault(why));
            int foldN = Sum(panel, s => s.Folded.Ended.GetValueOrDefault(why));

            string route = Rules.GoalRecord.Route(why) switch
            {
                Rules.GoalRecord.GoalRoute.Folded => "the reducer, on the event that caused it",
                Rules.GoalRecord.GoalRoute.Host => "a key on its host event",
                _ => "`GOALS.ENDED`",
            };

            string state = liveN == 0 && foldN == 0 ? "— never reached on this panel"
                : foldN == liveN ? "**yes**"
                : $"**NO — {N(liveN - foldN)} missing**";

            lines.Add($"| `{why}` | {N(liveN)} | {N(foldN)} | {route} | {state} |");
        }

        lines.Add($"| **all endings** | **{Total(panel, s => s.Live.TotalEnded)}** " +
                  $"| **{Total(panel, s => s.Folded.TotalEnded)}** | | |");
        lines.Add("");

        int vanished = Sum(panel, s => s.Live.TotalVanished);
        lines.Add($"**Removals that found nothing to remove: {N(vanished)}.**");
        if (vanished > 0)
        {
            lines.Add("");
            lines.Add("| ending | removals of a goal already gone |");
            lines.Add("|---|---|");
            foreach (GoalEnd why in Enum.GetValues<GoalEnd>())
            {
                int n = Sum(panel, s => s.Live.Vanished.GetValueOrDefault(why));
                if (n > 0) lines.Add($"| `{why}` | {N(n)} |");
            }

            lines.Add("");
            lines.Add("These are double-counted in the `live` column above and in the §1 audit: the " +
                      "goal was cleared earlier in the same tick, and the rules then ended it a second " +
                      "time under a different label. `created − ended = live` cannot detect it — that " +
                      "identity holds by construction whatever the labels say.");
        }

        lines.Add("");

        // ---- monotonic classification
        lines.Add("### Against the monotonic sweep");
        lines.Add("");
        lines.Add("| seed | events | created | ended | peak live | decreases | live at end |");
        lines.Add("|---|---|---|---|---|---|---|");

        foreach (GoalAuditSeed s in panel)
        {
            lines.Add($"| {N((int)s.Seed)} | {N(s.Events)} | {N(s.Live.TotalCreated)} " +
                      $"| {N(s.Live.TotalEnded)} | {N(s.Live.Peak)} | {N(s.Live.Decreases)} " +
                      $"| {N(s.Live.Live)} |");
        }

        lines.Add("");
        bool comesDown = true;
        foreach (GoalAuditSeed s in panel) if (s.Live.Decreases == 0) comesDown = false;

        lines.Add(comesDown
            ? "**Goals came down on every world of the panel.** They are not the `Grievance` / " +
              "`Fealty` / `Kin` / ore family: removal paths exist, there are many of them, and they " +
              "are heavily exercised. `StandingState` declined to score this at ruleset 6 because it " +
              "sweeps through the reducer and goals were not in the fold; they are now, so it can."
            : "**At least one world never removed a goal.** That is a stronger monotonic finding than " +
              "the sweep reported and needs reading before anything is emitted.");
        lines.Add("");

        // ---- the fold gap
        lines.Add("### What a fold reproduces");
        lines.Add("");
        lines.Add($"Every component of `WorldState`, field at a time — " +
                  $"{N(panel.Count > 0 ? panel[0].Checked.Count : 0)} of them — folded from each " +
                  "world's own log and compared against the live world.");
        lines.Add("");
        lines.Add("| seed | components checked | components that differ |");
        lines.Add("|---|---|---|");

        foreach (GoalAuditSeed s in panel)
        {
            string differ = s.Divergent.Count == 0
                ? "none"
                : string.Join(", ", Quote(s.Divergent));
            lines.Add($"| {N((int)s.Seed)} | {N(s.Checked.Count)} | {differ} |");
        }

        lines.Add("");

        SortedSet<string> union = new(StringComparer.Ordinal);
        foreach (GoalAuditSeed s in panel)
            foreach (string name in s.Divergent) union.Add(name);

        lines.Add(union.Count == 0
            ? "Nothing differs. The fold reproduces the world including its goals."
            : $"Divergent components across the whole panel: {string.Join(", ", Quote([.. union]))}.");

        lines.Add("");
        lines.Add("Components covered:");
        lines.Add("");
        if (panel.Count > 0)
            foreach (string name in panel[0].Checked) lines.Add($"  - `{name}`");

        return lines;
    }

    private static IEnumerable<string> Quote(IReadOnlyList<string> names)
    {
        foreach (string name in names) yield return $"`{name}`";
    }

    private static IEnumerable<string> Names(IReadOnlyList<GoalAuditSeed> panel)
    {
        foreach (GoalAuditSeed s in panel) yield return "seed " + N((int)s.Seed);
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static int Sum(IReadOnlyList<GoalAuditSeed> panel, Func<GoalAuditSeed, int> of)
    {
        int total = 0;
        foreach (GoalAuditSeed s in panel) total += of(s);
        return total;
    }

    private static string Total(IReadOnlyList<GoalAuditSeed> panel, Func<GoalAuditSeed, int> of) =>
        N(Sum(panel, of));
}
