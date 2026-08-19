using System.Globalization;
using WorldBuilder.Core.Geography;

namespace WorldBuilder.Core.Analysis;

/// <summary>Why a mutation key changed nothing, where it changed nothing.</summary>
public enum MutationVerdict
{
    /// <summary>The component the key names moved. The ordinary case.</summary>
    Real = 0,

    /// <summary>
    /// The key names something the state does not hold — a tie that is not live, a goal that has
    /// gone. This is the <c>GoalBook.Remove</c> identity: a claim whose referent is absent.
    /// </summary>
    NoReferent = 1,

    /// <summary>
    /// The referent exists and is already in the state the key asks for — an arc already closed, a
    /// place already held by the named house.
    /// </summary>
    AlreadyThere = 2,

    /// <summary>
    /// The delta is real and a domain clamp absorbs all of it: a legitimacy penalty at zero, a
    /// population loss at an empty place.
    ///
    /// <b>Reported apart from the other two on purpose.</b> A floor is a property of the quantity
    /// rather than a defect in the claim — the rules did apply a penalty of eight and the world's
    /// rule is that legitimacy stops at zero — so suppressing these would delete the record of a
    /// penalty that was levied. It is still a key that changes nothing and it is still counted here,
    /// because "which keys move the world" is the question and a reader should not have to know
    /// which quantities clamp.
    /// </summary>
    Absorbed = 3,

    /// <summary>
    /// The key names the null entity — <c>leg:-</c>, and nothing else on this panel.
    ///
    /// <b>Split from <see cref="NoReferent"/> because the two have different repairs.</b> An absent
    /// referent is a claim about something that used to exist or never did, and the emitting rule can
    /// check for it. A key naming <c>-</c> does not even parse into the shape its reducer case wants,
    /// so it is dropped a step earlier and by a different mechanism — and guarding it belongs in the
    /// one place every delta passes rather than in the rule, which is why ruleset 8 did not repair it
    /// alongside the severances. See <c>EventDraft.Delta</c>.
    ///
    /// Counted rather than folded into the others so the known instances are bounded: a test asserts
    /// the exact number, so this cannot grow while it waits for its own repair.
    /// </summary>
    NoEntity = 4,
}

/// <summary>One payload key that asserted a change, and whether the change happened.</summary>
public sealed record MutationKey(EventId At, int Year, EventKind Kind, string Family, string Key,
    MutationVerdict Verdict, string Note);

/// <summary>
/// One payload key written more than once into the same event.
///
/// <b>Not a phantom mutation, and reported apart from them for that reason.</b> Each occurrence is a
/// real delta the reducer applies in order, so the world is right — <c>Event.Data</c> is an ordered
/// list and both are kept through a round trip, because <c>JsonDocument</c> preserves duplicate
/// property names. What is wrong is the record: one event states two contradictory-looking changes to
/// one edge, anything reading the payload by name sees only the first, and the written JSON has a
/// duplicate object key, which most parsers in the world resolve as last-one-wins.
/// </summary>
public sealed record DuplicateKey(EventId At, int Year, EventKind Kind, string Key, int Times);

/// <summary>Every mutation key of one family, counted by verdict.</summary>
public sealed record FamilyTally(string Family, int Keys, int Real, int NoReferent, int AlreadyThere,
    int Absorbed, int NoEntity)
{
    public int Idle => NoReferent + AlreadyThere + Absorbed + NoEntity;

    /// <summary>
    /// Whether every key of this family moved the world. The audit's per-site verdict.
    /// </summary>
    public bool Clean => Idle == 0;
}

/// <param name="Examined">
/// Mutation keys inspected. Reported so a clean result can be told from a probe that looked at
/// nothing — the same reason the goal-row reach check carries an adversarial arm.
/// </param>
public sealed record MutationAuditSeed(
    ulong Seed,
    int Events,
    int Examined,
    IReadOnlyList<FamilyTally> Families,
    IReadOnlyList<MutationKey> Idle,
    IReadOnlyList<DuplicateKey> Duplicates)
{
    /// <summary>Keys naming something the state does not hold — the family this audit is named for.</summary>
    public int Phantom
    {
        get
        {
            int n = 0;
            foreach (FamilyTally f in Families) n += f.NoReferent;
            return n;
        }
    }

    /// <summary>
    /// Keys naming the null entity. A known, bounded defect awaiting its own repair —
    /// see <see cref="MutationVerdict.NoEntity"/>.
    /// </summary>
    public int Nobody
    {
        get
        {
            int n = 0;
            foreach (FamilyTally f in Families) n += f.NoEntity;
            return n;
        }
    }
}

/// <summary>
/// Every payload key that asserts a state change, and whether the state it names actually moved.
///
/// <b>The third instance of one family.</b> <c>GoalBook.Remove</c> notified its watcher whether or
/// not the book held the goal, and produced 15 phantom endings in 477 — invisible because
/// <c>created − ended = live</c> holds by construction whatever the labels say. The same shape then
/// turned up on the relation graph: a declaration of war carried <c>relDel</c> for an alliance
/// whether or not one existed, so `e:718` deleted a tie that had ended eleven years earlier. Two
/// instances is a coincidence and three is a family, so this measures **every** mutation key rather
/// than repairing the one that was found.
///
/// <b>Measured as a difference, not reasoned from the call sites.</b> §4 of the project reference
/// records what happens when a measurable property of the code is written out by hand: the list of
/// rules lacking floor protection named two that were protected and omitted one that was not, wrong
/// in both directions. So this reads the component each key names on both sides of the fold and asks
/// whether it moved. A site that stops emitting a key disappears from the table rather than passing
/// it, which is why <see cref="MutationAuditSeed.Examined"/> is reported.
///
/// <b>Through <see cref="EventReducer"/>, never a second reading of the payload.</b> The keys are
/// read to know what to probe; what the key *did* comes from the reducer applying it. A fold that
/// reimplements the reducer measures the reimplementation — see <c>RelationTrajectory</c>, which has
/// paid for that twice in both directions.
/// </summary>
public static class MutationAudit
{
    /// <summary>Simulates a world and audits its own record.</summary>
    public static MutationAuditSeed Run(ulong seed, int years)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return Of(sim.Log, seed, sim.State.Board);
    }

    /// <summary>
    /// Audits a log that already exists — a sealed baseline, or a world just run.
    ///
    /// The board is passed rather than looked up, for the reason <c>RelationTrajectory</c> states:
    /// a measurement panel makes one board per seed and never stores it, so "look up the
    /// repository's" is the wrong answer here and fails loudly.
    /// </summary>
    public static MutationAuditSeed Of(EventLog log, ulong seed, Board? board = null)
    {
        List<MutationKey> idle = [];
        Dictionary<string, int[]> tally = new(StringComparer.Ordinal);
        int examined = 0;
        List<DuplicateKey> duplicates = [];

        // Probes taken before the event is applied, read again after.
        //
        // <b>One judgment per distinct key, not per occurrence.</b> An event's payload is an ordered
        // list and nothing stops a draft writing the same key twice — `DecayAndDrift` does, once from
        // the grievance-decay loop and once from the balance-of-power loop — so judging occurrences
        // would report one net effect twice and call a real pair of deltas two idle keys. The
        // duplication is a finding in its own right and is counted as one.
        Dictionary<string, string> was = new(StringComparer.Ordinal);
        List<string> order = [];
        Dictionary<string, int> seen = new(StringComparer.Ordinal);

        Rendering.Replay.Walk(log, seed, (state, e) =>
        {
            foreach (string key in order)
            {
                examined++;

                string before = was[key];
                string after = Probe(state, key);
                MutationVerdict verdict = before == after ? Why(key, before) : MutationVerdict.Real;

                if (!tally.TryGetValue(Family(key), out int[]? counts))
                    tally[Family(key)] = counts = new int[6];

                counts[0]++;
                counts[1 + (int)verdict]++;

                if (verdict != MutationVerdict.Real)
                {
                    idle.Add(new MutationKey(e.Id, e.Year, e.Kind, Family(key), key, verdict,
                        $"{before} → {after}"));
                }

                if (seen[key] > 1) duplicates.Add(new DuplicateKey(e.Id, e.Year, e.Kind, key, seen[key]));
            }
        }, board, (state, e) =>
        {
            was.Clear();
            order.Clear();
            seen.Clear();

            foreach (KeyValuePair<string, string> kv in e.Data)
            {
                if (!IsMutation(kv.Key)) continue;

                seen[kv.Key] = seen.GetValueOrDefault(kv.Key) + 1;
                if (seen[kv.Key] > 1) continue;

                was[kv.Key] = Probe(state, kv.Key);
                order.Add(kv.Key);
            }
        });

        List<FamilyTally> families = [];
        foreach ((string family, int[] counts) in tally.OrderBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            families.Add(new FamilyTally(family, counts[0],
                counts[1 + (int)MutationVerdict.Real],
                counts[1 + (int)MutationVerdict.NoReferent],
                counts[1 + (int)MutationVerdict.AlreadyThere],
                counts[1 + (int)MutationVerdict.Absorbed],
                counts[1 + (int)MutationVerdict.NoEntity]));
        }

        return new MutationAuditSeed(seed, log.Count, examined, families, idle, duplicates);
    }

    /// <summary>
    /// The families this audit knows how to probe.
    ///
    /// <b>Named rather than inferred, and checked against the reducer by a test.</b> A key family
    /// the reducer applies and this list omits would be a site the audit silently does not cover,
    /// which is the unfalsifiable-check shape — so <c>MutationAuditTests</c> asserts the two lists
    /// agree rather than this comment claiming they do.
    /// </summary>
    public static readonly string[] Families =
    [
        "arcEnd", "ctrl", "disown", "goalArc", "goalEnd", "goalStep", "join", "leg", "pop",
        "rel", "relDel", "stock", "treas",
    ];

    /// <summary>
    /// <c>goalAdd</c> is deliberately absent from <see cref="Families"/>.
    ///
    /// It creates rather than mutates, so there is no prior state to compare against — and the
    /// reducer already refuses a second goal of the same kind and target for one owner rather than
    /// quietly placing it, which is the guard this audit exists to look for. Recorded here because
    /// an unexplained omission from a coverage list is indistinguishable from an oversight.
    /// </summary>
    public const string Creation = "goalAdd";

    private static bool IsMutation(string key) => Array.IndexOf(Families, Family(key)) >= 0;

    private static string Family(string key)
    {
        int at = key.IndexOf(':');
        return at < 0 ? key : key[..at];
    }

    /// <summary>
    /// Why a key that changed nothing changed nothing.
    ///
    /// Read off the probe rather than off the key: the probe of an absent referent is a fixed
    /// string, so "no live edge" and "already closed" are distinguishable without a second lookup.
    /// </summary>
    private static MutationVerdict Why(string key, string probe)
    {
        // A key naming the null entity is its own verdict, and it is checked first: `leg:-` reads as
        // an absent referent and is a different defect with a different repair. See
        // <see cref="MutationVerdict.NoEntity"/>.
        if (NamesNobody(key)) return MutationVerdict.NoEntity;

        // An absent referent is an absent referent whatever the family. Listing the families that
        // can have one would be the hand-written-list trap again, and it would mislabel a key naming
        // an entity that never existed as a clamped delta — which is the difference between "the
        // floor took it" and "this key is about nothing".
        if (probe == Absent) return MutationVerdict.NoReferent;

        return Family(key) switch
        {
            "pop" or "stock" or "leg" or "treas" => MutationVerdict.Absorbed,
            _ => MutationVerdict.AlreadyThere,
        };
    }

    /// <summary>
    /// Whether any entity this key names is the null id, which prints as <c>-</c>.
    ///
    /// Read off the key rather than off the probe, because that is where the defect is: the reducer
    /// never gets as far as looking the entity up. <c>leg:-</c> has two tokens where its case wants
    /// three, so it matches nothing and is dropped without a word.
    /// </summary>
    private static bool NamesNobody(string key)
    {
        foreach (string token in key.Split(':'))
            if (token == "-") return true;

        return false;
    }

    /// <summary>What the state does not hold, in a form no real probe can collide with.</summary>
    private const string Absent = "(absent)";

    /// <summary>
    /// The component one key names, rendered so before and after can be compared as strings.
    ///
    /// A string rather than a number because the components are of five different types — a
    /// controller is an id, an arc's end is a nullable year, a relation is presence and a value —
    /// and one comparison over all of them is what keeps the classification in one place.
    /// </summary>
    private static string Probe(WorldState state, string key)
    {
        string[] parts = key.Split(':');

        switch (parts[0])
        {
            case "pop" when parts.Length == 3:
                return Place(state, parts, 1) is { } p
                    ? p.Population.ToString(CultureInfo.InvariantCulture)
                    : Absent;

            case "stock" when parts.Length == 4:
            {
                if (Place(state, parts, 1) is not { } place) return Absent;
                if (!Enum.TryParse(parts[3], ignoreCase: true, out Resource r)) return Absent;
                return place.Stockpile[(int)r].ToString(CultureInfo.InvariantCulture);
            }

            case "leg" when parts.Length == 3:
                return Faction(state, parts, 1) is { } f
                    ? f.Legitimacy.ToString(CultureInfo.InvariantCulture)
                    : Absent;

            case "treas" when parts.Length == 3:
                return Faction(state, parts, 1) is { } t
                    ? t.Treasury.ToString(CultureInfo.InvariantCulture)
                    : Absent;

            case "ctrl" when parts.Length == 3:
                return Place(state, parts, 1) is { } held ? held.Controller.ToString() : Absent;

            case "join" when parts.Length == 3:
            case "disown" when parts.Length == 3:
                return Actor(state, parts, 1) is { } a ? $"{a.Faction}/{a.Title}" : Absent;

            case "arcEnd" when parts.Length == 3:
            {
                if (!EntityId.TryParse($"{parts[1]}:{parts[2]}", out EntityId id)) return Absent;
                if (id.Index == 0 || id.Index > state.Arcs.Count) return Absent;
                return state.ArcOf(id).EndYear?.ToString(CultureInfo.InvariantCulture) ?? "open";
            }

            case "rel" when parts.Length == 6:
            case "relDel" when parts.Length == 6:
            {
                if (!EntityId.TryParse($"{parts[1]}:{parts[2]}", out EntityId from)) return Absent;
                if (!EntityId.TryParse($"{parts[3]}:{parts[4]}", out EntityId to)) return Absent;
                if (!Enum.TryParse(parts[5], ignoreCase: true, out RelationKind kind)) return Absent;

                Relation? tie = state.Relations.Find(from, to, kind);
                return tie is null ? Absent : tie.Value.ToString(CultureInfo.InvariantCulture);
            }

            // The goal keys name a goal by the id the fold assigned it. Absent means the book does
            // not hold it — which the reducer refuses rather than shrugs at, so a NoReferent verdict
            // here would be unreachable; probed anyway, because "unreachable" is a claim about the
            // reducer that this audit is in a position to check.
            case "goalStep" when parts.Length == 2:
            case "goalArc" when parts.Length == 2:
            case "goalEnd" when parts.Length == 2:
            {
                if (!int.TryParse(parts[1], CultureInfo.InvariantCulture, out int id)) return Absent;

                foreach (Goal g in state.Goals.Snapshot())
                {
                    if (g.Id != id) continue;
                    return parts[0] switch
                    {
                        "goalArc" => g.Arc.ToString(),
                        "goalStep" => g.Progress.ToString(CultureInfo.InvariantCulture),
                        _ => "held",
                    };
                }

                return Absent;
            }

            default:
                return Absent;
        }
    }

    private static Place? Place(WorldState state, string[] parts, int at) =>
        Bounded(parts, at, state.Places.Count) is { } id ? state.PlaceOf(id) : null;

    private static Faction? Faction(WorldState state, string[] parts, int at) =>
        Bounded(parts, at, state.Factions.Count) is { } id ? state.FactionOf(id) : null;

    private static Actor? Actor(WorldState state, string[] parts, int at) =>
        Bounded(parts, at, state.Actors.Count) is { } id ? state.ActorOf(id) : null;

    /// <summary>
    /// The id a key names, or null where the state has no such entity yet.
    ///
    /// The genesis events name entities the same event creates, so a probe taken before one of them
    /// is applied is legitimately looking at a world that has none — which is an absent referent and
    /// not a bad key.
    /// </summary>
    private static EntityId? Bounded(string[] parts, int at, int count)
    {
        if (!EntityId.TryParse($"{parts[at]}:{parts[at + 1]}", out EntityId id)) return null;
        return id.Index >= 1 && id.Index <= count ? id : null;
    }

    // ---- reporting --------------------------------------------------------

    public static IReadOnlyList<string> Render(IReadOnlyList<MutationAuditSeed> panel)
    {
        List<string> lines =
        [
            "## Mutation-notify audit — does every key that asserts a change make one?",
            "",
            $"{N(panel.Count)} world(s): " +
            string.Join(", ", panel.Select(static s => $"seed {N2(s.Seed)} ({N(s.Events)} records)")) + ".",
            "",
            "**Every payload key the reducer applies is probed on both sides of its own event.** A " +
            "key is `real` where the component it names moved, and idle where it did not. Idle " +
            "splits four ways, and only the first is the `GoalBook.Remove` family:",
            "",
            "- `no referent` — the key names something the state does not hold. A claim about a tie " +
            "that is not live or a goal that has gone. **This is what ruleset 8 repaired.**",
            "- `no entity` — the key names the null id, which prints as `-`. A different defect with " +
            "a different repair, and not fixed at ruleset 8: it does not parse into the shape its " +
            "reducer case wants, so it is dropped a step earlier. Bounded by a test rather than " +
            "left to grow.",
            "- `already there` — the referent exists and is already in the state the key asks for.",
            "- `absorbed` — the delta is real and a floor or ceiling takes all of it. A property of " +
            "the quantity rather than a false claim, and reported apart for that reason.",
            "",
            "| family | keys | real | no referent | no entity | already there | absorbed | clean |",
            "|---|---|---|---|---|---|---|---|",
        ];

        SortedDictionary<string, int[]> pooled = new(StringComparer.Ordinal);
        foreach (MutationAuditSeed seed in panel)
        {
            foreach (FamilyTally f in seed.Families)
            {
                if (!pooled.TryGetValue(f.Family, out int[]? row)) pooled[f.Family] = row = new int[6];
                row[0] += f.Keys;
                row[1] += f.Real;
                row[2] += f.NoReferent;
                row[3] += f.NoEntity;
                row[4] += f.AlreadyThere;
                row[5] += f.Absorbed;
            }
        }

        foreach ((string family, int[] row) in pooled)
        {
            bool clean = row[2] + row[3] + row[4] + row[5] == 0;
            lines.Add($"| `{family}` | {N(row[0])} | {N(row[1])} | {Mark(row[2])} | {Mark(row[3])} " +
                      $"| {Mark(row[4])} | {Mark(row[5])} | {(clean ? "yes" : "**no**")} |");
        }

        int examined = panel.Sum(static s => s.Examined);
        int idle = panel.Sum(static s => s.Idle.Count);

        lines.Add("");
        lines.Add($"**{N(examined)} mutation key(s) examined across the panel, {N(idle)} idle.** The " +
                  "examined figure is here because a site that stops emitting a key altogether would " +
                  "leave a clean table and an unchanged world — a check that cannot fire is the " +
                  "shape this project has been caught by five times.");
        lines.Add("");

        lines.Add($"`{Creation}` is not probed: it creates rather than mutates, so there is no prior " +
                  "state to compare, and the reducer already refuses a duplicate rather than placing " +
                  "one quietly.");
        lines.Add("");

        lines.AddRange(Duplicates(panel));

        // Idle keys, grouped by the event kind that emitted them, because the site is what gets
        // repaired and a list of 600 record ids names no site.
        SortedDictionary<string, (int Count, MutationVerdict Verdict, EventId First, int Year)> sites =
            new(StringComparer.Ordinal);

        // Grouped by verdict as well as by site. One event kind can produce two different verdicts on
        // the same family — a legitimacy penalty a floor absorbs and one naming a house the state has
        // no record of — and a group keyed on the site alone reports whichever came first, which is
        // how the second verdict hides behind the commoner one.
        foreach (MutationAuditSeed seed in panel)
        {
            foreach (MutationKey k in seed.Idle)
            {
                string site = $"{EventKinds.Name(k.Kind)} / `{k.Family}` / `{k.Verdict}`";
                if (sites.TryGetValue(site, out (int Count, MutationVerdict V, EventId At, int Y) had))
                    sites[site] = (had.Count + 1, had.V, had.At, had.Y);
                else
                    sites[site] = (1, k.Verdict, k.At, k.Year);
            }
        }

        if (sites.Count == 0)
        {
            lines.Add("**No idle key anywhere in the panel.** Every key that asserts a change makes " +
                      "one.");
            return lines;
        }

        lines.Add("### Where the idle keys come from");
        lines.Add("");
        lines.Add("| event kind / family | idle keys | verdict | first |");
        lines.Add("|---|---|---|---|");

        foreach ((string site, (int count, MutationVerdict verdict, EventId at, int year)) in
                 sites.OrderByDescending(static kv => kv.Value.Count).ThenBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            lines.Add($"| {site} | {N(count)} | `{verdict}` | `{at}` Y{N(year)} |");
        }

        // Worked examples with the two values, because a verdict without the reading behind it is
        // the same unlabelled figure this project keeps paying for. One per site is enough to see
        // whether the classification is right.
        lines.Add("");
        lines.Add("### One of each, with the reading");
        lines.Add("");
        lines.Add("| seed | record | kind | key | verdict | before → after |");
        lines.Add("|---|---|---|---|---|---|");

        HashSet<string> shown = new(StringComparer.Ordinal);
        foreach (MutationAuditSeed seed in panel)
        {
            foreach (MutationKey k in seed.Idle)
            {
                string site = $"{EventKinds.Name(k.Kind)} / {k.Family} / {k.Verdict}";
                if (!shown.Add(site)) continue;

                lines.Add($"| {N2(seed.Seed)} | `{k.At}` Y{N(k.Year)} | {EventKinds.Name(k.Kind)} " +
                          $"| `{k.Key}` | `{k.Verdict}` | {k.Note} |");
            }
        }

        return lines;
    }

    /// <summary>
    /// Keys written twice into one event — an adjacent finding, kept adjacent.
    ///
    /// Found by this audit rather than looked for: the probe read one edge as unchanged across an
    /// event that carried two deltas for it, and the two cancelled. It is the reason the `rel` row
    /// above has any idle keys at all.
    /// </summary>
    private static IReadOnlyList<string> Duplicates(IReadOnlyList<MutationAuditSeed> panel)
    {
        int total = panel.Sum(static s => s.Duplicates.Count);
        if (total == 0)
        {
            return
            [
                "**No payload key is written twice into one event** anywhere in the panel.",
                "",
            ];
        }

        SortedDictionary<string, (int Keys, int Events, EventId First, int Year)> sites =
            new(StringComparer.Ordinal);
        HashSet<EventId> events = [];

        foreach (MutationAuditSeed seed in panel)
        {
            foreach (DuplicateKey d in seed.Duplicates)
            {
                events.Add(d.At);
                string site = $"{EventKinds.Name(d.Kind)} / `{Family(d.Key)}`";
                if (sites.TryGetValue(site, out (int K, int E, EventId At, int Y) had))
                    sites[site] = (had.K + 1, had.E, had.At, had.Y);
                else
                    sites[site] = (1, 0, d.At, d.Year);
            }
        }

        List<string> lines =
        [
            "### One key, written twice into one event",
            "",
            $"**{N(total)} key(s) appear more than once in their own event**, across " +
            $"{N(events.Count)} event(s). Found by this audit rather than looked for: the probe read " +
            "an edge as unchanged across an event carrying two deltas for it, and the two cancelled.",
            "",
            "**Not a phantom mutation.** Every occurrence is a real delta and the reducer applies " +
            "them in order, so the world is right, and both survive a round trip because " +
            "`Event.Data` is an ordered list and `JsonDocument` keeps duplicate property names. What " +
            "is wrong is the record: one event states two opposite changes to one edge, any reader " +
            "fetching the key by name sees only the first, and the written JSON carries a duplicate " +
            "object key that most parsers outside this repository resolve as last-one-wins.",
            "",
            "| event kind / family | duplicated keys | first |",
            "|---|---|---|",
        ];

        foreach ((string site, (int keys, int _, EventId at, int year)) in
                 sites.OrderByDescending(static kv => kv.Value.Keys).ThenBy(static kv => kv.Key, StringComparer.Ordinal))
        {
            lines.Add($"| {site} | {N(keys)} | `{at}` Y{N(year)} |");
        }

        lines.Add("");
        return lines;
    }

    private static string Mark(int n) => n == 0 ? "0" : $"**{N(n)}**";

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string N2(ulong value) => value.ToString(CultureInfo.InvariantCulture);
}
