namespace WorldBuilder.Inference;

/// <summary>What one rule did to one passage.</summary>
/// <param name="Extracted">Candidate assertions the rule built from the prose.</param>
/// <param name="Checked">Of those, how many it evaluated.</param>
/// <param name="Unresolvable">Of those, how many it could not evaluate, and said why.</param>
/// <param name="Fired">Findings raised.</param>
public sealed record RuleCounts(int Extracted, int Checked, int Unresolvable, int Fired)
{
    /// <summary>
    /// Every extracted assertion ends checked or unresolvable. There is no third path.
    ///
    /// A rule that drops an assertion without recording why presents as a rule that found
    /// nothing wrong, which is the failure this whole mechanism exists to stop presenting as a
    /// pass. Asserted rather than hoped for.
    /// </summary>
    public bool Accounted => Extracted == Checked + Unresolvable;

    /// <summary>What share of what it read this rule actually evaluated, as a percentage.</summary>
    public int CheckedPct => Extracted == 0 ? 100 : Checked * 100 / Extracted;
}

/// <summary>
/// How much of a passage each rule actually looked at.
///
/// The checker's error mode is silence, which is the right choice — a rule that cries wolf costs
/// the chronicle real content and then stops being read. But silence is indistinguishable from
/// absence, and round 11 proved what that costs: Tier 1 returned nothing on a render containing
/// a textbook violation of its own first rule, and it took a throwaway probe to discover why.
///
/// Four of the five causes were the same shape — <b>the rule was correct and the input never
/// reached it</b>. A missing partiality marker, a missing countable noun, an unhandled word
/// order, an unstripped possessive. None is visible in rule logic and all four produce exactly
/// the output of a clean passage.
///
/// So every rule now reports how much it saw. <see cref="RuleCounts.Extracted"/> is the number
/// that matters: a rule that built no assertions from a section full of prose did not pass it,
/// it never read it. The counts are stable across renders of the same log — the wording moves,
/// the number of countable claims in a section barely does — which makes them a far better
/// thing for the golden diff to compare than sentences.
/// </summary>
public sealed class Coverage
{
    private readonly Dictionary<string, int[]> _rules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _reasons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _spans = new(StringComparer.Ordinal);

    /// <summary>A candidate assertion was identified in the prose.</summary>
    public void Extracted(string rule, int n = 1) => Slot(rule)[0] += n;

    /// <summary>An assertion was compared against the record, or against the rest of the text.</summary>
    public void Checked(string rule, int n = 1) => Slot(rule)[1] += n;

    /// <summary>
    /// An assertion was extracted and then could not be evaluated, for a stated reason.
    ///
    /// The reason is not decoration. A rule dropping two thirds of what it reads is either
    /// meeting prose it has no business checking or failing to resolve something it should —
    /// and the two are indistinguishable from the count alone. Round 11's possessive bug looked
    /// exactly like the former and was the latter.
    /// </summary>
    public void Unresolvable(string rule, string reason, string? span = null, int n = 1)
    {
        Slot(rule)[2] += n;

        if (!_reasons.TryGetValue(rule, out Dictionary<string, int>? why))
            _reasons[rule] = why = new Dictionary<string, int>(StringComparer.Ordinal);

        why[reason] = why.GetValueOrDefault(reason) + n;

        if (span is not { Length: > 0 }) return;

        if (!_spans.TryGetValue(rule, out List<string>? seen)) _spans[rule] = seen = [];
        if (seen.Count < MaxSpans && !seen.Contains(span, StringComparer.Ordinal)) seen.Add(span);
    }

    /// <summary>
    /// The text a rule gave up on, so the giving-up is diagnosable.
    ///
    /// A reason says which branch was taken; only the span says what the branch was taken on. The
    /// possessive bug of round 11 shows in a reason as "no subject for the act" — indistinguishable
    /// from prose that genuinely has no subject — and shows in a span immediately, as a sentence
    /// with a subject the rule failed to read.
    /// </summary>
    public IReadOnlyList<string> Spans(string rule) =>
        _spans.TryGetValue(rule, out List<string>? seen) ? seen : [];

    /// <summary>Enough to diagnose a pattern, not enough to become a second transcript.</summary>
    private const int MaxSpans = 8;

    /// <summary>A finding was raised.</summary>
    public void Fired(string rule, int n = 1) => Slot(rule)[3] += n;

    /// <summary>Why a rule could not evaluate what it read, and how often, commonest first.</summary>
    public IReadOnlyList<(string Reason, int Count)> Reasons(string rule)
    {
        if (!_reasons.TryGetValue(rule, out Dictionary<string, int>? why)) return [];

        List<(string, int)> ordered = [.. why.Select(p => (p.Key, p.Value))];
        ordered.Sort(static (a, b) => b.Item2 != a.Item2
            ? b.Item2.CompareTo(a.Item2)
            : string.CompareOrdinal(a.Item1, b.Item1));

        return ordered;
    }

    /// <summary>Notes a rule as having run, so that zero is recorded rather than absent.</summary>
    public void Ran(string rule) => Slot(rule);

    public IReadOnlyDictionary<string, RuleCounts> Rules
    {
        get
        {
            Dictionary<string, RuleCounts> counts = new(StringComparer.Ordinal);
            foreach ((string rule, int[] slot) in _rules)
                counts[rule] = new RuleCounts(slot[0], slot[1], slot[2], slot[3]);
            return counts;
        }
    }

    /// <summary>Every rule name seen, in a stable order.</summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            List<string> names = [.. _rules.Keys];
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }

    public void Merge(Coverage other)
    {
        foreach ((string rule, int[] slot) in other._rules)
        {
            int[] mine = Slot(rule);
            for (int i = 0; i < 4; i++) mine[i] += slot[i];
        }

        foreach ((string rule, Dictionary<string, int> why) in other._reasons)
            foreach ((string reason, int count) in why)
            {
                if (!_reasons.TryGetValue(rule, out Dictionary<string, int>? mine))
                    _reasons[rule] = mine = new Dictionary<string, int>(StringComparer.Ordinal);
                mine[reason] = mine.GetValueOrDefault(reason) + count;
            }

        foreach ((string rule, List<string> spans) in other._spans)
        {
            if (!_spans.TryGetValue(rule, out List<string>? mine)) _spans[rule] = mine = [];
            foreach (string span in spans)
                if (mine.Count < MaxSpans && !mine.Contains(span, StringComparer.Ordinal)) mine.Add(span);
        }
    }

    /// <summary>
    /// Rules that extracted nothing from this scope.
    ///
    /// Reported and not judged. A rule finding nothing in a section is usually innocent — a
    /// partition-sum rule has no business finding anything in a section containing no
    /// partitions — and an earlier version of this also reported a rule that extracted and then
    /// checked nothing, on the theory that it could not be innocent. It could: a count with no
    /// list beside it is ordinary prose, and that version put five alarms on a clean chronicle.
    /// Adding a rule that cries wolf in order to detect rules that stay silent would be a poor
    /// trade.
    ///
    /// So this is a record rather than an accusation, and the accusation is the golden layer's:
    /// zero here means nothing on its own and means a great deal beside last round's six. Four
    /// of round 11's five causes are only visible in that comparison, and the fifth is visible
    /// in these counts the moment anyone looks at them.
    ///
    /// Never joins the findings. A section's place in the chronicle must not depend on how many
    /// rules had nothing to say about it.
    /// </summary>
    /// <summary>
    /// Rules with an assertion that ended neither checked nor explained, one line each.
    ///
    /// The invariant is <c>extracted == checked + unresolvable</c>, and it is the whole of item
    /// 1: a rule that drops an assertion without saying why presents as a rule that found
    /// nothing wrong. Empty means every path out of every rule is accounted for.
    /// </summary>
    public List<string> Unaccounted()
    {
        List<string> broken = [];

        foreach (string rule in Names)
        {
            int[] slot = _rules[rule];
            if (slot[0] == slot[1] + slot[2]) continue;

            broken.Add($"{rule}: extracted {slot[0]}, checked {slot[1]}, unresolvable {slot[2]} — " +
                       $"{slot[0] - slot[1] - slot[2]} left by a path that records nothing");
        }

        return broken;
    }

    public List<Fabrication> Inert()
    {
        List<Fabrication> findings = [];

        foreach (string rule in Names)
            if (_rules[rule][0] == 0)
                findings.Add(new Fabrication(rule, "rule-inert", $"{rule} extracted nothing here"));

        return findings;
    }

    /// <summary>
    /// Per-section counts, as JSON, for storing beside a golden render.
    ///
    /// Written at accept time and never regenerated. See
    /// <see cref="GoldenDiff.CompareCoverage"/> for why that distinction is the whole point.
    /// </summary>
    public static string ToJson(IReadOnlyDictionary<string, Coverage> scopes)
    {
        Dictionary<string, Dictionary<string, RuleCounts>> plain = new(StringComparer.Ordinal);

        foreach ((string scope, Coverage cover) in scopes)
        {
            Dictionary<string, RuleCounts> rules = new(StringComparer.Ordinal);
            foreach ((string rule, RuleCounts counts) in cover.Rules) rules[rule] = counts;
            plain[scope] = rules;
        }

        return System.Text.Json.JsonSerializer.Serialize(plain,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public static Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> FromJson(string json)
    {
        Dictionary<string, Dictionary<string, RuleCounts>>? read =
            System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, Dictionary<string, RuleCounts>>>(json);

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> scopes = new(StringComparer.Ordinal);
        if (read is null) return scopes;

        foreach ((string scope, Dictionary<string, RuleCounts> rules) in read) scopes[scope] = rules;
        return scopes;
    }

    private int[] Slot(string rule)
    {
        if (_rules.TryGetValue(rule, out int[]? slot)) return slot;
        return _rules[rule] = new int[4];
    }
}
