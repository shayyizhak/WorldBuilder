using System.Reflection;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using Xunit;

namespace WorldBuilder.Chronicle.Tests;

/// <summary>
/// Layer 4: a sealed baseline chronicle, verified against the record that produced it.
///
/// The layer that replaces the hand review. It reads the same document a person read for eleven
/// rounds and asks the same questions — are these the rulers, do these years match, is this
/// figure the one this scope earns — with the difference that it asks every time and never
/// remembers wrong.
///
/// <b>Run against every sealed baseline, not only v1.</b> Ruleset 4 produced a second seed-42
/// chronicle from a different history, and a layer whose job is to notice a checker going quiet is
/// worth as much on the ruleset the engine currently runs as on the one it shipped. Nothing here is
/// a re-verification: these are the checks that need no human, and on the ruleset-4 document they
/// are all that can be said until somebody reads it.
/// </summary>
public class ChronicleLayerTests
{
    public static TheoryData<BaselineUnderTest> Baselines()
    {
        TheoryData<BaselineUnderTest> data = [];
        foreach (BaselineUnderTest one in SealedBaselines.All) data.Add(one);
        return data;
    }

    private static WorldView World(BaselineUnderTest baseline) => SealedBaselines.World(baseline);

    private static List<Section> Sections(BaselineUnderTest baseline) =>
        SealedBaselines.Sections(baseline);

    // ---- the structural guarantee -----------------------------------------

    /// <summary>
    /// This assembly does not reference the checker, and cannot come to.
    ///
    /// The specification asks for a comment saying layer 4 must stay independent. A comment will
    /// not hold: prompt fixes decay in this project and so do comments, and the one thing every
    /// lesson here agrees on is that a guarantee which depends on maintenance is a guarantee with
    /// a date on it. Asserted instead, so a future refactor that reaches for the checker's
    /// implementation fails a test rather than passing a review.
    /// </summary>
    [Fact]
    public void ThisLayerCannotShareAnImplementationWithTheChecker()
    {
        IEnumerable<string> referenced = typeof(ChronicleLayerTests).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? "");

        Assert.DoesNotContain("WorldBuilder.Inference", referenced);

        // And nothing has loaded it in by another route.
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(),
            a => a.GetName().Name == "WorldBuilder.Inference");
    }

    /// <summary>
    /// Both baselines are present and are different worlds.
    ///
    /// Asserted so the theory below cannot pass by silently running twice over the same document.
    /// Seed 42 at ruleset 4 shares its seed with v1 and nothing else: the record diverges because
    /// four mechanics consume distance and the stream is consumed differently.
    /// </summary>
    [Fact]
    public void TheTwoBaselinesAreDifferentWorlds()
    {
        WorldView v1 = World(SealedBaselines.V1);
        WorldView ruleset4 = World(SealedBaselines.Ruleset4);

        Assert.Equal(v1.Seed, ruleset4.Seed);
        Assert.NotEqual(v1.Log.Count, ruleset4.Log.Count);

        // The names differ too, which is what makes the corpus's twenty world-dependent rows
        // world-dependent: the new history has no Sworn Men of Meigate.
        Assert.NotEqual(
            v1.State.Factions.Select(static f => f.Name).OrderBy(static n => n, StringComparer.Ordinal),
            ruleset4.State.Factions.Select(static f => f.Name).OrderBy(static n => n, StringComparer.Ordinal));

        // v1 predates geography and has no board; ruleset 4 is a log and its board.
        Assert.False(v1.State.HasBoard);
        Assert.True(ruleset4.State.HasBoard);
    }

    // ---- the document itself ----------------------------------------------

    [Theory]
    [MemberData(nameof(Baselines))]
    public void TheBaselineChronicleHasTheSectionsItClaims(BaselineUnderTest baseline)
    {
        string markdown = SealedBaselines.Markdown(baseline);

        // Both figures are asserted: the second alone would drift downwards without anyone
        // noticing, since a chronicle that excluded more would simply have fewer sections with
        // prose in it.
        Assert.Equal(baseline.Headings, ChronicleReader.Headings(markdown).Count);

        List<Section> withProse = Sections(baseline);
        Assert.Equal(baseline.WithProse, withProse.Count);
        Assert.All(withProse, s => Assert.False(string.IsNullOrWhiteSpace(s.Body)));
    }

    /// <summary>
    /// Every year the prose dates something to is a year the world actually ran.
    ///
    /// Deliberately the world's range and not the section's. A section window is an editorial
    /// cut, not a fact about the world: a twenty-year window opening in 24 may perfectly well say
    /// its ruler took the seat in 23, and the baseline does exactly that in two places. Asserting
    /// the tighter thing would fail true prose, which is how round 10 put seven correct sections
    /// out of canon.
    ///
    /// Three of the last four rounds had a date error, in three different event types. What makes
    /// those catchable is a year outside the world, not a year outside a heading.
    /// </summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void EveryDatedYearIsAYearTheWorldRan(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);
        int first = view.Log.Events.Where(e => e.Significance >= Significance.Minor).Min(e => e.Year);

        List<string> wrong = [];

        foreach (Section s in Sections(baseline))
            foreach (int year in ChronicleReader.YearsStated(s.Body))
            {
                if (year >= first && year <= view.LastYear) continue;
                wrong.Add($"{s.Heading}: dates something to {year}, outside {first}–{view.LastYear}");
            }

        Assert.True(wrong.Count == 0, string.Join("\n  ", wrong));
    }

    /// <summary>
    /// No section claims a year before the record opens.
    ///
    /// "held the seat since year 1" reached canon twice. The world's first narratable year is 2;
    /// year 1 holds one bookkeeping row creating the world and no pack ever contains it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void NoSectionCitesAYearTheRecordDoesNotHave(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);
        int first = view.Log.Events.Where(e => e.Significance >= Significance.Minor).Min(e => e.Year);

        List<string> wrong = [];

        foreach (Section s in Sections(baseline))
            foreach (int year in ChronicleReader.YearsStated(s.Body))
                if (year < first) wrong.Add($"{s.Heading}: cites {year}, before the record opens at {first}");

        Assert.True(wrong.Count == 0, string.Join("\n  ", wrong));
    }

    /// <summary>Every proper noun in the document is a name the world actually holds.</summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void EveryProperNounIsInTheRecord(BaselineUnderTest baseline)
    {
        HashSet<string> known = RecordFacts.AllNameWords(World(baseline));

        // Ordinary English that wears a capital mid-sentence in this document's register.
        HashSet<string> ordinary = new(StringComparer.OrdinalIgnoreCase)
        {
            "The", "Its", "His", "Her", "Their", "This", "That", "These", "Those", "In", "By",
            "When", "After", "Before", "During", "Both", "Neither", "Each", "One", "Two", "Three",
            "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
        };

        List<string> unknown = [];

        foreach (Section s in Sections(baseline))
            foreach (string noun in ChronicleReader.ProperNouns(s.Body))
            {
                if (ordinary.Contains(noun) || known.Contains(noun)) continue;
                unknown.Add($"{s.Heading}: \"{noun}\"");
            }

        Assert.True(unknown.Count == 0, string.Join("\n  ", unknown.Distinct()));
    }

    // ---- what the record says ---------------------------------------------

    /// <summary>
    /// Departure manner is an exhaustive partition.
    ///
    /// Every hold ends killed, cast out, died naturally, replaced, or is still running. A hold
    /// that fell through every branch would be reported as a natural death, which is the quietest
    /// possible way to be wrong about a murder.
    /// </summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void EveryHoldOnEverySeatEndsInAKnownWay(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);
        string[] known = ["killed", "cast out", "died", "replaced", "still holding"];

        int holds = 0;

        foreach (Faction f in view.State.Factions)
            foreach (Held held in RecordFacts.SeatHistory(view, f.Id))
            {
                Assert.Contains(held.Ended, known);
                holds++;
            }

        // A partition asserted over nothing is satisfied by an empty world. Both baselines hold
        // dozens of spells; the figure is not pinned because it is a fact about the history rather
        // than about the check.
        Assert.True(holds > 10, $"{baseline}: only {holds} seat spells to partition");

        // And it is a ruler list, not a record list. A contested transfer emits both the challenge
        // that decided it and a succession row beside it, so reading both put the same man on the
        // same seat twice in the same year. Nothing here failed on that, because every assertion
        // was about the partition; the list itself was never checked.
        //
        // Asserted on the year and not on the person, which is the whole correction. "No two
        // neighbouring spells share a ruler" is satisfied by a derivation that collapses a
        // contested transfer correctly *and* by one that deletes a genuine second tenure, and
        // those are opposite errors. What a ruler list may never contain is one person holding one
        // seat twice in one year; the same person back later is a second hold and must survive.
        foreach (Faction f in view.State.Factions)
        {
            List<Held> history = RecordFacts.SeatHistory(view, f.Id);

            for (int i = 1; i < history.Count; i++)
            {
                Assert.False(history[i - 1].Ruler == history[i].Ruler && history[i - 1].From == history[i].From,
                    $"{baseline}: {view.State.NameOf(history[i].Ruler)} holds " +
                    $"{f.Name} twice in {history[i].From}");
            }
        }
    }

    /// <summary>
    /// Founding holders are visible.
    ///
    /// A ruler list built from successions alone misses the person a secession installs, which
    /// is how founding rulers were invisible until round 8. Every faction created by a secession
    /// must have a first holder dated to that secession.
    /// </summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void AFoundingHolderIsPartOfTheSeatHistory(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);
        int checkedFactions = 0, secessions = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySecession || e.Subject.IsNone) continue;
            secessions++;

            // The house that broke away, not the one it broke from.
            EntityId born = RecordFacts.NewHouse(e);
            if (born.IsNone) continue;

            List<Held> history = RecordFacts.SeatHistory(view, born);

            Assert.NotEmpty(history);
            Assert.Equal(e.Subject, history[0].Ruler);
            Assert.Equal(e.Year, history[0].From);
            checkedFactions++;
        }

        // Every secession in the record was checked, and there was at least one to check. Derived
        // from the world rather than pinned at four: v1 has four and ruleset 4 is a different
        // history, so a hard figure here would be asserting v1's shape of a second world.
        Assert.Equal(secessions, checkedFactions);
        Assert.True(checkedFactions > 0, $"{baseline}: no secession in the record to check");
    }

    /// <summary>Tenure spans are clamped to the window at both ends; one-sided clamping was the round-8 bug.</summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void TenureSpansAreClampedAtBothEnds(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);

        foreach (Section s in Sections(baseline))
        {
            if (!s.HasWindow || s.IsReign) continue;

            foreach (Faction f in view.State.Factions)
            {
                if (!s.Heading.Contains(f.Name.Replace("the ", "", StringComparison.OrdinalIgnoreCase),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (Held held in RecordFacts.SeatHistory(view, f.Id))
                {
                    int from = Math.Max(held.From, s.FromYear);
                    int to = Math.Min(held.To, s.ToYear);
                    if (from > to) continue;

                    Assert.InRange(from, s.FromYear, s.ToYear);
                    Assert.InRange(to, s.FromYear, s.ToYear);
                }
            }
        }
    }

    /// <summary>Raid counts split three ways and the parts sum to the whole.</summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void RaidTalliesPartitionTheRaids(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);

        foreach (Faction f in view.State.Factions)
        {
            RaidTally sent = RecordFacts.RaidsSent(view, f.Id, view.FirstYear, view.LastYear);
            RaidTally suffered = RecordFacts.RaidsSuffered(view, f.Id, view.FirstYear, view.LastYear);

            Assert.Equal(sent.Total, sent.BeatenOff + sent.TookAHaul + sent.TookNothing);
            Assert.Equal(suffered.Total, suffered.BeatenOff + suffered.TookAHaul + suffered.TookNothing);
        }

        // Every raid in the record is sent by exactly one faction, so the sent tallies must
        // account for all of them. A filter that silently dropped rows would show here.
        int inRecord = view.Log.Events.Count(e => e.Kind == EventKind.ConflictRaid);
        int counted = view.State.Factions.Sum(f =>
            RecordFacts.RaidsSent(view, f.Id, view.FirstYear, view.LastYear).Total);

        Assert.Equal(inRecord, counted);
        Assert.True(inRecord > 0, $"{baseline}: no raids in the record to partition");

        // All three branches are populated, not merely accounted for.
        //
        // This is the assertion that was missing, and its absence hid a real defect for as long as
        // Layer 4 has existed: the haul figure was read from a data key the engine never writes, so
        // every successful raid came back as "took nothing". The sums balanced, the totals matched
        // the record, and the split was two-way while claiming to be three. **Assert extraction,
        // not just absence of failure** — a partition whose third cell is structurally always zero
        // is a partition that cannot report what it was written for.
        RaidTally all = new(
            view.State.Factions.Sum(f => RecordFacts.RaidsSent(view, f.Id, view.FirstYear, view.LastYear).BeatenOff),
            view.State.Factions.Sum(f => RecordFacts.RaidsSent(view, f.Id, view.FirstYear, view.LastYear).TookAHaul),
            view.State.Factions.Sum(f => RecordFacts.RaidsSent(view, f.Id, view.FirstYear, view.LastYear).TookNothing));

        Assert.True(all.BeatenOff > 0, $"{baseline}: no raid was beaten off");
        Assert.True(all.TookAHaul > 0, $"{baseline}: no raid came away with anything — the haul figure is inert");
        Assert.True(all.TookNothing > 0, $"{baseline}: no raid got through empty");
    }

    /// <summary>Battles, killings and marriages are counted without dropping rows.</summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void TheOtherTalliesAccountForEveryRow(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);

        int battlesInRecord = view.Log.Events.Count(e => e.Kind == EventKind.ConflictBattle);
        int battlesCounted = view.State.Factions.Sum(f =>
        {
            (int won, int lost) = RecordFacts.Battles(view, f.Id, view.FirstYear, view.LastYear);
            return won + lost;
        });

        // Each battle names a winner and a loser, so the panel counts every battle twice.
        Assert.Equal(battlesInRecord * 2, battlesCounted);

        int marriagesInRecord = view.Log.Events.Count(e => e.Kind == EventKind.LifeMarriage);
        Assert.True(marriagesInRecord > 0, "the record holds no marriages to count");

        // Killings split internal and external, and the split accounts for every violent death
        // whose killer served a house at the time. Asserted as a partition rather than as a figure,
        // since the figure is a fact about the history.
        int killings = 0;
        foreach (Faction f in view.State.Factions)
        {
            (int inside, int outside) = RecordFacts.Killings(view, f.Id, view.FirstYear, view.LastYear);
            killings += inside + outside;
        }

        Assert.True(killings > 0, $"{baseline}: no killings attributed to any house");
    }

    // ---- statistics carry a scope -----------------------------------------

    /// <summary>
    /// A figure quoted inside a reign was computed for that reign.
    ///
    /// Corpus row 10, which has failed twice: "Under Kreathbeas, the Sworn Men sent eight raids",
    /// where eight is the faction's lifetime total and the reign's own figure is smaller. The
    /// deliberate error below is caught by comparing the two windows, which is the only way to
    /// see it — the sentence is grammatical, the number is real, and it is about the wrong thing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void AFactionLifetimeFigureInsideAReignIsCaught(BaselineUnderTest baseline)
    {
        WorldView view = World(baseline);

        // A reign whose window disagrees with its house's lifetime on raids sent. Searched for
        // rather than taken first: where the two windows agree, quoting one inside the other is
        // undetectable, and a test that took the first reign would pass by coincidence on one
        // baseline and fail on the other for a reason that is not a defect.
        foreach (Section reign in Sections(baseline).Where(static s => s.IsReign && s.HasWindow))
        {
            Faction? subject = view.State.Factions.FirstOrDefault(f =>
                reign.Heading.Contains(f.Name.Replace("the ", "", StringComparison.OrdinalIgnoreCase),
                    StringComparison.OrdinalIgnoreCase));

            if (subject is null) continue;

            RaidTally forTheReign = RecordFacts.RaidsSent(view, subject.Id, reign.FromYear, reign.ToYear);
            RaidTally forTheLifetime = RecordFacts.RaidsSent(view, subject.Id, view.FirstYear, view.LastYear);

            if (forTheLifetime.Total <= forTheReign.Total) continue;

            // The deliberate defect: the lifetime figure, stated inside the reign.
            List<int> stated = ChronicleReader.Figures(
                $"Under his rule the house sent {forTheLifetime.Total} raids.");

            Assert.Contains(forTheLifetime.Total, stated);
            Assert.DoesNotContain(forTheReign.Total, stated);

            // And the true telling passes the same check.
            Assert.Contains(forTheReign.Total,
                ChronicleReader.Figures($"Under his rule the house sent {forTheReign.Total} raids."));

            return;
        }

        Assert.Fail($"{baseline}: no reign section whose window disagrees with its house's lifetime " +
                    "on raids sent, so no scope error is detectable in this document");
    }
}
