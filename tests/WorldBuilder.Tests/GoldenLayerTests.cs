using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Layer 5, against the sealed baseline.
///
/// The layer aimed at the largest single category of defect: a figure that moves when the log
/// has not, and a rule that stops reading what it used to read. Both are integer comparisons and
/// neither needs a model or a person reading prose.
///
/// The baseline is read-only here. A diff layer that could update its own reference is a diff
/// layer that passes by moving the thing it is measured against, so these tests assert that too.
/// </summary>
public class GoldenLayerTests
{
    private static string Baseline()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            string candidate = Path.Combine(at.FullName, "baselines", "v1", "seed-42");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException($"no baselines/v1/seed-42 above {AppContext.BaseDirectory}");
    }

    private static Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> Anchor() =>
        FindingsSidecar.ReadCoverage(Path.Combine(Baseline(), "chronicle-42.findings.json"));

    [Fact]
    public void TheAnchorIsReadableAndCarriesEveryScope()
    {
        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored = Anchor();

        Assert.Equal(15, stored.Count);
        Assert.All(stored.Values, rules => Assert.Equal(16, rules.Count));
    }

    [Fact]
    public void TheBaselineAgreesWithItself()
    {
        // The diff run against an unchanged baseline must be silent. A layer that reports drift
        // against its own reference is a layer nobody will read the output of.
        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored = Anchor();

        List<Drift> drift = GoldenDiff.CoverageSound(stored, GoldenDiff.AsCoverage(stored));

        Assert.DoesNotContain(drift, d => d.Fails);
    }

    [Fact]
    public void ARuleThatGoesToZeroInOneScopeIsFailedOutright()
    {
        // The exact signature of the silent-path family, and the one comparison that would have
        // caught departure going 4 to 0.
        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored = Anchor();

        (string scope, string rule, int was) = stored
            .SelectMany(s => s.Value.Where(r => r.Value.Extracted > 0)
                .Select(r => (s.Key, r.Key, r.Value.Extracted)))
            .First();

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> silenced = Silence(stored, scope, rule);

        List<Drift> drift = GoldenDiff.CoverageSound(stored, GoldenDiff.AsCoverage(silenced));

        Drift went = Assert.Single(drift, d => d.Kind == "went-silent" && d.Section == scope);
        Assert.True(went.Fails);
        Assert.Contains(rule, went.Detail, StringComparison.Ordinal);
        Assert.Contains(was.ToString(System.Globalization.CultureInfo.InvariantCulture), went.Detail,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ASharpDropShortOfZeroIsAlsoFailed()
    {
        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored = Anchor();

        (string scope, string rule, int was) = stored
            .SelectMany(s => s.Value.Where(r => r.Value.Extracted > 4)
                .Select(r => (s.Key, r.Key, r.Value.Extracted)))
            .First();

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> dropped =
            Replace(stored, scope, rule, new RuleCounts(was / 2, was / 2, 0, 0));

        List<Drift> drift = GoldenDiff.CoverageSound(stored, GoldenDiff.AsCoverage(dropped));

        Assert.Contains(drift, d => d.Kind == "floor" && d.Section == scope && d.Fails);
    }

    [Fact]
    public void AFigureThatMovesIsFailed()
    {
        string prose = File.ReadAllText(Path.Combine(Baseline(), "chronicle-42.md"));

        // A real figure from a real section, so the perturbation cannot silently do nothing —
        // an earlier version of this check edited a string the document did not contain and
        // reported a pass, which is the failure this whole suite is about.
        const string figure = "where 124 people died";
        Assert.Contains(figure, prose, StringComparison.Ordinal);

        string moved = prose.Replace(figure, "where 125 people died", StringComparison.Ordinal);
        Assert.NotEqual(prose, moved);

        List<Drift> drift = GoldenDiff.Compare(prose, moved);

        Assert.Contains(drift, d => d.Kind == "figure" && d.Fails && d.Detail.Contains("124", StringComparison.Ordinal));
        Assert.Contains(drift, d => d.Kind == "figure" && d.Fails && d.Detail.Contains("125", StringComparison.Ordinal));
    }

    [Fact]
    public void ProseThatMovesIsReportedAndNotFailed()
    {
        // Renders legitimately vary. A layer that failed on wording would be turned off.
        string prose = File.ReadAllText(Path.Combine(Baseline(), "chronicle-42.md"));
        string reworded = prose.Replace("The fighting continued", "Fighting carried on", StringComparison.Ordinal);

        Assert.NotEqual(prose, reworded);

        List<Drift> drift = GoldenDiff.Compare(prose, reworded);

        Assert.Contains(drift, d => d.Kind == "prose");
        Assert.DoesNotContain(drift, d => d.Fails);
    }

    [Fact]
    public void TheBaselineIsUnchangedByBeingDiffedAgainst()
    {
        string dir = Baseline();
        Dictionary<string, string> before = Hashes(dir);

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored = Anchor();
        GoldenDiff.CoverageSound(stored, GoldenDiff.AsCoverage(stored));
        GoldenDiff.Compare(
            File.ReadAllText(Path.Combine(dir, "chronicle-42.md")),
            File.ReadAllText(Path.Combine(dir, "chronicle-42.md")));

        Assert.Equal(before, Hashes(dir));
    }

    [Fact]
    public void TheSealStillVerifies()
    {
        string dir = Baseline();
        string sealedHash = File.ReadAllText(Path.Combine(dir, ".sealed")).Trim();

        Assert.Equal(sealedHash, Sha256(Path.Combine(dir, "manifest.json")));
    }

    // ---- helpers ----------------------------------------------------------

    private static Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> Silence(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored, string scope, string rule) =>
        Replace(stored, scope, rule, new RuleCounts(0, 0, 0, 0));

    private static Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> Replace(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored,
        string scope, string rule, RuleCounts with)
    {
        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> copy = new(StringComparer.Ordinal);

        foreach ((string name, IReadOnlyDictionary<string, RuleCounts> rules) in stored)
        {
            Dictionary<string, RuleCounts> here = new(rules, StringComparer.Ordinal);
            if (name == scope) here[rule] = with;
            copy[name] = here;
        }

        return copy;
    }

    private static Dictionary<string, string> Hashes(string dir)
    {
        Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(dir))
            hashes[Path.GetFileName(file)] = Sha256(file);
        return hashes;
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
    }
}
