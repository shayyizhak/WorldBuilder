using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// A contested transfer is one hold; the same man back later is two.
///
/// <b>What was wrong.</b> A contested transfer emits two records — the challenge or coup that
/// decided it, and a <c>POLITY.SUCCESSION</c> beside it carrying the state change — and the ruler
/// list collapsed any two <i>neighbouring</i> appearances by one person into one hold, whatever
/// years they carried. That is right for every contested transfer and wrong for a second tenure
/// with nobody recorded in between: the later hold was deleted from the list rather than
/// collapsed into the earlier one.
///
/// <b>Why nothing caught it.</b> The only assertion on the list was that no two neighbouring
/// spells shared a ruler, and that assertion passes under both errors. Collapsing correctly
/// satisfies it; deleting a tenure satisfies it too, and rather more emphatically. Assert the
/// collapse <i>and</i> the survival, or the test is satisfied by a derivation that drops both.
/// </summary>
public class SeatTransferTests
{
    private static readonly ulong[] Panel = ReferencePanel.Current;

    // ---- both shapes, at the entry point the product calls ------------------

    /// <summary>
    /// One world carrying both shapes on one seat, read through
    /// <see cref="ReferenceSet.SeatHistory"/> — the function <c>wb reference</c> and the candidate
    /// facts sheet call, not an inner step of it.
    ///
    /// Built by hand rather than found in a world, because a world containing an adjacent second
    /// tenure may not exist on this panel and a test that skipped when it could not find one would
    /// be a test that reported coverage it did not have.
    /// </summary>
    [Fact]
    public void ACollapsedTransferAndASecondTenureBothSurviveTheDerivation()
    {
        WorldView view = TwoShapes();
        List<SeatSpell> history = ReferenceSet.SeatHistory(view, EntityId.Faction(1));

        // Three holds: Ana from 10, Ana again from 18 after her exile, Bran from 22 — whose two
        // records are one hold. Four would mean the transfer was read twice; two would mean a
        // tenure was deleted.
        Assert.Equal(3, history.Count);

        Assert.Equal(EntityId.Actor(1), history[0].Ruler);
        Assert.Equal(10, history[0].From);

        // The second tenure survived. This is the assertion the old rule failed, and the one an
        // "assert no duplicate" test cannot make: Ana's two holds sit side by side in the record
        // with nobody between them, which is exactly what the old rule read as one hold.
        Assert.Equal(EntityId.Actor(1), history[1].Ruler);
        Assert.Equal(18, history[1].From);

        // The contested transfer collapsed: one spell, not two identical ones dated 22–22.
        Assert.Equal(EntityId.Actor(2), history[2].Ruler);
        Assert.Equal(22, history[2].From);
        Assert.Single(history, s => s.Ruler == EntityId.Actor(2));
    }

    /// <summary>Both shapes are present in the fixture, so the test above is not passing on an empty world.</summary>
    [Fact]
    public void TheFixtureActuallyContainsBothShapes()
    {
        List<SeatRepeat> repeats = SeatTransfers.Repeats(TwoShapes(), EntityId.Faction(1));

        Assert.Contains(repeats, r => r.Shape == SeatRepeatShape.ContestedTransfer);
        Assert.Contains(repeats, r => r.Shape == SeatRepeatShape.SecondTenure);

        // And the second tenure sits next to its earlier hold in the raw moves, which is what
        // makes it reachable by a collapse rule at all. A non-adjacent one proves nothing about
        // the rule, and every second tenure on the sealed panel happens to be non-adjacent —
        // which is exactly why the defect survived five worlds.
        Assert.Contains(repeats, r => r.Shape == SeatRepeatShape.SecondTenure && r.Adjacent);
    }

    // ---- the sealed panel ---------------------------------------------------

    /// <summary>
    /// Every repeated appearance on every seat, on every sealed baseline, fits one of the two
    /// shapes.
    ///
    /// A case fitting neither is escalated rather than given a rule of its own, so this asserts
    /// the classification is exhaustive rather than asserting a count. Run over the ruleset-4
    /// panel, the sealed v1 record and the ruleset-3 set, because a shape the current rules cannot
    /// produce may still be sitting in an archived record that Layer 3 depends on.
    /// </summary>
    [Theory]
    [InlineData("ruleset-4", 7UL)]
    [InlineData("ruleset-4", 42UL)]
    [InlineData("ruleset-4", 99UL)]
    [InlineData("ruleset-4", 1234UL)]
    [InlineData("ruleset-4", 2025UL)]
    [InlineData("v1", 42UL)]
    public void NoRepeatOnAnySeatFitsNeitherShape(string set, ulong seed)
    {
        WorldView view = Sealed(set, seed);

        List<SeatRepeat> unclassified =
            [.. SeatTransfers.Repeats(view).Where(static r => r.Shape == SeatRepeatShape.Unclassified)];

        Assert.True(unclassified.Count == 0,
            $"{set} seed {seed}:\n  " +
            string.Join("\n  ", unclassified.Select(r => r.Describe(view.State))));

        // And there is something to classify. Zero repeats would satisfy the line above without
        // the derivation ever meeting a contested transfer.
        Assert.NotEmpty(SeatTransfers.Contested(view));
    }

    /// <summary>
    /// The ruler lists this fix changed, on the sealed panel: none, and mechanically so.
    ///
    /// The queue item asks for the regenerated lists diffed against the pre-fix ones, which cannot
    /// be done by re-reading — the pre-fix rule is gone. So it is re-derived: the old rule
    /// collapsed adjacent same-person pairs whatever their years, the new one requires the year to
    /// match too, and the two produce the same list exactly when every <i>adjacent</i> repeat is a
    /// contested transfer.
    ///
    /// Asserted rather than stated, so the staged reference-set rows can be reported as unaffected
    /// with something behind the claim. It is not a permanent property of the engine: a world with
    /// an adjacent second tenure would fail here, and correctly — that is the world in which the
    /// old rule would have deleted a tenure, and the staged sheet would need re-staging.
    /// </summary>
    [Theory]
    [InlineData("ruleset-4", 7UL)]
    [InlineData("ruleset-4", 42UL)]
    [InlineData("ruleset-4", 99UL)]
    [InlineData("ruleset-4", 1234UL)]
    [InlineData("ruleset-4", 2025UL)]
    [InlineData("v1", 42UL)]
    public void TheFixMovesNoRulerListOnAnySealedBaseline(string set, ulong seed)
    {
        WorldView view = Sealed(set, seed);
        List<string> moved = [];

        foreach (Faction f in view.State.Factions)
        {
            List<SeatMove> moves = SeatTransfers.Moves(view, f.Id);

            // The pre-fix rule, re-derived here rather than remembered: collapse a neighbouring
            // repeat on the person alone.
            List<(int Year, EntityId Ruler)> before = [];
            foreach (SeatMove move in moves)
                if (before.Count == 0 || before[^1].Ruler != move.Ruler) before.Add((move.Year, move.Ruler));

            List<SeatSpell> after = ReferenceSet.SeatHistory(view, f.Id);

            if (before.Count == after.Count
                && !before.Where((row, i) => row.Ruler != after[i].Ruler || row.Year != after[i].From).Any())
            {
                continue;
            }

            moved.Add($"{f.Name}: {before.Count} spell(s) before, {after.Count} after");
        }

        Assert.True(moved.Count == 0,
            $"{set} seed {seed} — these ruler lists changed and the staged rows built from them " +
            $"need re-staging:\n  " + string.Join("\n  ", moved));
    }

    // ---- fixtures -----------------------------------------------------------

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, WorldView> Cache = new(StringComparer.Ordinal);

    private static WorldView Sealed(string set, ulong seed)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue($"{set}/{seed}", out WorldView? cached)) return cached;

            string path = Corpus.SealedWorld(set, seed, AppContext.BaseDirectory, Directory.GetCurrentDirectory())
                          ?? throw new FileNotFoundException($"no sealed baselines/{set}/seed-{seed}");

            (EventLog log, ulong archived) = JsonlIo.Read(path);
            return Cache[$"{set}/{seed}"] = WorldView.Build(log, archived);
        }
    }

    /// <summary>
    /// A record with one seat, one contested transfer and one adjacent second tenure.
    ///
    /// Written straight into an <see cref="EventLog"/> rather than simulated. A fixture that had to
    /// find these shapes in a generated world would be a fixture that depends on a ruleset, which
    /// is the mistake the corpus made once already.
    /// </summary>
    private static WorldView TwoShapes()
    {
        EventLog log = new();
        int sequence = 0;

        EntityId house = EntityId.Faction(1);
        EntityId ana = EntityId.Actor(1);
        EntityId bran = EntityId.Actor(2);
        EntityId seat = EntityId.Place(1);

        // Genesis names its new entity in the subject role and carries the rest as payload, and
        // the reducer refuses one out of order — which is why these are written the way the engine
        // writes them rather than the way they read best.
        Add(1, EventKind.GenesisWorld, [], [new("startYear", "1"), new("seed", "1")]);
        Add(1, EventKind.GenesisPlace, [new(Role.Subject, seat)],
            [new("name", "Holt"), new("placeKind", nameof(PlaceKind.Settlement)), new("population", "100")]);
        Add(1, EventKind.GenesisFaction, [new(Role.Subject, house)],
            [new("name", "the Holt Compact"), new("succession", nameof(SuccessionRule.Primogeniture)),
             new("seat", seat.ToString())]);
        Add(1, EventKind.GenesisActor, [new(Role.Subject, ana)],
            [new("name", "Ana Holt"), new("birthYear", "-20"), new("title", "Ruler"),
             new("place", seat.ToString()), new("faction", house.ToString())]);
        Add(1, EventKind.GenesisActor, [new(Role.Subject, bran)],
            [new("name", "Bran Holt"), new("birthYear", "-18"), new("title", "Retainer"),
             new("place", seat.ToString()), new("faction", house.ToString())]);

        // Ana takes the seat in 10, is cast out in 13, and takes it back in 18. An exile is not a
        // seat-moving record, so her two successions sit next to each other in the move list with
        // nothing between them — which is precisely the shape the old rule could not tell from a
        // contested transfer, and the reason it deleted the second hold.
        Add(10, EventKind.PolitySuccession, [new(Role.Subject, ana), new(Role.Faction, house)], []);
        Add(13, EventKind.PolityExile, [new(Role.Subject, ana), new(Role.Faction, house)], []);
        Add(18, EventKind.PolitySuccession, [new(Role.Subject, ana), new(Role.Faction, house)], []);

        // Bran takes it from her in 22, contested: two records, one year, one hold.
        Add(22, EventKind.PolityChallenge, [new(Role.Subject, bran), new(Role.Faction, house)], [],
            Outcome.Succeeded);
        Add(22, EventKind.PolitySuccession, [new(Role.Subject, bran), new(Role.Faction, house)], []);

        return WorldView.Build(log, 1);

        void Add(int year, EventKind kind, List<Participant> participants,
            List<KeyValuePair<string, string>> data, Outcome outcome = Outcome.NotApplicable)
        {
            log.Append(EventFactory.Create(
                log.NextId, year, kind, participants,
                causes: kind == EventKind.GenesisWorld ? [] : [new EventId(1)],
                data: data, outcome: outcome, sequence: sequence++));
        }
    }
}
