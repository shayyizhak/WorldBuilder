using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The holdout reader, at the entry point <c>wb holdouts</c> calls.
///
/// <b>What it is for.</b> Ruleset 4 keeps six of thirteen scopes out of canon on seed 42 where v1
/// kept three of fifteen, and from one document that is indistinguishable between the checker
/// working harder on a harder world and one or two rules over-firing. The distribution across five
/// seeds, grouped by rule, is what separates them.
///
/// <b>The verdict is computed, not read off.</b> The arms were written before the figures were,
/// and they are evaluated in code so that which arm gets taken is not a matter of which row caught
/// the eye. Pre-registration constrains the analyst only if the analyst does not get to pick the
/// arm afterwards.
///
/// <b>Nothing here recomputes a sidecar.</b> Running today's rules over yesterday's prose gives the
/// same figure on both sides of the comparison, so a rule that has since gone quiet agrees with the
/// bug it exists to expose.
/// </summary>
public class HoldoutTests
{
    private static string Root()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            string candidate = Path.Combine(at.FullName, "baselines");
            if (Directory.Exists(Path.Combine(candidate, "ruleset-4"))) return candidate;
        }

        throw new DirectoryNotFoundException($"no baselines/ruleset-4 above {AppContext.BaseDirectory}");
    }

    private static Holdouts.Report Report() => Holdouts.Build(Root(), "ruleset-4", "ruleset-3");

    [Fact]
    public void EverySeedInThePanelHasASidecarAndEveryFatalFindingHasAScope()
    {
        // ForSeed throws on a missing sidecar rather than reporting an empty panel, and on a fatal
        // finding whose scope has no coverage block. Both would otherwise read as a seed with
        // nothing held out, which is the wrong answer rather than a missing one.
        foreach (ulong seed in Holdouts.Panel)
        {
            SeedHoldouts one = Holdouts.ForSeed(Root(), "ruleset-4", seed);

            Assert.NotEmpty(one.Scopes);
            Assert.All(one.Excluded, h => Assert.Contains(h.Scope, one.Scopes));
            Assert.All(one.Excluded, h => Assert.NotEmpty(h.Rules));
        }
    }

    /// <summary>
    /// An inert entry is attributed to the rule it names, not to a rule called "rule-inert".
    ///
    /// The whole sidecar carries one <c>rule-inert</c> row per silent rule per scope, and reading
    /// the kind rather than the span would pile every silence in the panel onto one imaginary rule
    /// — which would look like an overwhelmingly strong signal and be an artefact of the file
    /// format.
    /// </summary>
    [Fact]
    public void AnInertRowIsAttributedToTheRuleItNames()
    {
        SidecarFinding inert = new("rule-inert", "a scope", "partition-sum", "extracted nothing", false, false);
        Assert.Equal("partition-sum", inert.Rule);

        SidecarFinding real = new("wrong-year", "a scope", "43", "no such year", true, true);
        Assert.Equal(RuleNames.Of("wrong-year"), real.Rule);
        Assert.NotEqual("wrong-year", real.Rule);
    }

    /// <summary>
    /// The degeneracy guard outranks every other arm.
    ///
    /// A panel below ten holdouts cannot answer the grouping question however concentrated it
    /// looks, and a rule set that let a nine-holdout panel report "over-firing" would be reading a
    /// pattern out of single digits — which is the failure the guard was written for after a tight
    /// panel silently turned a rank criterion into a coin flip.
    /// </summary>
    [Fact]
    public void AGuardedPanelIsVoidWhateverElseTheFiguresSay()
    {
        Holdouts.Report thin = Report() with
        {
            Seeds = [new SeedHoldouts(42, ["a", "b"], [new HeldOut("a", ["date"], 1, 1)])],
            ByRule = new Dictionary<string, int>(StringComparer.Ordinal) { ["date"] = 1 },
        };

        Assert.Equal(1, thin.TotalHoldouts);
        Assert.Equal(HoldoutVerdict.Underpowered, thin.Verdict);
    }

    /// <summary>
    /// The panel as it stands, and the arm the pre-committed rules take on it.
    ///
    /// Pinned so the verdict cannot drift while nobody is looking. It is not a bar: a future
    /// baseline set that lands on a different arm is a finding, and it should arrive as a failure
    /// here with the figures in the message rather than as a quiet change of subject.
    /// </summary>
    [Fact]
    public void ThePanelAsItStandsFallsToTheMiddleArm()
    {
        Holdouts.Report report = Report();

        Assert.Equal(20, report.TotalHoldouts);
        Assert.Equal(60, report.TotalScopes);

        // Above the guard, so the question is live.
        Assert.True(report.TotalHoldouts >= 10);

        // Not over-firing: no rule carries 60% of the panel.
        (string rule, int _, int share) = report.Heaviest;
        Assert.Equal("action", rule);
        Assert.True(share < 60, $"{rule} carries {share}% of the panel");

        // Not "checker working" either: eight distinct rules is comfortably over the four the arm
        // asks for, and the per-seed rate spread is what fails it.
        Assert.Equal(8, report.ByRule.Count(static p => p.Value > 0));
        Assert.True(report.RateRange.Width > 20,
            $"per-seed rate {report.RateRange} is inside the arm's 20 points after all");

        Assert.Equal(HoldoutVerdict.Escalate, report.Verdict);
        Assert.True(report.Halts);
    }

    /// <summary>
    /// The spread figure says what kind of figure it is.
    ///
    /// The arm is stated in points of width and a bare 31 reads as a standard deviation to the
    /// next person who meets it. That confusion has already cost this project one verdict, and the
    /// fix is in the emitter rather than in anyone's discipline.
    /// </summary>
    [Fact]
    public void TheRateSpreadIsAnIntervalAndPrintsAsOne()
    {
        Dispersion spread = Report().RateRange;

        Assert.True(spread.IsInterval);
        Assert.Contains("range=[", spread.ToString(), StringComparison.Ordinal);
        Assert.Contains("width=", spread.ToString(), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => spread.Figure);
    }

    /// <summary>
    /// Rules are firing findings out of an extraction counter that never moved.
    ///
    /// <b>Recorded rather than repaired.</b> The floor invariant is
    /// <c>extracted &gt;= previous_extracted</c>, so a rule whose extraction is structurally zero
    /// carries a floor of zero and can stop firing forever without the golden layer noticing. That
    /// is the silent-path signature inside the mechanism built to detect it. Correcting an
    /// extraction counter raises a floor, and re-baselining a floor is an explicit human act, not
    /// something that happens by rerunning — so this test pins the state and escalates it.
    /// </summary>
    [Fact]
    public void SomeFindingsComeFromRulesWhoseExtractionCounterNeverMoved()
    {
        List<Holdouts.Unaccounted> rows = Holdouts.FiredWithoutExtraction(Root(), "ruleset-4", Holdouts.Panel);

        Assert.NotEmpty(rows);

        // And the contradiction is in the file, not merely implied: the same scope carries a
        // rule-inert row for a rule that raised a fatal finding there.
        Assert.Contains(rows, static r => r.AlsoReportedInert);

        // Every such row names the kinds that fired, so the escalation is diagnosable rather than
        // a count. A row with no kinds would mean the attribution itself had failed.
        Assert.All(rows, r => Assert.NotEmpty(r.Kinds));
    }

    /// <summary>
    /// The comparison set is read too, and reading it changes nothing on disk.
    ///
    /// A diff layer that could write to its reference is a diff layer that passes by moving what it
    /// is measured against.
    /// </summary>
    [Fact]
    public void ReadingTheSidecarsLeavesThemUnchanged()
    {
        Dictionary<string, string> before = Hashes();
        Holdouts.Render(Report());
        Assert.Equal(before, Hashes());

        static Dictionary<string, string> Hashes()
        {
            Dictionary<string, string> hashes = new(StringComparer.Ordinal);

            foreach (string set in (string[])["ruleset-3", "ruleset-4"])
                foreach (ulong seed in Holdouts.Panel)
                {
                    string path = Holdouts.SidecarPath(Root(), set, seed);
                    using FileStream stream = File.OpenRead(path);
                    hashes[path] = Convert.ToHexStringLower(
                        System.Security.Cryptography.SHA256.HashData(stream));
                }

            return hashes;
        }
    }
}
