namespace WorldBuilder.Core;

/// <summary>
/// The five worlds a person reads against their record, and the five a sealed set already holds.
///
/// <b>Two lists, because they are two things.</b> The live panel is what gets cut, verified and
/// asserted against from now on. A sealed baseline set is a record of what was verified *then*, and
/// its seeds do not change when the live panel does — so every check that reads ruleset-3, -4 or -5
/// off disk keeps reading the seeds those directories contain. Collapsing the two would either
/// break every historical comparison or quietly freeze the live panel forever.
///
/// <b>Why this type exists at all.</b> The panel was written out by hand in twenty places — every
/// test file, six CLI defaults, and <c>Holdouts</c> — which is the same shape as the five re-fold
/// sites that each looked up the repository's stored board instead of the world's own. Five call
/// sites can drift apart; one source cannot. Changing a reference seed was a twenty-file edit and
/// is now a one-line one, and that is the difference between a panel that can be reconsidered and
/// a panel that is permanent by accident.
/// </summary>
public static class ReferencePanel
{
    /// <summary>
    /// The live reference panel.
    ///
    /// <b>Seed 99 was replaced by seed 1 at ruleset 6.</b> Screened against
    /// <c>docs/reference-seed-criteria.md</c>, whose criteria and search rule were committed before
    /// any candidate world was examined. Seed 99 failed R1: at ruleset 6 it never has a conspiracy
    /// uncovered — 0 <c>exposed</c> against 8 <c>seized</c> — so it could not support the
    /// per-seed assertion it was in the panel to support. Seed 1 is what the search rule returned,
    /// which is the lowest seed satisfying every criterion.
    /// </summary>
    public static readonly ulong[] Current = [1, 7, 42, 1234, 2025];

    /// <summary>
    /// The seeds the sealed ruleset-3, ruleset-4 and ruleset-5 sets contain.
    ///
    /// Read by anything that compares against those directories. It is not "the old panel" in a
    /// deprecated sense — it is the correct answer to "which worlds does that seal hold", and it
    /// stays correct however often the live panel is reconsidered.
    /// </summary>
    public static readonly ulong[] Sealed = [7, 42, 99, 1234, 2025];

    /// <summary>The live panel as the CLI's <c>--seeds</c> default.</summary>
    public static string CurrentText => string.Join(",", Current);
}
