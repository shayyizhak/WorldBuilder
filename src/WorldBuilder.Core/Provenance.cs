using System.Reflection;

namespace WorldBuilder.Core;

/// <summary>
/// Who produced a world, read from the build rather than declared in code.
///
/// The version is a real assembly attribute, so it cannot drift from what was actually built —
/// a figure restated in a source constant is a figure that goes stale silently, and this project
/// has already been bitten by exactly that with a rule count written down in a reference
/// document. <c>InformationalVersion</c> carries the commit as a <c>+sha</c> suffix, so the two
/// travel together and neither can be recorded without the other.
/// </summary>
public static class Engine
{
    private static readonly (string Version, string Commit) Build = Read();

    /// <summary>The engine's version, e.g. <c>1.2.0</c>. Empty only if the build carried none.</summary>
    public static string Version => Build.Version;

    /// <summary>The commit the engine was built from, or empty where the build had no source metadata.</summary>
    public static string Commit => Build.Commit;

    private static (string, string) Read()
    {
        string? informational = typeof(Engine).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational)) return ("", "");

        int plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus < 0
            ? (informational, "")
            : (informational[..plus], informational[(plus + 1)..]);
    }
}

/// <summary>
/// The version of the simulation's rules.
///
/// Distinct from the engine version, and the distinction is the point. The engine version says
/// which build wrote a file. The ruleset version says whether this build, given the same seed,
/// would produce the same world — and once a rule changes it would not.
///
/// That is why a materialised event log is the durable artefact and a seed is only provenance.
/// A world whose ruleset version does not match this build is still perfectly readable; what it
/// has lost is the ability to be regenerated from its seed. Those are different failures and a
/// reader has to be able to tell them apart.
///
/// Bump this whenever a change alters what the simulation would produce for an unchanged seed.
///
/// <b>The decision, recorded because it is about to matter.</b> The ruleset and the engine are
/// two things that currently coincide, not one thing with two names. They were both "1.2.0",
/// which reads as a constraint and is not one: the coup work ahead is a rule change that will
/// ship without an engine release, and the first time that happens a shared number becomes a
/// lie in a header that exists to be trusted.
///
/// So the ruleset gets its own sequence, starting at "1" and deliberately not matching any
/// engine version, so nobody can mistake the two for the same counter again. It is an integer
/// that increments when the rules change what the simulation produces; it says nothing about
/// what the engine can read, which is what <see cref="Engine.Version"/> is for.
/// </summary>
public static class Ruleset
{
    /// <summary>
    /// <b>2</b> — the coup resolution round. The first time this counter did any work, and the
    /// reason it was separated from the engine version before it was needed.
    ///
    /// Ruleset 1 modelled a conspiracy as a vendetta against a person, so an unrelated murder
    /// voided it, and had no branch in which a plotter could win. Ruleset 2 attaches the plot to
    /// the seat and gives the leak roll a third outcome. Seed 42 renders a different world under
    /// it, which is correct and expected: the engine version did not move, because nothing about
    /// what this build can read changed.
    /// </summary>
    /// <summary>
    /// <b>3</b> — the raid mechanic. A raid now weighs house against house rather than one man's
    /// Martial trait against a whole faction plus an undocumented 25, and its target selection
    /// counts what happened at a place rather than merely that something did.
    /// </summary>
    /// <summary>
    /// <b>4</b> — geography. The world is placed on an imported board, and four mechanics consult
    /// how far apart things are: raid targeting, war declaration, conquest and the pairing rules.
    ///
    /// Bumped once for the whole of Stage 6 rather than per mechanic, because the counter answers
    /// one question — would this build, given the same seed, produce the same world — and the
    /// answer became "no" the first time a place acquired a position. Bumping it four more times
    /// would record four answers to a question that had already been answered.
    ///
    /// The board is not in the log and cannot be, so a ruleset-4 world is a log <i>and</i> its
    /// board. That is what the bundle is for, and it is why the header carries a hash for the
    /// map: a world read beside the wrong one is internally consistent and about somewhere else.
    /// </summary>
    /// <summary>
    /// <b>5</b> — <c>DIPLO.ALLIANCE_BROKEN</c> is recorded. <b>An additive record change with no
    /// simulation change</b>, and the only bump of this counter so far that is.
    ///
    /// The alliance was already being destroyed: a war declaration has carried two
    /// <c>RelDel</c> keys since alliances existed, so the tie died inside the war's payload and no
    /// line of the log or the prose said so. Fifteen of the panel's twenty-four declarations sever
    /// a live pact. The event is emitted beside the declaration and carries no state delta, no
    /// draw from the stream and no arc.
    ///
    /// So the counter answers its usual question — would this build, given the same seed, produce
    /// the same world — with a "yes" it has never been able to give before, and moves anyway,
    /// because the *file* is different and a reader must be able to tell. What makes the claim
    /// more than an assertion is <c>AdditiveRecordTests</c>: every event of every sealed ruleset-4
    /// baseline still appears, in order, with its payload and its causal edges intact, and every
    /// insertion is this one kind. Ids and keys renumber, because both are derived from position
    /// in the log; nothing else moves.
    ///
    /// Worth being able to point at later. A ruleset bump normally means the histories are now
    /// different histories and every measurement against the old ones is void. This one does not,
    /// and the distinction is only durable if it is written down where the counter is.
    /// </summary>
    /// <summary>
    /// <b>6</b> — relations become terminable. Mechanics change, worlds change, and this is the
    /// ordinary kind of bump that ruleset 5 deliberately was not.
    ///
    /// Three rules end a tie where none did before. A declaration of war closes the border the
    /// two houses were moving goods across, definitionally and without a constant. A trade tie
    /// nothing has moved for twenty years is abandoned. A house that collapses takes its
    /// obligations with it — alliance, trade, vassalage, war — inside its own collapse event,
    /// which now carries the count and the kinds rather than dropping a dozen edges in silence.
    /// Memory is left alone: a grudge against a house that no longer exists is exactly what a
    /// world with a memory is supposed to keep.
    ///
    /// All three go through <c>RelationEnds</c>, which is the point rather than tidiness. Whatever
    /// ends a trade tie is the mechanism that ends any tie, and a per-kind mechanism means writing
    /// this again under a different name for every kind the monotonic sweep turns up.
    ///
    /// <b>The one constant is twenty years, and its argument predates every run that read it</b> —
    /// `docs/brief-step-two-design.md`, committed before the first ruleset-6 world existed. It is
    /// a timeout rather than a decay, and that is not a detail: decaying the edge value would have
    /// moved what `ProposeAlliance` scores against in every year of every world, which is a diffuse
    /// behavioural change riding inside a step whose subject is whether a tie exists at all.
    /// </summary>
    public const string Version = "6";
}
