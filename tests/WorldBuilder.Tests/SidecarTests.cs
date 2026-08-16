using System.Text.Json;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Stage 4, step 1: the query path's findings sidecar.
///
/// Layer 5's value is diffing the coverage block rather than the prose, and until now the answer
/// path had no block to diff. <c>departure</c> extraction went 4 → 0 between two v1.2 rounds and
/// nothing caught it, on precisely this path.
///
/// Zero inference throughout, like the whole of Stage 4: the suite runs against a scripted client
/// through the same functions the CLI calls.
/// </summary>
public class SidecarTests
{
    /// <summary>The archived v1 world: these fixtures assert facts about that world, not about
    /// whatever the current ruleset produces. See <see cref="BaselineWorld"/>.</summary>
    private static WorldView World(ulong seed = 42) => BaselineWorld.ForSeed(seed);

    /// <summary>Plans as instructed, then answers with fixed prose. Planning calls carry a schema.</summary>
    private static ScriptedLlmClient Scripted(string plan, string answer) =>
        new(req => req.Schema is { Length: > 0 } ? plan : answer);

    /// <summary>
    /// A suite run with no model. The subject is a real faction, so most questions retrieve and
    /// are answered, which is what puts prose in front of the rules.
    /// </summary>
    private static async Task<List<FindingScope>> RunSuite()
    {
        WorldView view = World();
        string faction = view.State.NameOf(EntityId.Faction(2));

        ScriptedLlmClient client = Scripted(
            $$"""{"shape":"factual","subject":"{{faction}}","topics":["POLITY","CONFLICT"]}""",
            "Three rulers held the seat in that time, and two of them were killed [e:1].");

        List<QuerySuite.Scored> scored = await QuerySuite.RunAsync(new QueryEngine(client, view), view);
        return FindingsSidecar.ForAnswers(scored);
    }

    /// <summary>The fourteen rules that run on an answer: everything except the completeness pair.</summary>
    private static List<string> AnswerPathRules() =>
        [.. RuleNames.All.Where(r => r is not (RuleNames.Coverage or RuleNames.Shape))];

    [Fact]
    public async Task ASuiteRunWritesAWellFormedSidecar()
    {
        List<FindingScope> scopes = await RunSuite();
        Assert.NotEmpty(scopes);

        string dir = Environment.GetEnvironmentVariable("WB_SIDECAR_DUMP")
            ?? Path.Combine(Path.GetTempPath(), $"wb-sidecar-{Guid.CreateVersion7()}");
        try
        {
            string path = Path.Combine(dir, "answers-42.findings.json");
            FindingsSidecar.Write(path, scopes);

            Assert.True(File.Exists(path));

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;

            Assert.True(root.TryGetProperty("findings", out JsonElement findings));
            Assert.True(root.TryGetProperty("scopes", out JsonElement written));
            Assert.Equal(scopes.Count, written.GetArrayLength());

            // The shape Layer 5 will read, on every finding.
            foreach (JsonElement f in findings.EnumerateArray())
            {
                foreach (string field in new[] { "rule", "scope", "span", "detail" })
                    Assert.Equal(JsonValueKind.String, f.GetProperty(field).ValueKind);

                foreach (string flag in new[] { "blocking", "fatal" })
                {
                    JsonValueKind kind = f.GetProperty(flag).ValueKind;
                    Assert.True(kind is JsonValueKind.True or JsonValueKind.False, $"{flag} is {kind}");
                }
            }
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("WB_SIDECAR_DUMP") is null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task TheScopeOfAnAnswerIsTheQuestion()
    {
        List<FindingScope> scopes = await RunSuite();

        List<string> questions = [.. QuerySuite.ForSeed42.Select(q => q.Text)];
        Assert.All(scopes, s => Assert.Contains(s.Scope, questions));
    }

    [Fact]
    public async Task EveryRuleAppearsInEveryScopesCoverageBlock()
    {
        // The point of the block. A rule missing from a scope is indistinguishable from a rule
        // that read the scope and found nothing, and the difference is the whole subject.
        List<FindingScope> scopes = await RunSuite();
        List<string> expected = AnswerPathRules();

        Assert.NotEmpty(scopes);
        foreach (FindingScope scope in scopes)
            Assert.Equal(expected, scope.Coverage.Names);
    }

    [Fact]
    public async Task ExtractedEqualsCheckedPlusUnresolvableThroughout()
    {
        // ACCOUNTING, per rule per scope. A rule that drops an assertion without saying why
        // presents as a rule that found nothing wrong.
        List<FindingScope> scopes = await RunSuite();

        Assert.NotEmpty(scopes);
        foreach (FindingScope scope in scopes)
        {
            Assert.Empty(scope.Coverage.Unaccounted());

            foreach ((string rule, RuleCounts c) in scope.Coverage.Rules)
                Assert.True(c.Accounted, $"{scope.Scope}: {rule} extracted {c.Extracted}, " +
                                         $"checked {c.Checked}, unresolvable {c.Unresolvable}");
        }
    }

    [Fact]
    public async Task TheRulesGatedOffOnTheAnswerPathDoNotRegisterAsInert()
    {
        // coverage and shape are completeness rules and are switched off here. A rule that was
        // switched off must not report as a rule that found nothing — an inert count is the one
        // signal that says "this rule never saw the input", and spending it on rules that were
        // deliberately not offered the input makes it worth less everywhere.
        List<FindingScope> scopes = await RunSuite();

        string json = FindingsSidecar.Json(scopes);
        using JsonDocument doc = JsonDocument.Parse(json);

        foreach (JsonElement f in doc.RootElement.GetProperty("findings").EnumerateArray())
        {
            if (f.GetProperty("rule").GetString() != "rule-inert") continue;

            string span = f.GetProperty("span").GetString() ?? "";
            Assert.NotEqual(RuleNames.Coverage, span);
            Assert.NotEqual(RuleNames.Shape, span);
        }

        foreach (FindingScope scope in scopes)
        {
            Assert.DoesNotContain(RuleNames.Coverage, scope.Coverage.Names);
            Assert.DoesNotContain(RuleNames.Shape, scope.Coverage.Names);
        }
    }

    [Fact]
    public async Task AnAnswerNoRuleReadIsNotAScope()
    {
        // A refusal, a rejected premise and an empty result are sentences the engine wrote from
        // the records; no rule ran on them. Entering them would put fourteen zeroes in the file
        // for prose no rule was offered.
        //
        // The refusal is a real one, produced by the engine rather than assembled here: a subject
        // that resolves to nothing in the world.
        WorldView view = World();

        ScriptedLlmClient client = Scripted(
            """{"shape":"factual","subject":"Cardinal Ravensburg of Atlantis","topics":[]}""",
            "Never reached.");

        QueryResult refused = await new QueryEngine(client, view).AskAsync("who was Cardinal Ravensburg?");

        Assert.False(refused.Answered);
        Assert.Empty(refused.Fabrication.Coverage.Names);

        QuerySuite.Scored scored = new(
            new SuiteQuestion("who was Cardinal Ravensburg?", Expectation.Nothing, "nobody"),
            refused, true, "refused, nothing retrieved");

        Assert.Empty(FindingsSidecar.ForAnswers([scored]));
    }

    [Fact]
    public async Task AnAnsweredQuestionIsAScope()
    {
        // The other half: an answer the rules did read enters the sidecar with its full block.
        WorldView view = World();
        string faction = view.State.NameOf(EntityId.Faction(2));

        ScriptedLlmClient client = Scripted(
            $$"""{"shape":"factual","subject":"{{faction}}","topics":["POLITY"]}""",
            "Three rulers held the seat, and two were killed [e:1].");

        QueryResult answered = await new QueryEngine(client, view).AskAsync($"who has ruled {faction}?");

        QuerySuite.Scored scored = new(
            new SuiteQuestion($"who has ruled {faction}?", Expectation.Answerable, "the seat history"),
            answered, true, "answered");

        List<FindingScope> scopes = FindingsSidecar.ForAnswers([scored]);

        Assert.Single(scopes);
        Assert.Equal(AnswerPathRules(), scopes[0].Coverage.Names);
    }

    [Fact]
    public void TheChronicleAndAnswerPathsShareOneWriter()
    {
        // Two shapes would drift, and a golden diff would need two parsers. Asserted here so a
        // future edit to one path cannot quietly fork the format.
        Coverage cover = new();
        cover.Ran("action");
        cover.Extracted("action");
        cover.Checked("action");

        FindingScope section = new("A section", [], cover);
        FindingScope answer = new("A question?", [], cover);

        string a = FindingsSidecar.Json([section]);
        string b = FindingsSidecar.Json([answer]);

        Assert.Equal(a.Replace("A section", "X", StringComparison.Ordinal),
                     b.Replace("A question?", "X", StringComparison.Ordinal));
    }

    [Fact]
    public void AFindingThatCostAScopeItsPlaceIsMarkedFatal()
    {
        Coverage cover = new();
        cover.Ran("action");

        Fabrication blocking = new("token", "no-such-event", "the records hold no such event");
        FindingScope excluded = new("A section", [blocking], cover) { Excluded = true };

        using JsonDocument doc = JsonDocument.Parse(FindingsSidecar.Json([excluded]));
        JsonElement first = doc.RootElement.GetProperty("findings")[0];

        Assert.True(first.GetProperty("blocking").GetBoolean());
        Assert.True(first.GetProperty("fatal").GetBoolean());
    }

    [Fact]
    public void OnTheAnswerPathFatalIsPerFindingRatherThanPerScope()
    {
        // A chronicle drops a section and puts a note in its place. An answer has one answer and
        // nowhere to put a warning, so the finding is what was acted on, not the question.
        Coverage cover = new();
        cover.Ran("action");

        Fabrication acted = new("token", "no-such-event", "the records hold no such event");
        Fabrication noted = new("other", "ambiguous-short-name", "two powers share that word");

        FindingScope scope = new("A question?", [acted, noted], cover)
        {
            Fatal = new HashSet<Fabrication> { acted },
        };

        using JsonDocument doc = JsonDocument.Parse(FindingsSidecar.Json([scope]));
        JsonElement findings = doc.RootElement.GetProperty("findings");

        Assert.True(findings[0].GetProperty("fatal").GetBoolean());
        Assert.False(findings[1].GetProperty("fatal").GetBoolean());
    }
}
