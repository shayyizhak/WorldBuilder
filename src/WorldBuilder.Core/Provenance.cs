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
    public const string Version = "3";
}
