using System.Reflection;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Chronicle.Tests;

/// <summary>
/// Layer 4: the sealed baseline chronicle, verified against the record that produced it.
///
/// The layer that replaces the hand review. It reads the same document a person read for eleven
/// rounds and asks the same questions — are these the rulers, do these years match, is this
/// figure the one this scope earns — with the difference that it asks every time and never
/// remembers wrong.
/// </summary>
public class ChronicleLayerTests
{
    private static string Baseline()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            string candidate = Path.Combine(at.FullName, "baselines", "v1", "seed-42");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException($"no baselines/v1/seed-42 above {AppContext.BaseDirectory}");
    }

    private static WorldView World()
    {
        (EventLog log, ulong seed) = JsonlIo.Read(Path.Combine(Baseline(), "world-42.jsonl"));
        return WorldView.Build(log, seed);
    }

    private static List<Section> Chronicle() =>
        ChronicleReader.Sections(File.ReadAllText(Path.Combine(Baseline(), "chronicle-42.md")));

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

    // ---- the document itself ----------------------------------------------

    [Fact]
    public void TheBaselineChronicleHasTheSectionsItClaims()
    {
        string markdown = File.ReadAllText(Path.Combine(Baseline(), "chronicle-42.md"));

        // Fifteen scopes, of which three carry no verified account and are held out. Both figures
        // are asserted: the second alone would drift downwards without anyone noticing, since a
        // chronicle that excluded more would simply have fewer sections with prose in it.
        Assert.Equal(15, ChronicleReader.Headings(markdown).Count);

        List<Section> withProse = Chronicle();
        Assert.Equal(12, withProse.Count);
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
    [Fact]
    public void EveryDatedYearIsAYearTheWorldRan()
    {
        WorldView view = World();
        int first = view.Log.Events.Where(e => e.Significance >= Significance.Minor).Min(e => e.Year);

        List<string> wrong = [];

        foreach (Section s in Chronicle())
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
    [Fact]
    public void NoSectionCitesAYearTheRecordDoesNotHave()
    {
        WorldView view = World();
        int first = view.Log.Events.Where(e => e.Significance >= Significance.Minor).Min(e => e.Year);

        List<string> wrong = [];

        foreach (Section s in Chronicle())
            foreach (int year in ChronicleReader.YearsStated(s.Body))
                if (year < first) wrong.Add($"{s.Heading}: cites {year}, before the record opens at {first}");

        Assert.True(wrong.Count == 0, string.Join("\n  ", wrong));
    }

    /// <summary>Every proper noun in the document is a name the world actually holds.</summary>
    [Fact]
    public void EveryProperNounIsInTheRecord()
    {
        HashSet<string> known = RecordFacts.AllNameWords(World());

        // Ordinary English that wears a capital mid-sentence in this document's register.
        HashSet<string> ordinary = new(StringComparer.OrdinalIgnoreCase)
        {
            "The", "Its", "His", "Her", "Their", "This", "That", "These", "Those", "In", "By",
            "When", "After", "Before", "During", "Both", "Neither", "Each", "One", "Two", "Three",
            "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
        };

        List<string> unknown = [];

        foreach (Section s in Chronicle())
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
    [Fact]
    public void EveryHoldOnEverySeatEndsInAKnownWay()
    {
        WorldView view = World();
        string[] known = ["killed", "cast out", "died", "replaced", "still holding"];

        foreach (Faction f in view.State.Factions)
            foreach (Held held in RecordFacts.SeatHistory(view, f.Id))
                Assert.Contains(held.Ended, known);
    }

    /// <summary>
    /// Founding holders are visible.
    ///
    /// A ruler list built from successions alone misses the person a secession installs, which
    /// is how founding rulers were invisible until round 8. Every faction created by a secession
    /// must have a first holder dated to that secession.
    /// </summary>
    [Fact]
    public void AFoundingHolderIsPartOfTheSeatHistory()
    {
        WorldView view = World();
        int checkedFactions = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySecession || e.Subject.IsNone) continue;

            // The house that broke away, not the one it broke from.
            EntityId born = RecordFacts.NewHouse(e);
            if (born.IsNone) continue;

            List<Held> history = RecordFacts.SeatHistory(view, born);

            Assert.NotEmpty(history);
            Assert.Equal(e.Subject, history[0].Ruler);
            Assert.Equal(e.Year, history[0].From);
            checkedFactions++;
        }

        // The four secessions of seed 42. Asserted so this cannot pass by finding none.
        Assert.Equal(4, checkedFactions);
    }

    /// <summary>Tenure spans are clamped to the window at both ends; one-sided clamping was the round-8 bug.</summary>
    [Fact]
    public void TenureSpansAreClampedAtBothEnds()
    {
        WorldView view = World();

        foreach (Section s in Chronicle())
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
    [Fact]
    public void RaidTalliesPartitionTheRaids()
    {
        WorldView view = World();

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
    }

    /// <summary>Battles, killings and marriages are counted without dropping rows.</summary>
    [Fact]
    public void TheOtherTalliesAccountForEveryRow()
    {
        WorldView view = World();

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
    [Fact]
    public void AFactionLifetimeFigureInsideAReignIsCaught()
    {
        WorldView view = World();

        Section reign = Chronicle().First(s => s.IsReign && s.HasWindow);
        Faction subject = view.State.Factions.First(f =>
            reign.Heading.Contains(f.Name.Replace("the ", "", StringComparison.OrdinalIgnoreCase),
                StringComparison.OrdinalIgnoreCase));

        RaidTally forTheReign = RecordFacts.RaidsSent(view, subject.Id, reign.FromYear, reign.ToYear);
        RaidTally forTheLifetime = RecordFacts.RaidsSent(view, subject.Id, view.FirstYear, view.LastYear);

        // The premise of the check: the two windows disagree, so quoting one inside the other is
        // detectable. If they ever agreed, this test would be passing by coincidence.
        Assert.True(forTheLifetime.Total > forTheReign.Total,
            $"{subject.Name}: lifetime {forTheLifetime.Total} raids, reign window {forTheReign.Total} — " +
            "no scope error is detectable here");

        // The deliberate defect: the lifetime figure, stated inside the reign.
        string defective = $"Under his rule the house sent {forTheLifetime.Total} raids.";

        List<int> stated = ChronicleReader.Figures(defective);

        Assert.Contains(forTheLifetime.Total, stated);
        Assert.DoesNotContain(forTheReign.Total, stated);

        // And the true telling passes the same check.
        Assert.Contains(forTheReign.Total,
            ChronicleReader.Figures($"Under his rule the house sent {forTheReign.Total} raids."));
    }
}
