namespace WorldBuilder.Inference;

/// <summary>
/// Which rule each finding kind belongs to.
///
/// Coverage is reported per rule, and a rule may reach its conclusion several ways — a date
/// error surfaces as <c>wrong-year</c>, <c>date-disagreement</c> or <c>relative-time</c>
/// depending on which construction carried it. Deriving the firing count from the kinds keeps
/// every rule's tally correct without a counter at each of the forty-odd places a finding is
/// raised, which is forty-odd places to forget one.
///
/// A kind with no entry reports under itself. That is deliberate: an unmapped kind shows up in
/// the coverage block under its own name rather than vanishing into a bucket, and a new rule
/// whose author forgot this table is visible instead of silent.
/// </summary>
public static class RuleNames
{
    public const string Action = "action";
    public const string Succession = "succession";
    public const string Outcome = "outcome";
    public const string Departure = "departure";
    public const string Tenure = "tenure";
    public const string Quantity = "quantity";
    public const string Date = "date";
    public const string Naming = "naming";
    public const string Coverage = "coverage";
    public const string Shape = "shape";

    private static readonly Dictionary<string, string> Owners = new(StringComparer.Ordinal)
    {
        // Tier 1 keeps its own names; they are the ones the specification's example uses.
        ["count-vs-list"] = SelfConsistency.Rules.CountEnumeration,
        ["hedged-exhaustive-list"] = SelfConsistency.Rules.CountEnumeration,
        ["count-vs-narration"] = SelfConsistency.Rules.CountNarration,
        ["partition-sum"] = SelfConsistency.Rules.PartitionSum,
        ["date-disagreement"] = SelfConsistency.Rules.DateAgreement,
        ["self-contradiction"] = SelfConsistency.Rules.SummaryBody,
        ["stray-capital"] = SelfConsistency.Rules.CoinedTerm,

        // Tier 2, grouped by the assertion types in the checker specification.
        ["no-such-event"] = Action,
        ["wrong-collapse"] = Action,
        ["unsupported-link"] = Action,
        ["wrong-direction"] = Action,
        ["unsupported-manner"] = Action,
        ["invented-mind"] = Action,
        ["event-told-twice"] = Action,

        ["never-held-the-seat"] = Succession,
        ["false-succession"] = Succession,
        ["unshared-pair"] = Succession,
        ["wrong-killer"] = Succession,
        ["no-such-killing"] = Succession,

        ["wrong-ender"] = Outcome,
        ["wrong-role"] = Outcome,
        ["hedged-outcome"] = Outcome,

        ["wrong-fate"] = Departure,

        ["wrong-seat"] = Tenure,
        ["missing-ruler"] = Tenure,
        ["outside-the-window"] = Tenure,

        ["vague-quantity"] = Quantity,
        ["wrong-scope-total"] = Quantity,
        ["outside-the-reign"] = Quantity,
        ["invented-particular"] = Quantity,
        ["forty-nine"] = Quantity,
        ["number-in-words"] = Quantity,

        ["wrong-year"] = Date,
        ["relative-time"] = Date,
        ["out-of-order"] = Date,
        ["no-such-act"] = Date,

        // Not a rule of the checker's own: an answer that cites a record it was never given.
        // Grouped with naming because that is the failure it is — a reference to something
        // outside the material, arrived at by producing an identifier rather than a word.
        ["unsupported-citation"] = Naming,

        ["ambiguous-short-name"] = Naming,
        ["describes-the-archive"] = Naming,

        // The vocabulary scan's own two verdicts. Unmapped, they reported under rule names of
        // their own — "name" beside "naming" — with the firing on one row and the extraction
        // that produced it on the other, so the scan showed as a rule that read eighty tokens
        // and objected to none of them while a phantom rule that had read nothing objected once.
        ["name"] = Naming,
        ["number"] = Naming,

        ["incomplete-enumeration"] = Coverage,
        ["too-aggregate"] = Shape,
        ["year-by-year"] = Shape,
    };

    public static string Of(string kind) => Owners.GetValueOrDefault(kind, kind);

    /// <summary>Every rule name, so a rule that fired nothing is still reported as a zero.</summary>
    public static IReadOnlyList<string> All
    {
        get
        {
            HashSet<string> names = new(Owners.Values, StringComparer.Ordinal);
            List<string> sorted = [.. names];
            sorted.Sort(StringComparer.Ordinal);
            return sorted;
        }
    }
}
