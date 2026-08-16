using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// v1.2 step 2: what retrieval is allowed to see, tested without a model.
///
/// Retrieval reads the record and rendering reads the narratable view, and the two rules that
/// used to be one are now separate. These pin the separation in both directions, because each
/// half fails in a way the other cannot see: letting a secret through is a leak, and stepping
/// over the bookkeeping is how a question about a famine gets answered without the harvest.
/// </summary>
public class RetrievalTests
{
    /// <summary>The archived v1 world; see <see cref="BaselineWorld"/>.</summary>
    private static WorldView World() => BaselineWorld.Seed42();

    /// <summary>
    /// Retrieval with no model in the loop.
    ///
    /// The plan is written by hand rather than parsed, which is the point: a wrong answer is
    /// usually a wrong retrieval, and this is where that can be asserted on every build instead
    /// of being read out of a chronicle once a round.
    /// </summary>
    private static List<EventId> Retrieve(
        WorldView view, QueryShape shape, string subject, string question,
        string[] topics, int from = int.MinValue, int to = int.MaxValue)
    {
        QueryEngine engine = new(new NoLlm(), view);

        return engine.Retrieve(engine.Ground(new QueryPlan
        {
            Shape = shape,
            Subject = subject,
            Question = question,
            Topics = topics,
            FromYear = from,
            ToYear = to,
        }));
    }

    /// <summary>Stands in for the planner, which these tests deliberately do not exercise.</summary>
    private sealed class NoLlm : ILlmClient
    {
        public string ModelTag => "none";

        public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("retrieval must not call the model");
    }

    /// <summary>A planner that returns a fixed plan, and an answerer that says nothing useful.</summary>
    private sealed class ScriptedPlanner(string planJson) : ILlmClient
    {
        private int _calls;

        public string ModelTag => "scripted";

        public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
            Task.FromResult(new LlmResult
            {
                Text = _calls++ == 0 ? planJson : "An answer.",
                Model = ModelTag,
            });
    }

    /// <summary>
    /// The whole path, with a planner that mistypes the subject.
    ///
    /// This is the test that was missing, and its absence cost the round twice. The fallback
    /// that reads the subject out of the question was written, unit-tested against the helper
    /// that implements it, and never reached from <see cref="QueryEngine.AskAsync"/> — which
    /// attached the question in the same object initialiser that consumed it, so the fallback
    /// ran against an empty string every time.
    ///
    /// A helper asserted in isolation proves the helper. Only the path proves the product.
    /// </summary>
    [Fact]
    public async Task TheWholePathRecoversFromAMistypedSubject()
    {
        WorldView view = World();

        ScriptedPlanner planner = new(
            "{\"shape\":\"factual\",\"subject\":\"Hadaie Commune\"," +
            "\"topics\":[\"POLITY.SUCCESSION\"],\"fromYear\":51,\"toYear\":51}");

        QueryEngine engine = new(planner, view);
        QueryResult result = await engine.AskAsync("Who ruled the Hadale Commune in year 51?");

        Assert.False(result.Plan.Entity.IsNone);
        Assert.NotEmpty(result.Retrieved);
        Assert.Contains(result.Retrieved,
            id => view.Describe(id).Contains("Durnrin Drar", StringComparison.Ordinal));
    }

    /// <summary>
    /// A question about the world retrieves by kind; a question about a name that does not
    /// exist retrieves nothing.
    ///
    /// Both arrive with no entity resolved, and treating them the same answered "which powers
    /// were destroyed" with silence. The difference is whether the subject is a category or a
    /// name — and guessing wide would turn a question about a power that never existed into a
    /// tour of the world.
    /// </summary>
    [Fact]
    public void AWorldScopedQuestionRetrievesByKind()
    {
        WorldView view = World();

        List<EventId> destroyed = Retrieve(view, QueryShape.Factual, "powers",
            "Which powers were destroyed?", ["POLITY.COLLAPSE"]);

        Assert.Equal(3, destroyed.Count);
        foreach (EventId id in destroyed)
            Assert.Equal(EventKind.PolityCollapse, view.Log.Get(id).Kind);

        List<EventId> brokeAway = Retrieve(view, QueryShape.Factual, "powers",
            "Which powers broke away, and from whom?", ["POLITY.SECESSION"]);

        Assert.Equal(4, brokeAway.Count);
    }

    [Fact]
    public void AnUnknownNameStillRetrievesNothing()
    {
        WorldView view = World();

        Assert.Empty(Retrieve(view, QueryShape.Factual, "Drelthorn League",
            "What happened to the Drelthorn League?", ["POLITY"]));
    }

    /// <summary>
    /// "Who ruled X in year N" is a question about a state, and the seat rarely changes in the
    /// year asked about.
    ///
    /// Durnrin Drar took the Hadale Commune in 47 and still held it in 51. Read as a window,
    /// the question about 51 retrieved nothing, which reads exactly like a world in which
    /// nobody ruled it.
    /// </summary>
    [Fact]
    public void WhoRuledInAGivenYearFindsTheHolderAtThatYear()
    {
        WorldView view = World();

        List<EventId> held = Retrieve(view, QueryShape.Factual, "the Hadale Commune",
            "Who ruled the Hadale Commune in year 51?",
            ["POLITY.SUCCESSION"], from: 51, to: 51);

        Assert.NotEmpty(held);
        Assert.Contains(held, id => view.Describe(id).Contains("Durnrin Drar", StringComparison.Ordinal));
    }

    /// <summary>
    /// A subject the planner mistyped still resolves, from the question.
    ///
    /// Told to copy the subject from the question as written, the model returned "Hade Commune"
    /// for the Hadale Commune. That resolved to nothing, and a well-formed question about a real
    /// power came back looking exactly like a question about a power that never existed.
    ///
    /// Fuzzy-matching the planner's string would be the obvious repair and the wrong one: it
    /// trades a miss for the chance of resolving to the wrong power. The question cannot be
    /// mistyped, so the names are matched against that instead.
    /// </summary>
    [Fact]
    public void AMistypedSubjectStillResolvesFromTheQuestion()
    {
        WorldView view = World();

        List<EventId> held = Retrieve(view, QueryShape.Factual, "Hade Commune",
            "Who ruled the Hadale Commune in year 51?",
            ["POLITY.SUCCESSION"], from: 51, to: 51);

        Assert.Contains(held, id => view.Describe(id).Contains("Durnrin Drar", StringComparison.Ordinal));
    }

    /// <summary>
    /// And the fallback does not invent a subject for a question that names none that exists.
    ///
    /// This is the risk the fallback carries: scanning the question for any known name could
    /// answer a question about the Drelthorn League with whatever else the sentence mentions.
    /// </summary>
    [Fact]
    public void TheFallbackDoesNotInventASubject()
    {
        WorldView view = World();

        Assert.Empty(Retrieve(view, QueryShape.Factual, "Drelthorn League",
            "What happened to the Drelthorn League?", ["POLITY"]));
    }

    /// <summary>And a year before the power existed still finds nobody.</summary>
    [Fact]
    public void WhoRuledBeforeThePowerExistedFindsNobody()
    {
        WorldView view = World();

        Assert.Empty(Retrieve(view, QueryShape.Factual, "the Sworn Men of Meigate",
            "Who ruled the Sworn Men of Meigate in year 5?",
            ["POLITY.SUCCESSION"], from: 5, to: 5));
    }

    /// <summary>
    /// A ruler list includes the holder the founding secession named.
    ///
    /// Successions and won challenges are two of the three sources; the third is the secession
    /// that creates a power and names who takes its seat. Without it the Vea Lode list came
    /// back with five of its six holders. The chronicle learned this at round 8 and the
    /// retrieval layer had to learn it again.
    /// </summary>
    [Fact]
    public void ARulerListIncludesTheFoundingHolder()
    {
        WorldView view = World();

        List<EventId> rulers = Retrieve(view, QueryShape.Factual, "the Vea Lode Covenant",
            "Who ruled the Vea Lode Covenant?", ["POLITY.SUCCESSION"]);

        Assert.Contains(rulers, id => view.Log.Get(id).Kind == EventKind.PolitySecession);
        Assert.Contains(rulers, id => view.Describe(id).Contains("Stald Gearngoll", StringComparison.Ordinal));
    }

    /// <summary>
    /// Asking about conspiracies retrieves the uncoverings, which are the only public record of
    /// them — and none of the plots, which are secret whether or not they were ever found out.
    /// </summary>
    [Fact]
    public void ConspiraciesAreRetrievedAsTheirUncoverings()
    {
        WorldView view = World();

        List<EventId> plots = Retrieve(view, QueryShape.Factual, "Paernmel Has",
            "Who conspired against Paernmel Has?", ["POLITY.COUP_PLOTTED"]);

        Assert.Equal(3, plots.Count);

        foreach (EventId id in plots)
        {
            Assert.Equal(EventKind.PolityCoupResolved, view.Log.Get(id).Kind);
            Assert.NotEqual(Visibility.Secret, view.Log.Get(id).Scope);
        }

        // The two that were never uncovered stay out of it.
        string all = string.Join(" ", plots.Select(view.Describe));
        Assert.DoesNotContain("Drouldthas Stour", all, StringComparison.Ordinal);
        Assert.DoesNotContain("Wuldweald Valdrith", all, StringComparison.Ordinal);
    }

    /// <summary>
    /// Secrecy is absolute, whatever else changes.
    ///
    /// The chronicle declining to narrate a conspiracy is worth nothing if a question answers
    /// it, so this is the one rule that must hold on every path into the query layer.
    /// </summary>
    [Fact]
    public void NoSecretEventIsEverRetrievable()
    {
        WorldView view = World();
        int secret = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Scope != Visibility.Secret) continue;
            secret++;

            Assert.False(ContextPackBuilder.IsRetrievable(e),
                $"{e.Id} is secret and retrievable: {view.Describe(e.Id)}");
        }

        Assert.True(secret > 50, $"seed 42 should hold plenty of secrets; found {secret}");
    }

    /// <summary>
    /// A causal walk stops at a secret rather than seeing through it.
    ///
    /// The chronicle's walk deliberately steps over an unnarratable cause and takes its causes
    /// instead, so the chain survives without the hidden link appearing. That is right for prose
    /// and wrong here: skipping the link but keeping what lay behind it hands over the shape of
    /// what was removed, and a reader who wanted the secret can read it off the gap.
    /// </summary>
    [Fact]
    public void ACausalWalkStopsAtASecretRatherThanSeeingThroughIt()
    {
        WorldView view = World();
        int walked = 0;

        foreach (Event e in view.Log.Events)
        {
            List<EventId> trace = ContextPackBuilder.Trace(view, e.Id, maxDepth: 24);
            if (trace.Count == 0) continue;

            walked++;
            foreach (EventId id in trace)
                Assert.NotEqual(Visibility.Secret, view.Log.Get(id).Scope);
        }

        Assert.True(walked > 100, $"expected to walk most of the record; walked {walked}");
    }

    /// <summary>
    /// The bookkeeping is retrievable, and that is the whole point of the separation.
    ///
    /// Roughly a third of the record is the yearly accounts. They are not history and have no
    /// place in a chronicle; they are also where the causes live, and a query layer reading the
    /// narratable view would answer "why was there a famine" with its political consequences and
    /// none of the harvest that caused it.
    /// </summary>
    [Fact]
    public void TheBookkeepingIsRetrievableThoughItIsNotNarratable()
    {
        WorldView view = World();
        int hidden = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Significance >= Significance.Minor || e.Scope == Visibility.Secret) continue;
            hidden++;

            Assert.False(ContextPackBuilder.IsRenderable(e));
            Assert.True(ContextPackBuilder.IsRetrievable(e));
        }

        Assert.True(hidden > 200,
            $"the readable view should hide a large slice of the record; it hides {hidden}");
    }

    /// <summary>
    /// And the separation shows up where the brief said it would: the causes of Hadale's
    /// secession include rows the chronicle cannot print.
    ///
    /// This is the concrete case behind the rule. <c>wb why</c> traces the secession to the
    /// Compact's own failed raid, and behind that to two yearly accounting rows recording that
    /// its standing had quietly eroded. A chronicle should not narrate those. An answer to "why
    /// did Hadale break away" is impoverished without them.
    /// </summary>
    [Fact]
    public void TheCausesOfASecessionIncludeRowsTheChronicleCannotPrint()
    {
        WorldView view = World();

        EventId secession = EventId.None;
        foreach (Event e in view.Log.Events)
            if (e.Kind == EventKind.PolitySecession && e.Year == 27) secession = e.Id;

        Assert.NotEqual(EventId.None, secession);

        List<EventId> trace = ContextPackBuilder.Trace(view, secession, maxDepth: 16);
        List<EventId> chain = [.. ContextPackBuilder.Chain(view, secession, maxDepth: 16).Events];

        Assert.Contains(trace, id => !ContextPackBuilder.IsRenderable(view.Log.Get(id)));
        Assert.DoesNotContain(chain, id => !ContextPackBuilder.IsRenderable(view.Log.Get(id)));
        Assert.True(trace.Count > chain.Count,
            $"the record-level trace should be the wider one: {trace.Count} against {chain.Count}");
    }
}
