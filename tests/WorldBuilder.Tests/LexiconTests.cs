using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Layer 2's second half: the lexicons and the normalisation, tested apart from the rules.
///
/// Four of the five causes of round 11's silent Tier 1 were the same thing — <b>the rule was
/// correct and the input never reached it</b>. A missing partiality marker, a missing countable
/// noun, an unhandled word order, an unstripped possessive. None of them is visible in rule
/// logic, and a rule test that asserts only "the expected finding appeared" passes right up
/// until the day the input stops arriving, at which point it also passes.
///
/// So these tests assert on the plumbing rather than the verdict, and the last of them asserts
/// on <see cref="Coverage"/> — that a rule <em>extracted</em> something — which is the only
/// assertion in the suite that an inert rule cannot satisfy.
/// </summary>
public class LexiconTests
{
    /// <summary>
    /// Every partiality marker behaves like every other, on one sentence with only the marker
    /// swapped.
    ///
    /// This is the test that would have caught cause 1 on its own. "including" was present and
    /// "included" was not, so round 7's case fired and round 11's identical case did not, and
    /// the difference between them was one word in one array.
    /// </summary>
    [Theory]
    [InlineData("including")]
    [InlineData("included")]
    [InlineData("among them")]
    [InlineData("amongst them")]
    [InlineData("such as")]
    [InlineData("notably")]
    [InlineData("chief among")]
    [InlineData("namely")]
    public void EveryPartialityMarkerFiresOnTheSameExhaustiveList(string marker)
    {
        string passage =
            $"Four people were murdered from within, {marker} Wilwound Ska, Nael War, " +
            "Paernrom Sir and Weallhous Dreld.";

        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(passage);

        Assert.True(findings.Any(f => f.Kind == "hedged-exhaustive-list"),
            $"marker '{marker}' did not mark the list as partial: " +
            (findings.Count == 0 ? "nothing fired" : string.Join("; ", findings.Select(f => f.Kind))));
    }

    /// <summary>
    /// Every countable noun yields its count.
    ///
    /// Cause 2: "people", "exiles" and "returns" were absent from the lexicon, so no rule ever
    /// looked at a roster of people however plainly it contradicted itself. A count that does
    /// not parse is a rule that never runs.
    /// </summary>
    [Theory]
    [InlineData("people", "were murdered from within")]
    [InlineData("exiles", "returned to take service")]
    [InlineData("returns", "took service with the power")]
    [InlineData("raids", "were sent against the Wurn League")]
    [InlineData("marriages", "married into other powers")]
    [InlineData("battles", "were fought and won")]
    [InlineData("places", "were taken from the Wurn League")]
    public void EveryCountableNounIsCounted(string noun, string verb)
    {
        // With a list, because an assertion is a count and a list — a count alone is not
        // something the rule can check and is not recorded as extracted.
        Coverage cover = new();
        SelfConsistency.Check($"Fourteen {noun} {verb}: Ska in 22, War in 24, and Sir in 26.", cover);

        RuleCounts counts = cover.Rules[SelfConsistency.Rules.CountEnumeration];

        Assert.True(counts.Extracted > 0,
            $"'fourteen {noun}' yielded no count — the noun is not in the lexicon");
    }

    /// <summary>
    /// A date is found whichever side of the phrase the name sits on.
    ///
    /// Cause 4: rule 1.3 assumed the name preceded the act, so "X was killed in 46" was read
    /// and "the murder of X in 47" was invisible. One event on two dates, and only one of them
    /// examined, which is a disagreement the rule cannot see by construction.
    /// </summary>
    [Theory]
    [InlineData("Thres Thrild was killed in 46.")]
    [InlineData("He ordered the murder of Thres Thrild in 46.")]
    [InlineData("The killing of Thres Thrild in 46 followed.")]
    public void ADateIsExtractedFromEitherWordOrder(string sentence)
    {
        Coverage cover = new();
        SelfConsistency.Check(sentence, cover);

        Assert.True(cover.Rules[SelfConsistency.Rules.DateAgreement].Checked > 0,
            $"no dated act was extracted from: {sentence}");
    }

    /// <summary>
    /// The possessive form of a name is the same name.
    ///
    /// Cause 5, and the one that took a throwaway probe to find. "Realsis Leirpu's" normalised
    /// to the subject <c>leirpu's</c>, which matched nobody, so rule 1.4 resolved a subject in
    /// no sentence and reported nothing — while extracting from every one of them.
    ///
    /// Asserted behaviourally rather than against the normaliser, because what matters is that
    /// the two spellings are one person: the rule must reach the same verdict either way.
    /// </summary>
    [Theory]
    [InlineData("Realsis Leirpu")]
    [InlineData("Realsis Leirpu's")]
    [InlineData("Realsis Leirpu’s")]
    public void APossessiveNameResolvesToTheSamePerson(string subject)
    {
        // The name is plain in the first claim and given in the argument's form in the second,
        // so the two only meet if the possessive normalises to the same person. With it left
        // on, the second claim files under "leirpu's" and contradicts nobody — which is exactly
        // what happened, and why the rule reported nothing while extracting from every sentence.
        string passage =
            "Realsis Leirpu took the seat in 20.\n" +
            $"Reports of {subject} taking service with the power in 20 followed.";

        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(passage);

        Assert.True(findings.Any(f => f.Kind == "self-contradiction"),
            $"'{subject}' did not resolve to a person: " +
            (findings.Count == 0 ? "nothing fired" : string.Join("; ", findings.Select(f => f.Kind))));
    }

    /// <summary>
    /// Every rule extracts something from a passage written to give it something.
    ///
    /// The general form of the lesson, and the assertion the rest of the suite was missing: a
    /// test that only checks for the absence of a false positive passes when the rule is inert.
    /// These fixtures each contain one thing for one rule, and the assertion is on the count of
    /// what was extracted rather than on the verdict reached.
    /// </summary>
    [Theory]
    [InlineData(SelfConsistency.Rules.CountEnumeration,
        "Three people were cast out: Le Vild in 33, Heth Fal in 35, and Nael War in 37.")]
    [InlineData(SelfConsistency.Rules.CountNarration,
        "The period saw three places taken from the Wurn League. It took Laehiford in 7 and Hadale in 20.")]
    [InlineData(SelfConsistency.Rules.PartitionSum,
        "Eleven rulers held the seat: five were killed and five were cast out.")]
    [InlineData(SelfConsistency.Rules.DateAgreement,
        "Wilwound Ska was killed in 31. The murder of Wilwound Ska in 31 ended the dispute.")]
    [InlineData(SelfConsistency.Rules.SummaryBody,
        "Realsis Leirpu took the seat in 20. He held it for twelve years.")]
    [InlineData(SelfConsistency.Rules.CoinedTerm,
        "The power answered with failed Counter-raids in 43.")]
    public void EveryRuleExtractsFromAFixtureWrittenForIt(string rule, string passage)
    {
        Coverage cover = new();
        SelfConsistency.Check(passage, cover);

        Assert.True(cover.Rules[rule].Extracted > 0,
            $"{rule} extracted nothing from a passage written to give it something:\n  {passage}");
    }

    /// <summary>
    /// And the whole of Tier 1 reads a real section rather than glancing at it.
    ///
    /// The unit fixtures above prove each rule can extract; this proves they all do, on the
    /// prose they will actually meet. A rule reporting zero here is inert on the chronicle
    /// whatever it does on a sentence written to suit it.
    /// </summary>
    [Fact]
    public void EveryTierOneRuleReadsARealSection()
    {
        const string section =
            "The Kebarrow Compact endured twenty years of fragmentation under eleven rulers. " +
            "Weallhous Dreld kept the seat after defeating Saern Meastouth's open challenge in 23, " +
            "but was killed by Gatros Hearn in 25. Gatros Hearn took the seat in 25 and was " +
            "cast out in 27. " +
            "Three people were cast out: Le Vild in 33, Heth Fal in 35, and Nael War in 37. " +
            "Of the eleven rulers, five were killed and six were cast out. " +
            "The Compact fought three battles at Kebarrow in 32, 33 and 34, winning each.";

        Coverage cover = new();
        SelfConsistency.Check(section, cover);

        foreach (string rule in new[]
        {
            SelfConsistency.Rules.CountEnumeration,
            SelfConsistency.Rules.CountNarration,
            SelfConsistency.Rules.PartitionSum,
            SelfConsistency.Rules.DateAgreement,
            SelfConsistency.Rules.SummaryBody,
        })
        {
            Assert.True(cover.Rules[rule].Extracted > 0, $"{rule} extracted nothing from a whole section");
        }
    }

    /// <summary>
    /// Every entry in the partiality lexicon, enumerated from the lexicon itself.
    ///
    /// The theory above lists its markers by hand, which proves the ones somebody thought to
    /// write down. This enumerates the array the rule actually reads, so a word added to the
    /// lexicon is tested the moment it is added and a word that does not work cannot sit there
    /// looking like coverage.
    /// </summary>
    public static TheoryData<string> AllPartialMarkers()
    {
        TheoryData<string> data = [];
        foreach (string marker in SelfConsistency.PartialMarkers) data.Add(marker);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPartialMarkers))]
    public void EveryEntryInThePartialityLexiconWorks(string marker)
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            $"Four people were cast out, {marker} Ska, War, Sir and Dreld.");

        Assert.True(findings.Any(f => f.Kind == "hedged-exhaustive-list"),
            $"'{marker}' is in PartialMarkers and does not mark a list as partial");
    }

    /// <summary>
    /// Every entry in the motive lexicon, enumerated the same way.
    ///
    /// Round 11 was a missing partiality marker; round 12 found the same shape in a second
    /// lexicon — <c>invented-mind</c> fired on "exploiting" in one passage and missed "motivated
    /// by" in another that entered canon. Both lists now prove themselves rather than being
    /// remembered.
    /// </summary>
    public static TheoryData<string> AllMindWords()
    {
        TheoryData<string> data = [];
        foreach (string word in FabricationCheck.MindWords) data.Add(word);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllMindWords))]
    public void EveryEntryInTheMotiveLexiconWorks(string word)
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        Assert.Contains(FabricationCheck.Check(pack, $"Stonand Ker was {word} the earlier raid.").Findings,
            f => f.Kind == "invented-mind" && f.Token == word);
    }

    /// <summary>The archived v1 world; see <see cref="BaselineWorld"/>.</summary>
    private static WorldView World() => BaselineWorld.Seed42();

    /// <summary>
    /// A death toll is not a person dying.
    ///
    /// Widening rule 1.3's subject search to reach past four words raised its coverage from 91%
    /// to 97% and, on the first real document it met, read "in 31, fourteen died" as a place
    /// called Griwick dying — because the nearest proper noun was a place and Tier 1 has no way
    /// to know that. A number in front of the verb is the tell, and it is the only one available
    /// without the world.
    /// </summary>
    [Theory]
    [InlineData("A raid on Griwick was beaten off, and in 31, fourteen died while 33 abandoned the place.")]
    [InlineData("Hunger struck Meigate in 26, and nine more died in 27.")]
    [InlineData("At Hadale in 40, eighty-six people died.")]
    public void ADeathTollIsNotAPersonDying(string sentence)
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            sentence + "\nGriwick was taken in 35.");

        Assert.DoesNotContain(findings, f => f.Kind == "date-disagreement");
    }

    /// <summary>
    /// A rule performing hundreds of comparisons and never objecting deserves the opposite
    /// sanity check: confirm it can object at all.
    ///
    /// <c>coined-term</c> ran 588 comparisons across thirteen scopes and fired nothing, which is
    /// either a clean chronicle or a rule incapable of firing, and the coverage block cannot
    /// tell those apart. Nothing else in the suite asks a high-volume rule to prove it has a
    /// positive case.
    /// </summary>
    [Fact]
    public void AHighVolumeRuleCanStillObject()
    {
        Coverage cover = new();
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            "The power answered with failed Counter-raids and repeated Seat-claims in 43.", cover);

        Assert.True(cover.Rules[SelfConsistency.Rules.CoinedTerm].Extracted > 0);
        Assert.Contains(findings, f => f.Kind == "stray-capital");
    }

    /// <summary>
    /// The golden layer's coverage diff, on numbers rather than on prose.
    ///
    /// This is the assertion the specification says would have caught Tier 1 going inert, so it
    /// gets a test of its own rather than being trusted because it looks right. Verified once by
    /// hand as well — removing a single countable noun from the lexicon and running
    /// <c>wb test golden</c> reported the Hadale section going from two extractions to none.
    /// </summary>
    [Fact]
    public void TheCoverageDiffCatchesARuleGoingQuiet()
    {
        Coverage before = new();
        before.Extracted("count-enumeration", 6);
        before.Extracted("date-agreement", 20);
        before.Extracted("partition-sum", 4);

        Coverage after = new();
        after.Extracted("count-enumeration", 0);   // gone entirely
        after.Extracted("date-agreement", 19);     // ordinary rewording
        after.Extracted("partition-sum", 1);       // a steep drop

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored =
            new(StringComparer.Ordinal) { ["A power, 2-21"] = before.Rules };

        Dictionary<string, Coverage> current =
            new(StringComparer.Ordinal) { ["A power, 2-21"] = after };

        List<Drift> drift = GoldenDiff.CoverageSound(stored, current);

        // A rule that read six and now reads none is reported under its own name. It is still a
        // floor breach and still fails; calling it went-silent keeps the one comparison that
        // matters from reading like the ordinary drops beside it.
        Assert.Contains(drift, d => d.Kind == "went-silent" && d.Detail.Contains("count-enumeration",
            StringComparison.Ordinal));

        // A steep drop short of zero stays a floor breach.
        Assert.Contains(drift, d => d.Kind == "floor" && d.Detail.Contains("partition-sum",
            StringComparison.Ordinal));

        Assert.All(drift, d => Assert.True(d.Fails));
    }

    /// <summary>
    /// FLOOR is strict: any drop at all is a drop.
    ///
    /// An earlier version allowed anything above half, on the theory that a rewritten section
    /// legitimately states fewer claims. It does — and so does a rule that has stopped reading
    /// half of them, which is the case that matters. A tolerance band is a place for a
    /// regression to live, so the tolerance is now a deliberate human act instead.
    /// </summary>
    [Fact]
    public void AnyDropBelowTheBaselineFails()
    {
        Coverage before = new();
        before.Extracted("count-enumeration", 6);
        before.Checked("count-enumeration", 6);

        // Everything it read, it evaluated — so ACCOUNTING is clean and only FLOOR speaks.
        Coverage after = new();
        after.Extracted("count-enumeration", 5);
        after.Checked("count-enumeration", 5);

        List<Drift> drift = GoldenDiff.CoverageSound(
            new Dictionary<string, IReadOnlyDictionary<string, RuleCounts>>(StringComparer.Ordinal)
                { ["A power, 2-21"] = before.Rules },
            new Dictionary<string, Coverage>(StringComparer.Ordinal) { ["A power, 2-21"] = after });

        Drift one = Assert.Single(drift);
        Assert.Equal("floor", one.Kind);
        Assert.True(one.Fails);
    }

    /// <summary>Reading more than the baseline is not a finding.</summary>
    [Fact]
    public void ReadingMoreThanTheBaselineIsFine()
    {
        Coverage before = new();
        before.Extracted("count-enumeration", 6);
        before.Checked("count-enumeration", 6);

        Coverage after = new();
        after.Extracted("count-enumeration", 9);
        after.Checked("count-enumeration", 9);

        Assert.Empty(GoldenDiff.CoverageSound(
            new Dictionary<string, IReadOnlyDictionary<string, RuleCounts>>(StringComparer.Ordinal)
                { ["A power, 2-21"] = before.Rules },
            new Dictionary<string, Coverage>(StringComparer.Ordinal) { ["A power, 2-21"] = after }));
    }

    /// <summary>
    /// The two halves catch opposite failures, and neither catches the other's.
    ///
    /// This is the round-12-and-13 pair as one test. Extracting 33 and checking 1 satisfies
    /// FLOOR and violates ACCOUNTING; extracting 2 and checking 2 satisfies ACCOUNTING and
    /// violates FLOOR. Either alone would call one of these clean.
    /// </summary>
    [Fact]
    public void EachHalfCatchesWhatTheOtherMisses()
    {
        Coverage baseline = new();
        baseline.Extracted("partition-sum", 33);
        baseline.Checked("partition-sum", 33);

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored =
            new(StringComparer.Ordinal) { ["A power, 2-21"] = baseline.Rules };

        // Round 12: everything read, almost nothing evaluated, the rest into an early return.
        Coverage roundTwelve = new();
        roundTwelve.Extracted("partition-sum", 33);
        roundTwelve.Checked("partition-sum", 1);

        List<Drift> twelve = GoldenDiff.CoverageSound(stored,
            new Dictionary<string, Coverage>(StringComparer.Ordinal) { ["A power, 2-21"] = roundTwelve });

        Assert.Contains(twelve, d => d.Kind == "accounting");
        Assert.DoesNotContain(twelve, d => d.Kind == "floor");

        // Round 13: the accounting made perfect by reading almost nothing.
        Coverage roundThirteen = new();
        roundThirteen.Extracted("partition-sum", 2);
        roundThirteen.Checked("partition-sum", 2);

        List<Drift> thirteen = GoldenDiff.CoverageSound(stored,
            new Dictionary<string, Coverage>(StringComparer.Ordinal) { ["A power, 2-21"] = roundThirteen });

        Assert.Contains(thirteen, d => d.Kind == "floor");
        Assert.DoesNotContain(thirteen, d => d.Kind == "accounting");
    }

    /// <summary>
    /// Accepting a run that reads less than the baseline is refused, not recorded.
    ///
    /// The re-baselining policy in one assertion. A floor that follows the last run down is not
    /// a floor, and the way that happens is nobody deciding it — running the accept command
    /// again after a change, which is what the last two rounds each did.
    /// </summary>
    [Fact]
    public void AcceptingBelowTheBaselineIsRefused()
    {
        Coverage baseline = new();
        baseline.Extracted("partition-sum", 33);
        baseline.Checked("partition-sum", 33);

        Coverage now = new();
        now.Extracted("partition-sum", 2);
        now.Checked("partition-sum", 2);

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored =
            new(StringComparer.Ordinal) { ["A power, 2-21"] = baseline.Rules };
        Dictionary<string, Coverage> current =
            new(StringComparer.Ordinal) { ["A power, 2-21"] = now };

        Assert.NotEmpty(GoldenDiff.WouldLowerAFloor(stored, current));

        // And the same run once the extraction is restored: nothing in the way of accepting.
        Coverage fixedUp = new();
        fixedUp.Extracted("partition-sum", 33);
        fixedUp.Checked("partition-sum", 33);

        Assert.Empty(GoldenDiff.WouldLowerAFloor(stored,
            new Dictionary<string, Coverage>(StringComparer.Ordinal) { ["A power, 2-21"] = fixedUp }));
    }

    /// <summary>
    /// Coverage survives a round trip through the file it is stored in.
    ///
    /// The stored counts are the load-bearing half of the comparison — they are what today's
    /// rules are measured against — so a serialisation that quietly dropped a rule would make
    /// the diff report agreement it had not established.
    /// </summary>
    [Fact]
    public void StoredCoverageRoundTrips()
    {
        Coverage cover = new();
        cover.Extracted("count-enumeration", 6);
        cover.Checked("count-enumeration", 4);
        cover.Fired("count-enumeration", 1);
        cover.Ran("partition-sum");

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> read = Coverage.FromJson(
            Coverage.ToJson(new Dictionary<string, Coverage>(StringComparer.Ordinal) { ["A"] = cover }));

        Assert.Equal(new RuleCounts(6, 4, 0, 1), read["A"]["count-enumeration"]);
        Assert.Equal(new RuleCounts(0, 0, 0, 0), read["A"]["partition-sum"]);
    }
}
