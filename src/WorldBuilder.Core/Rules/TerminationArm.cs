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
        _ => throw new FormatException(
            $"unknown termination arm '{name}'. One of: all, none, war, collapse, disuse, " +
            "war+collapse, war+disuse, collapse+disuse."),
    };
}
