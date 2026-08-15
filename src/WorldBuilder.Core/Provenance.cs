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
/// </summary>
public static class Ruleset
{
    public const string Version = "1.2.0";
}
