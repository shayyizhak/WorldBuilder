using System.Globalization;
using System.Text.Json;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>One row of a findings sidecar, read back rather than recomputed.</summary>
/// <param name="Kind">
/// The finding kind, which is what the sidecar calls <c>rule</c> — <c>wrong-year</c>,
/// <c>partition-sum</c>, <c>rule-inert</c>. <see cref="Rule"/> is the rule that owns it.
/// </param>
public sealed record SidecarFinding(string Kind, string Scope, string Span, string Detail, bool Blocking, bool Fatal)
{
    /// <summary>
    /// The rule this finding is attributed to.
    ///
    /// An inert entry names its rule in the span rather than in the kind, because the kind is the
    /// literal <c>rule-inert</c>. Reading the kind for those would attribute every silence in the
    /// document to one imaginary rule called "rule-inert", which is the shape of an answer that
    /// looks like a strong signal and is an artefact of the file format.
    /// </summary>
    public string Rule => string.Equals(Kind, "rule-inert", StringComparison.Ordinal)
        ? Span
        : RuleNames.Of(Kind);
}

/// <summary>A scope kept out of canon, and what put it there.</summary>
public sealed record HeldOut(string Scope, IReadOnlyList<string> Rules, int Blocking, int Fatal);

/// <summary>What one seed's sidecar says about holdouts.</summary>
public sealed record SeedHoldouts(ulong Seed, IReadOnlyList<string> Scopes, IReadOnlyList<HeldOut> Excluded)
{
    public int Total => Scopes.Count;

    /// <summary>Held out as a percentage of scopes offered. Zero scopes reports zero, not a divide.</summary>
    public int RatePct => Total == 0 ? 0 : Excluded.Count * 100 / Total;
}

/// <summary>Where each rule's verdict falls, once the whole panel is in one place.</summary>
public enum HoldoutVerdict
{
    /// <summary>Fewer than ten holdouts across the panel. The grouping question is void, by prior agreement.</summary>
    Underpowered = 1,

    /// <summary>One rule accounts for most of the panel and is firing more elsewhere than it used to.</summary>
    OverFiring = 2,

    /// <summary>Holdouts spread across four or more rules at a rate that holds across seeds.</summary>
    CheckerWorking = 3,

    /// <summary>Neither. Recorded, and escalated as prose judgement rather than decided here.</summary>
    Escalate = 4,
}

/// <summary>
/// Where the checker's holdouts land, grouped by the rule that caused them.
///
/// <b>The question.</b> Ruleset 4 holds out six of thirteen scopes on seed 42 where v1 held out
/// three of fifteen. That is either the checker working harder on a harder world or one or two
/// rules over-firing, and the two look identical from a single document.
///
/// <b>Read from the sidecar, never recomputed.</b> Re-running today's rules over yesterday's prose
/// gives the same figure on both sides of the comparison, so a rule that has since gone quiet
/// agrees with the bug it was meant to expose. The same argument
/// <see cref="GoldenDiff.CompareCoverage"/> makes, and it applies with more force here because the
/// two sides are different rulesets.
///
/// <b>What a cross-ruleset comparison can and cannot say.</b> Ruleset 4 assigns positions at
/// worldgen and four mechanics consume distance, so the same seed is a different history: the
/// scope lists are not two versions of one document and the denominators are not comparable in
/// the way two runs of one ruleset would be. Firing counts still compare, because a rule going
/// silent between rulesets is the signature this exists to catch, and that signature does not
/// depend on the two documents describing the same events.
/// </summary>
public static class Holdouts
{
    /// <summary>The seed panel. Five, because five baselines were cut, not because five is a sample size.</summary>
    public static readonly ulong[] Panel = Core.ReferencePanel.Sealed;

    /// <summary>Findings and their fatal flags, back out of a stored sidecar.</summary>
    public static List<SidecarFinding> ReadFindings(string path)
    {
        List<SidecarFinding> findings = [];

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("findings", out JsonElement list)) return findings;

        foreach (JsonElement f in list.EnumerateArray())
        {
            findings.Add(new SidecarFinding(
                f.GetProperty("rule").GetString() ?? "",
                f.GetProperty("scope").GetString() ?? "",
                f.GetProperty("span").GetString() ?? "",
                f.GetProperty("detail").GetString() ?? "",
                f.GetProperty("blocking").GetBoolean(),
                f.GetProperty("fatal").GetBoolean()));
        }

        return findings;
    }

    /// <summary>
    /// The sidecar for one seed of one baseline set.
    ///
    /// <c>baselines/&lt;set&gt;/seed-&lt;n&gt;/chronicle-&lt;n&gt;.findings.json</c>, and it must
    /// exist. A missing sidecar reported as an empty panel would read as a seed with no holdouts,
    /// which is the wrong answer rather than a missing one.
    /// </summary>
    public static string SidecarPath(string root, string set, ulong seed)
    {
        string n = seed.ToString(CultureInfo.InvariantCulture);
        return Path.Combine(root, set, $"seed-{n}", $"chronicle-{n}.findings.json");
    }

    public static SeedHoldouts ForSeed(string root, string set, ulong seed)
    {
        string path = SidecarPath(root, set, seed);
        if (!File.Exists(path)) throw new FileNotFoundException($"no sidecar for {set} seed {seed}", path);

        List<SidecarFinding> findings = ReadFindings(path);
        List<string> scopes = [.. FindingsSidecar.ReadCoverage(path).Keys];

        // A scope is held out where a finding in it was marked fatal. That flag is written by the
        // one place that decides — a scope kept out of canon makes every blocking finding in it
        // fatal — so reading it back is reading the decision rather than re-deriving it from the
        // findings and hoping the reconstruction matches.
        Dictionary<string, List<SidecarFinding>> byScope = new(StringComparer.Ordinal);
        foreach (SidecarFinding f in findings)
        {
            if (!f.Fatal) continue;
            if (!byScope.TryGetValue(f.Scope, out List<SidecarFinding>? bucket))
                byScope[f.Scope] = bucket = [];
            bucket.Add(f);
        }

        List<HeldOut> excluded = [];

        foreach (string scope in scopes)
        {
            if (!byScope.TryGetValue(scope, out List<SidecarFinding>? fatal)) continue;

            List<string> rules = [.. fatal.Select(static f => f.Rule).Distinct(StringComparer.Ordinal)];
            rules.Sort(StringComparer.Ordinal);

            excluded.Add(new HeldOut(scope, rules, fatal.Count(static f => f.Blocking), fatal.Count));
        }

        // A fatal finding on a scope the coverage block never listed would vanish here. It cannot
        // happen through the writer — both halves come from the same scope list — and it is worth
        // one line to make sure of that rather than to assume it.
        foreach (string scope in byScope.Keys)
            if (!scopes.Contains(scope, StringComparer.Ordinal))
                throw new InvalidDataException($"{path}: fatal finding on \"{scope}\", which has no coverage block");

        return new SeedHoldouts(seed, scopes, excluded);
    }

    /// <summary>
    /// Firing counts per rule, summed over the scopes that survived.
    ///
    /// Held-out scopes are excluded on purpose. A rule that put a scope out of canon fired inside
    /// it by definition, so counting those would answer "did this rule fire where it caused a
    /// holdout" — which is a tautology. The question is whether the same rule is also firing more
    /// on the passages that stayed in, which is what distinguishes a rule working harder from a
    /// rule over-firing.
    /// </summary>
    public static Dictionary<string, int> FiringOnSurvivors(string root, string set, IReadOnlyList<ulong> seeds)
    {
        Dictionary<string, int> fired = new(StringComparer.Ordinal);

        foreach (ulong seed in seeds)
        {
            SeedHoldouts holdouts = ForSeed(root, set, seed);
            HashSet<string> excluded = new(holdouts.Excluded.Select(static h => h.Scope), StringComparer.Ordinal);

            foreach ((string scope, IReadOnlyDictionary<string, RuleCounts> rules)
                     in FindingsSidecar.ReadCoverage(SidecarPath(root, set, seed)))
            {
                if (excluded.Contains(scope)) continue;
                foreach ((string rule, RuleCounts counts) in rules)
                    fired[rule] = fired.GetValueOrDefault(rule) + counts.Fired;
            }
        }

        return fired;
    }

    /// <summary>
    /// Extraction counts per rule, summed over the scopes that survived.
    ///
    /// <b>Reported, and fenced out of the decision rule.</b> The pre-committed arms are stated in
    /// firing counts and they stay stated in firing counts — an analyst who swaps the statistic
    /// after seeing the table is not running a pre-registered test, whatever the new statistic's
    /// merits. This exists because the firing figure turned out to be structurally near-zero on
    /// survivors, which is a fact about the instrument worth recording beside the verdict rather
    /// than a licence to change the verdict.
    /// </summary>
    public static Dictionary<string, int> ExtractionOnSurvivors(string root, string set, IReadOnlyList<ulong> seeds)
    {
        Dictionary<string, int> extracted = new(StringComparer.Ordinal);

        foreach (ulong seed in seeds)
        {
            SeedHoldouts holdouts = ForSeed(root, set, seed);
            HashSet<string> excluded = new(holdouts.Excluded.Select(static h => h.Scope), StringComparer.Ordinal);

            foreach ((string scope, IReadOnlyDictionary<string, RuleCounts> rules)
                     in FindingsSidecar.ReadCoverage(SidecarPath(root, set, seed)))
            {
                if (excluded.Contains(scope)) continue;
                foreach ((string rule, RuleCounts counts) in rules)
                    extracted[rule] = extracted.GetValueOrDefault(rule) + counts.Extracted;
            }
        }

        return extracted;
    }

    /// <summary>A rule that raised a finding in a scope its extraction counter says it never read.</summary>
    public sealed record Unaccounted(ulong Seed, string Scope, string Rule, int Fired, IReadOnlyList<string> Kinds)
    {
        /// <summary>
        /// True where the same scope also carries a <c>rule-inert</c> row for this rule.
        ///
        /// Both rows in one file is the contradiction worth naming: the sidecar says the rule
        /// extracted nothing here, and says a finding it owns kept the section out of canon.
        /// </summary>
        public required bool AlsoReportedInert { get; init; }
    }

    /// <summary>
    /// Findings raised by rules whose extraction counter stayed at zero.
    ///
    /// <b>Why this matters more than it looks.</b> The floor invariant is
    /// <c>extracted &gt;= previous_extracted</c>, so a rule that fires without extracting has a
    /// floor of zero and can go silent forever without the golden layer noticing. That is the
    /// silent-path signature, sitting inside the mechanism built to detect it — the same shape the
    /// project reference forbids on purpose for a geography rule written before its terrain pack
    /// exists, arrived at here by accident, in rules that are firing right now.
    ///
    /// Reported and not fixed. Correcting an extraction counter raises a floor, and re-baselining a
    /// floor is an explicit human action rather than something that happens by rerunning.
    /// </summary>
    public static List<Unaccounted> FiredWithoutExtraction(string root, string set, IReadOnlyList<ulong> seeds)
    {
        List<Unaccounted> rows = [];

        foreach (ulong seed in seeds)
        {
            string path = SidecarPath(root, set, seed);
            List<SidecarFinding> findings = ReadFindings(path);

            foreach ((string scope, IReadOnlyDictionary<string, RuleCounts> rules) in FindingsSidecar.ReadCoverage(path))
                foreach ((string rule, RuleCounts counts) in rules)
                {
                    if (counts.Fired == 0 || counts.Extracted > 0) continue;

                    List<string> kinds =
                    [
                        .. findings
                            .Where(f => string.Equals(f.Scope, scope, StringComparison.Ordinal)
                                        && !string.Equals(f.Kind, "rule-inert", StringComparison.Ordinal)
                                        && string.Equals(f.Rule, rule, StringComparison.Ordinal))
                            .Select(static f => f.Kind)
                            .Distinct(StringComparer.Ordinal),
                    ];

                    kinds.Sort(StringComparer.Ordinal);

                    rows.Add(new Unaccounted(seed, scope, rule, counts.Fired, kinds)
                    {
                        AlsoReportedInert = findings.Any(f =>
                            string.Equals(f.Kind, "rule-inert", StringComparison.Ordinal)
                            && string.Equals(f.Scope, scope, StringComparison.Ordinal)
                            && string.Equals(f.Span, rule, StringComparison.Ordinal)),
                    });
                }
        }

        return rows;
    }

    /// <summary>The whole panel, both sets, and the verdict the decision rules give it.</summary>
    public sealed record Report
    {
        public required string Set { get; init; }
        public required string Against { get; init; }
        public required IReadOnlyList<SeedHoldouts> Seeds { get; init; }
        public required IReadOnlyList<SeedHoldouts> Baseline { get; init; }

        /// <summary>Held-out scopes attributed to each rule. A scope with two causes counts under both.</summary>
        public required IReadOnlyDictionary<string, int> ByRule { get; init; }

        public required IReadOnlyDictionary<string, int> FiredOnSurvivors { get; init; }
        public required IReadOnlyDictionary<string, int> FiredOnSurvivorsBaseline { get; init; }

        /// <summary>Reported beside the verdict, never used by it. See <see cref="ExtractionOnSurvivors"/>.</summary>
        public required IReadOnlyDictionary<string, int> ExtractedOnSurvivors { get; init; }

        public required IReadOnlyDictionary<string, int> ExtractedOnSurvivorsBaseline { get; init; }

        /// <summary>Findings raised by rules whose extraction counter never moved.</summary>
        public required IReadOnlyList<Unaccounted> Unattributed { get; init; }

        public int TotalHoldouts => Seeds.Sum(static s => s.Excluded.Count);
        public int TotalScopes => Seeds.Sum(static s => s.Total);

        /// <summary>
        /// The spread of per-seed holdout rates, as an interval that prints both ends and its width.
        ///
        /// The decision rule below is stated in points of width, and a bare 30 would read as a
        /// standard deviation to anyone who met it downstream. That confusion has already cost
        /// this project one verdict.
        /// </summary>
        public Dispersion RateRange => Dispersion.Range(
            Seeds.Count == 0 ? 0 : Seeds.Min(static s => s.RatePct),
            Seeds.Count == 0 ? 0 : Seeds.Max(static s => s.RatePct),
            Seeds.Count);

        /// <summary>The rule causing most holdouts, and its share of the panel as a percentage.</summary>
        public (string Rule, int Scopes, int SharePct) Heaviest
        {
            get
            {
                if (ByRule.Count == 0 || TotalHoldouts == 0) return ("", 0, 0);

                KeyValuePair<string, int> top = ByRule
                    .OrderByDescending(static p => p.Value)
                    .ThenBy(static p => p.Key, StringComparer.Ordinal)
                    .First();

                return (top.Key, top.Value, top.Value * 100 / TotalHoldouts);
            }
        }

        /// <summary>
        /// Rules that fired on surviving scopes at the comparison set and fire nowhere now.
        ///
        /// The silent-path signature, and it escalates on its own regardless of every other
        /// figure here: five times out of five, the rule was correct and its input stopped
        /// arriving. Reported as a list because the count alone is the least useful form of it.
        /// </summary>
        public IReadOnlyList<string> WentSilent
        {
            get
            {
                List<string> silent = [];

                foreach ((string rule, int before) in FiredOnSurvivorsBaseline)
                    if (before > 0 && FiredOnSurvivors.GetValueOrDefault(rule) == 0)
                        silent.Add(rule);

                silent.Sort(StringComparer.Ordinal);
                return silent;
            }
        }

        /// <summary>
        /// The pre-committed rules, applied mechanically.
        ///
        /// Written before the figures were computed and evaluated here rather than read off a
        /// table by hand, so the arm that gets taken is not a matter of which figure caught the
        /// eye. Pre-registration constrains the analyst; it only does that if the analyst does not
        /// get to pick the arm afterwards.
        /// </summary>
        public HoldoutVerdict Verdict
        {
            get
            {
                // The degeneracy guard fires first and outranks everything below it. A pattern
                // read out of single-digit totals is the failure mode the guard exists for.
                if (TotalHoldouts < 10) return HoldoutVerdict.Underpowered;

                (string rule, int _, int share) = Heaviest;

                bool firingMore = rule.Length > 0
                    && FiredOnSurvivors.GetValueOrDefault(rule) > FiredOnSurvivorsBaseline.GetValueOrDefault(rule);

                if (share >= 60 && firingMore) return HoldoutVerdict.OverFiring;

                int distinct = ByRule.Count(static p => p.Value > 0);
                if (distinct >= 4 && RateRange.Width <= 20) return HoldoutVerdict.CheckerWorking;

                return HoldoutVerdict.Escalate;
            }
        }

        /// <summary>True where the phase's halt conditions are met, whatever the verdict's wording.</summary>
        public bool Halts => WentSilent.Count > 0 || Verdict is HoldoutVerdict.OverFiring or HoldoutVerdict.Escalate;
    }

    public static Report Build(string root, string set, string against, IReadOnlyList<ulong>? seeds = null)
    {
        IReadOnlyList<ulong> panel = seeds ?? Panel;

        List<SeedHoldouts> current = [.. panel.Select(s => ForSeed(root, set, s))];
        List<SeedHoldouts> baseline = [.. panel.Select(s => ForSeed(root, against, s))];

        Dictionary<string, int> byRule = new(StringComparer.Ordinal);
        foreach (SeedHoldouts seed in current)
            foreach (HeldOut scope in seed.Excluded)
                foreach (string rule in scope.Rules)
                    byRule[rule] = byRule.GetValueOrDefault(rule) + 1;

        return new Report
        {
            Set = set,
            Against = against,
            Seeds = current,
            Baseline = baseline,
            ByRule = byRule,
            FiredOnSurvivors = FiringOnSurvivors(root, set, panel),
            FiredOnSurvivorsBaseline = FiringOnSurvivors(root, against, panel),
            ExtractedOnSurvivors = ExtractionOnSurvivors(root, set, panel),
            ExtractedOnSurvivorsBaseline = ExtractionOnSurvivors(root, against, panel),
            Unattributed = FiredWithoutExtraction(root, set, panel),
        };
    }

    /// <summary>The whole thing as lines, in the shape the phase report needs.</summary>
    public static List<string> Render(Report report)
    {
        List<string> lines =
        [
            $"# Holdout distribution — {report.Set} against {report.Against}",
            "",
            $"Read from the stored sidecars. {report.TotalHoldouts} held-out scope(s) of " +
            $"{report.TotalScopes} across {report.Seeds.Count} seeds.",
            "",
            "## Per seed",
            "",
            "| seed | scopes | held out | rate | rules |",
            "|---|---|---|---|---|",
        ];

        foreach (SeedHoldouts seed in report.Seeds)
        {
            List<string> rules = [.. seed.Excluded.SelectMany(static h => h.Rules).Distinct(StringComparer.Ordinal)];
            rules.Sort(StringComparer.Ordinal);

            lines.Add($"| {seed.Seed} | {seed.Total} | {seed.Excluded.Count} | {seed.RatePct}% | " +
                      $"{(rules.Count == 0 ? "—" : string.Join(", ", rules))} |");
        }

        lines.Add("");
        lines.Add($"Per-seed holdout rate {report.RateRange}, in percentage points.");
        lines.Add("");

        // Said here rather than left to whoever reads the table, because the number is the first
        // thing anyone reaches for and it is the one thing in this file that will not bear weight.
        lines.Add("> **The rate is a draw, not a measurement, and is retired as a halt condition.** " +
                  "A cut with no render cache to inherit has its prose written again by a " +
                  "non-deterministic model, and a different half of the chronicle falls out of " +
                  "canon: ruleset 7 → 8 moved the panel rate 34.5% → 44.8% on worlds byte-identical " +
                  "apart from fourteen payload keys nothing reads. Every cross-ruleset cut is cold, " +
                  "so rates from different rulesets are independent draws rather than a series. " +
                  "Compare across warm cuts; never as a gate. **What the rest of this file says " +
                  "about rules — which fire, which have floors, which extract nothing — is " +
                  "structural and unaffected.**");
        lines.Add("");
        lines.Add("## Every held-out scope");
        lines.Add("");
        lines.Add("| seed | scope | rules | blocking | fatal |");
        lines.Add("|---|---|---|---|---|");

        foreach (SeedHoldouts seed in report.Seeds)
            foreach (HeldOut scope in seed.Excluded)
                lines.Add($"| {seed.Seed} | {scope.Scope} | {string.Join(", ", scope.Rules)} | " +
                          $"{scope.Blocking} | {scope.Fatal} |");

        lines.Add("");
        lines.Add("## Grouped by rule");
        lines.Add("");
        lines.Add("Firing counts are the pre-committed statistic. Extraction is beside them for reading " +
                  "and takes no part in the verdict.");
        lines.Add("");
        lines.Add($"| rule | held-out scopes | share | fired on survivors ({report.Set}) | " +
                  $"fired on survivors ({report.Against}) | extracted on survivors ({report.Set}) | " +
                  $"extracted on survivors ({report.Against}) |");
        lines.Add("|---|---|---|---|---|---|---|");

        HashSet<string> everyRule = new(report.ByRule.Keys, StringComparer.Ordinal);
        everyRule.UnionWith(report.FiredOnSurvivors.Keys);
        everyRule.UnionWith(report.FiredOnSurvivorsBaseline.Keys);

        List<string> ordered = [.. everyRule];
        ordered.Sort(StringComparer.Ordinal);

        foreach (string rule in ordered)
        {
            int held = report.ByRule.GetValueOrDefault(rule);
            int share = report.TotalHoldouts == 0 ? 0 : held * 100 / report.TotalHoldouts;

            lines.Add($"| {rule} | {held} | {share}% | {report.FiredOnSurvivors.GetValueOrDefault(rule)} | " +
                      $"{report.FiredOnSurvivorsBaseline.GetValueOrDefault(rule)} | " +
                      $"{report.ExtractedOnSurvivors.GetValueOrDefault(rule)} | " +
                      $"{report.ExtractedOnSurvivorsBaseline.GetValueOrDefault(rule)} |");
        }

        lines.Add("");
        lines.Add("## Findings raised by rules that extracted nothing");
        lines.Add("");

        if (report.Unattributed.Count == 0)
        {
            lines.Add("None. Every finding came from a rule whose extraction counter had moved.");
        }
        else
        {
            lines.Add("A rule with an extraction counter stuck at zero has a floor of zero, so it can go " +
                      "silent forever without the golden layer noticing. Where the same scope also carries " +
                      "a `rule-inert` row, the sidecar states both that the rule read nothing here and that " +
                      "a finding it owns decided canon.");
            lines.Add("");
            lines.Add("| seed | scope | rule | fired | kinds | also `rule-inert` |");
            lines.Add("|---|---|---|---|---|---|");

            foreach (Unaccounted row in report.Unattributed)
            {
                lines.Add($"| {row.Seed} | {row.Scope} | {row.Rule} | {row.Fired} | " +
                          $"{(row.Kinds.Count == 0 ? "—" : string.Join(", ", row.Kinds))} | " +
                          $"{(row.AlsoReportedInert ? "yes" : "no")} |");
            }
        }

        lines.Add("");
        lines.Add("## Scope selection");
        lines.Add("");
        lines.Add($"The denominator moved, so the scope *list* moved too. Diffed against {report.Against} " +
                  "on the same seeds — and these are different histories, not two renderings of one, " +
                  "so a scope present in one and absent from the other is usually a power that does " +
                  "not exist in the other world.");
        lines.Add("");

        for (int i = 0; i < report.Seeds.Count; i++)
        {
            SeedHoldouts now = report.Seeds[i];
            SeedHoldouts was = report.Baseline[i];

            lines.Add($"**Seed {now.Seed}** — {was.Total} scopes at {report.Against}, {now.Total} at {report.Set}.");
            lines.Add("");

            foreach (string gone in was.Scopes.Where(s => !now.Scopes.Contains(s, StringComparer.Ordinal)))
                lines.Add($"- gone: {gone}");
            foreach (string added in now.Scopes.Where(s => !was.Scopes.Contains(s, StringComparer.Ordinal)))
                lines.Add($"- new: {added}");

            lines.Add("");
        }

        lines.Add("## Verdict");
        lines.Add("");

        (string heaviest, int scopes, int sharePct) = report.Heaviest;
        int distinct = report.ByRule.Count(static p => p.Value > 0);

        lines.Add($"- Panel holdouts: {report.TotalHoldouts} " +
                  $"({(report.TotalHoldouts < 10 ? "under the degeneracy guard's ten" : "at or above the guard's ten")})");
        lines.Add($"- Heaviest rule: {(heaviest.Length == 0 ? "—" : heaviest)} at {scopes} scope(s), {sharePct}% of the panel");
        lines.Add($"- Distinct rules attributed: {distinct}");
        lines.Add($"- Per-seed rate {report.RateRange}, width {report.RateRange.Width:0.#} points against the rule's 20");
        lines.Add($"- Went non-zero to zero on survivors: " +
                  $"{(report.WentSilent.Count == 0 ? "none" : string.Join(", ", report.WentSilent))}");
        lines.Add($"- Findings from rules that extracted nothing: {report.Unattributed.Count}");
        lines.Add("");
        lines.Add($"**{report.Verdict}.**{(report.Halts ? " Halts." : "")}");

        return lines;
    }
}
