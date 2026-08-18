using System.Globalization;
using System.Text;

namespace WorldBuilder.Inference;

/// <summary>
/// One rule's floor coverage across whatever was measured, and how far it reaches.
/// </summary>
/// <param name="Reason">
/// <c>floored everywhere</c>, <c>zero at baseline</c> (reachable, unreached in some scopes), or
/// <c>no floor on this panel</c> (zero in every scope of every seed measured). The last says what
/// was observed and not why — see <see cref="GoldenDiff.FloorClassification"/>.
/// </param>
public sealed record FloorReason(string Rule, int WithFloor, int Scopes, string Reason)
{
    public int Pct => Scopes == 0 ? 0 : WithFloor * 100 / Scopes;
}

/// <summary>One difference between a stored render and a new one.</summary>
public sealed record Drift(string Section, string Kind, string Detail)
{
    /// <summary>
    /// Whether this difference is a regression rather than a rewrite.
    ///
    /// The log does not change between renders, so a figure that moves is wrong in one of the
    /// two. Prose that moves is the model being a model. And a coverage floor that moves down
    /// is a rule that has stopped reading something it used to read, whatever the prose did.
    ///
    /// <c>checked-ratio</c> is deliberately not here. It was the round-12 gate and
    /// <c>coverage-sound</c> supersedes it: a low ratio is now either an ACCOUNTING failure or
    /// an honest handoff to another rule, and gating on it as well would fail builds twice for
    /// one fault and reward extracting less.
    /// </summary>
    public bool Fails => Kind is "figure" or "section-missing" or "floor" or "accounting" or "went-silent";
}

/// <summary>
/// Layer 5: a new chronicle against a stored known-good one.
///
/// The cheapest useful thing in the suite, and the one aimed at the largest single category of
/// defect. Four of round 11's twelve findings were defects that had already been fixed — a date
/// that had been corrected, a figure that had verified, a scope rule that had held — and every
/// one of them was a figure or a phrase that had changed against the previous render. None was
/// caught by the checker. They were caught because a person happened to remember.
///
/// Deliberately not clever. It extracts the numbers a section states and compares them; where
/// they differ, one render is wrong and a human should be told which.
/// </summary>
public static class GoldenDiff
{
    /// <summary>A chronicle split into sections, keyed by heading.</summary>
    public static Dictionary<string, string> Sections(string markdown)
    {
        Dictionary<string, string> sections = new(StringComparer.Ordinal);
        string heading = "";
        StringBuilder body = new();

        foreach (string line in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                Flush();
                heading = line[4..].Trim();
                continue;
            }

            if (line.StartsWith('#')) continue;
            if (line.StartsWith('_') || line.StartsWith('>')) continue;
            body.AppendLine(line);
        }
        Flush();

        return sections;

        void Flush()
        {
            string text = body.ToString().Trim();
            body.Clear();
            if (heading.Length > 0 && text.Length > 0) sections[heading] = text;
        }
    }

    /// <summary>
    /// Tier 1 coverage for a finished document, per section.
    ///
    /// A pure function of the text and the current rules, which is what makes it comparable
    /// across rounds: the log has not changed, so the number of countable claims and dated acts
    /// in a section should barely move even when every sentence does.
    /// </summary>
    public static Dictionary<string, Coverage> CoverageOf(string markdown)
    {
        Dictionary<string, Coverage> coverage = new(StringComparer.Ordinal);

        foreach ((string heading, string body) in Sections(markdown))
        {
            Coverage cover = new();
            SelfConsistency.Check(body, cover);
            coverage[heading] = cover;
        }

        return coverage;
    }

    /// <summary>
    /// <b>coverage-sound</b>: the two invariants that must hold together, per rule per scope.
    ///
    /// <code>
    /// ACCOUNTING   extracted == checked + unresolvable    nothing dropped after extraction
    /// FLOOR        extracted &gt;= previous_extracted        nothing dropped before it
    /// </code>
    ///
    /// Neither is worth anything alone, and three rounds running, one was satisfied by breaking
    /// the other. Round 11: the rules never received input. Round 12: they received it and
    /// discarded it after extraction — ACCOUNTING violated, thirty-two of thirty-three partition
    /// assertions vanishing into an early return. Round 13: the fix for that closed the gap by
    /// extracting less, so extraction collapsed to two assertions with ACCOUNTING perfect,
    /// <c>unresolvable</c> zero and <c>accounted</c> true everywhere — against prose that had not
    /// changed a word.
    ///
    /// ACCOUNTING alone is satisfiable by extracting nothing. FLOOR alone is satisfiable by
    /// extracting everything and checking none of it. Asserted together or not at all.
    ///
    /// The stored counts must be the ones written when the baseline was accepted, never
    /// recomputed from the stored text. Recomputing runs today's rules over yesterday's prose,
    /// so a rule that has since gone quiet reports the same figure on both sides and the
    /// comparison agrees with the bug it exists to find.
    /// </summary>
    public static List<Drift> CoverageSound(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored,
        IReadOnlyDictionary<string, Coverage> current)
    {
        List<Drift> drift = [];

        // FLOOR, over the stored scopes: a rule that read six claims here last round and reads
        // two now has lost four to a lexicon or a pattern, whatever the prose did.
        foreach ((string section, IReadOnlyDictionary<string, RuleCounts> before) in stored)
        {
            if (!current.TryGetValue(section, out Coverage? now)) continue;   // the prose diff has it

            foreach ((string rule, RuleCounts was) in before)
            {
                if (was.Extracted == 0) continue;

                int extracted = now.Rules.TryGetValue(rule, out RuleCounts? c) ? c.Extracted : 0;
                if (extracted >= was.Extracted) continue;

                // Non-zero to zero is called by its own name. It is still a floor breach and
                // still fails, but it is the exact signature of the silent-path family — the
                // rule did not read less, it read nothing — and a report that buries it among
                // ordinary drops makes the one comparison that matters look like the others.
                drift.Add(extracted == 0
                    ? new Drift(section, "went-silent",
                        $"{rule} extracted {was.Extracted} here at the baseline and nothing now")
                    : new Drift(section, "floor",
                        $"{rule} extracted {was.Extracted} here at the baseline and {extracted} now"));
            }
        }

        // ACCOUNTING, over the current scopes: every assertion ends checked or explained.
        foreach ((string section, Coverage now) in current)
            foreach (string line in now.Unaccounted())
                drift.Add(new Drift(section, "accounting", line));

        return drift;
    }

    /// <summary>
    /// How much of the document FLOOR actually protects, and where it does not.
    ///
    /// <b>The gap this closes.</b> <see cref="CoverageSound"/> skips any row the baseline recorded
    /// at zero — correctly, because a rule that read nothing then cannot read less now — and says
    /// nothing about having done so. On seed 42 that is 129 rows of 208: FLOOR protection silently
    /// absent for most of the document, in a layer whose entire purpose is to notice absence. It
    /// is the <c>rule-inert</c> condition one layer up, and it was invisible for the same reason
    /// <c>rule-inert</c> was invented to fix.
    ///
    /// <b>Reported, never failed.</b> A zero floor is not a regression, and a rule that is
    /// construction-gated has honest zeros — <c>partition-sum</c> needs a partition. Gating on
    /// this would manufacture exactly the false positives that once cost seven true sections. The
    /// row is a note whose job is to keep the denominator in front of the reader.
    ///
    /// <b>Why the reason is not classified here.</b> Two different conditions produce a zero: a
    /// rule with no extraction call site at all, and one whose call site this document never
    /// reached. They are indistinguishable inside one document by construction — both read zero —
    /// and separating them needs the whole seed panel. See
    /// <see cref="FloorClassification"/>, which is where the split is drawn and where it belongs.
    /// </summary>
    public static List<Drift> FloorCoverage(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored)
    {
        Dictionary<string, (int WithFloor, int Total)> byRule = new(StringComparer.Ordinal);

        foreach ((_, IReadOnlyDictionary<string, RuleCounts> rules) in stored)
            foreach ((string rule, RuleCounts counts) in rules)
            {
                (int with, int total) = byRule.GetValueOrDefault(rule, (0, 0));
                byRule[rule] = (counts.Extracted > 0 ? with + 1 : with, total + 1);
            }

        List<string> names = [.. byRule.Keys];
        names.Sort(StringComparer.Ordinal);

        List<Drift> drift = [];
        int floored = 0, rows = 0;

        foreach (string rule in names)
        {
            (int with, int total) = byRule[rule];
            floored += with;
            rows += total;

            if (with == total) continue;

            drift.Add(new Drift("(coverage)", "no-floor",
                with == 0
                    ? $"{rule}: no floor in any of {total} scope(s) — FLOOR cannot fail for it here"
                    : $"{rule}: floor in {with} of {total} scope(s); {total - with} unprotected"));
        }

        if (rows > 0)
            drift.Add(new Drift("(coverage)", "floor-reach",
                $"FLOOR compared {floored} of {rows} rule rows across {stored.Count} scope(s)"));

        return drift;
    }

    /// <summary>
    /// Why each rule's floor is missing, drawn across the whole seed panel.
    ///
    /// The split the single-document view cannot make:
    ///
    /// <list type="bullet">
    /// <item><b>zero at baseline</b> — zero in some scopes and non-zero in others. The call site is
    /// reachable and those documents did not reach it, which is a fact about scope selection
    /// rather than about the rule.</item>
    /// <item><b>no floor on this panel</b> — zero in every scope of every seed. FLOOR cannot fail
    /// for it on any seed here.</item>
    /// </list>
    ///
    /// <b>What the second label deliberately does not claim.</b> It would be natural to call it
    /// "not instrumented", and for three of the four rules it finds that is true. It is not true
    /// for <c>quantity</c>, which has an <c>Extracted</c> call site that no panel seed reaches — a
    /// dead path rather than a missing one, and a worse finding rather than a lesser one. Counts
    /// alone cannot tell "no call site" from "call site never reached", and a label asserting the
    /// first would be the absent-versus-unknown conflation this project has now met in five
    /// places. The measurement reports the reach; which of the two produced it is read off the
    /// source and stated by a person.
    /// </summary>
    public static List<FloorReason> FloorClassification(
        IReadOnlyList<IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>>> panel)
    {
        Dictionary<string, (int WithFloor, int Total)> totals = new(StringComparer.Ordinal);

        foreach (IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> seed in panel)
            foreach ((_, IReadOnlyDictionary<string, RuleCounts> rules) in seed)
                foreach ((string rule, RuleCounts counts) in rules)
                {
                    (int with, int total) = totals.GetValueOrDefault(rule, (0, 0));
                    totals[rule] = (counts.Extracted > 0 ? with + 1 : with, total + 1);
                }

        List<string> names = [.. totals.Keys];
        names.Sort(StringComparer.Ordinal);

        List<FloorReason> rows = [];
        foreach (string rule in names)
        {
            (int with, int total) = totals[rule];
            rows.Add(new FloorReason(rule, with, total,
                with == 0 ? "no floor on this panel" : with == total ? "floored everywhere" : "zero at baseline"));
        }

        return rows;
    }

    /// <summary>
    /// Stored counts rebuilt as <see cref="Coverage"/>, so a sidecar read from disk can be
    /// compared against another sidecar rather than against rules re-run over prose.
    ///
    /// Comparing two sidecars is the only sound way to diff a baseline: both sides were written
    /// by the checker that produced them, over packs rather than over text, and both carry all
    /// sixteen rules. Recomputing one side from its markdown yields Tier 1 alone, so every Tier 2
    /// rule reads as having gone to zero — a diff that fails loudly for a reason that is entirely
    /// its own doing.
    /// </summary>
    public static Dictionary<string, Coverage> AsCoverage(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> scopes)
    {
        Dictionary<string, Coverage> rebuilt = new(StringComparer.Ordinal);

        foreach ((string scope, IReadOnlyDictionary<string, RuleCounts> rules) in scopes)
        {
            Coverage cover = new();

            foreach ((string rule, RuleCounts counts) in rules)
            {
                cover.Ran(rule);
                if (counts.Extracted > 0) cover.Extracted(rule, counts.Extracted);
                if (counts.Checked > 0) cover.Checked(rule, counts.Checked);
                if (counts.Unresolvable > 0) cover.Unresolvable(rule, "recorded at the baseline", null, counts.Unresolvable);
                if (counts.Fired > 0) cover.Fired(rule, counts.Fired);
            }

            rebuilt[scope] = cover;
        }

        return rebuilt;
    }

    /// <summary>
    /// Whether accepting this run as the new baseline would lower any floor.
    ///
    /// Re-baselining has to be something a person decides, not something that happens by running
    /// the command again. A floor that quietly follows the last run down is not a floor — it is
    /// the drift it exists to stop, with a file to make it look deliberate.
    /// </summary>
    public static List<Drift> WouldLowerAFloor(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored,
        IReadOnlyDictionary<string, Coverage> current) =>
        [.. CoverageSound(stored, current).Where(d => d.Kind == "floor")];

    /// <summary>
    /// What share of what each rule read it actually evaluated, against a bar.
    ///
    /// A sharper assertion than any diff, because it needs no previous render to compare with:
    /// a rule checking one of the thirty-three assertions it extracted is a failing build and
    /// nobody has to read a chronicle to know it. That was <c>partition-sum</c>'s real state
    /// while every other signal said the section was clean.
    ///
    /// Reported per rule over the whole document rather than per scope. A scope holding two
    /// assertions of a kind swings between 50% and 100% on one sentence, and a bar that trips
    /// on that is a bar people learn to ignore.
    /// </summary>
    public static List<Drift> CheckedRatio(IReadOnlyDictionary<string, Coverage> scopes, int bar = 95)
    {
        Coverage tally = new();
        foreach (Coverage cover in scopes.Values) tally.Merge(cover);

        List<Drift> drift = [];

        foreach (string rule in tally.Names)
        {
            RuleCounts counts = tally.Rules[rule];

            if (!counts.Accounted)
            {
                drift.Add(new Drift("(whole document)", "unaccounted",
                    $"{rule} extracted {counts.Extracted}, checked {counts.Checked} and explained " +
                    $"{counts.Unresolvable} — the rest left by a path that records nothing"));
            }

            if (counts.Extracted == 0 || counts.CheckedPct >= bar) continue;

            string why = string.Join("; ",
                tally.Reasons(rule).Select(r => $"{r.Count} {r.Reason}"));

            drift.Add(new Drift("(whole document)", "checked-ratio",
                $"{rule} evaluated {counts.Checked} of {counts.Extracted} ({counts.CheckedPct}%)" +
                (why.Length == 0 ? "" : $" — {why}")));
        }

        return drift;
    }

    public static List<Drift> Compare(string golden, string current)
    {
        Dictionary<string, string> before = Sections(golden);
        Dictionary<string, string> after = Sections(current);

        List<Drift> drift = [];

        foreach ((string heading, string old) in before)
        {
            if (!after.TryGetValue(heading, out string? now))
            {
                drift.Add(new Drift(heading, "section-missing",
                    "the stored chronicle has this section and the new one does not"));
                continue;
            }

            drift.AddRange(CompareFigures(heading, old, now));

            if (!string.Equals(Normalise(old), Normalise(now), StringComparison.Ordinal))
                drift.Add(new Drift(heading, "prose", "the wording changed"));
        }

        foreach (string heading in after.Keys)
            if (!before.ContainsKey(heading))
                drift.Add(new Drift(heading, "section-added", "not in the stored chronicle"));

        drift.Sort(static (a, b) => a.Fails != b.Fails
            ? b.Fails.CompareTo(a.Fails)
            : string.CompareOrdinal(a.Section + a.Kind, b.Section + b.Kind));

        return drift;
    }

    /// <summary>
    /// The figures a section states, as a multiset, compared between renders.
    ///
    /// A multiset rather than a sequence: a section may legitimately reorder its material, and
    /// what matters is whether it now asserts a number it did not assert before. Years and
    /// counts are not separated — a year that moves is exactly as much a regression as a count
    /// that moves, and three of the last four rounds had a date error.
    /// </summary>
    private static List<Drift> CompareFigures(string heading, string golden, string current)
    {
        Dictionary<int, int> before = Figures(golden);
        Dictionary<int, int> after = Figures(current);

        List<int> keys = [.. before.Keys];
        foreach (int key in after.Keys) if (!before.ContainsKey(key)) keys.Add(key);
        keys.Sort();

        List<Drift> drift = [];

        foreach (int value in keys)
        {
            int was = before.GetValueOrDefault(value);
            int now = after.GetValueOrDefault(value);
            if (was == now) continue;

            drift.Add(new Drift(heading, "figure",
                now == 0 ? $"{value} is gone (stated {was}× before)"
                : was == 0 ? $"{value} is new (stated {now}×)"
                : $"{value} stated {was}× before, {now}× now"));
        }

        return drift;
    }

    /// <summary>Every number a passage states, with how many times it states it.</summary>
    private static Dictionary<int, int> Figures(string passage)
    {
        Dictionary<int, int> counts = [];

        foreach (string raw in passage.Split(
                     [' ', ',', '.', ';', ':', '(', ')', '\n', '\r', '—', '–'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) continue;
            counts[value] = counts.GetValueOrDefault(value) + 1;
        }

        return counts;
    }

    /// <summary>Whitespace-insensitive text, so a reflow is not a rewrite.</summary>
    private static string Normalise(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
