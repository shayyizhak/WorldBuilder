using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using Xunit;

namespace WorldBuilder.Chronicle.Tests;

/// <summary>
/// The v1 hand-verified ruler facts, checked against the sealed record, at every contested
/// transfer they cross.
///
/// <b>Why this exists.</b> A contested transfer emits two records — the challenge that decided it
/// and a <c>POLITY.SUCCESSION</c> beside it — and a ruler list that reads both as separate holds
/// puts the same person on the same seat twice. The v1 reference facts are hand-verified and Layer
/// 3 depends on them permanently for twenty of twenty-eight scoped rows, so if any of those lists
/// crossed a contested transfer and counted it twice, the error is in the one place nothing
/// downstream questions.
///
/// <b>Crossings that agree are recorded, not skipped.</b> "No crossing found" and "crossings found
/// and every one agreed" are different states and only the second is evidence. The test therefore
/// asserts that the lists cross contested transfers at all, and then that they are right.
///
/// <b>Derived here a second time.</b> Nothing below calls the engine's own seat derivation. The
/// contested transfers are read straight off the log and the lists come from
/// <see cref="RecordFacts"/>, which is Layer 4's independent copy — a check of a hand-verified
/// fact that ran through the derivation under suspicion would be checking the derivation against
/// itself.
/// </summary>
public class V1ReferenceFactsTests
{
    private static WorldView V1 => SealedBaselines.World(SealedBaselines.V1);

    /// <summary>One transfer that emitted two records: the seat, the person, the year, both ids.</summary>
    private sealed record Crossing(EntityId Faction, EntityId Ruler, int Year, EventId Decider, EventId Succession);

    /// <summary>
    /// Every contested transfer in the record, from the record.
    ///
    /// A decider — a challenge or a resolved coup the challenger won — and a succession, on one
    /// seat, in one year, naming one person. Nothing else counts, and a pair that is two
    /// successions or two challenges is deliberately not swept in here: it would be a shape
    /// nobody has seen, and inventing a rule for it is what the brief forbids.
    /// </summary>
    private static List<Crossing> ContestedTransfers(WorldView view)
    {
        List<Crossing> crossings = [];

        foreach (Faction f in view.State.Factions)
        {
            List<(int Year, EntityId Ruler, EventId Id, EventKind Kind)> moves = [];

            foreach (Event e in view.Log.Events)
            {
                bool moved = e.Kind switch
                {
                    EventKind.PolitySuccession => e.Faction == f.Id,
                    EventKind.PolityChallenge or EventKind.PolityCoupResolved =>
                        e.Faction == f.Id && e.Outcome == Outcome.Succeeded,
                    _ => false,
                };

                if (moved && !e.Subject.IsNone) moves.Add((e.Year, e.Subject, e.Id, e.Kind));
            }

            for (int i = 0; i < moves.Count; i++)
                for (int j = i + 1; j < moves.Count; j++)
                {
                    if (moves[i].Ruler != moves[j].Ruler || moves[i].Year != moves[j].Year) continue;

                    bool oneIsASuccession =
                        (moves[i].Kind == EventKind.PolitySuccession) ^ (moves[j].Kind == EventKind.PolitySuccession);

                    if (!oneIsASuccession) continue;

                    (int _, EntityId ruler, EventId first, EventKind firstKind) = moves[i];
                    (int year, EntityId _, EventId second, EventKind _) = moves[j];

                    crossings.Add(firstKind == EventKind.PolitySuccession
                        ? new Crossing(f.Id, ruler, year, second, first)
                        : new Crossing(f.Id, ruler, year, first, second));
                }
        }

        return crossings;
    }

    private static Faction House(WorldView view, string name) =>
        view.State.Factions.Single(f => f.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

    private static string List(WorldView view, EntityId faction) =>
        string.Join("; ", RecordFacts.SeatHistory(view, faction)
            .Select(h => $"{view.State.NameOf(h.Ruler)} {h.From}"));

    // ---- the intersection ---------------------------------------------------

    /// <summary>
    /// The sealed v1 record contains contested transfers, and every one is on a seat some
    /// hand-verified fact is about.
    ///
    /// Asserted first, because everything below is only worth anything if there is a crossing to
    /// find. A version of this that quietly found none would report the strongest possible verdict
    /// on the weakest possible evidence.
    /// </summary>
    [Fact]
    public void TheV1RecordHasContestedTransfersOnSeatsTheReferenceFactsName()
    {
        WorldView view = V1;
        List<Crossing> crossings = ContestedTransfers(view);

        Assert.NotEmpty(crossings);

        // The Kebarrow seat carries most of them and is the subject of two hand-verified negative
        // facts — Stonand Ker never held it, Hehum Skul never ruled. The Hadale seat carries one
        // and is the subject of a hand-verified positive one.
        Assert.Contains(crossings, c => c.Faction == House(view, "Kebarrow").Id);
        Assert.Contains(crossings, c => c.Faction == House(view, "Hadale").Id);

        // Every crossing names two distinct records. One id twice would mean the pair was matched
        // against itself, and the count of crossings would be meaningless.
        Assert.All(crossings, c => Assert.NotEqual(c.Decider, c.Succession));
    }

    /// <summary>
    /// The Vea Lode ruler list, exactly as §8 has it.
    ///
    /// Six holds, and the years are the hand-verified ones. This is the list the brief names, and
    /// the answer to whether it crossed a contested transfer is that it did not — the seat has
    /// none — so the list is correct for a reason unrelated to the collapse rule, and that is
    /// worth stating rather than leaving as a pass.
    /// </summary>
    [Fact]
    public void TheVeaLodeRulerListMatchesTheRecord()
    {
        WorldView view = V1;
        EntityId veaLode = House(view, "Vea Lode").Id;

        (string Name, int From)[] handVerified =
        [
            ("Stald Gearngoll", 29),
            ("Veillpea Dourn", 45),
            ("Thres Thrild", 46),
            ("Gatros Hearn", 47),
            ("Keithfal Naell", 48),
            ("Herpeim Raern", 50),
        ];

        List<Held> derived = RecordFacts.SeatHistory(view, veaLode);

        Assert.Equal(handVerified.Length, derived.Count);

        for (int i = 0; i < handVerified.Length; i++)
        {
            Assert.Equal(handVerified[i].Name, view.State.NameOf(derived[i].Ruler));
            Assert.Equal(handVerified[i].From, derived[i].From);
        }

        // And the reason it is safe: no contested transfer on this seat, so no crossing to
        // double-count. Recorded as an assertion so that a future record which does put one there
        // fails here rather than passing quietly.
        Assert.DoesNotContain(ContestedTransfers(view), c => c.Faction == veaLode);
    }

    /// <summary>
    /// The Hadale ruler list crosses a contested transfer, and agrees with the record anyway.
    ///
    /// The hand-verified fact is that Durnrin Drar took the seat in 47 and held it at 51. The seat
    /// changed hands contested in 38 — two records for Sou Dra — so the list this fact sits at the
    /// end of does cross one. It is the crossing the brief asks to be recorded as checked.
    /// </summary>
    [Fact]
    public void TheHadaleRulerListCrossesAContestedTransferAndAgrees()
    {
        WorldView view = V1;
        EntityId hadale = House(view, "Hadale").Id;

        List<Crossing> crossed = [.. ContestedTransfers(view).Where(c => c.Faction == hadale)];
        Assert.NotEmpty(crossed);

        List<Held> derived = RecordFacts.SeatHistory(view, hadale);

        // One hold per holder, and the contested year appears once. Two spells dated 38 would be
        // the double count.
        Assert.Equal(derived.Count, derived.Select(h => h.Ruler).Distinct().Count());

        foreach (Crossing c in crossed)
            Assert.Single(derived, h => h.Ruler == c.Ruler && h.From == c.Year);

        // The hand-verified fact itself: the last hold is Durnrin Drar's, from 47, still running.
        Held last = derived[^1];
        Assert.Equal("Durnrin Drar", view.State.NameOf(last.Ruler));
        Assert.Equal(47, last.From);
        Assert.Equal("still holding", last.Ended);
    }

    /// <summary>
    /// The two hand-verified negative facts, on the seat that carries the most contested transfers.
    ///
    /// Stonand Ker never held a seat; Hehum Skul was a named heir whose claim was set aside. Both
    /// are checked against every seat in the record rather than against the Kebarrow one alone —
    /// "never held a seat" is a claim about the whole world, and checking it on one seat would be
    /// a weaker fact wearing the words of a stronger one.
    ///
    /// A double-counted contested transfer is exactly how a name that never held a seat could
    /// appear on one, which is what puts these two facts in this file.
    /// </summary>
    [Fact]
    public void NobodyTheReferenceFactsSayNeverRuledAppearsOnAnySeat()
    {
        WorldView view = V1;

        foreach (string name in (string[])["Stonand Ker", "Hehum Skul"])
        {
            foreach (Faction f in view.State.Factions)
                Assert.DoesNotContain(RecordFacts.SeatHistory(view, f.Id),
                    h => string.Equals(view.State.NameOf(h.Ruler), name, StringComparison.Ordinal));
        }

        // The Kebarrow list is the one they would have appeared on, and it does cross contested
        // transfers — so the negative facts are being checked against a list at risk, not a list
        // that was never exposed.
        EntityId kebarrow = House(view, "Kebarrow").Id;
        Assert.Contains(ContestedTransfers(view), c => c.Faction == kebarrow);

        List<Held> kebarrowList = RecordFacts.SeatHistory(view, kebarrow);
        Assert.NotEmpty(kebarrowList);

        // No holder appears twice in one year on this seat. The double-count, asserted where it
        // would have happened, with the list in the message so a failure is readable.
        for (int i = 1; i < kebarrowList.Count; i++)
            Assert.False(kebarrowList[i - 1].Ruler == kebarrowList[i].Ruler
                         && kebarrowList[i - 1].From == kebarrowList[i].From,
                $"the Kebarrow list double-counts a transfer: {List(view, kebarrow)}");
    }

    /// <summary>
    /// The Sworn Men of Meigate had no ruler in year 5, because the house did not exist.
    ///
    /// A hand-verified negative that a ruler list can only get wrong at its front end — the place
    /// a founding holder installed by a secession lives, and the source that was missed until
    /// round 8.
    /// </summary>
    [Fact]
    public void TheSwornMenOfMeigateHaveNoHolderBeforeTheirFounding()
    {
        WorldView view = V1;
        List<Held> derived = RecordFacts.SeatHistory(view, House(view, "Meigate").Id);

        Assert.NotEmpty(derived);
        Assert.Equal(19, derived[0].From);
        Assert.DoesNotContain(derived, h => h.From < 19);
    }
}
