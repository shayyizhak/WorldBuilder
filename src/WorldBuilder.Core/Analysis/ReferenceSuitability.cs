using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>One seed measured against the reference-seed criteria.</summary>
public sealed record Suitability(
    ulong Seed, int Exposed, int Seized, int WarArcs, int Reigns, int Powers, int Events)
{
    /// <summary>The record carries both branches the verification layers read.</summary>
    public bool R1 => Exposed > 0 && Seized > 0;

    /// <summary>The world fills the scopes <c>wb book</c> builds by default.</summary>
    public bool R2 => WarArcs >= 2 && Reigns >= 2 && Powers >= 2;

    /// <summary>Within ±35% of the measurement panel's median length.</summary>
    public bool R3 => Events >= ReferenceSuitability.ShortestUsefulWorld
                      && Events <= ReferenceSuitability.LongestUsefulWorld;

    public bool Suitable => R1 && R2 && R3;

    public string Line() => string.Create(CultureInfo.InvariantCulture,
        $"seed {Seed,-8} R1 coup branches {(R1 ? "ok " : "NO ")}(exposed {Exposed,2}, seized {Seized,2})  " +
        $"R2 scopes {(R2 ? "ok " : "NO ")}({WarArcs} wars, {Reigns} reigns, {Powers} powers)  " +
        $"R3 length {(R3 ? "ok " : "NO ")}({Events})  →  {(Suitable ? "SUITABLE" : "rejected")}");
}

/// <summary>
/// Whether a seed makes a usable reference world, against the criteria in
/// <c>docs/reference-seed-criteria.md</c> — which were written and committed before any candidate
/// was examined.
///
/// <b>The criteria are about the record, not about the world being pleasant.</b> A reference world
/// exists so a person can read prose against a record; it must therefore contain the constructions
/// the verification layers read and fill the scopes the book builds. It does not have to be a world
/// where the good houses win, and selecting for that is how a panel comes to be unable to show a
/// defect it has.
///
/// Two criteria the brief suggested are deliberately absent — no runaway before Y40, and two houses
/// standing at the end. Both are the brake problem, which is out of scope, and both fail on the
/// majority of ordinary worlds. Selecting on them would build a panel that cannot exhibit an unfixed
/// engine defect, in exactly the five worlds anyone reads.
/// </summary>
public static class ReferenceSuitability
{
    /// <summary>±35% of the measurement panel's median of 709 events.</summary>
    public const int ShortestUsefulWorld = 461;

    public const int LongestUsefulWorld = 957;

    public static Suitability Of(WorldView view)
    {
        int exposed = 0, seized = 0;
        HashSet<EntityId> powers = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind == EventKind.PolityCoupResolved)
            {
                if (e.GetString("mode") == "exposed") exposed++;
                if (e.GetString("mode") == "seized") seized++;
            }

            if (e.Significance < Significance.Major) continue;
            foreach (Participant p in e.Participants)
                if (p.Id.Kind == EntityKind.Faction) powers.Add(p.Id);
        }

        int wars = 0;
        foreach (Arc a in view.State.Arcs)
            if (a.Kind == ArcKind.War) wars++;

        // Reign spells rather than succession events: a contested transfer emits two records and
        // one hold, and counting records would say a world has more reigns to write about than it
        // has. SeatTransfers already settles that distinction and is reused rather than restated.
        int reigns = 0;
        foreach (Faction f in view.State.Factions)
            reigns += ReferenceSet.SeatHistory(view, f.Id).Count;

        return new Suitability(view.Seed, exposed, seized, wars, reigns, powers.Count, view.Log.Count);
    }
}
