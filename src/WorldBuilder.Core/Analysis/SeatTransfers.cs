using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>One record that put somebody on a seat, before anything has been collapsed.</summary>
/// <param name="Faction">The seat.</param>
/// <param name="Ruler">Who the record put on it.</param>
/// <param name="Via">
/// Which record moved the seat. A contested transfer emits two, and telling them apart is the
/// whole of this file — so the kind is carried rather than reduced to "a seat moved".
/// </param>
public sealed record SeatMove(EntityId Faction, EntityId Ruler, int Year, EventId Id, EventKind Via);

/// <summary>What a repeated appearance on one seat turned out to be.</summary>
public enum SeatRepeatShape
{
    /// <summary>
    /// One transfer, two records: the challenge or coup that decided it, and the succession row
    /// beside it carrying the state change. Same seat, same person, same year. Collapses.
    /// </summary>
    ContestedTransfer = 1,

    /// <summary>
    /// The same person back on the same seat in a later year. Two holds, and collapsing them
    /// would delete a tenure from the ruler list. Does not collapse.
    /// </summary>
    SecondTenure = 2,

    /// <summary>
    /// Neither shape. Two records in one year that are not a decider and its succession — two
    /// successions, say, or two challenges. Nothing here invents a third rule for it: it is
    /// reported and escalated.
    /// </summary>
    Unclassified = 3,
}

/// <summary>A person appearing twice on one seat, and what the two records actually were.</summary>
public sealed record SeatRepeat(
    EntityId Faction,
    EntityId Ruler,
    SeatMove First,
    SeatMove Second,
    bool Adjacent,
    SeatRepeatShape Shape)
{
    /// <summary>True where the pair sits side by side in the seat order, so a collapse rule reaches it.</summary>
    public bool ReachedByCollapse => Adjacent;

    public string Describe(WorldState state) => string.Create(CultureInfo.InvariantCulture,
        $"{state.Label(Faction)}: {state.NameOf(Ruler)} " +
        $"{First.Year} ({EventKinds.Name(First.Via)} {First.Id}) and " +
        $"{Second.Year} ({EventKinds.Name(Second.Via)} {Second.Id}) — " +
        $"{Shape}{(Adjacent ? "" : ", not adjacent")}");
}

/// <summary>
/// What the seat-moving records say before a ruler list is made of them.
///
/// <b>Why this is separate from <see cref="ReferenceSet.SeatHistory"/>.</b> The ruler list is a
/// collapsed view, and the question here is about what got collapsed. A rule that folds two rows
/// into one is invisible from its own output: "no duplicate in the list" is satisfied both by
/// collapsing a contested transfer correctly and by deleting a second tenure, and those are
/// opposite errors. So the raw moves are enumerated once, here, and the collapse rule is
/// expressed against a classification rather than against a hunch about adjacency.
///
/// <b>The rule the derivation now follows: same person, same seat, <i>same year</i> collapses;
/// same person, same seat, different years does not.</b> The previous rule collapsed any two
/// adjacent appearances whatever their years, which is correct for every contested transfer and
/// wrong for a man who takes a seat back with nobody recorded in between.
/// </summary>
public static class SeatTransfers
{
    /// <summary>
    /// Every record that put somebody on a seat, in year order.
    ///
    /// Four sources. The third and fourth are the ones that get missed: a coup resolved in the
    /// challenger's favour moves the seat exactly as an open challenge does, and a secession names
    /// the parent house as its <c>Faction</c> with the new house as a bystander, which is the
    /// opposite of the obvious reading.
    /// </summary>
    public static List<SeatMove> Moves(WorldView view, EntityId faction)
    {
        List<SeatMove> moves = [];

        foreach (Event e in view.Log.Events)
        {
            switch (e.Kind)
            {
                case EventKind.PolitySuccession when e.Faction == faction:
                case EventKind.PolityChallenge when e.Faction == faction && e.Outcome == Outcome.Succeeded:
                case EventKind.PolityCoupResolved when e.Faction == faction && e.Outcome == Outcome.Succeeded:
                    if (!e.Subject.IsNone) moves.Add(new SeatMove(faction, e.Subject, e.Year, e.Id, e.Kind));
                    break;

                case EventKind.PolitySecession when Bystander(e) == faction:
                    if (!e.Subject.IsNone) moves.Add(new SeatMove(faction, e.Subject, e.Year, e.Id, e.Kind));
                    break;
            }
        }

        moves.Sort(static (a, b) => a.Year != b.Year
            ? a.Year.CompareTo(b.Year)
            : a.Id.Value.CompareTo(b.Id.Value));

        return moves;
    }

    /// <summary>Every seat in the world, so nothing is checked on a hand-picked faction.</summary>
    public static List<SeatRepeat> Repeats(WorldView view)
    {
        List<SeatRepeat> all = [];
        foreach (Faction f in view.State.Factions) all.AddRange(Repeats(view, f.Id));
        return all;
    }

    /// <summary>
    /// Every case where one person appears twice on one seat, classified.
    ///
    /// Every pair, not only the adjacent ones. Restricting this to what the collapse rule happens
    /// to reach would make the check agree with the rule by construction, and the rule is the thing
    /// under examination.
    /// </summary>
    public static List<SeatRepeat> Repeats(WorldView view, EntityId faction)
    {
        List<SeatMove> moves = Moves(view, faction);
        List<SeatRepeat> repeats = [];

        for (int i = 0; i < moves.Count; i++)
            for (int j = i + 1; j < moves.Count; j++)
            {
                if (moves[i].Ruler != moves[j].Ruler) continue;

                repeats.Add(new SeatRepeat(
                    faction, moves[i].Ruler, moves[i], moves[j],
                    Adjacent: j == i + 1,
                    Shape: Classify(moves[i], moves[j])));
            }

        return repeats;
    }

    /// <summary>
    /// A pair in one year is one transfer only if the two records are a decider and its succession.
    ///
    /// Two successions in one year, or two challenges, are not a contested transfer however much
    /// they look like one from the collapsed list — and the brief is explicit that a case fitting
    /// neither shape is escalated rather than given a rule of its own.
    /// </summary>
    private static SeatRepeatShape Classify(SeatMove first, SeatMove second)
    {
        if (first.Year != second.Year) return SeatRepeatShape.SecondTenure;

        bool decider = Decides(first.Via) ^ Decides(second.Via);
        bool succession = (first.Via == EventKind.PolitySuccession) ^ (second.Via == EventKind.PolitySuccession);

        return decider && succession ? SeatRepeatShape.ContestedTransfer : SeatRepeatShape.Unclassified;
    }

    /// <summary>The record that settled a contest, as opposed to the one that recorded the result.</summary>
    private static bool Decides(EventKind kind) =>
        kind is EventKind.PolityChallenge or EventKind.PolityCoupResolved;

    /// <summary>
    /// Every contested transfer in a record: seat, year, and both record ids.
    ///
    /// The enumeration a hand-verified ruler list is intersected against. A list that crossed one
    /// of these and counted both records has the same man on the same seat twice; a list that
    /// crossed one and is right is worth recording as checked, because "no crossing found" and
    /// "crossings found and all agreed" are different states and only one of them is evidence.
    /// </summary>
    public static List<SeatRepeat> Contested(WorldView view) =>
        [.. Repeats(view).Where(static r => r.Shape == SeatRepeatShape.ContestedTransfer)];

    private static EntityId Bystander(Event e)
    {
        foreach (Participant p in e.Participants)
            if (p.Role == Role.Bystander && p.Id.Kind == EntityKind.Faction) return p.Id;

        return EntityId.None;
    }
}
