using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>What a panel of worlds was observed to do to one piece of standing state.</summary>
public enum Exercised
{
    /// <summary>Never non-zero anywhere on the panel. Says nothing at all about removal.</summary>
    NeverPresent = 0,

    /// <summary>Went up and never came down anywhere on the panel.</summary>
    OnlyUp = 1,

    /// <summary>Came down somewhere on the panel. A removal path exists and something reached it.</summary>
    CameDown = 2,
}

/// <summary>One piece of standing state and what the panel did to it.</summary>
/// <param name="Removals">Observed decreases, pooled across the panel.</param>
/// <param name="SeedsWithRemoval">How many of the panel's worlds saw one at all.</param>
public sealed record StandingItem(
    string Group, string Name, Exercised Exercised, int Removals, int SeedsWithRemoval, int Seeds)
{
    public string Describe() => Exercised switch
    {
        Exercised.NeverPresent => "**never present** — nothing to remove",
        Exercised.OnlyUp => "**only ever went up**",
        _ => $"came down {Removals.ToString(CultureInfo.InvariantCulture)}× " +
             $"in {SeedsWithRemoval.ToString(CultureInfo.InvariantCulture)} of " +
             $"{Seeds.ToString(CultureInfo.InvariantCulture)} world(s)",
    };
}

/// <summary>
/// The monotonic sweep's measurable half: every piece of standing state, and whether anything on
/// the panel ever made it smaller.
///
/// <b>This answers one of the three questions the sweep asks and cannot answer the others.</b> The
/// classification the brief wants is: has a removal path exercised on the panel / has a removal
/// path never exercised / has no removal path at all. Only the first is visible from records. The
/// difference between the second and the third is a fact about the source, and this project has
/// already paid for confusing them — `quantity` read zero across sixty scopes of an entire panel
/// while having had a live call site all along, so a whole panel of zero establishes "this panel
/// did not reach it" and never "it cannot be reached".
///
/// So the column here is named for what it observed. The classification is done by reading the
/// rules, by a person, and the two are put side by side in the report rather than one being
/// inferred from the other.
///
/// <b>Measured through the reducer.</b> Same reason as <see cref="RelationTrajectory"/>: a sweep
/// that reimplements the fold sweeps the reimplementation, and the first version of that file
/// missed the whole of war and peace because <c>AtWar</c> is applied in code rather than through a
/// payload verb.
/// </summary>
public static class StandingState
{
    /// <summary>
    /// Standing state this instrument cannot see, and why — named rather than omitted.
    ///
    /// <b>Empty as of ruleset 7, and kept because a named list is the honest form of the answer.</b>
    ///
    /// At ruleset 6 it named <see cref="GoalBook"/>. Goals were created by the perception phase
    /// directly and by no event, so a world replayed from its record held none — and a sweep that
    /// replays the record duly reported "live goals: never present" for state that drives the entire
    /// action phase. That is the absent-versus-unknown conflation with a number attached, and naming it
    /// here instead of scoring it was the point. Ruleset 7 put every goal transition into the record,
    /// so this sweep can see them and the list is empty; <c>docs/goalbook-phase-2-report.md</c>.
    ///
    /// The next piece of state that turns out to live outside the fold goes here, rather than being
    /// quietly swept as a zero.
    ///
    /// <b>Two things this comment used to assert and both were wrong</b>, corrected by measuring rather
    /// than by rereading: the reducer touched goals at six points and not the one it claimed, and it
    /// was removal that was half-folded while creation was entirely outside.
    /// </summary>
    public static readonly string[] OutsideTheFold = [];

    /// <summary>
    /// Walks a panel and reports, for every piece of standing state, whether it was ever seen to
    /// decrease.
    /// </summary>
    public static List<StandingItem> Sweep(IReadOnlyList<(EventLog Log, ulong Seed)> panel)
    {
        Dictionary<string, (string Group, int Removals, int Seeds, bool Present)> tally =
            new(StringComparer.Ordinal);

        foreach ((EventLog log, ulong seed) in panel)
        {
            Dictionary<string, int> here = new(StringComparer.Ordinal);
            Dictionary<string, int> previous = new(StringComparer.Ordinal);
            HashSet<string> present = new(StringComparer.Ordinal);
            Dictionary<RelationKey, int> edgeWas = [];

            Rendering.Replay.Walk(log, seed, (state, _) =>
            {
                // Relation values, one edge at a time, as well as summed.
                //
                // The summed row is not sufficient and `Fealty` is the proof: a won contest writes
                // +18 to the winner and −12 to the ruler in a single event, so the world's total
                // rises by 6 and a real decrease is invisible. A total that never falls is
                // therefore *not* the same as a kind with no way down, which is what an earlier
                // version of this comment claimed.
                Dictionary<RelationKey, int> edgeNow = [];

                foreach (Relation r in state.Relations.All)
                {
                    string key = "relations " + r.Key.Kind + " (a single edge's value)";
                    present.Add(key);
                    if (!tally.ContainsKey(key)) tally[key] = ("relations", 0, 0, false);

                    if (edgeWas.TryGetValue(r.Key, out int was) && r.Value < was)
                        here[key] = here.GetValueOrDefault(key) + 1;

                    edgeNow[r.Key] = r.Value;
                }

                // Replaced wholesale rather than updated, so an edge that ended and was later
                // made again is compared against nothing. Carrying the dead edge's value forward
                // scored a fresh trade tie worth 8, made after a 25-point one was severed, as a
                // seventeen-point decrease — the new mechanic's own terminations reappearing as
                // evidence that trade values decay, which they do not.
                edgeWas = edgeNow;

                foreach ((string group, string name, int value) in Measure(state))
                {
                    string key = group + " " + name;

                    // Present means ever non-zero, never "the sweep asked about it". Every row is
                    // emitted on every event, so treating asked-about as present left
                    // NeverPresent a category no input could reach — a numerator with no
                    // reachable path, reintroduced inside the instrument built to look for
                    // exactly that. It reported `Rivalry`, which no rule anywhere reads or
                    // writes, as a relation kind that only ever went up.
                    if (value != 0) present.Add(key);

                    if (previous.TryGetValue(key, out int was) && value < was)
                        here[key] = here.GetValueOrDefault(key) + 1;

                    previous[key] = value;
                    if (!tally.ContainsKey(key)) tally[key] = (group, 0, 0, false);
                }
            });

            foreach (string key in present)
            {
                (string group, int removals, int seeds, bool _) = tally[key];
                int mine = here.GetValueOrDefault(key);
                tally[key] = (group, removals + mine, seeds + (mine > 0 ? 1 : 0), true);
            }
        }

        List<StandingItem> rows = [];
        foreach ((string key, (string group, int removals, int seeds, bool everPresent)) in tally)
        {
            string name = key[(key.IndexOf(' ', StringComparison.Ordinal) + 1)..];

            rows.Add(new StandingItem(group, name,
                !everPresent ? Exercised.NeverPresent
                : removals > 0 ? Exercised.CameDown
                : Exercised.OnlyUp,
                removals, seeds, panel.Count));
        }

        rows.Sort(static (a, b) =>
        {
            int c = string.CompareOrdinal(a.Group, b.Group);
            return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
        });

        return rows;
    }

    /// <summary>
    /// Every standing quantity in the world, as one number each.
    ///
    /// <b>Totals, not per-entity values.</b> A per-entity sweep would report that a faction's
    /// treasury came down, which is true and uninteresting: money is spent. The question this
    /// phase asks is whether a *kind* of state has a way down at all, and the whole world's
    /// holding of it is the readable unit for that.
    ///
    /// <b>But a total that never falls is not a kind with no way down</b>, and `Fealty` is where
    /// that was found out: a won contest writes +18 and −12 in one event, so the total rises by
    /// six and a genuine decrease leaves no trace in it. Relation values are therefore swept per
    /// edge as well, in <see cref="Sweep"/>. The rows here are the readable summary; the per-edge
    /// rows are what the classification is actually safe to rest on.
    ///
    /// The collections are counted as well as the scalars, because "every collection an entity
    /// carries" is half of what the sweep was asked to enumerate — and a list that only grows is
    /// the same defect as a number that only rises.
    /// </summary>
    private static IEnumerable<(string Group, string Name, int Value)> Measure(WorldState state)
    {
        foreach (RelationKind kind in Enum.GetValues<RelationKind>())
        {
            int n = 0;
            foreach (Relation r in state.Relations.All)
                if (r.Key.Kind == kind) n++;
            yield return ("relations", kind + " (edges)", n);
        }

        // Relation *values* as well as relation counts. An edge that can never be removed but can
        // fall to zero is a different animal from one that only accumulates, and the grievance
        // ratchet — 260 crossing 40, none clearing — is a claim about the value, not the edge.
        foreach (RelationKind kind in Enum.GetValues<RelationKind>())
        {
            int total = 0;
            foreach (Relation r in state.Relations.All)
                if (r.Key.Kind == kind) total += r.Value;
            yield return ("relations", kind + " (summed value)", total);
        }

        int legitimacy = 0, treasury = 0;
        foreach (Faction f in state.Factions) { legitimacy += f.Legitimacy; treasury += f.Treasury; }
        yield return ("scalars", "faction legitimacy (summed)", legitimacy);
        yield return ("scalars", "faction treasury (summed)", treasury);

        int population = 0;
        int[] stock = new int[Resources.Count];
        foreach (Place p in state.Places)
        {
            population += p.Population;
            for (int r = 0; r < Resources.Count; r++) stock[r] += p.Stockpile[r];
        }
        yield return ("scalars", "settled population (summed)", population);
        foreach (Resource r in Resources.All)
            yield return ("scalars", Resources.Name(r) + " in store (summed)", stock[(int)r]);

        // Goals, swept like everything else as of ruleset 7. They were named in OutsideTheFold and
        // omitted here until the record carried them, because a fold that creates no goals reports
        // "never present" for the state that drives the whole action phase.
        yield return ("collections", "live goals", state.Goals.Snapshot().Count);

        int goalProgress = 0;
        foreach (Goal g in state.Goals.Snapshot()) goalProgress += g.Progress;
        yield return ("scalars", "goal progress (summed)", goalProgress);

        int openArcs = 0;
        foreach (Arc a in state.Arcs) if (a.IsOpen) openArcs++;
        yield return ("collections", "open arcs", openArcs);
        yield return ("collections", "arcs (ever)", state.Arcs.Count);

        int living = 0, exiles = 0, titled = 0;
        foreach (Actor a in state.Actors)
        {
            if (!a.IsAlive) continue;
            living++;
            if (a.Title == Title.Exile) exiles++;
            if (a.Title is not (Title.Commoner or Title.Exile)) titled++;
        }
        yield return ("collections", "living actors", living);
        yield return ("collections", "actors (ever)", state.Actors.Count);
        yield return ("collections", "living exiles", exiles);
        yield return ("collections", "titled actors", titled);

        int standing = 0;
        foreach (Faction f in state.Factions) if (!state.IsDefunct(f.Id)) standing++;
        yield return ("collections", "factions standing", standing);
        yield return ("collections", "factions (ever)", state.Factions.Count);
        yield return ("collections", "places", state.Places.Count);

        int held = 0;
        foreach (Place p in state.Places) if (!p.Controller.IsNone) held++;
        yield return ("collections", "places under a flag", held);
    }

    public static IReadOnlyList<string> Render(IReadOnlyList<StandingItem> rows, int seeds)
    {
        List<string> lines =
        [
            "| group | standing state | on this panel |",
            "|---|---|---|",
        ];

        foreach (StandingItem row in rows)
            lines.Add($"| {row.Group} | `{row.Name}` | {row.Describe()} |");

        int onlyUp = 0, absent = 0;
        foreach (StandingItem row in rows)
        {
            if (row.Exercised == Exercised.OnlyUp) onlyUp++;
            if (row.Exercised == Exercised.NeverPresent) absent++;
        }

        lines.Add("");
        lines.Add($"{rows.Count.ToString(CultureInfo.InvariantCulture)} piece(s) of standing state " +
                  $"over {seeds.ToString(CultureInfo.InvariantCulture)} world(s); " +
                  $"**{onlyUp.ToString(CultureInfo.InvariantCulture)} only ever went up**, " +
                  $"{absent.ToString(CultureInfo.InvariantCulture)} never present.");
        lines.Add("");
        lines.Add("A row that only ever went up is not thereby a row with no removal path, and a " +
                  "row that was never present says nothing about removal at all. Both distinctions " +
                  "are read off the rules and asserted by a person; counts cannot make them.");

        lines.Add("");
        if (OutsideTheFold.Length == 0)
        {
            lines.Add("Standing state this sweep cannot see: **none**. Every piece of state is folded " +
                      "from the log, so a zero here means the panel never held the thing rather than " +
                      "that the instrument could not look.");
        }
        else
        {
            lines.Add("Standing state this sweep cannot see, named rather than scored:");
            foreach (string note in OutsideTheFold) lines.Add($"  - {note}");
        }

        return lines;
    }
}
