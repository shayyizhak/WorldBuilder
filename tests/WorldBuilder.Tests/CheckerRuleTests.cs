using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Layer 2 of the test suite: one positive and one negative case for every self-consistency
/// rule, written by hand against no world and no model.
///
/// These are the tests that would have caught the checker going quiet. Tier 1 returned an empty
/// result on a render containing a textbook 1.1 violation, and nothing in the suite noticed
/// because every other test asserted against real prose that happened not to contain one. A
/// rule with no unit test is a rule that can stop working silently.
///
/// Cases are the ones named in the specification, verbatim where the specification gives them.
/// </summary>
public class CheckerRuleTests
{
    /// <summary>
    /// The finding fired, <b>and</b> the rule that owns it read something to reach it.
    ///
    /// The second half is the one that would have caught round 11. A test asserting only that a
    /// finding appeared still passes on a rule that reaches the right answer from the wrong
    /// input, and a test asserting only that nothing fired passes on a rule that is inert — which
    /// is the whole failure this layer exists to detect. Every positive case asserts extraction.
    /// </summary>
    private static void Fires(string rule, string passage)
    {
        Coverage cover = new();
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(passage, cover);

        Assert.True(
            findings.Any(f => f.Kind == rule),
            $"expected '{rule}' on: {passage}\n  got: " +
            (findings.Count == 0 ? "nothing" : string.Join("; ", findings.Select(f => $"{f.Kind}: {f.Context}"))));

        string owner = RuleNames.Of(rule);
        int extracted = cover.Rules.TryGetValue(owner, out RuleCounts? counts) ? counts.Extracted : 0;

        Assert.True(extracted > 0,
            $"'{rule}' fired but its owning rule '{owner}' extracted nothing — the finding was " +
            $"reached without reading the passage.\n  on: {passage}");
    }

    private static void Silent(string passage)
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(passage);
        Assert.True(
            findings.Count == 0,
            $"expected nothing on: {passage}\n  got: " +
            string.Join("; ", findings.Select(f => $"{f.Kind}: {f.Context}")));
    }

    /// <summary>
    /// Every Tier 1 rule has both halves, asserted as data rather than left to be counted by
    /// hand across a file of individually named facts.
    ///
    /// A rule with only a firing case can be a rule that fires on everything, and a rule with
    /// only a clean case can be inert. Neither is visible from a passing suite unless the pair is
    /// required somewhere a new rule cannot be added without meeting it.
    /// </summary>
    [Theory]
    [InlineData(SelfConsistency.Rules.CountEnumeration,
        "Four people were murdered from within, including Weallhous Dreld in 25, " +
        "Wilwound Ska in 31, Nael War in 37, and Paernrom Sir in 38.",
        "During this transition, four exiles returned to take service with the Commune: " +
        "Kou Peis in 32, Sou Dra in 34, Realsis Leirpu in 35, and Thosruld Lul in 39.")]
    [InlineData(SelfConsistency.Rules.CountNarration,
        "The period saw three places taken from the Wurn League. It took Laehiford in 7 and Hadale in 20.",
        "Six exiles returned to take service with the Covenant.\nStald Gearngoll took " +
        "the seat in 29. Kou Peis was cast out in 45. Veillpea Dourn took the seat in 45.")]
    [InlineData(SelfConsistency.Rules.PartitionSum,
        "Eleven rulers held the seat: five were killed and five were cast out.",
        "Twelve people were cast out: six for attempted murder, four for a lost claim, " +
        "and two for a lost challenge.")]
    [InlineData(SelfConsistency.Rules.DateAgreement,
        "Thres Thrild was killed in 46. The murder of Thres Thrild in 47 ended the dispute.",
        "Thres Thrild was killed in 47. The murder of Thres Thrild in 47 went unpunished.")]
    [InlineData(SelfConsistency.Rules.SummaryBody,
        "The period began in 20 when Laehiford broke from the Kebarrow Compact, with " +
        "Realsis Leirpu taking the seat.\nHe took service with the power in 20.",
        "The period began in 20 when Laehiford broke from the Kebarrow Compact, with " +
        "Realsis Leirpu taking the seat.\nRealsis Leirpu held the seat until 32.")]
    [InlineData(SelfConsistency.Rules.CoinedTerm,
        "The power answered with failed Counter-raids in 43.",
        "The power answered with failed counter-raids in 43.")]
    public void EveryTierOneRuleHasAFiringCaseAndACleanOne(string rule, string fires, string clean)
    {
        Coverage cover = new();
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(fires, cover);

        Assert.True(findings.Any(f => RuleNames.Of(f.Kind) == rule),
            $"{rule} did not fire on its positive case:\n  {fires}");
        Assert.True(cover.Rules[rule].Extracted > 0, $"{rule} fired without extracting:\n  {fires}");

        Assert.DoesNotContain(SelfConsistency.Check(clean), f => RuleNames.Of(f.Kind) == rule);
    }

    // ---- 1.1 count versus enumeration -------------------------------------

    /// <summary>
    /// The round-11 case, verbatim: fourteen stated, fourteen named, and the list marked as
    /// partial. Every word of it is true and the "included" is the defect — it tells a reader
    /// there are more when the list is complete.
    /// </summary>
    [Fact]
    public void R1_1_ExhaustiveListMarkedPartial() =>
        Fires("hedged-exhaustive-list",
            "Fourteen people returned from exile and took service with the power between 22 " +
            "and 51. These returns included Trem Lolkoll in 22, Math Ham in 24, Sou Dra in 24, " +
            "Teillmol Lund in 31, Le Vild in 34, Drarka Draernthun in 35, Heth Fal in 37, " +
            "Herpeim Raern in 39, Draes Wild in 43, Stonand Ker in 46, Voudreirn Wer in 46, " +
            "Kou Peis in 47, Thurnean Kourn in 48, and Drouldthas Stour in 51.");

    /// <summary>The round-7 case of the same shape, in a shorter list of people.</summary>
    [Fact]
    public void R1_1_FourNamedOfFour() =>
        Fires("hedged-exhaustive-list",
            "Four people were murdered from within, including Weallhous Dreld in 25, " +
            "Wilwound Ska in 31, Nael War in 37, and Paernrom Sir in 38.");

    [Fact]
    public void R1_1_MoreNamedThanCounted() =>
        Fires("count-vs-narration",
            "Two marriages bound the commune to other powers: Sor Pean married Thres Thrild " +
            "in 37, and Ta Poveil married Kaes Rou in 48. A second marriage to the Sworn Men " +
            "of Meigate occurred in 49.");

    [Fact]
    public void R1_1_FewerToldThanCounted() =>
        Fires("count-vs-narration",
            "The Compact engaged in two wars against the Wurn League, fighting three battles " +
            "which it won. In 7, it defeated the Wurn League at Laehiford. In 8, it defeated " +
            "them again at Hadale. Peace was made in 9.");

    [Fact]
    public void R1_1_AWorldTotalNarratedShort() =>
        Fires("count-vs-narration",
            "The period saw seven rulers, five of them killed, and three places taken from the " +
            "Wurn League.\nIt took Laehiford from them in year 7 and took Hadale from them in " +
            "year 20.");

    /// <summary>Two exact partitions of a total, both correct. Nothing may fire.</summary>
    [Fact]
    public void R1_1_ExactSplitsPass() =>
        Silent("It sent six raids, three of which carried off plunder from Kebarrow in 4 and " +
               "17 and Laehiford in 12, while three were beaten off at Hadale in 7 and 22 and " +
               "Meigate in 13.");

    /// <summary>A genuinely partial list: three named against fourteen, correctly hedged.</summary>
    /// <summary>A list of people that matches its own count exactly.</summary>
    [Fact]
    public void R1_1_AnExactListOfPeoplePasses() =>
        Silent("During this transition, four exiles returned to take service with the Commune: " +
               "Kou Peis in 32, Sou Dra in 34, Realsis Leirpu in 35, and Thosruld Lul in 39.");

    /// <summary>
    /// A count with no list at all. Judging these across the whole section counted every dated
    /// name in it and reported three correct sentences as short.
    /// </summary>
    [Fact]
    public void R1_1_ACountWithNoListPasses()
    {
        Silent("Six exiles returned to take service with the Covenant.\nStald Gearngoll took " +
               "the seat in 29. Kou Peis was cast out in 45. Veillpea Dourn took the seat in 45.");

        Silent("Seven exiles returned to take service during the period.\nHeth Fal took the " +
               "seat in 39. Teillmol Lund died in 50. Draes Wild married Ror Rim in 44.");
    }

    [Fact]
    public void R1_1_AGenuinelyPartialListPasses() =>
        Silent("Fourteen returned from exile, among them Trem Lolkoll in 22, Math Ham in 24 " +
               "and Sou Dra in 24.");

    // ---- 1.2 partition sums -----------------------------------------------

    [Fact]
    public void R1_2_PartsShortOfTheTotal() =>
        Fires("partition-sum", "Eleven rulers held the seat: five killed and five cast out.");

    [Fact]
    public void R1_2_PartsShortOfAShorterTotal() =>
        Fires("partition-sum",
            "Five people held the seat: two died, one was replaced, and one was cast out.");

    [Fact]
    public void R1_2_AnExactPartitionPasses() =>
        Silent("Twelve people were cast out: six for attempted murder, four for a lost claim, " +
               "and two for a lost challenge.");

    // ---- 1.3 internal date agreement --------------------------------------

    [Fact]
    public void R1_3_OneEventTwoDates() =>
        Fires("date-disagreement",
            "Thres Thrild was killed in 46. The murder of Thres Thrild in 47 went unpunished.");

    [Fact]
    public void R1_3_AgreeingDatesPass() =>
        Silent("Thres Thrild was killed in 47. The murder of Thres Thrild in 47 went unpunished.");

    // ---- 1.4 summary versus body ------------------------------------------

    /// <summary>
    /// The round-11 case: the opening says Realsis Leirpu took the seat at the secession and
    /// the body says he took service with the power, in the same year. Both cannot be true of
    /// one man, and no event access is needed to know it.
    /// </summary>
    [Fact]
    public void R1_4_TheBodyContradictsTheOpening() =>
        Fires("self-contradiction",
            "The period began in 20 when Laehiford broke from the Kebarrow Compact, with " +
            "Realsis Leirpu taking the seat.\nHe took service with the power in 20.");

    [Fact]
    public void R1_4_ConsistentClaimsPass() =>
        Silent("The period began in 20 when Laehiford broke from the Kebarrow Compact, with " +
               "Realsis Leirpu taking the seat.\nRealsis Leirpu held the seat until 32.");

    // ---- coined terms -----------------------------------------------------

    /// <summary>
    /// Round 11: "failed Counter-raids". The capital is what turns two ordinary words into a
    /// term of art, and counter-raid is not a thing this world has.
    /// </summary>
    [Fact]
    public void ACoinedTermWearingACapitalIsCaught() =>
        Fires("stray-capital", "The power answered with failed Counter-raids in 43 and 44.");

    [Fact]
    public void TheSameWordsUncapitalisedPass() =>
        Silent("The power answered with failed counter-raids in 43 and 44.");

    /// <summary>A real name must survive this, capital and all.</summary>
    [Fact]
    public void AProperNounMidSentenceIsLeftAlone() =>
        Silent("The raid on Threi Cut in 44 was beaten off by the Vea Lode Covenant.");

    /// <summary>And a sentence may still open with an ordinary capitalised word.</summary>
    [Fact]
    public void ASentenceOpeningWithACommonNounPasses() =>
        Silent("Raids on Griwick in 33 and 47 were beaten off. Peace followed.");
}
