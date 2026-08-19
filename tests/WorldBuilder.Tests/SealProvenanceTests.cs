using System.Globalization;
using System.Text.Json;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// A seal's commit contains the ruleset it seals.
///
/// <b>The one thing naming a commit is for.</b> A baseline manifest records an engine commit so that
/// a reader can go to that commit, build, and regenerate the world it seals. A manifest claiming
/// ruleset 8 whose commit contains ruleset 6 names a build that would produce a different history —
/// and nothing inside the artefact can tell: the seal verifies, every artefact hash matches, and the
/// property the seal exists to carry is the only one that does not hold.
///
/// <b>It happened three times before anything checked.</b> Always the same way: bump the ruleset in
/// the working tree, cut the baselines, commit afterwards — and the build stamps HEAD, which is the
/// commit *before* the bump. Rulesets 5 and 7 exist in no commit of this repository at all as a
/// result, so those two sets can never be re-cut correctly; the code that produced them was never
/// stored. They are named below as permanent exceptions, with that reason, and nothing else may join
/// them: <see cref="BaselineArchive.Cut"/> now refuses, so a fourth instance cannot be created.
/// </summary>
public class SealProvenanceTests
{
    /// <summary>
    /// Sets whose commit does not contain their ruleset, and cannot be made to.
    ///
    /// <b>Not a tolerance — a record of two unrepairable sets.</b> Neither ruleset 5 nor ruleset 7
    /// was ever committed, so there is no commit either set could name that would satisfy this. The
    /// list is asserted to be exactly these two, so it cannot grow quietly, and the cut-time refusal
    /// is what makes that assertion hold rather than hope.
    /// </summary>
    private static readonly Dictionary<string, string> Unrepairable = new(StringComparer.Ordinal)
    {
        ["ruleset-5"] = "ruleset 5 was never committed; no commit contains it",
        ["ruleset-7"] = "ruleset 7 was never committed; no commit contains it",
    };

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? at = new(Directory.GetCurrentDirectory()); at is not null; at = at.Parent)
            if (Directory.Exists(Path.Combine(at.FullName, ".git"))) return at.FullName;

        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
            if (Directory.Exists(Path.Combine(at.FullName, ".git"))) return at.FullName;

        throw new DirectoryNotFoundException("no repository root above the test's working directory");
    }

    private sealed record Sealed(string Set, string Seed, string Ruleset, string Commit);

    /// <summary>
    /// Every manifest under <c>baselines/</c> that names both a ruleset and a commit.
    ///
    /// Discovered rather than listed, so a set added later is covered without anybody remembering to
    /// add it here — which is the failure mode a hand-written list of sets would have.
    /// </summary>
    private static List<Sealed> Manifests(string root)
    {
        List<Sealed> found = [];

        string baselines = Path.Combine(root, "baselines");
        if (!Directory.Exists(baselines)) return found;

        foreach (string path in Directory.EnumerateFiles(baselines, "manifest.json",
                     SearchOption.AllDirectories))
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement at = doc.RootElement;

            string ruleset = at.TryGetProperty("ruleset_version", out JsonElement r)
                ? r.GetString() ?? "" : "";
            string commit = at.TryGetProperty("engine_commit", out JsonElement c)
                ? c.GetString() ?? "" : "";

            // The v1 baseline predates the ruleset counter entirely. A manifest that makes no claim
            // cannot make a false one, so it is out of scope rather than an exception.
            if (ruleset.Length == 0 || commit.Length == 0) continue;

            string seedDir = Path.GetDirectoryName(path)!;
            string setDir = Path.GetDirectoryName(seedDir)!;

            found.Add(new Sealed(Path.GetFileName(setDir), Path.GetFileName(seedDir), ruleset, commit));
        }

        return found;
    }

    [Fact]
    public void EverySealsCommitContainsTheRulesetItSeals()
    {
        string root = RepositoryRoot();
        List<Sealed> manifests = Manifests(root);

        // Non-vacuous: this passes trivially over an empty baselines tree, which is exactly what a
        // check that cannot fire looks like.
        Assert.True(manifests.Count >= 10,
            $"only {manifests.Count.ToString(CultureInfo.InvariantCulture)} sealed manifest(s) found; " +
            "the check is not covering the archive");

        List<string> wrong = [];
        int verified = 0;

        foreach (Sealed s in manifests)
        {
            string? actual = BaselineArchive.RulesetAt(root, s.Commit);

            // A commit git cannot reach is a different failure and is reported as one. It is not
            // treated as a pass: a set whose commit has gone is a set nobody can regenerate either.
            if (actual is null)
            {
                wrong.Add($"{s.Set}/{s.Seed}: commit {s.Commit[..12]} cannot be read, so its claim " +
                          $"to ruleset {s.Ruleset} cannot be checked");
                continue;
            }

            if (string.Equals(actual, s.Ruleset, StringComparison.Ordinal)) { verified++; continue; }

            if (Unrepairable.ContainsKey(s.Set)) continue;

            wrong.Add($"{s.Set}/{s.Seed}: manifest claims ruleset {s.Ruleset}, but commit " +
                      $"{s.Commit[..12]} contains ruleset {actual}. The seal names a commit the " +
                      "world cannot be regenerated from — commit the ruleset, rebuild, re-run and " +
                      "re-cut.");
        }

        Assert.True(wrong.Count == 0, string.Join("\n", wrong));

        // And something was actually verified, so a run in which every set fell into the exception
        // list would fail rather than read as clean.
        Assert.True(verified >= 5,
            $"only {verified.ToString(CultureInfo.InvariantCulture)} manifest(s) had their claim " +
            "confirmed; the rest were exceptions, which is not a passing check");
    }

    /// <summary>
    /// The exception list is exactly the two sets that cannot be repaired, and both are still there.
    ///
    /// <b>Both directions matter.</b> A set added to the list quietly is the check being widened to
    /// fit a new failure — which is how a guard stops guarding. A set removed from it means those
    /// baselines were re-cut against a commit that does contain their ruleset, which cannot happen
    /// without that ruleset being committed, and would be worth knowing.
    /// </summary>
    [Fact]
    public void TheUnrepairableSetsAreTheTwoRulesetsThatWereNeverCommitted()
    {
        string root = RepositoryRoot();

        Assert.Equal(["ruleset-5", "ruleset-7"], Unrepairable.Keys.OrderBy(static k => k, StringComparer.Ordinal));

        // The reason each is on the list, restated as a check: no commit in this repository's history
        // carries that ruleset. If one ever does, the set can be re-cut and the exception removed.
        foreach (string set in Unrepairable.Keys)
        {
            string version = set["ruleset-".Length..];

            Assert.DoesNotContain(Commits(root), c =>
                string.Equals(BaselineArchive.RulesetAt(root, c), version, StringComparison.Ordinal));
        }
    }

    /// <summary>Commit ids on the current branch, newest first, bounded so the walk stays cheap.</summary>
    private static List<string> Commits(string root)
    {
        System.Diagnostics.ProcessStartInfo start =
            new("git", ["log", "--format=%H", "-40"]) { WorkingDirectory = root, RedirectStandardOutput = true };

        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        List<string> commits = [];

        while (process.StandardOutput.ReadLine() is { } line)
            if (line.Trim().Length > 0) commits.Add(line.Trim());

        process.WaitForExit();
        return commits;
    }
}
