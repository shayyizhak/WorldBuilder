using System.Globalization;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>One retrieval path, and whether a goal row can come out of it.</summary>
/// <param name="Reached">Goal rows this path actually returned.</param>
/// <param name="Total">Records the path returned altogether, so a zero can be told from an empty run.</param>
public sealed record RetrievalProbe(string Path, string Question, int Reached, int Total, string Note);

/// <summary>The two files §2 splits the record into, and the rule that split them.</summary>
public sealed record RecordSplit(int History, int Bookkeeping, int GenesisMoved);

/// <summary>
/// Staging for a human verification session — <c>docs/loop-stage-reference-set-r7.md</c>.
///
/// <b>Nothing here verifies anything.</b> Every artefact carries
/// <see cref="ReferenceSet.Unverified"/> at the top, and every facts-sheet row carries
/// <c>verified: no</c>. The one non-regenerable cost in this project is human attention, and the
/// job is to make the reading cheaper without pretending to have done it.
///
/// <b>Why this is separate from <see cref="ReferenceSet"/>.</b> That type is in Core and takes no
/// inference at all — it is the record and the fold. Two of this loop's checks need the query
/// engine, which means the Inference assembly, and one of them needs a live model. Putting them
/// together would have made the pure derivation depend on a client it does not use.
/// </summary>
public static class ReferenceStaging
{
    // ---- §1.1 do goal rows reach retrieval? --------------------------------

    /// <summary>
    /// Topics that would pull ruleset 7's bookkeeping rows if a planner ever emitted them.
    ///
    /// Used as an adversarial probe rather than as an expectation. The point of naming it is that a
    /// report of "no goal row reached retrieval" is worthless unless something in the run
    /// demonstrates a goal row *can* be reached — otherwise it is the silent-path family again, a
    /// pass produced by a detector that cannot fire.
    /// </summary>
    public const string GoalTopic = "GOALS";

    /// <summary>
    /// Whether a goal row can reach a retrieval set, measured path by path.
    ///
    /// Four paths exist and they behave differently, so each is probed rather than the engine being
    /// characterised as a whole:
    ///
    /// <list type="bullet">
    /// <item><b>entity-scoped</b> — walks <c>Log.ForEntity</c>. Goal rows carry no participants, so
    /// they are not in any entity's history and cannot appear however the topics are set.</item>
    /// <item><b>world-scoped</b> — scans every event and excludes only secrecy, so this is the path
    /// that can reach them, and only when the plan names a matching topic.</item>
    /// <item><b>causal trace</b> — walks recorded cause edges. Goal rows cite nothing and nothing
    /// cites them, by the design decision in <c>GoalRecord.Form</c>, so they are unreachable.</item>
    /// <item><b>seat-at-year</b> — filters to the succession kinds.</item>
    /// </list>
    /// </summary>
    public static List<RetrievalProbe> GoalRowReach(QueryEngine engine, WorldView view)
    {
        List<RetrievalProbe> probes = [];

        EntityId faction = FirstFaction(view);
        EntityId ruler = view.State.FactionOf(faction).Leader;

        // 1. Entity-scoped with no topic filter at all, which is the widest an entity query gets:
        //    MatchesTopic returns true for everything when the topic list is empty.
        probes.Add(Probe(engine, view, "entity-scoped, no topic filter",
            new QueryPlan
            {
                Shape = QueryShape.Factual,
                Subject = view.State.NameOf(faction),
                Entity = faction,
                Topics = [],
                Question = "what happened to this power",
            },
            "widest entity query available — an empty topic list matches every kind"));

        // 2. Entity-scoped, asking for goal rows by name. Still nothing, because the path never
        //    consults the kind: it consults the entity index, and goal rows are not in it.
        probes.Add(Probe(engine, view, "entity-scoped, asking for GOALS",
            new QueryPlan
            {
                Shape = QueryShape.Factual,
                Subject = view.State.NameOf(faction),
                Entity = faction,
                Topics = [GoalTopic],
                Question = "what did this power want",
            },
            "adversarial: the topic is named and the path still cannot reach a row without participants"));

        // 3. World-scoped, asking for goal rows by name, over a window narrow enough to stay under
        //    the world-wide cap. This is the reachable path, and it is here precisely so the zeroes
        //    above are not the only evidence.
        //
        //    The subject has to be a world-word exactly — `IsWorldScoped` matches against a fixed
        //    set, so "which powers" is not world-scoped and "powers" is. The window matters for the
        //    same kind of reason: `WorldWide` returns nothing at all above sixty matches, on the
        //    principle that the first forty of two hundred secessions is a confident answer missing
        //    most of its subject. Seed 42 holds 76 goal rows, so asking across all 51 years trips the
        //    cap and returns zero — which reads exactly like an unreachable path. That is how the
        //    first version of this probe failed, and it is why it is bounded now.
        int mid = view.FirstYear + (view.LastYear - view.FirstYear) / 2;
        probes.Add(Probe(engine, view, "world-scoped, asking for GOALS, one decade",
            new QueryPlan
            {
                Shape = QueryShape.Factual,
                Subject = "powers",
                Entity = EntityId.None,
                Topics = [GoalTopic],
                Question = "what did the powers want",
                FromYear = mid,
                ToYear = mid + 9,
            },
            "adversarial: the one path that can reach them, so a zero elsewhere means something"));

        // 4. The same plan across the whole record, to show the cap rather than leave it inferred.
        probes.Add(Probe(engine, view, "world-scoped, asking for GOALS, all years",
            new QueryPlan
            {
                Shape = QueryShape.Factual,
                Subject = "powers",
                Entity = EntityId.None,
                Topics = [GoalTopic],
                Question = "what did the powers want",
            },
            "the same plan unbounded: above sixty matches the world-wide path returns nothing, so " +
            "this zero is the cap and not unreachability"));

        // 5. Causal trace from a real event.
        EventId anchor = LastRenderable(view);
        probes.Add(Probe(engine, view, "causal trace",
            new QueryPlan
            {
                Shape = QueryShape.Causal,
                Subject = view.State.NameOf(ruler.IsNone ? faction : ruler),
                Entity = ruler.IsNone ? faction : ruler,
                Topics = [],
                Question = anchor.IsNone ? "why did this happen" : view.Describe(anchor),
            },
            "walks recorded cause edges; goal rows have none in either direction"));

        // 6. Seat-at-year.
        probes.Add(Probe(engine, view, "seat-at-year",
            new QueryPlan
            {
                Shape = QueryShape.Factual,
                Subject = view.State.NameOf(faction),
                Entity = faction,
                Topics = ["POLITY"],
                Question = $"who ruled {view.State.NameOf(faction)} in year {view.LastYear}",
                ToYear = view.LastYear,
            },
            "filters to the succession kinds"));

        return probes;
    }

    private static RetrievalProbe Probe(
        QueryEngine engine, WorldView view, string path, QueryPlan plan, string note)
    {
        List<EventId> got = engine.Retrieve(plan);

        int goals = 0;
        foreach (EventId id in got)
            if (IsGoalRow(view.Log.Get(id))) goals++;

        return new RetrievalProbe(path, plan.Question, goals, got.Count, note);
    }

    public static bool IsGoalRow(Event e) => e.Kind is EventKind.GoalsFormed or EventKind.GoalsEnded;

    /// <summary>
    /// The §1.1 answer, as a sentence and a halt bit.
    /// </summary>
    public static (bool Halt, IReadOnlyList<string> Lines) RenderReach(
        IReadOnlyList<RetrievalProbe> probes, IReadOnlyList<RetrievalProbe> live)
    {
        List<string> lines =
        [
            "## §1.1 — do goal rows reach query retrieval?",
            "",
            "Probed per retrieval path, with two adversarial plans included so a zero is evidence " +
            "rather than an absence of evidence. A check that cannot fire is the silent-path family, " +
            "and it has appeared five times in this project already.",
            "",
            "| path | records returned | goal rows | what the path does |",
            "|---|---|---|---|",
        ];

        int reachable = 0;
        foreach (RetrievalProbe p in probes)
        {
            if (p.Reached > 0) reachable++;
            lines.Add($"| {p.Path} | {N(p.Total)} | **{N(p.Reached)}** | {p.Note} |");
        }

        lines.Add("");

        bool adversarialFired = false;
        foreach (RetrievalProbe p in probes)
            if (p.Path.Contains("world-scoped", StringComparison.Ordinal) && p.Reached > 0)
                adversarialFired = true;

        lines.Add(adversarialFired
            ? "**The detector fires.** A world-scoped plan naming `GOALS` does return goal rows, so " +
              "the zeroes on the other paths are measurements and not a broken probe."
            : "**WARNING — the detector did not fire on any path.** Every number above is therefore " +
              "uninterpretable: nothing here demonstrates a goal row can be retrieved at all, so a " +
              "zero cannot be distinguished from a probe that does not work.");
        lines.Add("");

        // The empirical half: the real path, planner included, over the archived question set.
        lines.Add("### The same question asked end to end");
        lines.Add("");
        if (live.Count == 0)
        {
            lines.Add("Not run — no model was available. The structural result above stands on its own " +
                      "for the paths it covers, but it cannot say what a *planner* emits, which is the " +
                      "half that decides whether this happens in practice.");
        }
        else
        {
            lines.Add($"{N(live.Count)} staged candidate(s) spread across the four categories, planned " +
                      "by the live model and retrieved for real. Nothing was generated — the run stops " +
                      "at retrieval, which is what the question is about.");
            lines.Add("");
            lines.Add("| question | records | goal rows | where it retrieved nothing, why |");
            lines.Add("|---|---|---|---|");

            int live_goals = 0;
            foreach (RetrievalProbe p in live)
            {
                live_goals += p.Reached;
                lines.Add($"| {Trim(p.Question)} | {N(p.Total)} | {N(p.Reached)} " +
                          $"| {(p.Note.Length == 0 ? "—" : p.Note)} |");
            }

            lines.Add("");
            lines.Add(live_goals == 0
                ? "**No goal row reached retrieval on any of them.**"
                : $"**{N(live_goals)} goal row(s) reached retrieval.** See the halt below.");

            // A question that retrieved nothing is not thereby a question that failed, and the
            // difference is the whole reason the reason column exists. A negative-premise question is
            // *supposed* to retrieve nothing: the layer is meant to notice the premise is false and
            // say so instead of answering. Counting those as empties would have reported the
            // false-premise detector working as a defect, which the first version of this paragraph
            // did.
            int refused = live.Count(static p => p.Note.StartsWith("false premise", StringComparison.Ordinal));
            int empty = live.Count(static p =>
                p.Total == 0 && !p.Note.StartsWith("false premise", StringComparison.Ordinal));

            if (refused > 0)
            {
                lines.Add("");
                lines.Add($"**{N(refused)} of {N(live.Count)} retrieved nothing because the layer " +
                          "reported the premise false** — which is the negative-premise questions " +
                          "working, not failing. Those slots exist to catch a layer that answers " +
                          "everything, and this one declined to.");
            }

            if (empty > 0)
            {
                lines.Add("");
                lines.Add($"**{N(empty)} of {N(live.Count)} retrieved nothing and gave a reason that " +
                          "is not a false premise.** The structural probes reach those questions' " +
                          "supporting records when the plan is built here, so what fails is the " +
                          "planning rather than the retrieval — a question answerable from an " +
                          "entity-bound plan and unanswerable end to end is unaskable as phrased, " +
                          "whatever its records say. It belongs in `questions.md`'s classification " +
                          "column and does not bear on §1.1.");
            }

            foreach (RetrievalProbe p in live) if (p.Reached > 0) reachable++;
        }

        lines.Add("");

        // The halt is on the *ordinary* paths, not on the adversarial one. A plan that names GOALS
        // reaching goal rows is the engine working; the question the loop asks is whether an ordinary
        // question does it.
        bool halt = false;
        foreach (RetrievalProbe p in probes)
        {
            bool adversarial = p.Note.StartsWith("adversarial", StringComparison.Ordinal);
            if (!adversarial && p.Reached > 0) halt = true;
        }

        foreach (RetrievalProbe p in live) if (p.Reached > 0) halt = true;

        lines.Add(halt
            ? "**HALT.** A retrieval set that is not an adversarial probe contains a goal row, which " +
              "changes what a correct answer looks like. The sixteen questions must not be built on " +
              "the assumption that bookkeeping is invisible to the query path."
            : "**Proceed.** No ordinary retrieval path reaches a goal row. Two structural reasons, " +
              "both worth having in writing because either could change without anybody noticing: " +
              "the rows carry **no participants**, so `Log.ForEntity` never lists them and every " +
              "entity-scoped question is blind to them by construction; and they carry **no causes " +
              "in either direction**, so a causal trace cannot walk into one. The one path that can " +
              "reach them is a world-scoped plan whose topics name `GOALS`, and nothing in the " +
              "planner's vocabulary suggests that string.");

        return (halt, lines);
    }

    // ---- §1.2 which scopes are held out? ----------------------------------

    public static IReadOnlyList<string> RenderHoldouts(
        SeedHoldouts seed, IReadOnlyList<SidecarFinding> findings, int panelHeldOut, int panelScopes)
    {
        Dictionary<string, List<SidecarFinding>> fatalByScope = new(StringComparer.Ordinal);
        foreach (SidecarFinding f in findings)
        {
            if (!f.Fatal) continue;
            if (!fatalByScope.TryGetValue(f.Scope, out List<SidecarFinding>? bucket))
                fatalByScope[f.Scope] = bucket = [];
            bucket.Add(f);
        }

        List<string> lines =
        [
            "## §1.2 — which of seed 42's scopes are held out?",
            "",
            "A question drawn from a held-out scope has no passage behind it. Marked here so §4 can " +
            "flag those questions rather than discovering it during the session.",
            "",
            "| scope | in canon | rules that fired |",
            "|---|---|---|",
        ];

        foreach (string scope in seed.Scopes)
        {
            bool held = fatalByScope.ContainsKey(scope);
            string rules = held
                ? string.Join(", ", Distinct(fatalByScope[scope]))
                : "—";

            lines.Add($"| {scope} | {(held ? "**held out**" : "yes")} | {rules} |");
        }

        int heldOut = seed.Excluded.Count;
        int scopes = seed.Scopes.Count;

        lines.Add("");
        lines.Add($"**Seed 42: {N(heldOut)} of {N(scopes)} scopes held out " +
                  $"({Percent(heldOut, scopes)}), against the panel's {N(panelHeldOut)} of " +
                  $"{N(panelScopes)} ({Percent(panelHeldOut, panelScopes)}).**");

        return lines;
    }

    private static IEnumerable<string> Distinct(List<SidecarFinding> findings)
    {
        List<string> rules = [.. findings.Select(static f => f.Rule).Distinct(StringComparer.Ordinal)];
        rules.Sort(StringComparer.Ordinal);
        return rules;
    }

    // ---- §2 the record, split by class ------------------------------------

    /// <summary>
    /// The rule that splits the record, stated once and applied by machine.
    ///
    /// <b>An event is bookkeeping where it is <c>Significance.Bookkeeping</c> <i>and names nobody</i>.</b>
    /// One clause, one principle: a record with no participants is about the world's accounting rather
    /// than about anybody in it, and a chronicle can draw on nothing it cannot attribute.
    ///
    /// <b>Significance alone is the wrong rule, and finding that out is the useful part.</b> The flag
    /// does not mean "engine internals" — it means "do not narrate this twice". `SettleCoup` marks a
    /// `POLITY.SUCCESSION` bookkeeping because the challenge event beside it already said who took the
    /// seat, and a founding succession is quiet because the faction's genesis already said it. Both are
    /// real seat changes and both are exactly what the facts sheet's ruler lists derive from, so
    /// splitting on significance put the sheet's own sources in the file the sheet is told not to read.
    /// Same for `GENESIS.FACTION`, which is where the Powers section comes from. Two meanings sharing
    /// one flag is the ambiguous-label defect this project keeps finding in its own record.
    ///
    /// What the rule sorts out, on seed 42: the yearly accounts (`ECONOMY.YIELD`), the genesis header,
    /// the two `GOALS.*` kinds, and the `absorbed` appointments that move actors between houses through
    /// `join:` deltas without naming one. Everything with a party to it stays in the history.
    /// </summary>
    public static bool IsBookkeeping(Event e) =>
        e.Significance == Significance.Bookkeeping && e.Participants.Count == 0;

    public static (RecordSplit Split, IReadOnlyList<string> History, IReadOnlyList<string> Bookkeeping)
        SplitRecord(WorldView view, string seal)
    {
        List<string> history =
        [
            $"# Seed {view.Seed} — the record a chronicle could draw on",
            "",
            ReferenceSet.Unverified,
            "",
            $"Ruleset {Ruleset.Version}, staged against seal `{seal}`.",
            "",
            "**The full record, not the `.log` view.** The view prints Minor and above, which hides " +
            "the yearly accounts and much of the economy's causal influence — a measurement taken " +
            "over it has been wrong three times.",
            "",
            "Split rule: an event is **bookkeeping** where it is `Significance == Bookkeeping` *and " +
            "names nobody* — a record with no participants is about the world's accounting rather " +
            "than about anybody in it, and a chronicle can draw on nothing it cannot attribute. " +
            "Everything with a party to it is here. See `record-bookkeeping.md` for the other half.",
            "",
        ];

        List<string> book =
        [
            $"# Seed {view.Seed} — engine bookkeeping",
            "",
            ReferenceSet.Unverified,
            "",
            $"Ruleset {Ruleset.Version}, staged against seal `{seal}`.",
            "",
            "**Nothing in this file belongs in the facts sheet.** It is emitted so a person can " +
            "confirm the split was right, not so they read it.",
            "",
            "Split rule: `Significance == Bookkeeping` **and no participants**. Significance alone " +
            "would not do: the flag means \"do not narrate this twice\", not \"engine internals\", so " +
            "it also catches the founding successions and the genesis rows that the facts sheet's " +
            "ruler lists and Powers section are derived from. Those are in `record-history.md`.",
            "",
        ];

        int nHistory = 0, nBook = 0, genesis = 0;

        // Per-file, so each gets a year heading when its own first event of that year lands and a
        // year that is entirely bookkeeping leaves no empty heading in the history file.
        int historyYear = int.MinValue, bookYear = int.MinValue;

        foreach (Event e in view.Log.Events)
        {
            bool bookkeeping = IsBookkeeping(e);
            List<string> into = bookkeeping ? book : history;
            int seen = bookkeeping ? bookYear : historyYear;

            if (e.Year != seen)
            {
                if (bookkeeping) bookYear = e.Year; else historyYear = e.Year;
                into.Add("");
                into.Add($"### Year {N(e.Year)}");
                into.Add("");
            }

            into.Add(Line(view, e));

            if (bookkeeping) nBook++;
            else
            {
                nHistory++;
                if (e.Significance == Significance.Bookkeeping) genesis++;
            }
        }

        history.Add("");
        history.Add($"**{N(nHistory)} record(s)**, of which {N(genesis)} are quiet rows the split rule " +
                    "keeps here because they name somebody — foundings, births, comings of age and the " +
                    "successions that another event narrated.");

        book.Add("");
        book.Add($"**{N(nBook)} record(s).**");

        return (new RecordSplit(nHistory, nBook, genesis), history, book);
    }

    /// <summary>
    /// One record, with everything a reader checking a derivation needs: id, kind, participants,
    /// causes, outcome and the full payload.
    ///
    /// The payload is not abbreviated. §2's whole reason for existing is that the `.log` view hides
    /// things, and a staging file that hid a different set of things would be the same defect with a
    /// different boundary.
    /// </summary>
    private static string Line(WorldView view, Event e)
    {
        StringBuilder sb = new();
        sb.Append("- `").Append(e.Id).Append("` **").Append(EventKinds.Name(e.Kind)).Append("**");

        if (e.Outcome != Outcome.NotApplicable) sb.Append(" [").Append(e.Outcome).Append(']');
        if (e.Scope != Visibility.Public) sb.Append(" [").Append(e.Scope).Append(']');
        if (e.Significance == Significance.Bookkeeping) sb.Append(" [bookkeeping]");

        foreach (Participant p in e.Participants)
            sb.Append(' ').Append(p.Role.ToString().ToLowerInvariant()).Append('=')
              .Append(view.State.Label(p.Id));

        if (!e.Arc.IsNone) sb.Append(" arc=").Append(view.State.Label(e.Arc));

        if (e.Causes.Count > 0)
        {
            sb.Append(" because=");
            for (int i = 0; i < e.Causes.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(e.Causes[i]);
            }
        }

        if (e.Data.Count > 0)
        {
            sb.Append(" {");
            for (int i = 0; i < e.Data.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(e.Data[i].Key).Append('=').Append(e.Data[i].Value);
            }

            sb.Append('}');
        }

        return sb.ToString();
    }

    // ---- §3 the skeleton facts sheet ---------------------------------------

    /// <summary>Every row is a claim nobody has checked, and says so.</summary>
    private const string NotVerified = "`verified: no`";

    /// <summary>
    /// The facts sheet, in the verification protocol's §2 order — highest downstream dependence
    /// first, so a session that runs short leaves the load-bearing items done.
    ///
    /// <b>Every row carries its derivation, not just its number.</b> <c>474 = 185 + 133 + 156,
    /// plague deaths Y26–28, records e:…</c> and never <c>474 dead</c>: a count without its
    /// derivation cannot later distinguish "the world changed" from "the way we counted changed",
    /// which now matters more because the world has changed four times.
    /// </summary>
    public static IReadOnlyList<string> FactsSheet(WorldView view, string seal, SeedHoldouts holdouts)
    {
        WorldState state = view.State;
        RelationTrajectory.Report ties = RelationTrajectory.Of(view.Log, view.Seed, state.Board);

        List<string> lines =
        [
            $"# Candidate facts sheet — seed {view.Seed}, ruleset {Ruleset.Version}",
            "",
            ReferenceSet.Unverified,
            "",
            $"Staged against seal `{seal}`. **A later ruleset change must invalidate this sheet " +
            "visibly** — that is what the seal is here for, and a row re-used under ruleset 8 " +
            "without re-checking it against a new seal is a row about a world that no longer exists.",
            "",
            $"{N(view.Log.Count)} records, years {N(view.FirstYear)}–{N(view.LastYear)}. " +
            $"Every row below is {NotVerified}.",
            "",
        ];

        Section1Seats(lines, view, holdouts);
        Section2Powers(lines, view);
        Section3Counts(lines, view, ties);
        Section4FalsePremises(lines, view);

        return lines;
    }

    /// <summary>
    /// §2 item 1 — seats and ruler lists. Nothing downstream questions these.
    ///
    /// <b>Derived from the record directly, and the collapsed list is shown beside the raw moves.</b>
    /// The protocol is explicit that a ruler list must not come from the ruler-list derivation: "no
    /// duplicate in the list" is satisfied both by collapsing a contested transfer correctly and by
    /// deleting a genuine second tenure, and those are opposite errors invisible from the output.
    /// </summary>
    private static void Section1Seats(List<string> lines, WorldView view, SeedHoldouts holdouts)
    {
        WorldState state = view.State;

        lines.Add("## 1. Seats and ruler lists");
        lines.Add("");
        lines.Add("**Read the raw moves, not the collapsed list.** Both are here. The collapse rule is " +
                  "*same person, same seat, same year collapses; different years does not* — and every " +
                  "seat where it fires is flagged below with both record ids, so the collapse can be " +
                  "checked by hand rather than trusted.");
        lines.Add("");
        lines.Add("**A tenure ends when the faction does.** The terminal hold closes at the house's " +
                  "`POLITY.COLLAPSE` year where there is one, and at the last year of the record " +
                  "otherwise. Without that branch three of this world's seats claimed a holder for " +
                  "years after the house under him was gone.");
        lines.Add("");
        lines.Add("**How a hold ended is resolved against person *and* faction, inside the hold's own " +
                  "years.** One death record was closing two tenures on two seats — the same man led " +
                  "two houses in one life — and it can only be right about one of them. The term " +
                  $"`{ReferenceSet.FactionEnded}` is the fall-through: cast out, killed, died and " +
                  $"`{ReferenceSet.StillHolding}` all say something about the person, and a seat that " +
                  "stopped existing says nothing about who last sat on it.");
        lines.Add("");

        List<SeatRepeat> contested = SeatTransfers.Contested(view);

        foreach (Faction f in state.Factions)
        {
            List<SeatMove> moves = SeatTransfers.Moves(view, f.Id);
            if (moves.Count == 0) continue;

            List<SeatSpell> spells = ReferenceSet.SeatHistory(view, f.Id);

            lines.Add($"### {f.Name} ({f.Id})");
            lines.Add("");
            lines.Add($"- claim: {spells.Count} hold(s) of this seat — " +
                      string.Join(", ", spells.Select(s =>
                          $"{state.NameOf(s.Ruler)} {N(s.From)}–{(s.Open ? "" : N(s.To))} ({s.Ended})")));
            lines.Add($"- derivation: {moves.Count} seat-moving record(s) collapsed to {spells.Count} hold(s) — " +
                      string.Join("; ", moves.Select(m =>
                          $"{N(m.Year)} {state.NameOf(m.Ruler)} via {EventKinds.Name(m.Via)} {m.Id}")));
            lines.Add($"- scope: this seat only, years {N(view.FirstYear)}–{N(view.LastYear)}");
            lines.Add($"- {NotVerified}");

            List<SeatRepeat> here = [.. contested.Where(r => r.Faction == f.Id)];
            if (here.Count > 0)
            {
                lines.Add("");
                lines.Add("  **Contested transfer — check this collapse by hand:**");
                foreach (SeatRepeat r in here)
                {
                    lines.Add($"  - {r.Describe(state)}");
                    lines.Add($"    - both records: `{r.First.Id}` ({EventKinds.Name(r.First.Via)}, " +
                              $"Y{N(r.First.Year)}) and `{r.Second.Id}` ({EventKinds.Name(r.Second.Via)}, " +
                              $"Y{N(r.Second.Year)})");
                    lines.Add($"    - the rule {(r.First.Year == r.Second.Year ? "collapses" : "does **not** collapse")} " +
                              $"these, because the years {(r.First.Year == r.Second.Year ? "match" : "differ")}");
                    lines.Add($"    - reached by the adjacency rule: {(r.ReachedByCollapse ? "yes" : "**no**")}");
                }
            }

            lines.Add("");
        }

        if (contested.Count == 0)
        {
            lines.Add("**No contested transfer anywhere in this world.** The collapse rule never " +
                      "fires, so no ruler row rests on it — which is a weaker reason for the list " +
                      "being right than the rule handling it, and worth stating as such.");
            lines.Add("");
        }

        Held(lines, holdouts);
    }

    private static void Section2Powers(List<string> lines, WorldView view)
    {
        WorldState state = view.State;

        lines.Add("## 2. Powers, foundings, secessions, collapses");
        lines.Add("");

        foreach (Faction f in state.Factions)
        {
            EventId origin = view.Log.OriginOf(f.Id);
            lines.Add($"- **{f.Name}** ({f.Id}) — founded by `{origin}` " +
                      $"({(view.Log.TryGet(origin, out Event o) ? EventKinds.Name(o.Kind) + " Y" + N(o.Year) : "no record")}), " +
                      $"seat {state.Label(f.Seat)}, {N(state.HoldingCount(f.Id))} holding(s) at the end. {NotVerified}");
        }

        lines.Add("");

        foreach ((EventKind kind, string what) in new[]
                 {
                     (EventKind.PolitySecession, "Secessions"),
                     (EventKind.PolityPartition, "Partitions"),
                     (EventKind.PolityCollapse, "Collapses"),
                 })
        {
            List<Event> found = [.. view.Log.Events.Where(e => e.Kind == kind)];

            lines.Add($"### {what}");
            lines.Add("");
            if (found.Count == 0)
            {
                lines.Add($"- claim: none. {NotVerified}");
            }
            else
            {
                lines.Add($"- claim: {N(found.Count)}");
                lines.Add("- derivation: " + string.Join("; ", found.Select(e =>
                    $"Y{N(e.Year)} {(e.Where.IsNone ? state.Label(e.Faction) : state.Label(e.Where))} `{e.Id}`")));
                lines.Add($"- scope: whole world, years {N(view.FirstYear)}–{N(view.LastYear)}");
                lines.Add($"- {NotVerified}");
            }

            lines.Add("");
        }
    }

    /// <summary>
    /// §2 item 3 — counts and spans, each with its arithmetic written out.
    ///
    /// <b>Terminated relations state a span, not a pair.</b> There is no tombstone in
    /// <c>RelationGraph</c> by design, so the log is the only place *ended* differs from *never
    /// existed*, and a trade or alliance fact without a span is incomplete rather than terse.
    /// </summary>
    private static void Section3Counts(List<string> lines, WorldView view, RelationTrajectory.Report ties)
    {
        WorldState state = view.State;

        lines.Add("## 3. Counts and spans");
        lines.Add("");
        lines.Add("Every figure below is `claim = parts, population, records`. A bare total is an " +
                  "unlabelled figure, which this project treats as a fabrication vector regardless of " +
                  "who reads it next.");
        lines.Add("");

        // Runs of famine and plague, where the per-year parts are the whole point.
        foreach ((EventKind kind, string what, string field) in new[]
                 {
                     (EventKind.EconomyPlague, "Plague deaths", "deaths"),
                     (EventKind.EconomyFamine, "Famine deaths", "deaths"),
                 })
        {
            List<(EntityId Where, List<Event> Run)> runs = ReferenceSet.Runs(view, kind);
            if (runs.Count == 0) continue;

            lines.Add($"### {what}");
            lines.Add("");

            foreach ((EntityId where, List<Event> run) in runs)
            {
                List<int> parts = [];
                List<string> ids = [];
                foreach (Event e in run)
                {
                    parts.Add(Math.Abs(e.GetInt($"pop:{where}")));
                    ids.Add(e.Id.ToString());
                }

                int total = parts.Sum();
                lines.Add($"- claim: {N(total)} at {state.Label(where)}");
                lines.Add($"  - derivation: `{N(total)} = {string.Join(" + ", parts.Select(N))}`, " +
                          $"{what.ToLowerInvariant()} Y{N(run[0].Year)}–Y{N(run[^1].Year)}, " +
                          $"records {string.Join(", ", ids)}");
                lines.Add($"  - scope: this place, this run only — **not** a world total and not a faction lifetime");
                lines.Add($"  - {NotVerified}");
            }

            lines.Add("");
        }

        // Simple kind counts, still with their record ids so a reader can re-derive rather than re-read.
        lines.Add("### Event counts by kind");
        lines.Add("");
        lines.Add("| what | claim | derivation | scope |");
        lines.Add("|---|---|---|---|");

        foreach ((EventKind kind, string what) in new[]
                 {
                     (EventKind.LifeDeathViolent, "killings"),
                     (EventKind.LifeDeathNatural, "natural deaths"),
                     (EventKind.ConflictRaid, "raids"),
                     (EventKind.ConflictBattle, "battles"),
                     (EventKind.ConflictConquest, "conquests"),
                     (EventKind.PolityExile, "exiles"),
                     (EventKind.PolityExileReturn, "returns from exile"),
                     (EventKind.LifeMarriage, "marriages"),
                 })
        {
            List<Event> found = [.. view.Log.Events.Where(e => e.Kind == kind)];
            string ids = found.Count == 0
                ? "no records"
                : found.Count <= 12
                    ? string.Join(", ", found.Select(static e => e.Id.ToString()))
                    : string.Join(", ", found.Take(12).Select(static e => e.Id.ToString())) +
                      $", … ({N(found.Count - 12)} more)";

            lines.Add($"| {what} | {N(found.Count)} | {EventKinds.Name(kind)} × {N(found.Count)}: {ids} " +
                      $"| whole world, Y{N(view.FirstYear)}–Y{N(view.LastYear)} |");
        }

        lines.Add("");
        lines.Add($"All {NotVerified}. **Raids split three ways** — the render rounds got this wrong " +
                  "repeatedly — so a raid count is incomplete without its outcomes:");
        lines.Add("");

        int raidWon = 0, raidLost = 0, raidNothing = 0;
        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid) continue;
            if (e.Outcome != Outcome.Succeeded) raidLost++;
            else if (e.GetInt("loot") > 0) raidWon++;
            else raidNothing++;
        }

        lines.Add($"- claim: {N(raidWon + raidLost + raidNothing)} raids = {N(raidWon)} carried off " +
                  $"plunder + {N(raidNothing)} succeeded and took nothing + {N(raidLost)} beaten off");
        lines.Add($"  - derivation: `Outcome == Succeeded` split on the `loot` payload key being above zero");
        lines.Add($"  - {NotVerified}");
        lines.Add("");

        // Role-and-outcome: the Paernmel Has shape.
        lines.Add("### Where role and outcome both decide the count");
        lines.Add("");
        lines.Add("**The discriminator, written out:** an assassination record names a subject (who " +
                  "acted), an object (who it was done to) and an outcome. **Role and outcome are two " +
                  "questions, and both apply to both roles** — so a person appears in four ways: the " +
                  "target of an attempt that failed, the target of the one that worked, the sponsor of " +
                  "a killing that succeeded, and the sponsor of an attempt that failed. Adding all " +
                  "four gives the number of records, which answers no question anybody asked.");
        lines.Add("");
        lines.Add("**The fourth column was added because the third was mislabelled.** It used to " +
                  "pool everything a person sponsored under *killings they ordered*, and the three " +
                  "columns did add up to the record count — so the arithmetic was right and the label " +
                  "was not. For nine of this world's people the only sponsorship was a botched " +
                  "attempt, and *how many killings did he order?* read one from a table whose honest " +
                  "answer is none. The section's own lesson, half-applied: the target's side split on " +
                  "outcome and the sponsor's side did not.");
        lines.Add("");

        List<AttemptTally> attempts = ReferenceSet.Attempts(view);
        if (attempts.Count == 0)
        {
            lines.Add("No assassination record in this world, so the shape does not arise here.");
        }
        else
        {
            lines.Add("| person | records naming them | failed attempts on them | killed " +
                      "| killings they ordered | attempts they ordered that failed |");
            lines.Add("|---|---|---|---|---|---|");
            foreach (AttemptTally t in attempts)
            {
                lines.Add($"| {state.Label(t.Actor)} | {N(t.Records)} | {N(t.FailedAgainst)} " +
                          $"| {N(t.KilledBy)} | {N(t.Ordered)} | {N(t.OrderedFailed)} |");
            }

            int partition = attempts.Count(static t => t.Partitions);

            lines.Add("");
            lines.Add("Every row above lists records rather than a total, per the rule. " +
                      $"**The four columns partition the record count on {N(partition)} of " +
                      $"{N(attempts.Count)} row(s)** — a check on the split rather than a claim about " +
                      "it, and the reason the mislabelled three-column version was not caught by " +
                      $"arithmetic. All {NotVerified}.");

            // A sponsor whose every sponsorship failed is the case the old label got wrong, and it
            // is named rather than left for a reader to notice, because it is the whole finding.
            List<AttemptTally> botchedOnly =
                [.. attempts.Where(static t => t.Ordered == 0 && t.OrderedFailed > 0)];

            if (botchedOnly.Count > 0)
            {
                lines.Add("");
                lines.Add($"**{N(botchedOnly.Count)} person(s) sponsored an attempt and never a " +
                          "killing** — asked *how many killings did they order*, the answer is none " +
                          "for every one of them: " +
                          string.Join(", ", botchedOnly.Select(t =>
                              $"{state.NameOf(t.Actor)} ({N(t.OrderedFailed)})")) + ".");
            }
        }

        lines.Add("");

        // Terminated relations, with spans.
        lines.Add("### Terminated relations — every fact states a span");
        lines.Add("");
        lines.Add("New engine behaviour at ruleset 6. A tie that ended is a different fact from one " +
                  "that never existed, and the log is the only place the distinction lives — there is " +
                  "no tombstone in `RelationGraph`, by design. **A fact about who traded with whom is " +
                  "incomplete without the span.**");
        lines.Add("");

        if (ties.Terminations.Count == 0)
        {
            lines.Add("No relation ended in this world. Every tie the graph holds at the end has held " +
                      "since it was made, and no fact here needs a closing year — which is itself a " +
                      $"claim to check. {NotVerified}");
        }
        else
        {
            lines.Add("**Both ends of every span are folded from the record**, not read off the " +
                      "closing event. Only `ECONOMY.TRADE_COLLAPSE` carries a `made` key, so a span " +
                      "opened from payload alone opened with `?` on every alliance and every war — " +
                      "and there are 4 `DIPLO.ALLIANCE_FORMED` records in this world against dozens " +
                      "of payload keys setting an alliance, almost all of them on a marriage. That " +
                      "is `RelationTrajectory`'s own first defect inverted, so the answer is the " +
                      "same one: replay through `EventReducer` and read the graph it produces.");
            lines.Add("");
            lines.Add("A `?` opening year would now mean *the record holds no making for this tie*, " +
                      "not *the derivation did not look* — which is the absent-versus-unknown " +
                      "distinction this table exists to keep. Any such row says so in words.");
            lines.Add("");
            lines.Add("| tie | span | ended by | cause named | record |");
            lines.Add("|---|---|---|---|---|");

            foreach (Termination t in ties.Terminations)
            {
                lines.Add($"| {t.Kind} {state.Label(t.From)} ↔ {state.Label(t.To)} | {t.Span} " +
                          $"| {EventKinds.Name(t.Via)} | {t.Cause} | `{t.At}` |");
            }

            int unopened = ties.Terminations.Count(static t => t.Made is null);

            lines.Add("");
            lines.Add($"{N(ties.Terminations.Count)} termination(s), " +
                      (unopened == 0
                          ? "every one with both ends of its span from the record"
                          : $"**{N(unopened)} of them with no making anywhere in the record** — " +
                            "which is a finding about the log rather than about this table") +
                      $". All {NotVerified}.");
        }

        lines.Add("");

        // Dispersion figures keep their emission labels.
        lines.Add("### Dispersion");
        lines.Add("");
        lines.Add("**Labels are kept exactly as emitted** (`sd=`, `range=[a, b] width=`, `cv=`, " +
                  "`ci95=`, `var=`) and must not be stripped when transcribing. A range read as a " +
                  "spread is one of the three ambiguous-figure defects this project has already paid " +
                  "for.");
        lines.Add("");

        List<int> perYear = [];
        for (int y = view.FirstYear; y <= view.LastYear; y++)
            perYear.Add(view.Log.Events.Count(e => e.Year == y));

        if (perYear.Count > 1)
        {
            int lo = perYear.Min(), hi = perYear.Max();
            lines.Add($"- records per year: `range=[{N(lo)}, {N(hi)}] width={N(hi - lo)}`, " +
                      $"n={N(perYear.Count)} years");
            lines.Add($"  - derivation: every record's `year` field, counted; " +
                      $"total {N(view.Log.Count)} over {N(perYear.Count)} years");
            lines.Add($"  - scope: whole record including bookkeeping — see `record-bookkeeping.md`");
            lines.Add($"  - {NotVerified}");
        }

        lines.Add("");
    }

    private static void Section4FalsePremises(List<string> lines, WorldView view)
    {
        lines.Add("## 4. Candidate false premises");
        lines.Add("");
        lines.Add("Claims that are *plausibly* true and are not. These are what let a query suite " +
                  "fail: a layer that answers everything cannot be caught by questions that all have " +
                  "answers.");
        lines.Add("");

        List<ReferenceSet.FalsePremise> premises = ReferenceSet.FalsePremises(view);
        if (premises.Count == 0)
        {
            lines.Add("None found. That is a finding rather than a gap — §4 of the protocol wants at " +
                      "least three questions built on these, and without candidates the coverage " +
                      "requirement cannot be met.");
            lines.Add("");
            return;
        }

        // Grouped by shape, because three questions of one shape test one thing three times.
        foreach (ReferenceSet.FalsePremise p in premises)
        {
            lines.Add($"- **[{p.Shape}]** {p.Fact}");
            lines.Add($"  - question it supports: {p.Question}");
            lines.Add(p.Records.Count == 0
                ? "  - records: none — this rests on the record *not* containing something, which is " +
                  "the point and also why it cannot be checked by looking one thing up"
                : "  - records that make it checkable: " + string.Join(", ", p.Records.Select(static r => r.ToString())));
            lines.Add($"  - {NotVerified}");
        }

        SortedDictionary<string, int> byShape = new(StringComparer.Ordinal);
        foreach (ReferenceSet.FalsePremise p in premises)
            byShape[p.Shape] = byShape.GetValueOrDefault(p.Shape) + 1;

        lines.Add("");
        lines.Add("By shape: " + string.Join(", ", byShape.Select(kv => $"{kv.Key} × {N(kv.Value)}")) +
                  ". §4 wants three questions from these and they should not all be one shape.");

        lines.Add("");
        lines.Add($"{N(premises.Count)} candidate(s).");
        lines.Add("");
    }

    private static void Held(List<string> lines, SeedHoldouts holdouts)
    {
        if (holdouts.Excluded.Count == 0) return;

        lines.Add("**Scopes held out of canon**, so a claim drawn from one has no passage behind it:");
        foreach (HeldOut h in holdouts.Excluded) lines.Add($"  - {h.Scope} ({string.Join(", ", h.Rules)})");
        lines.Add("");
    }

    // ---- §4 candidate questions --------------------------------------------

    /// <summary>What a candidate question carries, and what the machine could work out about it.</summary>
    /// <param name="Wrong">What a wrong answer would look like — the field that makes a question able to fail.</param>
    /// <param name="Causal">Records the causal retrieval path returned.</param>
    /// <param name="Factual">Records the factual retrieval path returned.</param>
    /// <param name="Reaches">Whether both paths returned at least one of the supporting records.</param>
    public sealed record Candidate(
        string Text,
        string Answer,
        IReadOnlyList<EventId> Records,
        string Wrong,
        string Category,
        bool TurnsOnAYear,
        string Scope,
        int Causal,
        int Factual,
        bool Reaches)
    {
        public bool SuiteEligible => Reaches;
    }

    /// <summary>Category labels, so the coverage requirements can be counted rather than eyeballed.</summary>
    public const string NegativePremise = "negative-premise";
    public const string SuppliedFigure = "supplied-figure";
    public const string TerminatedRelation = "terminated-relation";
    public const string Ordinary = "ordinary";

    /// <summary>
    /// Candidate questions, with each one's retrieval measured under both classifications.
    ///
    /// <b>Both paths are run with plans built here, not by the planner.</b> §4's requirement is that
    /// the correct answer be reachable under either classification, and the planner's classification
    /// is the thing that is unstable — the same question with a byte-identical body was classified
    /// causal in one run and factual in another. Asking the planner would measure the instability;
    /// building both plans measures what the loop actually needs to know, which is whether the answer
    /// survives either outcome. It is also deterministic, so a re-run reproduces the table.
    ///
    /// <b>"Reachable" is a proxy, and a stated one.</b> A machine cannot tell whether an answer is
    /// correct. What it can tell is whether the records the answer rests on came back, and that is
    /// what `Reaches` means: both paths returned at least one supporting record. A person still has
    /// to read the answer.
    /// </summary>
    public static List<Candidate> Questions(
        QueryEngine engine, WorldView view, SeedHoldouts holdouts, RelationTrajectory.Report ties)
    {
        WorldState state = view.State;
        List<Candidate> made = [];

        HashSet<string> heldOut = new(StringComparer.Ordinal);
        foreach (HeldOut h in holdouts.Excluded) heldOut.Add(h.Scope);

        // --- ruler and seat questions, the highest-dependence rows -----------
        foreach (Faction f in state.Factions)
        {
            List<SeatSpell> spells = ReferenceSet.SeatHistory(view, f.Id);
            if (spells.Count == 0) continue;

            made.Add(Make(engine, view, heldOut,
                $"Who has ruled {f.Name}?",
                string.Join(", ", spells.Select(s => $"{state.NameOf(s.Ruler)} {N(s.From)}–{N(s.To)}")),
                SeatRecords(view, f.Id),
                $"a list missing {state.NameOf(spells[0].Ruler)}, or one naming somebody who never held it, " +
                "or one that collapses two separate tenures of the same person into a single span",
                Ordinary, turnsOnAYear: false, f.Id, [f.Id.ToString(), "POLITY"]));

            // A transition year has two defensible answers and therefore cannot fail correctly.
            // Where no hold on this seat has an interior year, no question is emitted at all —
            // a seat asked about with no unambiguous year is a seat with no question, not a
            // question with a year picked anyway.
            if (Interior(spells) is { } inside)
            {
                SeatSpell hold = inside.Hold;
                made.Add(Make(engine, view, heldOut,
                    $"Who ruled {f.Name} in year {N(inside.Year)}?",
                    $"{state.NameOf(hold.Ruler)}, who held it {N(hold.From)}–{N(hold.To)} — " +
                    $"{N(inside.Year)} is inside that hold and on neither of its edges",
                    SeatRecords(view, f.Id),
                    $"the previous holder, or silence — a seat changes hands rarely and almost never in " +
                    "the year asked about, so a window read literally finds nothing and reads exactly " +
                    "like a world in which nobody ruled",
                    Ordinary, turnsOnAYear: true, f.Id, [f.Id.ToString(), "POLITY"], toYear: inside.Year));
            }
        }

        // --- why-questions over the structural turns -------------------------
        foreach (EventKind kind in new[]
                 {
                     EventKind.PolitySecession, EventKind.PolityCollapse,
                     EventKind.DiploWarDeclared, EventKind.ConflictConquest,
                 })
        {
            foreach (Event e in view.Log.Events)
            {
                if (e.Kind != kind) continue;

                EntityId about = e.Faction.IsNone ? e.Where : e.Faction;
                if (about.IsNone) continue;

                // A genesis row is not a cause, so it is not offered as one. Two of seed 42's four
                // causal questions used to answer *why was war declared over Threi Cut* with the
                // record of Threi Cut coming into existence, which is where the walk stopped rather
                // than what it found. Same rule as `ContextPackBuilder.Trace`, applied to the same
                // records, so the staged answer and the retrieval path agree about what a cause is.
                List<EventId> causes =
                    [.. e.Causes.Where(c => view.Log.TryGet(c, out Event at) &&
                                            !ContextPackBuilder.IsGenesis(at))];

                made.Add(Make(engine, view, heldOut,
                    $"Why did {Describe(state, kind, e)} in year {N(e.Year)}?",
                    Because(view, e, causes),
                    [e.Id, .. causes],
                    causes.Count == 0
                        ? "any reason at all. The record gives none, so a confident answer here is " +
                          "invented whatever it says — and an answer citing the world's own genesis " +
                          "is the same mistake the derivation used to make"
                        : "a motive nobody recorded, or the ancestry of a different event of the " +
                          "same kind in another decade. **Naming the record without the reason is " +
                          "also wrong**: the id is the citation, not the answer, and a response that " +
                          "cites it while describing the wrong event has not answered the question",
                    Ordinary, turnsOnAYear: true, about, [Family(kind)]));

                break;   // one per kind is enough; the shape repeats
            }
        }

        // --- supplied-figure questions ---------------------------------------
        foreach (EventKind kind in new[] { EventKind.EconomyPlague, EventKind.EconomyFamine })
        {
            List<(EntityId Where, List<Event> Run)> runs = ReferenceSet.Runs(view, kind);
            string what = kind == EventKind.EconomyPlague ? "plague" : "famine";

            foreach ((EntityId where, List<Event> run) in runs)
            {
                int total = run.Sum(e => Math.Abs(e.GetInt($"pop:{where}")));
                if (total == 0) continue;

                // <b>The span goes in the question wherever the place had more than one episode.</b>
                // Meigate suffered three separate famines, so "how many died in the famine at
                // Meigate" has three defensible answers and cannot fail — a question with more than
                // one right answer is worse than no question, because a suite scores it as passing
                // whichever the layer says. Only a place with exactly one episode of a kind can be
                // asked about without a year.
                int episodes = runs.Count(r => r.Where == where);
                bool needsSpan = episodes > 1;

                string span = run[0].Year == run[^1].Year
                    ? $"in year {N(run[0].Year)}"
                    : $"between {N(run[0].Year)} and {N(run[^1].Year)}";

                made.Add(Make(engine, view, heldOut,
                    needsSpan
                        ? $"How many died in the {what} at {state.NameOf(where)} {span}?"
                        : $"How many died in the {what} at {state.NameOf(where)}?",
                    $"{N(total)} — and the figure must be restated, not summarised",
                    [.. run.Select(static e => e.Id)],
                    $"\"hundreds\", \"many\", or any phrasing that does not contain {N(total)}. A " +
                    "supplied figure going unused is caught by nothing — no rule covers it, so a " +
                    "suite question is the only detector there is" +
                    (needsSpan
                        ? $". {state.NameOf(where)} suffered {N(episodes)} separate {what}s, so an " +
                          "answer about the wrong one is also wrong"
                        : ""),
                    SuppliedFigure, turnsOnAYear: needsSpan, where, ["ECONOMY"],
                    fromYear: needsSpan ? run[0].Year : int.MinValue,
                    toYear: needsSpan ? run[^1].Year : int.MaxValue));

                break;
            }
        }

        // --- terminated-relation questions -----------------------------------
        foreach (Termination t in ties.Terminations)
        {
            made.Add(Make(engine, view, heldOut,
                $"Did {state.NameOf(t.From)} and {state.NameOf(t.To)} ever hold a {t.Kind} tie?",
                t.Made is { } from
                    ? $"Yes, from {N(from)} to {N(t.Year)}. **Ended, not never-existed.**"
                    : $"Yes — it ended in {N(t.Year)}, and no record in this world makes it, so the " +
                      "opening year is unknown rather than unlooked-for.",
                [t.At],
                "\"no\" or \"there is no record of one\" — which is the absent-versus-ended " +
                "conflation, and the graph holds no tombstone so the log is the only place the " +
                "difference lives. An answer without a span is also wrong, even when it says yes",
                TerminatedRelation, turnsOnAYear: true, t.From, [Family(t.Via)]));

            if (made.Count(c => c.Category == TerminatedRelation) >= 2) break;
        }

        // --- negative premises ------------------------------------------------
        //
        // Planned against the subject the premise names, not against nothing. The premise is false;
        // the subject is not — the person exists and their records come back, which is exactly what
        // makes the question a trap. Planning these with no entity retrieved nothing at all and
        // marked all five `classification-sensitive`, which was a fact about the probe.
        foreach (ReferenceSet.FalsePremise p in ReferenceSet.FalsePremises(view))
        {
            made.Add(Make(engine, view, heldOut,
                p.Question,
                $"the premise is false: {p.Fact}",
                p.Records,
                "any answer that accepts the premise and supplies a name, a year or a reason — a " +
                "layer that answers everything cannot be caught by questions that all have answers",
                NegativePremise, turnsOnAYear: false, p.About, []));
        }

        // --- counts, spans and the role-and-outcome shape ---------------------
        foreach (AttemptTally t in ReferenceSet.Attempts(view))
        {
            if (t.Records < 3) continue;

            made.Add(Make(engine, view, heldOut,
                $"How many times was {state.NameOf(t.Actor)} the target of a failed attempt?",
                $"{N(t.FailedAgainst)} — of {N(t.Records)} records naming him; {N(t.KilledBy)} " +
                $"killed him, {N(t.Ordered)} were killings he ordered and {N(t.OrderedFailed)} were " +
                "attempts he ordered that failed",
                AssassinationRecords(view, t.Actor),
                $"{N(t.Records)}, which is the number of records rather than the answer to the " +
                $"question; or {N(t.Ordered + t.OrderedFailed)}, which pools the killings he ordered " +
                "with the attempts that failed — role and outcome both decide this count, and both " +
                "of them decide it on both sides",
                Ordinary, turnsOnAYear: false, t.Actor, ["CONFLICT.ASSASSINATION"]));
        }

        foreach ((EventKind kind, string what) in new[]
                 {
                     (EventKind.ConflictRaid, "raids"),
                     (EventKind.ConflictBattle, "battles"),
                     (EventKind.PolityExile, "exiles"),
                 })
        {
            foreach (Faction f in state.Factions)
            {
                int n = view.Log.ForEntity(f.Id).Count(id => view.Log.Get(id).Kind == kind);
                if (n < 2) continue;

                made.Add(Make(engine, view, heldOut,
                    $"How many {what} involved {f.Name}?",
                    $"{N(n)}",
                    [.. view.Log.ForEntity(f.Id).Where(id => view.Log.Get(id).Kind == kind)],
                    $"a figure that counts the whole world's {what} rather than this power's, or one " +
                    "that silently drops the ones it lost — a statistic carries a scope and a " +
                    "faction-lifetime figure restated inside a reign is a live defect class",
                    Ordinary, turnsOnAYear: false, f.Id, [Family(kind)]));

                break;
            }
        }

        return made;
    }

    /// <summary>
    /// The answer to a why-question, in words, with the record beside it rather than instead of it.
    ///
    /// <b>An id is a lookup instruction, not an answer.</b> These four candidates used to read
    /// <c>the recorded causes, walked back: e:506</c>, which nothing can be held against: any
    /// response mentioning `e:506` satisfies it, and one naming the wrong person while citing the
    /// right record passes. Every other category in the file states its answer in words, and the
    /// field that makes a question able to fail — *what would a wrong answer look like* — cannot be
    /// written at all for an answer that is a pointer.
    ///
    /// So the cause is described: *because Sou Dra was exiled from the Kebarrow Compact (`e:506`)*
    /// is a claim a reader can hold against the record and watch fail.
    /// </summary>
    private static string Because(WorldView view, Event e, IReadOnlyList<EventId> causes)
    {
        if (causes.Count == 0)
        {
            return e.Causes.Count == 0
                ? "the record names no cause for it, and the honest answer says so"
                : "the record names no cause for it beyond the world's own genesis, which is where " +
                  "the walk stops rather than what it found — so the honest answer says so";
        }

        // Not truncated. This is the answer a reader checks the layer against, and half a sentence
        // is a different claim from the one the record makes.
        return "because " + string.Join("; and because ", causes.Select(c =>
            $"{view.Describe(c)} (`{c}`)"));
    }

    /// <summary>
    /// A year inside one hold and on neither of its edges, with the hold it came from.
    ///
    /// <b>Every "who ruled in year N" question this loop staged named a transition year</b> — all
    /// five of them, because the year picked was the year the last holder took the seat, which is
    /// by construction the year the one before him lost it. Two people held that seat that year and
    /// the record supports naming either, so the question cannot fail correctly: a suite scores it
    /// as passing whichever the layer says. Same defect as the Meigate famine question, which was
    /// caught because a place had suffered three famines and "the famine" named none of them.
    ///
    /// A hold's edges are shared with its neighbours — the year it opens is the year the previous
    /// hold closes, and the year it closes is the year the next one opens — so the interior is
    /// strictly between them. A hold of one or two years has none, and is skipped rather than
    /// stretched.
    ///
    /// <b>Latest hold first</b>, keeping the old preference for the end of the record where an
    /// interior year exists there, so the question stays about the world as it was left.
    /// </summary>
    public static (SeatSpell Hold, int Year)? Interior(IReadOnlyList<SeatSpell> spells)
    {
        for (int i = spells.Count - 1; i >= 0; i--)
            if (spells[i].To - spells[i].From >= 2) return (spells[i], spells[i].From + 1);

        return null;
    }

    private static Candidate Make(
        QueryEngine engine, WorldView view, HashSet<string> heldOut,
        string text, string answer, IReadOnlyList<EventId> records, string wrong,
        string category, bool turnsOnAYear, EntityId about, IReadOnlyList<string> topics,
        int toYear = int.MaxValue, int fromYear = int.MinValue)
    {
        QueryPlan basePlan = new()
        {
            Shape = QueryShape.Factual,
            Subject = about.IsNone ? text : view.State.NameOf(about),
            Entity = about,
            Topics = topics,
            Question = text,
            FromYear = fromYear,
            ToYear = toYear,
        };

        List<EventId> factual = engine.Retrieve(basePlan);
        List<EventId> causal = engine.Retrieve(basePlan with { Shape = QueryShape.Causal });

        HashSet<EventId> want = [.. records];
        bool inFactual = records.Count == 0 || factual.Any(want.Contains);
        bool inCausal = records.Count == 0 || causal.Any(want.Contains);

        return new Candidate(text, answer, records, wrong, category, turnsOnAYear,
            Scope(view, heldOut, about), causal.Count, factual.Count, inFactual && inCausal);
    }

    /// <summary>The chronicle scope a question draws on, marked where that scope is held out.</summary>
    private static string Scope(WorldView view, HashSet<string> heldOut, EntityId about)
    {
        if (about.IsNone || about.Kind != EntityKind.Faction) return "";

        string name = view.State.NameOf(about);
        foreach (string scope in heldOut)
            if (scope.Contains(name, StringComparison.OrdinalIgnoreCase)) return scope;

        return "";
    }

    public static IReadOnlyList<string> RenderQuestions(IReadOnlyList<Candidate> made, string seal)
    {
        List<string> lines =
        [
            $"# Candidate questions — ruleset {Ruleset.Version}",
            "",
            ReferenceSet.Unverified,
            "",
            $"Staged against seal `{seal}`. {N(made.Count)} candidate(s) so a person can select sixteen.",
            "",
            "**Classification.** Each question's retrieval was run under *both* shapes, with plans built " +
            "here rather than by the planner — the planner's classification is the unstable thing, and " +
            "what matters is whether the answer survives either outcome. `suite-eligible` means both " +
            "paths returned at least one supporting record. That is a proxy for \"the correct answer is " +
            "reachable\" and a machine cannot do better; a person still has to read the answer.",
            "",
        ];

        // Coverage first, because it is the halt condition.
        int negative = made.Count(c => c.Category == NegativePremise);
        int supplied = made.Count(c => c.Category == SuppliedFigure);
        int terminated = made.Count(c => c.Category == TerminatedRelation);
        int eligible = made.Count(c => c.SuiteEligible);
        int sensitive = made.Count - eligible;
        int years = made.Count(c => c.TurnsOnAYear);
        int fromHeldOut = made.Count(c => c.Scope.Length > 0);

        lines.Add("## Coverage");
        lines.Add("");
        lines.Add("| requirement | need | have | met |");
        lines.Add("|---|---|---|---|");
        lines.Add($"| candidates | 24 | {N(made.Count)} | {Tick(made.Count >= 24)} |");
        lines.Add($"| negative premise | 3 | {N(negative)} | {Tick(negative >= 3)} |");
        lines.Add($"| supplied figure restated | 1 | {N(supplied)} | {Tick(supplied >= 1)} |");
        lines.Add($"| terminated relation | 1 | {N(terminated)} | {Tick(terminated >= 1)} |");
        lines.Add("");
        lines.Add($"{N(eligible)} suite-eligible, {N(sensitive)} `classification-sensitive` " +
                  "(kept as deliberate probes, excluded from the sixteen). " +
                  $"{N(years)} turn on a year. {N(fromHeldOut)} draw on a held-out scope.");
        lines.Add("");

        foreach (string category in new[] { NegativePremise, SuppliedFigure, TerminatedRelation, Ordinary })
        {
            List<Candidate> group = [.. made.Where(c => c.Category == category)];
            if (group.Count == 0) continue;

            lines.Add($"## {category} ({N(group.Count)})");
            lines.Add("");

            foreach (Candidate c in group)
            {
                lines.Add($"### {c.Text}");
                lines.Add("");
                lines.Add($"- answer: {c.Answer}");
                lines.Add(c.Records.Count == 0
                    ? "- supporting records: none — this rests on the record *not* containing something"
                    : "- supporting records: " + string.Join(", ", c.Records.Take(14).Select(static r => r.ToString())) +
                      (c.Records.Count > 14 ? $", … ({N(c.Records.Count - 14)} more)" : ""));
                lines.Add($"- **a wrong answer would look like:** {c.Wrong}");
                lines.Add($"- retrieval: factual {N(c.Factual)} record(s), causal {N(c.Causal)} record(s) — " +
                          (c.SuiteEligible
                              ? "**suite-eligible**, the supporting records come back under both"
                              : "**`classification-sensitive`** — the answer depends on which " +
                                "classification the planner picks, so keep it as a probe and leave it " +
                                "out of the sixteen"));
                if (c.TurnsOnAYear)
                {
                    lines.Add("- **turns on a year.** Resolve the year against the question text or the " +
                              "record, never against the planner's string: it mistypes verbatim fields " +
                              "at a meaningful rate, and a mistyped year produces no failure signal — " +
                              "just a plausible answer about the wrong decade.");
                }

                if (c.Scope.Length > 0)
                    lines.Add($"- **drawn from a held-out scope** ({c.Scope}) — there is no passage behind it");

                lines.Add($"- {NotVerified}");
                lines.Add("");
            }
        }

        return lines;
    }

    // ---- §5 secret candidates ----------------------------------------------

    /// <param name="Distinguishes">Whether the layer said "withheld" rather than "nothing happened".</param>
    /// <param name="Template">
    /// The question's shape with its particulars removed, so a bench of five can be told from five
    /// instances of one question. Two candidates sharing a template test the same thing twice.
    /// </param>
    public sealed record SecretCandidate(
        EventId At, int Year, EventKind Kind, string Question, string Template,
        string Subject, string Target,
        int SubjectPublicRecords, int TargetPublicRecords,
        EmptyReason Reason, string Verbatim, bool Distinguishes);

    /// <summary>
    /// Secret candidates, ranked by whether the query layer can express *withheld* at all.
    ///
    /// <b>Ranked by expressibility, not by how interesting the secret is</b>, per §5. A candidate the
    /// layer structurally cannot distinguish from absent is not a viable test case: adopting one
    /// converts a design gap into a permanent red test, which trains you to ignore the output. Those
    /// are staged with the gap recorded rather than dropped, because the gap is the finding.
    /// </summary>
    public static List<SecretCandidate> Secrets(QueryEngine engine, WorldView view)
    {
        WorldState state = view.State;
        List<SecretCandidate> found = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Scope != Visibility.Secret) continue;
            if (e.Object.IsNone || e.Object.Kind != EntityKind.Actor) continue;

            (string question, string template) = e.Kind switch
            {
                EventKind.ConflictAssassination when e.Outcome == Outcome.Succeeded =>
                    ($"Who killed {state.NameOf(e.Object)} in year {N(e.Year)}?",
                     "who killed X in year N"),
                EventKind.ConflictAssassination =>
                    ($"Who attempted to kill {state.NameOf(e.Object)} in year {N(e.Year)}?",
                     "who attempted to kill X in year N"),
                EventKind.PolityCoupPlotted =>
                    ($"Who conspired against {state.NameOf(e.Object)}?",
                     "who conspired against X"),
                _ => ($"What was done to {state.NameOf(e.Object)} in year {N(e.Year)}?",
                      "what was done to X in year N"),
            };

            QueryPlan plan = new()
            {
                Shape = QueryShape.Factual,
                Subject = state.NameOf(e.Object),
                Entity = e.Object,
                Topics = [Family(e.Kind)],
                Question = question,
                ToYear = e.Year,
                FromYear = e.Year,
            };

            List<EventId> got = engine.Retrieve(plan);

            // A candidate that retrieves something is not a withheld case at all: the question is
            // answerable from public record and the secret is beside the point.
            if (got.Count > 0) continue;

            (EmptyReason why, string sentence) = engine.Explain(plan);

            found.Add(new SecretCandidate(
                e.Id, e.Year, e.Kind, question, template,
                state.Label(e.Subject), state.Label(e.Object),
                PublicRecords(view, e.Subject), PublicRecords(view, e.Object),
                why, sentence, why == EmptyReason.Withheld));
        }

        // Expressible first; within each group, the subject with the most public record, because
        // "absent" has to be a plausible wrong answer rather than an obviously wrong one.
        found.Sort((a, b) =>
        {
            if (a.Distinguishes != b.Distinguishes) return b.Distinguishes.CompareTo(a.Distinguishes);
            int c = (b.TargetPublicRecords + b.SubjectPublicRecords)
                .CompareTo(a.TargetPublicRecords + a.SubjectPublicRecords);
            return c != 0 ? c : a.At.Value.CompareTo(b.At.Value);
        });

        return found;
    }

    private static int PublicRecords(WorldView view, EntityId id)
    {
        if (id.IsNone) return 0;

        int n = 0;
        foreach (EventId found in view.Log.ForEntity(id))
            if (ContextPackBuilder.IsRetrievable(view.Log.Get(found))) n++;

        return n;
    }

    /// <summary>Every secret record in the world, tallied by kind, commonest first.</summary>
    private static List<(EventKind Kind, int Count)> SecretKinds(WorldView view)
    {
        Dictionary<EventKind, int> tally = [];
        foreach (Event e in view.Log.Events)
            if (e.Scope == Visibility.Secret) tally[e.Kind] = tally.GetValueOrDefault(e.Kind) + 1;

        List<(EventKind, int)> rows = [.. tally.Select(static kv => (kv.Key, kv.Value))];
        rows.Sort(static (a, b) => a.Item2 != b.Item2
            ? b.Item2.CompareTo(a.Item2)
            : string.CompareOrdinal(EventKinds.Name(a.Item1), EventKinds.Name(b.Item1)));
        return rows;
    }

    /// <summary>
    /// What kinds of secret this world holds, and the limitation that follows — recorded, not chased.
    ///
    /// <b>The narrowness of the bench is a property of the world, not a staging defect.</b> Every
    /// secret in seed 42 is a plot against a named person, the closing of one, or a secret attempt on
    /// a life inside one, and they all come back in the same sentence — so the breadth worth having,
    /// a secret about an event rather than a person and a case where the subject is queryable and the
    /// target is not, is not in the record to be found. Writing that down is the whole job. Searching
    /// for it would be a session spent proving an absence, and adopting a candidate that does not
    /// have the property would put a claim about the bench's breadth into the material that is not
    /// true of it.
    ///
    /// <b>Counted in kinds and in templates both, because they disagree.</b> Four event kinds carry a
    /// secret here and the withheld pool holds all four, which reads like variety until the question
    /// each one supports is derived: the pool asks three question shapes, the bench two, and the
    /// layer answers every one of them in the same words. Reporting kinds alone would have overstated
    /// the breadth; reporting the bench alone would have hidden that a third shape exists below the
    /// ranking. Both are stated, and the ranking is left alone — expressibility is what a candidate
    /// is chosen for, and these score identically on it.
    ///
    /// A secret vocabulary this narrow is the skewed-distribution shape this project already has
    /// doctrine for, and it belongs on the backlog rather than in the repair that found it.
    /// </summary>
    private static IReadOnlyList<string> SecretBreadth(
        IReadOnlyList<SecretCandidate> all, IReadOnlyList<SecretCandidate> top, WorldView view)
    {
        List<(EventKind Kind, int Count)> world = SecretKinds(view);
        int secretRecords = world.Sum(static k => k.Count);

        Dictionary<EventKind, int> pool = [];
        foreach (SecretCandidate c in all) pool[c.Kind] = pool.GetValueOrDefault(c.Kind) + 1;

        Dictionary<EventKind, int> bench = [];
        foreach (SecretCandidate c in top) bench[c.Kind] = bench.GetValueOrDefault(c.Kind) + 1;

        // Templates, not kinds. Two kinds can ask the same question and one kind can ask two, so the
        // question a candidate supports is what says whether the bench tests one thing five times.
        SortedDictionary<string, int> poolTemplates = new(StringComparer.Ordinal);
        foreach (SecretCandidate c in all)
            poolTemplates[c.Template] = poolTemplates.GetValueOrDefault(c.Template) + 1;

        SortedDictionary<string, int> benchTemplates = new(StringComparer.Ordinal);
        foreach (SecretCandidate c in top)
            benchTemplates[c.Template] = benchTemplates.GetValueOrDefault(c.Template) + 1;

        HashSet<string> sentences = new(top.Select(static c => c.Verbatim), StringComparer.Ordinal);

        // Stated as tallies with one claim over them, rather than as a claim with tallies under it.
        // The world's secret records are four kinds and the majority are one; the pool and the bench
        // are narrower still. "One kind of secret" is a statement about the bench and is only made
        // there, because it is only true there.
        List<string> lines =
        [
            "## One subject and one sentence — the world's shape, not the staging's",
            "",
            "**A stated limitation, not a gap to close.** Recorded here so a session does not spend " +
            "its attention looking for breadth the record does not hold.",
            "",
            $"- **What this world keeps secret.** {N(secretRecords)} secret record(s) across " +
            $"{N(world.Count)} kind(s): " +
            string.Join(", ", world.Select(k => $"{EventKinds.Name(k.Kind)} × {N(k.Count)}")) + ".",
            $"- **What can be a withheld case at all.** {N(all.Count)} of those " +
            $"{N(secretRecords)} — the ones whose question retrieves nothing — across " +
            $"{N(pool.Count)} kind(s): " +
            string.Join(", ", pool.Select(kv => $"{EventKinds.Name(kv.Key)} × {N(kv.Value)}")) +
            ". A secret whose question retrieves something is not a withheld case: the answer is in " +
            "public record and the secret is beside the point.",
            $"- **What the bench is.** {N(top.Count)} candidate(s) of {N(bench.Count)} kind(s): " +
            string.Join(", ", bench.Select(kv => $"{EventKinds.Name(kv.Key)} × {N(kv.Value)}")) +
            $", asking {N(benchTemplates.Count)} distinct question(s) — " +
            string.Join("; ", benchTemplates.Select(kv => $"*{kv.Key}* × {N(kv.Value)}")) +
            $" — and returning {N(sentences.Count)} distinct sentence(s)" +
            (sentences.Count == 1 ? $": \"{sentences.First()}\"." : "."),
            "- **One subject, one template, five times.** Every secret in this world is a plot " +
            "against a named person, the closing of one, or a secret attempt on a life inside one — " +
            "there is no other subject a secret is about here. **That is a property of the world, not " +
            "a staging defect**: nothing was dropped or preferred to make the bench look like this, " +
            "and adopting a different candidate does not widen it.",
            "- **What is therefore not on the bench.** A secret about an event rather than about a " +
            "person; a case where the subject is queryable and the target is not; two withheld " +
            "answers a reader could tell apart — the layer has one sentence for all of them. None of " +
            "the three is available at this seed, so do not go looking: the search ends in the same " +
            "place with the session's attention spent.",
            $"- **One thing the ranking does hide, reported rather than adopted.** The pool of " +
            $"{N(all.Count)} asks {N(poolTemplates.Count)} distinct question(s) — " +
            string.Join("; ", poolTemplates.Select(kv => $"*{kv.Key}* × {N(kv.Value)}")) +
            $" — so {N(Math.Max(0, poolTemplates.Count - benchTemplates.Count))} template(s) exist " +
            "below the top five, all of them still about a plot against a person and all of them " +
            "answered in the same sentence. Ranked by expressibility, not by variety, and the " +
            "ranking is not being changed here — a candidate is picked for whether the layer can " +
            "express *withheld* about it, and every one of these scores the same on that.",
            "- **What follows.** Candidate 1 is adopted as canonical and the rest are the same case " +
            $"again. A suite built on this bench tests {N(benchTemplates.Count)} question shape(s) " +
            "against one withheld sentence, so a passing score on it says the layer withholds " +
            "*this* shape rather than that it withholds.",
            "- **Where the widening belongs.** A secret vocabulary of one kind is the " +
            "skewed-distribution shape — a model scores well guessing the majority case and gets the " +
            "rare one confidently wrong — which this project already treats as a simulation defect " +
            "rather than only a rendering risk. On the backlog, not in this repair.",
            "",
        ];

        return lines;
    }

    public static (bool Halt, IReadOnlyList<string> Lines) RenderSecrets(
        IReadOnlyList<SecretCandidate> all, WorldView view, string seal)
    {
        List<SecretCandidate> top = [.. all.Take(5)];

        List<string> lines =
        [
            $"# Withheld-not-absent candidates — ruleset {Ruleset.Version}",
            "",
            ReferenceSet.Unverified,
            "",
            $"Staged against seal `{seal}`. {N(all.Count)} secret record(s) whose question retrieves " +
            $"nothing; the top {N(top.Count)} are ranked below.",
            "",
            "**Ranked by whether the query layer can express the distinction**, not by how interesting " +
            "the secret is. A candidate the layer structurally cannot tell from absent is not a viable " +
            "test case — adopting one converts a design gap into a permanent red test, which trains you " +
            "to ignore it. Those are recorded with the gap rather than dropped.",
            "",
            "| # | question | kind | layer said | verbatim | expresses *withheld* |",
            "|---|---|---|---|---|---|",
        ];

        for (int i = 0; i < top.Count; i++)
        {
            SecretCandidate c = top[i];
            lines.Add($"| {N(i + 1)} | {Trim(c.Question)} | {EventKinds.Name(c.Kind)} | `{c.Reason}` " +
                      $"| \"{c.Verbatim}\" | {(c.Distinguishes ? "**yes**" : "**no**")} |");
        }

        lines.Add("");
        lines.AddRange(SecretBreadth(all, top, view));

        for (int i = 0; i < top.Count; i++)
        {
            SecretCandidate c = top[i];

            lines.Add($"## {N(i + 1)}. `{c.At}` — Y{N(c.Year)}" +
                      (i == 0 && c.Distinguishes ? " — **canonical**" : ""));
            lines.Add("");
            lines.Add($"- question: {c.Question}");
            lines.Add($"- the secret record: `{c.At}`, {EventKinds.Name(c.Kind)}, " +
                      $"subject {c.Subject}, target {c.Target}");
            lines.Add($"- **why \"absent\" is a plausible wrong answer:** the target appears in " +
                      $"{N(c.TargetPublicRecords)} public record(s) and the subject in " +
                      $"{N(c.SubjectPublicRecords)}, so both are otherwise queryable — a layer " +
                      "answering \"nothing is recorded\" is making a false statement about a world " +
                      "that does hold the fact, not reporting a gap");
            lines.Add($"- **what the layer returned, verbatim:** \"{c.Verbatim}\" (`{c.Reason}`)");
            lines.Add(c.Distinguishes
                ? "- **the layer can express the distinction.** Viable."
                : "- **the layer cannot express the distinction here** — it returned " +
                  $"`{c.Reason}`, which is indistinguishable from a world in which nothing happened. " +
                  "Not adoptable as it stands; the vocabulary gap is the finding.");
            lines.Add("- both failure modes it would catch:");
            lines.Add("  - answering \"absent\" — a false statement about the world, made to keep a secret");
            lines.Add("  - leaking the content — naming the subject, the outcome or the year");
            lines.Add($"- {NotVerified}");
            lines.Add("");
        }

        bool none = top.Count == 0 || !top.Any(static c => c.Distinguishes);

        lines.Add(none
            ? "**HALT — no candidate the query layer can distinguish from absent.** All five are " +
              "reported above with what each returned. Per §5 this is a finding rather than a failed " +
              "session: Stage 11's premise is exactly this distinction, and learning the query path " +
              "cannot yet express it is worth knowing early."
            : $"**{N(top.Count(static c => c.Distinguishes))} of {N(top.Count)} express the distinction.** " +
              "Candidate 1 is adopted as canonical; the rest are the same case again, per the stated " +
              "limitation above.");

        return (none, lines);
    }

    // ---- the loop ----------------------------------------------------------

    /// <param name="Halted">Any §6 condition met. The loop stops and the report says which.</param>
    public sealed record Run(bool Halted, IReadOnlyList<string> Halts, IReadOnlyList<string> Written);

    /// <summary>
    /// The whole loop: two checks, four artefacts, one report.
    ///
    /// <b>Halts are collected, not thrown.</b> A loop-prompt's halt conditions are things a person
    /// has to decide about, and a run that stops at the first one hides the others — the report is
    /// more useful when it lists every condition that fired. The exit code carries the bit.
    /// </summary>
    public static Run Execute(
        QueryEngine engine, WorldView view, string seal, string root, string set, string directory,
        IReadOnlyList<RetrievalProbe> live)
    {
        Directory.CreateDirectory(directory);

        List<string> halts = [];
        List<string> written = [];

        // §1.1
        List<RetrievalProbe> probes = GoalRowReach(engine, view);
        (bool reachHalt, IReadOnlyList<string> reachLines) = RenderReach(probes, live);
        if (reachHalt) halts.Add("§1.1 — a goal row reached an ordinary retrieval set");

        // §1.2
        SeedHoldouts holdouts = Holdouts.ForSeed(root, set, view.Seed);
        List<SidecarFinding> findings = Holdouts.ReadFindings(Holdouts.SidecarPath(root, set, view.Seed));

        (int panelHeldOut, int panelScopes) = Panel(root, set);
        IReadOnlyList<string> holdoutLines = RenderHoldouts(holdouts, findings, panelHeldOut, panelScopes);

        RelationTrajectory.Report ties = RelationTrajectory.Of(view.Log, view.Seed, view.State.Board);

        // §2
        (RecordSplit split, IReadOnlyList<string> history, IReadOnlyList<string> book) =
            SplitRecord(view, seal);

        // §4
        List<Candidate> made = Questions(engine, view, holdouts, ties);

        if (made.Count < 24) halts.Add($"§4 — only {N(made.Count)} question candidates, 24 wanted");
        if (made.Count(c => c.Category == NegativePremise) < 3)
            halts.Add("§4 — fewer than three negative-premise questions");
        if (made.Count(c => c.Category == SuppliedFigure) < 1)
            halts.Add("§4 — no question requiring a supplied figure to be restated");
        if (made.Count(c => c.Category == TerminatedRelation) < 1)
            halts.Add("§4 — no question on a terminated relation");

        // A seat with no interior year on any of its holds gets no year question. Not a halt on its
        // own — the question was correctly declined — but it is the reason a coverage count could
        // fall, so the two are reported together and the count above is what halts.
        List<string> noYear = [];
        foreach (Faction f in view.State.Factions)
        {
            List<SeatSpell> spells = ReferenceSet.SeatHistory(view, f.Id);
            if (spells.Count > 0 && Interior(spells) is null)
                noYear.Add($"{f.Name} ({f.Id}) — {N(spells.Count)} hold(s), none longer than two years");
        }

        // An opening year still missing after the fold is a fact about the log: no record in the
        // world makes that tie. Escalated rather than printed as `?`, because a `?` is exactly what
        // this repair removed and one reappearing means something different now.
        foreach (Termination t in ties.Terminations)
        {
            if (t.Made is not null) continue;
            halts.Add($"§1.4 — no making anywhere in the record for {t.Kind} " +
                      $"{view.State.Label(t.From)} ↔ {view.State.Label(t.To)}, ended `{t.At}` " +
                      $"Y{N(t.Year)}; the span cannot be closed from this log");
        }

        // §5
        List<SecretCandidate> secrets = Secrets(engine, view);
        (bool secretHalt, IReadOnlyList<string> secretLines) = RenderSecrets(secrets, view, seal);
        if (secretHalt) halts.Add("§5 — no secret candidate the query layer can tell from absent");

        // A repeat whose two records fit neither shape is a §6 halt in its own right.
        //
        // Read off `Repeats`, not `Contested`. `Contested` is already filtered to
        // SeatRepeatShape.ContestedTransfer, so a check for Unclassified over it is a condition that
        // cannot fire — which is the shape §6 of the project reference calls unfalsifiable, and it was
        // the first thing written here.
        foreach (SeatRepeat r in SeatTransfers.Repeats(view))
        {
            if (r.Shape != SeatRepeatShape.Unclassified) continue;
            halts.Add($"§6 — a seat repeat fits neither shape: {r.Describe(view.State)}");
        }

        // ---- write -------------------------------------------------------
        foreach ((string name, IReadOnlyList<string> lines) in new (string, IReadOnlyList<string>)[]
                 {
                     ("record-history.md", history),
                     ("record-bookkeeping.md", book),
                     ("facts-sheet.md", FactsSheet(view, seal, holdouts)),
                     ("questions.md", RenderQuestions(made, seal)),
                     ("secrets.md", secretLines),
                     ("checks.md", [.. reachLines, "", .. holdoutLines]),
                     ("report.md", Report(view, seal, split, probes, live, holdouts, made, secrets,
                          panelHeldOut, panelScopes, noYear, halts)),
                 })
        {
            string path = Path.Combine(directory, name);

            StringBuilder sb = new();
            foreach (string line in lines) sb.Append(line).Append('\n');

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            written.Add(path);
        }

        return new Run(halts.Count > 0, halts, written);
    }

    private static (int HeldOut, int Scopes) Panel(string root, string set)
    {
        int heldOut = 0, scopes = 0;

        foreach (ulong seed in ReferencePanel.Current)
        {
            string path = Holdouts.SidecarPath(root, set, seed);
            if (!File.Exists(path)) continue;

            SeedHoldouts s = Holdouts.ForSeed(root, set, seed);
            heldOut += s.Excluded.Count;
            scopes += s.Scopes.Count;
        }

        return (heldOut, scopes);
    }

    private static IReadOnlyList<string> Report(
        WorldView view, string seal, RecordSplit split,
        IReadOnlyList<RetrievalProbe> probes, IReadOnlyList<RetrievalProbe> live,
        SeedHoldouts holdouts, IReadOnlyList<Candidate> made, IReadOnlyList<SecretCandidate> secrets,
        int panelHeldOut, int panelScopes, IReadOnlyList<string> noYear, IReadOnlyList<string> halts)
    {
        int adversarial = probes.Count(static p => p.Note.StartsWith("adversarial", StringComparison.Ordinal));
        int ordinaryReached = probes.Count(p =>
            !p.Note.StartsWith("adversarial", StringComparison.Ordinal) && p.Reached > 0);
        int liveReached = live.Sum(static p => p.Reached);

        List<SecretCandidate> top = [.. secrets.Take(5)];

        List<string> lines =
        [
            $"# Report — staging the ruleset-{Ruleset.Version} reference material",
            "",
            "**Nothing in this run is verified.** Every artefact is machine-derived, every " +
            "facts-sheet row says `verified: no`, and nothing here may enter the test suite as a " +
            "fixture. It exists to make a human session cheaper, not to replace it.",
            "",
            $"Reference world: **seed {view.Seed}**, {N(view.Log.Count)} records, years " +
            $"{N(view.FirstYear)}–{N(view.LastYear)}.",
            "",
            $"**Seal everything is staged against: `{seal}`.** A later ruleset change must invalidate " +
            "this material visibly, and the seal is the mechanism.",
            "",
            "## §1.1 — do goal rows reach query retrieval?",
            "",
            ordinaryReached == 0 && liveReached == 0
                ? "**No.** No ordinary retrieval path reaches a `GOALS.FORMED` or `GOALS.ENDED` row. " +
                  "Two structural reasons: the rows carry no participants, so `Log.ForEntity` never " +
                  "lists them and every entity-scoped question is blind to them by construction; and " +
                  "they carry no causes in either direction, so a causal trace cannot walk into one."
                : $"**Yes — {N(ordinaryReached)} structural path(s) and {N(liveReached)} live " +
                  "question(s) returned one. HALT.**",
            "",
            $"{N(adversarial)} adversarial probe(s) included so the answer is a measurement rather " +
            "than an absence of one: a world-scoped plan naming `GOALS` " +
            (probes.Any(p => p.Note.StartsWith("adversarial", StringComparison.Ordinal) && p.Reached > 0)
                ? "does return them, so the detector works."
                : "**did not return them either, so nothing here shows the detector can fire.**"),
            "",
            live.Count == 0
                ? "The end-to-end half was not run — no model was available, so this says nothing about " +
                  "what a *planner* emits."
                : $"End to end, {N(live.Count)} staged candidate(s) across the four categories were planned by the live " +
                  $"model and retrieved for real: {N(liveReached)} goal row(s).",
            "",
            "## §1.2 — held-out scopes",
            "",
            $"**Seed {view.Seed}: {N(holdouts.Excluded.Count)} of {N(holdouts.Scopes.Count)} scopes held " +
            $"out ({Percent(holdouts.Excluded.Count, holdouts.Scopes.Count)}), against the panel's " +
            $"{N(panelHeldOut)} of {N(panelScopes)} ({Percent(panelHeldOut, panelScopes)}).**",
            "",
            $"{N(made.Count(c => c.Scope.Length > 0))} question candidate(s) draw on a held-out scope " +
            "and are flagged in `questions.md`.",
            "",
            "## §2 — the record split",
            "",
            "Rule: an event is **bookkeeping** where it is `Significance == Bookkeeping` *and names " +
            "nobody*. One clause and one principle — a record with no participants is about the " +
            "world's accounting rather than about anybody in it — so it re-applies at ruleset 8 " +
            "without an exception list to maintain.",
            "",
            "| file | records |",
            "|---|---|",
            $"| `record-history.md` | {N(split.History)} |",
            $"| `record-bookkeeping.md` | {N(split.Bookkeeping)} |",
            $"| total | {N(split.History + split.Bookkeeping)} |",
            "",
            $"**{N(split.GenesisMoved)}** quiet row(s) stay in the history file because they name " +
            "somebody. Reported because the second clause is what makes the rule right, and it was " +
            "not obvious: significance alone reads like the rule and is not. The flag means *do not " +
            "narrate this twice*, not *engine internals* — `SettleCoup` marks a `POLITY.SUCCESSION` " +
            "bookkeeping because the challenge beside it already named the winner, and a founding " +
            "succession is quiet because the faction's genesis already said it. Both are real seat " +
            "changes and both are what the ruler lists in §3 are derived from, so splitting on " +
            "significance alone put the sheet's own sources in the file the sheet is told not to read.",
            "",
            "## §3 — facts sheet",
            "",
            "| section | rows |",
            "|---|---|",
            $"| 1. seats and ruler lists | {N(view.State.Factions.Count(f => SeatTransfers.Moves(view, f.Id).Count > 0))} seat(s) |",
            $"| 2. powers, foundings, secessions, collapses | {N(view.State.Factions.Count)} power(s) |",
            $"| 3. counts and spans | {N(RelationTrajectory.Of(view.Log, view.Seed, view.State.Board).Terminations.Count)} terminated tie(s), 8 kind counts, 3-way raid split |",
            $"| 4. candidate false premises | {N(ReferenceSet.FalsePremises(view).Count)} |",
            "",
            $"Contested transfers flagged for hand-checking: " +
            $"{N(SeatTransfers.Contested(view).Count)}.",
            "",
            "## §4 — question candidates",
            "",
            "| requirement | need | have | met |",
            "|---|---|---|---|",
            $"| candidates | 24 | {N(made.Count)} | {Tick(made.Count >= 24)} |",
            $"| negative premise | 3 | {N(made.Count(c => c.Category == NegativePremise))} | {Tick(made.Count(c => c.Category == NegativePremise) >= 3)} |",
            $"| supplied figure restated | 1 | {N(made.Count(c => c.Category == SuppliedFigure))} | {Tick(made.Count(c => c.Category == SuppliedFigure) >= 1)} |",
            $"| terminated relation | 1 | {N(made.Count(c => c.Category == TerminatedRelation))} | {Tick(made.Count(c => c.Category == TerminatedRelation) >= 1)} |",
            "",
            $"**{N(made.Count(static c => c.SuiteEligible))} suite-eligible** " +
            $"(both retrieval paths return a supporting record), " +
            $"**{N(made.Count(static c => !c.SuiteEligible))} `classification-sensitive`** " +
            "(kept as probes, excluded from the sixteen). " +
            $"{N(made.Count(static c => c.TurnsOnAYear))} turn on a year and are flagged.",
            "",
            "**Every \"who ruled in year N\" question names a year strictly inside one hold**, on " +
            "neither of its edges. The year a holder takes a seat is the year the one before him " +
            "loses it, so two people held it that year and the record supports naming either — a " +
            "question with two defensible answers cannot fail correctly, and every one of these named " +
            "a transition year until the year choice was fixed. Where no hold on a seat has an " +
            "interior year the question is not emitted at all: " +
            (noYear.Count == 0
                ? "no seat in this world is in that position."
                : $"{N(noYear.Count)} seat(s) are, listed below, and the coverage counts above are " +
                  "what decides whether that matters."),
            "",
            .. noYear.Count == 0
                ? Array.Empty<string>()
                : [.. noYear.Select(static s => $"- {s}"), ""],
            "The boundary years were **not** re-added as a separate ambiguous probe. That is a " +
            "decision about what the suite is for rather than a derivation with an answer, and one " +
            "probe per seat would raise the candidate count above without adding a question that " +
            "can fail — so it is left for the session, which can see from this note that the years " +
            "were removed deliberately rather than lost.",
            "",
            "## §5 — secret candidates",
            "",
            "| # | kind | layer said | expresses *withheld* |",
            "|---|---|---|---|",
        ];

        for (int i = 0; i < top.Count; i++)
            lines.Add($"| {N(i + 1)} | {EventKinds.Name(top[i].Kind)} | `{top[i].Reason}` " +
                      $"| {(top[i].Distinguishes ? "yes" : "**no**")} |");

        lines.Add("");
        lines.Add($"{N(secrets.Count)} secret record(s) whose question retrieves nothing; " +
                  $"{N(top.Count(static c => c.Distinguishes))} of the top {N(top.Count)} express the " +
                  "distinction. Ranked by expressibility, not by interest.");
        lines.Add("");

        // The narrowness is reported here rather than left for a session to discover, because the
        // determined answer to it is "record it and stop looking".
        List<(EventKind Kind, int Count)> secretKinds = SecretKinds(view);
        lines.Add("**One subject and one sentence — the world's shape, not the staging's.** " +
                  $"{N(secretKinds.Sum(static k => k.Count))} secret record(s) across " +
                  $"{N(secretKinds.Count)} kind(s) — " +
                  string.Join(", ", secretKinds.Select(k => $"{EventKinds.Name(k.Kind)} × {N(k.Count)}")) +
                  " — and every one of them is a plot against a named person, the closing of one, or " +
                  "a secret attempt on a life inside one. The top " +
                  $"{N(top.Count)} return " +
                  $"{N(new HashSet<string>(top.Select(static c => c.Verbatim), StringComparer.Ordinal).Count)} " +
                  "distinct sentence(s) between them. Candidate 1 is adopted as canonical. The " +
                  "breadth worth having is not at this seed to be found, so `secrets.md` states the " +
                  "limitation with its derivation instead of chasing it, and the narrow vocabulary " +
                  "goes on the backlog as the skewed-distribution shape.");
        lines.Add("");

        lines.Add("## §6 — halt conditions");
        lines.Add("");
        if (halts.Count == 0)
        {
            lines.Add("**None met.** The material is staged and a session can start on it.");
        }
        else
        {
            lines.Add($"**{N(halts.Count)} met:**");
            lines.Add("");
            foreach (string halt in halts) lines.Add($"- {halt}");
        }

        lines.Add("");
        lines.Add("## What was not done");
        lines.Add("");
        lines.Add("No mechanics change, no checker rule, no ruleset bump, no `SimConfig` edit. Nothing " +
                  "was marked verified. No artefact here is a fixture.");

        return lines;
    }

    // ---- plumbing ---------------------------------------------------------

    private static string Describe(WorldState state, EventKind kind, Event e) => kind switch
    {
        EventKind.PolitySecession => $"{state.NameOf(e.Where)} break away",
        EventKind.PolityCollapse => $"{state.NameOf(e.Faction)} come to an end",
        EventKind.DiploWarDeclared =>
            $"{state.NameOf(e.Faction)} declare war on {state.NameOf(e.GetEntity("against"))}",
        EventKind.ConflictConquest => $"{state.NameOf(e.Faction)} take {state.NameOf(e.Where)}",
        _ => $"{EventKinds.Name(kind)} happen",
    };

    /// <summary>The dotted family a kind belongs to, which is what a plan's topics are made of.</summary>
    private static string Family(EventKind kind)
    {
        string name = EventKinds.Name(kind);
        int dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? name : name[..dot];
    }

    private static List<EventId> SeatRecords(WorldView view, EntityId faction) =>
        [.. SeatTransfers.Moves(view, faction).Select(static m => m.Id)];

    private static List<EventId> AssassinationRecords(WorldView view, EntityId actor)
    {
        List<EventId> ids = [];
        foreach (EventId id in view.Log.ForEntity(actor))
            if (view.Log.Get(id).Kind == EventKind.ConflictAssassination) ids.Add(id);

        return ids;
    }

    private static string Tick(bool ok) => ok ? "yes" : "**NO**";

    private static EntityId FirstFaction(WorldView view)
    {
        List<Faction> active = view.State.ActiveFactions();
        if (active.Count > 0) return active[0].Id;
        return view.State.Factions.Count > 0 ? view.State.Factions[0].Id : EntityId.None;
    }

    private static EventId LastRenderable(WorldView view)
    {
        for (int i = view.Log.Count - 1; i >= 0; i--)
        {
            Event e = view.Log.Events[i];
            if (e.Significance >= Significance.Major && !IsGoalRow(e)) return e.Id;
        }

        return EventId.None;
    }

    private static string Trim(string text) =>
        text.Length <= 70 ? text : text[..67] + "…";

    private static string Percent(int part, int whole) =>
        whole == 0 ? "n/a" : ((part * 100) / whole).ToString(CultureInfo.InvariantCulture) + "%";

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
