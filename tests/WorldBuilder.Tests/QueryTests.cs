using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The query layer, tested without inference. The guarantees that matter are all structural —
/// what is retrieved, what the model is shown, and whether its citations resolve — so none of
/// them need a model running to verify.
/// </summary>
public class QueryTests
{
    /// <summary>The archived v1 world; see <see cref="BaselineWorld"/>.</summary>
    private static WorldView World(ulong seed = 42) => BaselineWorld.ForSeed(seed);

    /// <summary>A client that plans as instructed and then answers with fixed text.</summary>
    private static ScriptedLlmClient Scripted(string plan, string answer) =>
        new(req => req.Schema is { Length: > 0 } ? plan : answer);

    [Fact]
    public async Task AQuestionAboutNobodyIsRefusedRatherThanAnswered()
    {
        // The failure this exists to prevent: a confident answer assembled from the model's
        // memory of the world rather than from anything the engine actually found.
        WorldView view = World();
        ScriptedLlmClient client = Scripted(
            """{"shape":"factual","subject":"Cardinal Ravensburg of Atlantis","topics":[]}""",
            "The Cardinal ruled for thirty years.");

        QueryResult result = await new QueryEngine(client, view).AskAsync("who was Cardinal Ravensburg?");

        Assert.False(result.Answered);
        Assert.Empty(result.Retrieved);
        Assert.DoesNotContain("thirty years", result.Answer, StringComparison.OrdinalIgnoreCase);

        // A fact about the world rather than about a search: the name belongs to nothing here.
        Assert.Equal(EmptyReason.NoSuchEntity, result.Empty);
        Assert.Contains("no Cardinal Ravensburg", result.Answer, StringComparison.OrdinalIgnoreCase);

        // One call to plan, and none to answer: with nothing retrieved there is nothing to say.
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task AFactualQuestionRetrievesOnlyThatSubjectsRecords()
    {
        WorldView view = World();
        EntityId faction = EntityId.Faction(2);
        string name = view.State.NameOf(faction);

        ScriptedLlmClient client = Scripted(
            $$"""{"shape":"factual","subject":"{{name}}","topics":["POLITY"]}""",
            "They were ruled by several people [e:1].");

        QueryResult result = await new QueryEngine(client, view).AskAsync($"who has ruled {name}?");

        Assert.True(result.Answered);
        Assert.Equal(faction, result.Plan.Entity);

        foreach (EventId id in result.Retrieved)
        {
            Event e = view.Log.Get(id);
            Assert.StartsWith("POLITY", EventKinds.Name(e.Kind), StringComparison.Ordinal);

            bool mentionsSubject = false;
            foreach (Participant p in e.Participants)
                if (p.Id == faction) mentionsSubject = true;
            Assert.True(mentionsSubject);
        }
    }

    [Fact]
    public async Task ACausalQuestionReturnsTheAncestryOfTheEventItAsksAbout()
    {
        WorldView view = World();
        EntityId faction = EntityId.Faction(2);
        string name = view.State.NameOf(faction);

        // Ask about a record that genuinely has antecedents, phrased in its own words. Asking
        // "why did X decline?" matched nothing and returned a lone event with no causes — which
        // is honest, but proves nothing about chain retrieval.
        EventId target = EventId.None;
        int deepest = 0;
        foreach (EventId id in view.Log.ForEntity(faction))
        {
            if (view.Log.Get(id).Significance < Significance.Major) continue;
            int depth = ContextPackBuilder.Chain(view, id, 16).Events.Count;
            if (depth > deepest) { deepest = depth; target = id; }
        }

        Assert.True(deepest > 2, "seed produced no faction event with a chain behind it");

        string question = $"why {view.Describe(target)}";
        ScriptedLlmClient client = Scripted(
            $$"""{"shape":"causal","subject":"{{name}}","topics":[]}""",
            "Because of earlier events [e:1].");

        QueryResult result = await new QueryEngine(client, view).AskAsync(question);

        Assert.Equal(QueryShape.Causal, result.Plan.Shape);
        Assert.True(result.Retrieved.Count > 1, "a causal answer should return the steps, not one record");

        // Connected: at least one retrieved record cites another retrieved record.
        HashSet<EventId> inChain = [.. result.Retrieved];
        int linked = 0;
        foreach (EventId id in result.Retrieved)
            foreach (EventId cause in view.Log.Get(id).Causes)
                if (inChain.Contains(cause)) linked++;

        Assert.True(linked > 0, "a causal answer returned records with no edges between them");
    }

    [Fact]
    public async Task SecretsAreNeverRetrieved()
    {
        WorldView view = World();
        EntityId faction = EntityId.Faction(2);

        ScriptedLlmClient client = Scripted(
            $$"""{"shape":"factual","subject":"{{view.State.NameOf(faction)}}","topics":[]}""",
            "Something happened [e:1].");

        QueryResult result = await new QueryEngine(client, view).AskAsync("what happened?");

        foreach (EventId id in result.Retrieved)
            Assert.NotEqual(Visibility.Secret, view.Log.Get(id).Scope);
    }

    [Fact]
    public async Task CitationsOutsideTheRetrievedSetAreReported()
    {
        // A plausible-looking id the engine never supplied is the exact thing retrieval is meant
        // to make impossible, so it is measured rather than trusted away.
        WorldView view = World();
        EntityId faction = EntityId.Faction(2);

        ScriptedLlmClient client = Scripted(
            $$"""{"shape":"factual","subject":"{{view.State.NameOf(faction)}}","topics":["POLITY"]}""",
            "It fell in 40 [e:99999].");

        QueryResult result = await new QueryEngine(client, view).AskAsync("when did it fall?");

        Assert.Contains("e:99999", result.BadCitations);
    }

    [Fact]
    public async Task AMalformedPlanFallsBackInsteadOfThrowing()
    {
        WorldView view = World();
        ScriptedLlmClient client = Scripted("not json at all", "irrelevant");

        QueryResult result = await new QueryEngine(client, view).AskAsync("who ruled?");

        Assert.Equal(QueryShape.Factual, result.Plan.Shape);
    }

    [Fact]
    public async Task RetrievalIsBoundedSoTheWorldIsNeverStuffedIntoContext()
    {
        // Handing over the whole log works at fifty years and fabricates the moment the world
        // outgrows the context window. The cap is the point of the design.
        //
        // Simulated rather than loaded from the baseline, and deliberately: this asserts a
        // property of the engine at a scale no archived world has, not a fact about the v1 world.
        Simulation big = new(42);
        big.Run(150);
        WorldView view = WorldView.Build(big.Log, 42);
        EntityId faction = EntityId.Faction(2);

        ScriptedLlmClient client = Scripted(
            $$"""{"shape":"factual","subject":"{{view.State.NameOf(faction)}}","topics":[]}""",
            "Answer [e:1].");

        QueryResult result = await new QueryEngine(client, view).AskAsync("everything about them?");

        Assert.True(result.Retrieved.Count <= 40,
            $"{result.Retrieved.Count} records retrieved; retrieval must stay bounded");
    }
}
