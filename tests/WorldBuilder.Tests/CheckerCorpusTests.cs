using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The checker's acceptance test: every fabrication found by hand across nine rounds, verbatim,
/// each asserted to fire a rule.
///
/// This exists because the corpus was accumulating in conversation and nowhere else. Nine
/// rounds of reading produced a hand-verified set of false sentences with known-correct
/// answers, which is worth more than any of the individual fixes — a fix stops one defect, the
/// corpus stops the whole class from coming back unnoticed.
///
/// Two of these are marked as not yet caught. They are here anyway, skipped with a reason: an
/// honest list of what the checker cannot see is more useful than a list of what it can.
/// </summary>
public class CheckerCorpusTests
{
    private static WorldView World(ulong seed = 42, int years = 50)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return WorldView.Build(sim.Log, seed);
    }

    private static ContextPack Faction(WorldView view, int id, int from, int to) =>
        ContextPackBuilder.Faction(view, EntityId.Faction(id), from, to);

    /// <summary>
    /// Asserts that a whole section fires a named rule. Distinct from <see cref="Fires"/>
    /// because the completeness rules only run on a finished section — an excerpt cannot be
    /// asked whether it named every ruler.
    /// </summary>
    private static void FiresWhole(ContextPack pack, string rule, string passage) =>
        Fires(pack, rule, passage, wholeSection: true);

    /// <summary>Asserts that a passage fires a named rule against a given scope.</summary>
    private static void Fires(ContextPack pack, string rule, string passage, bool wholeSection = false)
    {
        FabricationReport report = FabricationCheck.Check(pack, passage, wholeSection);
        Assert.True(
            report.Findings.Any(f => f.Kind == rule),
            $"expected '{rule}' on: {passage}\n  got: " +
            (report.Findings.Count == 0
                ? "nothing"
                : string.Join("; ", report.Findings.Select(f => $"{f.Kind}: {f.Context}"))));
    }

    // ---- rounds 3–5: the succession fabrication ---------------------------

    [Fact]
    public void R5_KerNeverHeldTheSeat()
    {
        WorldView view = World();
        ContextPack pack = Faction(view, 2, 22, 41);

        foreach (string claim in new[]
        {
            "Ska was murdered by Stonand Ker, who was in turn set aside by Le Vild.",
            "Ska was killed by Stonand Ker, who was succeeded by Le Vild.",
        })
        {
            FabricationReport report = FabricationCheck.Check(pack, claim);
            Assert.Contains(report.Findings,
                f => f.Kind is "never-held-the-seat" or "false-succession" or "unshared-pair");
        }
    }

    // ---- round 6: an inverted outcome -------------------------------------

    [Fact]
    public void R6_DreldsRuleEndedByTheManWhoLost() =>
        Fires(Faction(World(), 2, 22, 41), "wrong-ender",
            "The rule of Weallhous Dreld ended when he was beaten in an open challenge by " +
            "Saern Meastouth, who was then killed by Dreld.");

    [Fact]
    public void R6_ARaidPointedTheWrongWay()
    {
        WorldView view = World();
        FabricationReport report = FabricationCheck.Check(Faction(view, 2, 22, 41),
            "Hadale broke away from the Compact to form the Hadale Commune after a raid on " +
            "the Compact was beaten off.");

        Assert.Contains(report.Findings, f => f.Kind is "wrong-direction" or "ambiguous-short-name");
    }

    // ---- round 7 ----------------------------------------------------------

    [Fact]
    public void R7_KilledRulersDescribedAsCastOut()
    {
        WorldView view = World();
        FabricationReport report = FabricationCheck.Check(Faction(view, 2, 22, 41),
            "Le Vild was cast out in 33, Heth Fal in 35, Nael War in 37, and Paernrom Sir in 38, " +
            "before Kondruth Tru was cast out in 39.");

        Assert.Contains(report.Findings, f => f.Kind == "wrong-fate" && f.Token == "war");
        Assert.Contains(report.Findings, f => f.Kind == "wrong-fate" && f.Token == "sir");
    }

    [Fact]
    public void R7_ARaidThatDoesNotExist() =>
        Fires(Faction(World(), 2, 22, 41), "no-such-event",
            "The Compact suffered three raids: one by the Sworn Men of Meigate on Hadale in 23, " +
            "one by the Sworn Men of Laehiford on Kebarrow in 23, and one by the Griwick Compact " +
            "on Kebarrow in 32.");

    [Fact]
    public void R7_IncludingBeforeAnExhaustiveList()
    {
        WorldView view = World();
        ContextPack pack = Faction(view, 2, 22, 41);

        List<string> names = [];
        foreach (Tenure t in pack.Digest.Tenures)
        {
            string surname = ContextPackBuilder.Surname(t.Holder);
            if (!names.Contains(surname)) names.Add(surname);
            if (names.Count == 3) break;
        }

        Fires(pack, "hedged-exhaustive-list",
            $"Three people were killed, including {names[0]}, {names[1]} and {names[2]}.");
    }

    /// <summary>A reign is a spell in one seat. Heth Fal held two, and they are two scopes.</summary>
    [Fact]
    public void R7_AReignUnderTheWrongFaction()
    {
        WorldView view = World();

        EntityId hethFal = EntityId.None;
        foreach (Actor a in view.State.Actors)
            if (a.Name.EndsWith(" Fal", StringComparison.Ordinal)) hethFal = a.Id;

        List<ReignSpell> spells = ContextPackBuilder.Reigns(view, hethFal);
        Assert.Equal(2, spells.Count);
        Assert.NotEqual(spells[0].Faction, spells[1].Faction);

        ContextPack laehiford = ContextPackBuilder.Reign(view, spells[^1]);
        foreach (EventId id in laehiford.Events)
        {
            bool namesTheSeat = false;
            foreach (Participant p in view.Log.Get(id).Participants)
                if (p.Id == spells[^1].Faction) namesTheSeat = true;
            Assert.True(namesTheSeat);
        }
    }

    // ---- round 8 ----------------------------------------------------------

    [Fact]
    public void R8_DanpaDidNotKillSeirn() =>
        Fires(Faction(World(), 3, 4, 23), "wrong-killer",
            "The period ended with Turaer Danpa holding the seat after killing Befu Seirn.");

    [Fact]
    public void R8_ASummaryContradictingItsBody() =>
        Fires(Faction(World(), 3, 4, 23), "wrong-killer",
            "The period ended with Turaer Danpa holding the seat after killing Befu Seirn. " +
            "Bu Rumpirn had Befu Seirn murdered in year 23, and Turaer Danpa took the seat.");

    [Fact]
    public void R8_TheWrongPowerCollapsed() =>
        Fires(Faction(World(), 2, 2, 21), "wrong-collapse",
            "Peace was made with the Wurn League in year 21 as the Kebarrow Compact collapsed.");

    [Fact]
    public void R8_ThresThrildKilledInTheWrongYear() =>
        Fires(Faction(World(), 2, 42, 51), "wrong-year",
            "In 46, he ordered the murder of Veillpea Dourn at Vea Lode and Thres Thrild at Griwick.");

    [Fact]
    public void R8_BaedrosMamCourtedAwayInTheWrongYear()
    {
        WorldView view = World();

        EntityId hethFal = EntityId.None;
        foreach (Actor a in view.State.Actors)
            if (a.Name.EndsWith(" Fal", StringComparison.Ordinal)) hethFal = a.Id;

        ContextPack pack = ContextPackBuilder.Reign(view, ContextPackBuilder.Reigns(view, hethFal)[^1]);
        Fires(pack, "wrong-year", "Voudreirn Wer won Baedros Mam away from the ruler in 49.");
    }

    // ---- round 9 ----------------------------------------------------------

    /// <summary>Tier 1: a count of two, three named. No event access needed.</summary>
    [Fact]
    public void R9_TwoMarriagesThenThreeNamed()
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            "Two marriages bound the commune to other powers: Sor Pean married Thres Thrild in " +
            "37, linking it to the Vea Lode Covenant, and Ta Poveil married Kaes Rou in 48, " +
            "linking it to the Sworn Men of Meigate. A second marriage to the Sworn Men of " +
            "Meigate occurred in 49.");

        Assert.Contains(findings, f => f.Kind == "count-vs-narration");
    }

    /// <summary>Tier 1: two wars stated, one told. No event access needed.</summary>
    [Fact]
    public void R9_TwoWarsStatedOneNarrated()
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            "The Compact engaged in two wars against the Wurn League, fighting three battles " +
            "which it won. In 7, it defeated the Wurn League at Laehiford, taking the " +
            "settlement. In 8, it defeated them again at Hadale. Peace was made in 9 after two " +
            "years of war.");

        Assert.Contains(findings, f => f.Kind == "count-vs-narration");
    }

    /// <summary>
    /// The same count-versus-list failure in the form it took on the re-render: the members are
    /// marked by their years rather than by repeating the noun, and there are three of them
    /// under a heading of two.
    /// </summary>
    [Fact]
    public void R9_TwoMarriagesThenThreeDatedMembers()
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            "Two marriages tied the commune to other powers: one to the Vea Lode Covenant in " +
            "37 and two to the Sworn Men of Meigate in 48 and 49.");

        Fabrication found = Assert.Single(findings, f => f.Kind == "count-vs-list");
        Assert.Contains("3 named", found.Context, StringComparison.Ordinal);
    }

    /// <summary>
    /// The counterpart: a list whose members share a year must not be reported as short. Two
    /// marriages in 48 and one in 50 is three members and two distinct years.
    /// </summary>
    [Fact]
    public void ADatedListWhoseMembersShareAYearIsLeftAlone()
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            "The Kebarrow Compact formed three marriages tying it to other powers: two to the " +
            "Sworn Men of Laehiford in 48 and one to the Hadale Commune in 50.");

        Assert.DoesNotContain(findings, f => f.Kind is "count-vs-list" or "count-vs-narration");
    }

    [Fact]
    public void R9_ThreiCutRevoltInTheWrongYear() =>
        Fires(Faction(World(), 3, 4, 23), "wrong-year",
            "The Compact's standing fell, leading to uprisings at Vea Lode in 15 and Threi Cut in 15.");

    /// <summary>A raid that took nothing is not a raid that carried off plunder.</summary>
    [Fact]
    public void R9_AZeroHaulIsNotPlunder()
    {
        WorldView view = World();
        int empty = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid) continue;
            if (e.Outcome != Outcome.Succeeded || e.GetInt("loot") > 0) continue;

            empty++;
            string line = view.Describe(e.Id);
            Assert.Contains("takes nothing", line, StringComparison.Ordinal);
            Assert.DoesNotContain("carrying off", line, StringComparison.Ordinal);
        }

        Assert.True(empty > 0, "seed 42 should contain raids that took nothing");

        // And the digest counts them apart from both the repulses and the hauls.
        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);
            Assert.Equal(digest.RaidsLaunched,
                digest.RaidsLaunchedBeatenOff + digest.RaidsLaunchedEmpty
                + digest.RaidsOut.Count(r => r.Result == RaidResult.Plunder));
        }
    }

    // ---- round 10 ---------------------------------------------------------

    /// <summary>
    /// The contester/heir swap, back for a second time in a different section. Veillpea Dourn
    /// contested; Kou Peis was the named heir and lost the seat he was in line for.
    /// </summary>
    [Fact]
    public void R10_TheHeirDescribedAsTheContester() =>
        Fires(Faction(World(), 7, 29, 48), "wrong-role",
            "Kou Peis contested the succession but lost the election to Veillpea Dourn in 45 " +
            "and was cast out.");

    /// <summary>And the true telling of the same dispute must pass.</summary>
    [Fact]
    public void R10_TheTrueTellingOfADisputePasses()
    {
        FabricationReport report = FabricationCheck.Check(Faction(World(), 7, 29, 48),
            "Veillpea Dourn contested the claim of the named heir Kou Peis in 45, took the " +
            "seat, and Kou Peis was cast out.");

        Assert.DoesNotContain(report.Findings, f => f.Kind == "wrong-role");
    }

    /// <summary>Tier 1: a world total leaked into a faction's section, and its own narration says so.</summary>
    [Fact]
    public void R10_AWorldTotalInsideAFactionSection()
    {
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            "The period saw seven rulers, five of them killed, and three places taken from the " +
            "Wurn League.\nIt defeated the Wurn League at Laehiford in year 7 and took " +
            "Laehiford from them. It defeated them again at Hadale in year 20 and took Hadale " +
            "from them.");

        Assert.Contains(findings, f => f.Kind == "count-vs-narration");
    }

    /// <summary>
    /// A seat-holder the passage never names. Not a Tier 1 catch: the man dropped here IS named
    /// in the section, as somebody Math Ham had murdered, so no text-only count can tell that
    /// he is missing as a ruler.
    /// </summary>
    [Fact]
    public void R10_ARulerTheSectionNeverNames() =>
        FiresWhole(Faction(World(), 1, 2, 21), "missing-ruler",
            "Math Ham held the seat from year 7 to 17. Trem Lolkoll held the seat from year 17 " +
            "to 20. He ordered the murder of Searn Sisrill in year 8, Reweld Wul in year 15, " +
            "and Thulgea Bu in year 16.");

    /// <summary>One killing told twice: murdered, and then killed again by the same hand.</summary>
    [Fact]
    public void R10_OneKillingToldTwice() =>
        Fires(Faction(World(), 2, 2, 21), "event-told-twice",
            "Thra Bround was murdered by Nael War in year 18 and killed by Nael War at Meigate.");

    /// <summary>An intent nobody recorded, and an outcome given as a shrug.</summary>
    [Fact]
    public void R10_InventedMotiveAndHedgedOutcome()
    {
        WorldView view = World();

        Fires(Faction(view, 7, 49, 51), "invented-mind",
            "Threi Cut rose against the Covenant, exploiting this weakness.");

        Fires(Faction(view, 3, 24, 36), "hedged-outcome",
            "The Compact suffered ten raids during this period, most beaten off.");
    }

    // ---- true sentences the checks must leave alone -----------------------

    /// <summary>
    /// Round 11, item 4: the two shape failures, which are opposites and had been alternating
    /// every round under prompt wording alone.
    ///
    /// Both are style findings — neither makes a section false — and both are worth a retry,
    /// which is the distinction that stops the oscillation. A rule the retry can act on settles
    /// where an instruction did not.
    /// </summary>
    [Fact]
    public void ASectionThatNamesOneOfItsElevenRulersIsTooAggregate()
    {
        WorldView view = World();
        ContextPack pack = Faction(view, 2, 22, 41);
        Assert.True(pack.Digest.Tenures.Count >= 5);

        FabricationReport report = FabricationCheck.Check(pack,
            "The period saw eleven rulers, five of them killed. The Compact sent seven raids " +
            "and suffered four. Its standing fell throughout. Weallhous Dreld held the seat " +
            "longest.", wholeSection: true);

        Fabrication finding = Assert.Single(report.Findings, f => f.Kind == "too-aggregate");
        Assert.False(finding.BlocksCanon);
        Assert.True(finding.WorthRetrying);
    }

    /// <summary>And the same window with its cast restored must pass.</summary>
    [Fact]
    public void ASectionThatNamesHalfItsRulersPasses()
    {
        WorldView view = World();
        ContextPack pack = Faction(view, 2, 22, 41);

        List<string> names = [];
        foreach (Tenure t in pack.Digest.Tenures)
        {
            string surname = ContextPackBuilder.Surname(t.Holder);
            if (!names.Contains(surname)) names.Add(surname);
        }

        string prose = string.Join(", ", names.Take((names.Count + 1) / 2)) + " each held the seat.";

        Assert.DoesNotContain(FabricationCheck.Check(pack, prose, wholeSection: true).Findings,
            f => f.Kind == "too-aggregate");
    }

    /// <summary>The opposite failure: the log with joining words, one sentence per year.</summary>
    [Fact]
    public void ASentencePerYearInOrderIsTheLogTransliterated()
    {
        WorldView view = World();

        FabricationReport report = FabricationCheck.Check(Faction(view, 2, 42, 51),
            "In 43, a raid was beaten off. In 45, a marriage was made. In 46, a killing was " +
            "ordered. In 47, another killing was ordered. In 48, a marriage bound two powers. " +
            "In 49, a raid was beaten off. In 50, a marriage was made. In 51, the seat changed.",
            wholeSection: true);

        Fabrication finding = Assert.Single(report.Findings, f => f.Kind == "year-by-year");
        Assert.False(finding.BlocksCanon);
        Assert.True(finding.WorthRetrying);
    }

    /// <summary>Prose that dates things without walking the log in order must pass.</summary>
    [Fact]
    public void DatedProseThatIsNotAWalkOfTheLogPasses() =>
        Assert.DoesNotContain(
            FabricationCheck.Check(Faction(World(), 2, 42, 51),
                "The decade opened badly. In 43 a raid was beaten off, and two more failed " +
                "before it ended. Paernmel Has ordered two killings, the second in 47. The " +
                "seat changed hands once, in 51.", wholeSection: true).Findings,
            f => f.Kind == "year-by-year");

    /// <summary>
    /// Every false positive the round-12 checks produced on their first contact with real prose.
    ///
    /// Eight new rules, one wrongly-excluded section. The count keeps coming out at roughly one
    /// per round no matter how carefully the rules are written, which is why the rule is to run
    /// them against the whole chronicle before believing any of them, and to pin whatever they
    /// get wrong here.
    /// </summary>
    [Fact]
    public void TheRoundTwelveChecksLeaveTrueSentencesAlone()
    {
        WorldView view = World();

        // A sentence that states the plunder split correctly. Both halves are true and the
        // second half is the negation of the first, so reading the sentence whole charged the
        // plunder claim with the three repulses it had just reported accurately.
        FabricationReport report = FabricationCheck.Check(Faction(view, 3, 4, 23),
            "It sent six raids, three of which carried off plunder from Kebarrow in years 4 " +
            "and 17 and Laehiford in year 12, while three were beaten off at Hadale in years " +
            "7 and 22 and Meigate in year 13.");

        Assert.DoesNotContain(report.Findings, f => f.Kind == "no-such-event");
    }

    /// <summary>
    /// The round-12 confirming render, verbatim. Every round-11 defect was gone and the prose
    /// had moved to a shape none of the rules had met: totals split into two sub-counts, each
    /// with its own bracketed list.
    ///
    /// Seven true sentences were held out of canon over it, across four sections. Grouped
    /// sub-lists are what the new prompt asks for, so this shape is now the normal one and the
    /// rules have to read it.
    /// </summary>
    [Fact]
    public void TheGroupedSubListShapeIsReadCorrectly()
    {
        WorldView view = World();

        (ContextPack Pack, string Claim)[] cases =
        [
            // Three outcomes, three sub-counts, adding to five.
            (Faction(view, 1, 2, 21),
             "The League sent out five raids: one carried off plunder from Griwick in year 6, " +
             "while three got through but took nothing, and one was beaten off."),

            // Six raids as two groups of three, each group with its own bracketed places.
            (Faction(view, 3, 4, 23),
             "The Compact sent six raids: three carried off plunder (Kebarrow in 4, Laehiford " +
             "in 12, Kebarrow in 17) and three were beaten off (Hadale in 7, Meigate in 13, " +
             "Hadale in 22)."),

            // A haul inside a sub-count is a quantity, not another part of the total.
            (Faction(view, 3, 4, 23),
             "It suffered three raids: one on Griwick in 6 by the Wurn League, which carried " +
             "off 33 grain and killed 45, and two beaten off (Griwick in 13 by the Kebarrow " +
             "Compact, Vea Lode in 19 by the Kebarrow Compact)."),

            // A year that belongs to the man who lost the challenge, not to the man he lost to.
            (Faction(view, 2, 22, 41),
             "He was cast out after losing a subsequent challenge to Teillmol Lund in 27. " +
             "Teillmol Lund was also cast out in 28 for attempted murder."),

            // The year after the comma belongs to the famine, not to the raid before it.
            (Faction(view, 7, 29, 48),
             "A raid on Griwick was beaten off, and in 31, fourteen died while 33 abandoned " +
             "the place."),

            // A count in one sentence and an unrelated list in the next.
            (Faction(view, 5, 20, 51),
             "Seven exiles returned to take service during these years. One marriage tied the " +
             "power to another: Draes Wild married Ror Rim in 44, binding the Sworn Men to the " +
             "Kebarrow Compact."),
        ];

        foreach ((ContextPack pack, string claim) in cases)
        {
            IReadOnlyList<Fabrication> blocking = FabricationCheck.Check(pack, claim).Blocking;

            Assert.True(blocking.Count == 0,
                $"held out of canon: {claim}\n  " +
                string.Join("\n  ", blocking.Select(f => $"{f.Kind}: {f.Context}")));
        }
    }

    /// <summary>
    /// And the one true finding in the same section, which must survive all of that: the model
    /// wrote "Hedale" for Hadale, and an invented place is exactly what the checker is for.
    /// </summary>
    [Fact]
    public void AMisspeltPlaceIsStillCaught() =>
        Assert.Contains(
            FabricationCheck.Check(Faction(World(), 3, 4, 23),
                "Three raids were beaten off (Hedale in 7, Meigate in 13, Hadale in 22).").Findings,
            f => f.Token.Contains("Hedale", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every false positive the round-10 checks produced on their first contact with real
    /// prose, verbatim. Seven correct sections were excluded from canon by these, which is the
    /// failure the whole design is supposed to avoid: a checker that cries wolf costs the
    /// chronicle real content and then stops being read.
    /// </summary>
    [Fact]
    public void TheRoundTenChecksLeaveTrueSentencesAlone()
    {
        WorldView view = World();

        (ContextPack Pack, string Claim, string Rule)[] cases =
        [
            // Raiding a place is raiding the power that holds it — and the digest says so in
            // those words, so the checker was contradicting the engine's own supplied text.
            (Faction(view, 4, 19, 51),
             "Under Kreathbeas, the Sworn Men sent eight raids: two against the Kebarrow " +
             "Compact in 20 and 23, both carrying off plunder.", "wrong-direction"),

            // "against" here governs killings, not raids.
            (Faction(view, 2, 22, 41),
             "The Compact's own raids on Threi Cut and Griwick were both beaten off, and it " +
             "ordered two killings against people of other powers.", "wrong-direction"),

            // The second mention of a place carries the conquest's year, not the revolt's.
            (Faction(view, 3, 24, 36),
             "Threi Cut rose against the Compact in 31, and the Vea Lode Covenant took Threi " +
             "Cut in 34.", "wrong-year"),

            // An elided verb: one "beat", two battles.
            (Faction(view, 7, 29, 48),
             "It fought two battles, both won: it beat the Griwick Compact at Threi Cut in 34, " +
             "killing 13, and at Griwick in 35, killing 113.", "count-vs-list"),

            // A partition, not an enumeration — it never undertakes to name the three.
            (Faction(view, 7, 29, 48),
             "Three people were cast out: two for the losing claim and one, Beas Krouthea, " +
             "for conspiracy against the seat.", "incomplete-enumeration"),

            // The subject of "was cast out" is the man who made the attempt, not its target.
            (Faction(view, 2, 42, 51),
             "In 45, Keithfal Naell attempted Paernmel Has's life and was cast out.",
             "wrong-fate"),

            // A hyphenated common word the prose chose to capitalise is not an invented place.
            (Faction(view, 2, 2, 21),
             "These attacks triggered failed Counter-raids by the Compact in year 5.", "name"),
        ];

        foreach ((ContextPack pack, string claim, string rule) in cases)
        {
            Assert.DoesNotContain(
                FabricationCheck.Check(pack, claim).Findings,
                f => f.Kind == rule);
        }
    }

    /// <summary>
    /// Every false positive the v1.2 unresolvable split produced on its first contact with real
    /// prose. Two, from one change, in the shape the rule predicts.
    ///
    /// Both had been there for rounds and neither was visible, which is the whole argument for
    /// the split. "The lookup was performed and found nothing" was being recorded as "the lookup
    /// could not be performed", so two broken extractions sat in the quiet branch: one reading
    /// four words past the end of a name, the other unable to look up a raid by the power it was
    /// aimed at. Making the branch speak is what exposed them — and would have shipped them as
    /// accusations against true sentences.
    /// </summary>
    [Fact]
    public void TheUnresolvableSplitLeavesTrueSentencesAlone()
    {
        WorldView view = World();

        // A raid on a real target, followed by a body count. The phrase-reader takes up to four
        // words after "on", so the target arrived as "hadale killed 16 but" and matched nothing;
        // narrowed to a known name it matched, and then "16" was read as the year.
        FabricationReport hadale = FabricationCheck.Check(Faction(view, 2, 2, 21),
            "The Compact's raid on Vea Lode was beaten off, and its raid on Hadale killed 16 " +
            "but took little.");

        Assert.DoesNotContain(hadale.Findings, f => f.Kind == "no-such-event");

        // A raid named by the power it was aimed at rather than by the town. Raids were indexed
        // by place alone, so a sentence describing three real raids was told none of them
        // happened.
        FabricationReport covenant = FabricationCheck.Check(Faction(view, 2, 42, 51),
            "It sent three raids against the Vea Lode Covenant, targeting Vea Lode in 43.");

        Assert.DoesNotContain(covenant.Findings, f => f.Kind == "no-such-event");
    }

    /// <summary>
    /// And the split still fires where it should: a killing the records do not hold, asserted
    /// with no year at all.
    ///
    /// The undated form is the one that used to fall out of the loop before the question was
    /// ever asked, because the year was read first. Whether a killing happened does not depend
    /// on the prose troubling to date it.
    /// </summary>
    [Fact]
    public void AnUndatedKillingTheRecordsDoNotHoldStillFires() =>
        Fires(Faction(World(), 7, 29, 48), "no-such-killing",
            "The house rose to power after the murder of Kou Peis.");

    // ---- known gaps -------------------------------------------------------

    /// <summary>
    /// Two events two years apart welded into one relative clause: "took the seat in 48 when
    /// his house ended", where the house ended in 50.
    ///
    /// Not caught. Tier 1.3 compares the same act dated twice; this needs "his house ended" and
    /// "the power was finished" to be recognised as one event, which is synonymy rather than
    /// arithmetic. Left as a Tier 2 job and recorded here so the gap is visible.
    /// </summary>
    [Fact(Skip = "known gap: needs event-phrase synonymy, not arithmetic — Tier 2")]
    public void R9_TwoEventsFusedIntoOneClause() =>
        Fires(Faction(World(), 4, 19, 51), "date-disagreement",
            "Tor Nathgoull, who took the seat in 48 when his house ended.");

    /// <summary>
    /// "killing 149 men" where the records say 149 dead. Gender is a particular and is not
    /// supplied. Not caught: "men" is ordinary English and no rule distinguishes it from the
    /// legitimate uses in "the Sworn Men of Meigate".
    /// </summary>
    [Fact(Skip = "known gap: an invented particular expressed in ordinary words")]
    public void R9_GenderInvented() =>
        Fires(Faction(World(), 7, 49, 51), "invented-particular",
            "the Covenant defeated the Sworn Men of Meigate at Meigate, killing 149 men.");
}
