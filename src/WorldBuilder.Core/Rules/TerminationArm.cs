namespace WorldBuilder.Core.Rules;

/// <summary>
/// Which of the termination rules a diagnostic run is allowed to use.
///
/// <b>Why this exists.</b> Ruleset 6 landed three rules at once — war closes a border, a collapsing
/// house drops its obligations, a tie nobody has used in twenty years lapses — and seed 99 lost a
/// quarter of its history, its `distinct deep-chain shapes` fell 69 → 45 and a coup branch stopped
/// being reached. Three changes and one effect is not a finding; it is three candidate findings
/// wearing one coat. This is the switch that takes them apart.
///
/// <b>An arm world is a diagnostic artefact, not a history.</b> Same standing as a proximity
/// control and marked the same way: named in the world header and in the genesis event, and
/// refused by <c>wb baseline cut</c>. A world that ran under a subset of its own ruleset is
/// internally consistent and about nowhere.
///
/// <b>The validity check is built in.</b> Disuse fires zero times on seed 99, so the
/// <see cref="Disuse"/> arm must reproduce the ruleset-5 log exactly. If it does not, the switch
/// itself moved the world and every other arm's figure is void — the same reasoning as the
/// identity proximity control, which exists to prove the substitution machinery consumes nothing.
/// </summary>
[Flags]
public enum TerminationArm
{
    /// <summary>No relation ends. Reproduces ruleset 5.</summary>
    None = 0,

    /// <summary>A declaration of war ends the belligerents' trade tie.</summary>
    War = 1,

    /// <summary>A collapsing house takes its obligations with it.</summary>
    Collapse = 2,

    /// <summary>A trade tie nothing has moved for twenty years lapses.</summary>
    Disuse = 4,

    /// <summary>
    /// Trade ties removed at random on a supplied schedule. <b>Not a rule</b> — a synthetic
    /// treatment, and the discriminating arm of the war-rule experiment.
    ///
    /// Without it, any war-versus-null effect is confounded with "ties came down": a world that
    /// is knife-edge sensitive to losing trade ties at all would produce exactly the same
    /// contrast, and the fix for that is a world-design problem rather than a rule defect. The
    /// arm removes the same number of ties in the same years as the war arm did, chosen
    /// uniformly at random on <see cref="RngPurpose.Control"/>, which no rule may draw on.
    ///
    /// Requires <see cref="Simulation.RandomTies"/> to be set, and throws if it is not: an arm
    /// that silently removed nothing would report the collapse arm's figures under the random
    /// arm's name.
    /// </summary>
    RandomTrade = 8,

    /// <summary>Ruleset 6 as shipped.</summary>
    All = War | Collapse | Disuse,
}

public static class TerminationArms
{
    /// <summary>The name carried in the world header, and the empty string for a real world.</summary>
    public static string NameOf(TerminationArm arm) => arm switch
    {
        TerminationArm.All => "",
        TerminationArm.None => "terminate-none",
        TerminationArm.War => "terminate-war",
        TerminationArm.Collapse => "terminate-collapse",
        TerminationArm.Disuse => "terminate-disuse",
        _ => "terminate-" + arm.ToString().Replace(", ", "+").ToLowerInvariant(),
    };

    public static TerminationArm Parse(string name) => name.Trim().ToLowerInvariant() switch
    {
        "" or "all" => TerminationArm.All,
        "none" => TerminationArm.None,
        "war" => TerminationArm.War,
        "collapse" => TerminationArm.Collapse,
        "disuse" => TerminationArm.Disuse,
        "war+collapse" => TerminationArm.War | TerminationArm.Collapse,
        "war+disuse" => TerminationArm.War | TerminationArm.Disuse,
        "collapse+disuse" => TerminationArm.Collapse | TerminationArm.Disuse,
        "random" => TerminationArm.Collapse | TerminationArm.RandomTrade,
        _ => throw new FormatException(
            $"unknown termination arm '{name}'. One of: all, none, war, collapse, disuse, " +
            "random, war+collapse, war+disuse, collapse+disuse."),
    };
}

/// <summary>
/// The schedule the random arm removes trade ties on, and the account of whether it managed to.
///
/// <b>Matched per world, not on average.</b> A random arm that removed a different number of ties
/// from the war arm, or removed them in different years, is measuring a different treatment and
/// the contrast between them means nothing. The schedule is therefore taken from the war arm's own
/// run on the same seed and board — one entry per removal, carrying the year it happened in.
///
/// <b>A miss is a halt, not a rounding error.</b> If a year comes up with no live trade tie to
/// remove, the arms are no longer matched, and the honest thing is for the run to say so rather
/// than to quietly remove fewer.
/// </summary>
public sealed class RandomTieSchedule(IReadOnlyList<int> years)
{
    private readonly Dictionary<int, int> _due = Build(years);

    /// <summary>One entry per scheduled removal, carrying the year it is due in.</summary>
    public IReadOnlyList<int> Years { get; } = [.. years];

    public int Removed { get; private set; }

    /// <summary>Scheduled removals that found no live tie to take. Must be zero.</summary>
    public int Missed { get; private set; }

    /// <summary>Whether this arm actually delivered the treatment it was supposed to.</summary>
    public bool Matched => Missed == 0 && Removed == Years.Count;

    public int DueIn(int year) => _due.GetValueOrDefault(year);

    public void Note(bool removed)
    {
        if (removed) Removed++;
        else Missed++;
    }

    private static Dictionary<int, int> Build(IReadOnlyList<int> years)
    {
        Dictionary<int, int> due = [];
        foreach (int y in years) due[y] = due.GetValueOrDefault(y) + 1;
        return due;
    }
}
