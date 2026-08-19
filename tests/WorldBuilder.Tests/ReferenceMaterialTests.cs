using System.Text.RegularExpressions;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The four properties the reference material's repair pass turned into rules.
///
/// <b>Each one is a defect that was found by reading, and each is checkable.</b> A second derivation
/// agreeing with the first rules out arithmetic and transcription and nothing else; what these pin is
/// the class of mistake, so the next seed and the next ruleset cannot make it again quietly.
///
/// <b>Panel-wide, not seed 42.</b> Three of the four were found on seed 42 and nothing suggests that
/// is where they only occur — a derivation defect is a property of the derivation. The fourth needs
/// the query engine and runs on the panel too, because building the questions is what emits the year.
/// </summary>
public class ReferenceMaterialTests
{
    private static WorldView World(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);
        return WorldView.Build(sim.Log, seed);
    }

    /// <summary>
    /// No tenure extends past its faction's collapse year.
    ///
    /// Three of seed 42's five seats did. The Vea Lode Covenant's last ruler was shown holding to
    /// year 51 when the collapse record twelve years earlier cites his own death as its cause —
    /// a claim the sheet's own sources contradicted, and it survived because the terminal hold's
    /// closing year was the last year of the record with no branch for a seat that stopped existing.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void NoTenureOutlivesItsFaction(ulong seed)
    {
        WorldView view = World(seed);

        int collapsed = 0;
        foreach (Faction f in view.State.Factions)
        {
            if (ReferenceSet.CollapseYear(view, f.Id) is not { } end) continue;

            List<SeatSpell> spells = ReferenceSet.SeatHistory(view, f.Id);
            if (spells.Count == 0) continue;

            collapsed++;

            foreach (SeatSpell s in spells)
            {
                Assert.True(s.To <= end,
                    $"{view.State.NameOf(s.Ruler)} holds {f.Name} to {s.To}, past its collapse in {end}");

                // And nobody is "still holding" a seat that no longer exists, which is the same
                // defect stated in the vocabulary rather than in the years.
                Assert.False(s.Open,
                    $"{view.State.NameOf(s.Ruler)} is '{ReferenceSet.StillHolding}' on a seat that " +
                    $"ended in {end}");
            }
        }

        Assert.True(collapsed > 0, "no house collapsed on this seed, so nothing above was exercised");
    }

    /// <summary>
    /// A hold's departure record names that hold's own faction and falls inside its own years.
    ///
    /// One death record was closing two tenures on two seats: Stonand Ker was killed in year 47 as
    /// the Griwick Compact's leader, and the same record was read as the end of a Wurn League tenure
    /// that had stopped in 34. It cannot be right about both, and the derivation had no way to tell —
    /// it searched the person's whole life and never asked which house the record was about.
    ///
    /// <b>Asserted by re-finding the record the term rests on</b>, not by trusting the derivation to
    /// report itself. A hold that says <c>killed</c> must have a violent death naming that person and
    /// that faction inside its years, or the term is unsupported.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void EveryDepartureTermHasARecordNamingBothPersonAndFaction(ulong seed)
    {
        WorldView view = World(seed);

        int checked_ = 0;
        foreach (Faction f in view.State.Factions)
        {
            foreach (SeatSpell s in ReferenceSet.SeatHistory(view, f.Id))
            {
                EventKind? want = s.Ended switch
                {
                    "killed" => EventKind.LifeDeathViolent,
                    "died" => EventKind.LifeDeathNatural,
                    "cast out" => EventKind.PolityExile,
                    _ => null,
                };

                if (want is not { } kind) continue;

                checked_++;

                bool supported = view.Log.Events.Any(e =>
                    e.Kind == kind && e.Subject == s.Ruler && e.Faction == f.Id &&
                    e.Year >= s.From && e.Year <= s.To);

                Assert.True(supported,
                    $"{f.Name}: {view.State.NameOf(s.Ruler)} {s.From}–{s.To} says '{s.Ended}', and no " +
                    $"{EventKinds.Name(kind)} names him and {f.Id} inside those years");
            }
        }

        Assert.True(checked_ > 0, "no hold on this seed ended in a departure, so nothing was exercised");
    }

    /// <summary>
    /// No "who ruled in year N" question names a year in which two people held the seat.
    ///
    /// All five of seed 42's did, without exception, because the year asked about was the year the
    /// last holder took the seat — which is by construction the year the one before him lost it. Both
    /// names are supported by the record, so the question cannot fail correctly: a suite scores it as
    /// passing whichever the layer says. Same class as the Meigate famine question, and five of
    /// thirty candidates rather than one.
    ///
    /// <b>Read off the emitted question text.</b> The property belongs to what the loop writes into
    /// <c>questions.md</c>, and a test on the year-picking helper would pass just as happily with the
    /// helper's answer thrown away.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void NoSeatYearQuestionNamesATransitionYear(ulong seed)
    {
        WorldView view = World(seed);
        QueryEngine engine = new(new CacheOnlyLlmClient("none"), view);

        RelationTrajectory.Report ties = RelationTrajectory.Of(view.Log, view.Seed, view.State.Board);
        List<ReferenceStaging.Candidate> made =
            ReferenceStaging.Questions(engine, view, new SeedHoldouts(seed, [], []), ties);

        int asked = 0;
        foreach (ReferenceStaging.Candidate c in made)
        {
            Match m = Regex.Match(c.Text, @"^Who ruled (?<who>.+) in year (?<year>\d+)\?$");
            if (!m.Success) continue;

            Faction? f = view.State.Factions.FirstOrDefault(f => f.Name == m.Groups["who"].Value);
            Assert.NotNull(f);

            int year = int.Parse(m.Groups["year"].Value);
            List<SeatSpell> spells = ReferenceSet.SeatHistory(view, f.Id);

            List<SeatSpell> holding = [.. spells.Where(s => s.From <= year && year <= s.To)];

            asked++;
            Assert.True(holding.Count == 1,
                $"'{c.Text}' has {holding.Count} defensible answer(s): " +
                string.Join(", ", holding.Select(s => $"{view.State.NameOf(s.Ruler)} {s.From}–{s.To}")));
        }

        // Every seat with a hold longer than two years is asked about, so the loop above is not
        // vacuous on any panel seed.
        int askable = view.State.Factions.Count(f =>
            ReferenceStaging.Interior(ReferenceSet.SeatHistory(view, f.Id)) is not null);

        Assert.Equal(askable, asked);
        Assert.True(asked > 0, "no seat on this seed has an interior year, so nothing was exercised");
    }

    /// <summary>
    /// A relation span states no opening year only where no record makes that tie.
    ///
    /// It used to state one only where the *closing event's payload* carried a <c>made</c> key, which
    /// only <c>ECONOMY.TRADE_COLLAPSE</c> does — so every alliance and every war opened with a
    /// <c>?</c> that meant "the derivation did not look there". That is the absent-versus-unknown
    /// conflation, in the one table whose whole purpose is telling ended from never-existed.
    ///
    /// This is <c>RelationTrajectory</c>'s first defect inverted — that one read payload keys and
    /// missed war and peace, applied in code — so it is asserted the same way: against the fold.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void EverySpanOpensWhereTheRecordMakesTheTie(ulong seed)
    {
        WorldView view = World(seed);
        RelationTrajectory.Report ties = RelationTrajectory.Of(view.Log, view.Seed, view.State.Board);

        Assert.NotEmpty(ties.Terminations);

        foreach (Termination t in ties.Terminations)
        {
            Assert.NotNull(t.Made);
            Assert.True(t.Made <= t.Year,
                $"{t.Kind} {t.From}↔{t.To} is made in {t.Made} and ended in {t.Year}");

            // The opening year is a year the record could have made it in: a tie is made by an
            // event, so some event stands at that year.
            Assert.Contains(view.Log.Events, e => e.Year == t.Made);
        }

        // Where the closing event does carry a `made` key, the fold agrees with it. The payload was
        // not wrong, only absent everywhere else — and a fold that disagreed with the one source
        // there was would be the more interesting finding.
        int cross = 0;
        foreach (Termination t in ties.Terminations)
        {
            int payload = view.Log.Get(t.At).GetInt("made", int.MinValue);
            if (payload == int.MinValue) continue;

            cross++;
            Assert.Equal(payload, t.Made);
        }

        Assert.True(cross > 0, "no closing event on this seed carries a `made` key to cross-check");
    }
}
