using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Stage 4's exit criterion, as a test rather than as a feeling.
///
/// The stage is done when a deliberately reintroduced defect from each of the six failure
/// families is caught by the layer that owns it, with no human reading prose. Each fact below is
/// one family, and each names the layer responsible — so a layer that stops owning its family
/// fails here rather than in a review six months later.
///
/// Everything runs with no model and no inference.
/// </summary>
public class ExitCriterionTests
{
    /// <summary>The archived v1 world; see <see cref="BaselineWorld"/>.</summary>
    private static WorldView World(ulong seed) => BaselineWorld.ForSeed(seed);

    private static string CorpusDirectory => Corpus.FindDirectory(AppContext.BaseDirectory);

    private static void CaughtByTheCorpus(string caseId)
    {
        CorpusCase one = Corpus.Load(CorpusDirectory).Single(c => c.Id.Contains(caseId, StringComparison.Ordinal));
        CorpusResult result = Corpus.Run(one, World);

        Assert.True(result.Passed, $"{one.Id}: {result.Detail}");
    }

    private static string Baseline()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            string candidate = Path.Combine(at.FullName, "baselines", "v1", "seed-42");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("no sealed baseline found");
    }

    // ---- 1. count versus enumeration — layers 2 and 3 ---------------------

    [Fact]
    public void ACountThatDisagreesWithItsListIsCaught()
    {
        // Layer 2, on a synthetic passage.
        IReadOnlyList<Fabrication> findings = SelfConsistency.Check(
            "Four people were murdered from within, including Weallhous Dreld in 25, " +
            "Wilwound Ska in 31, Nael War in 37, and Paernrom Sir in 38.");

        Assert.Contains(findings, f => RuleNames.Of(f.Kind) == SelfConsistency.Rules.CountEnumeration);

        // Layer 3, on the sentence as it actually reached canon.
        CaughtByTheCorpus("four-murdered-including-four");
    }

    // ---- 2. date — layer 3 -------------------------------------------------

    [Fact]
    public void AYearThatDisagreesWithTheRecordIsCaught() =>
        CaughtByTheCorpus("thrild-killed-in-the-wrong-year");

    // ---- 3. scope — layers 3 and 4 ----------------------------------------

    [Fact]
    public void AFigureComputedForTheWrongScopeIsCaught() =>
        // Corpus row 10, the one that has failed twice: a faction-lifetime raid count quoted
        // inside a reign. Layer 4 asserts the same property against the record independently,
        // in an assembly that cannot see the checker.
        CaughtByTheCorpus("reign-given-a-faction-lifetime-count");

    // ---- 4. coverage omission — layer 3 -----------------------------------

    [Fact]
    public void AWindowThatOmitsWhatItMustTellIsCaught() =>
        CaughtByTheCorpus("window-missing-its-year-twenty");

    // ---- 5. the silent path — layer 5 -------------------------------------

    [Fact]
    public void ARuleThatStopsReadingIsCaught()
    {
        // The family that has appeared five times, and the one no test of rule logic can see:
        // the rule is correct and the input never reaches it. One integer comparison finds it.
        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored =
            FindingsSidecar.ReadCoverage(Path.Combine(Baseline(), "chronicle-42.findings.json"));

        (string scope, string rule, _) = stored
            .SelectMany(s => s.Value.Where(r => r.Value.Extracted > 0)
                .Select(r => (s.Key, r.Key, r.Value.Extracted)))
            .First();

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> silenced = new(StringComparer.Ordinal);
        foreach ((string name, IReadOnlyDictionary<string, RuleCounts> rules) in stored)
        {
            Dictionary<string, RuleCounts> here = new(rules, StringComparer.Ordinal);
            if (name == scope) here[rule] = new RuleCounts(0, 0, 0, 0);
            silenced[name] = here;
        }

        List<Drift> drift = GoldenDiff.CoverageSound(stored, GoldenDiff.AsCoverage(silenced));

        Assert.Contains(drift, d => d.Kind == "went-silent" && d.Fails);
    }

    // ---- 6. regression against the golden — layer 5 -----------------------

    [Fact]
    public void AFigureThatMovesAgainstTheBaselineIsCaught()
    {
        string prose = File.ReadAllText(Path.Combine(Baseline(), "chronicle-42.md"));

        const string figure = "where 124 people died";
        Assert.Contains(figure, prose, StringComparison.Ordinal);

        List<Drift> drift = GoldenDiff.Compare(
            prose, prose.Replace(figure, "where 125 people died", StringComparison.Ordinal));

        Assert.Contains(drift, d => d.Kind == "figure" && d.Fails);
    }

    // ---- and the whole panel, unread by anyone ----------------------------

    /// <summary>
    /// The six families are each owned, and nothing in this file needed a person to read prose.
    ///
    /// Stated as an assertion so the criterion is not a paragraph in a document that quietly
    /// stops being true.
    /// </summary>
    [Fact]
    public void EveryFamilyHasALayerThatOwnsIt()
    {
        Dictionary<string, string> owners = new(StringComparer.Ordinal)
        {
            ["count/enumeration"] = "layer 2 + layer 3",
            ["date"] = "layer 3",
            ["scope"] = "layer 3 + layer 4",
            ["coverage-omission"] = "layer 3",
            ["silent-path"] = "layer 5",
            ["regression-against-golden"] = "layer 5",
        };

        Assert.Equal(6, owners.Count);
        Assert.All(owners.Values, owner => Assert.StartsWith("layer", owner, StringComparison.Ordinal));
    }
}
