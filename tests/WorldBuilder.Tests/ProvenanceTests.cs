using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Stage 3: what produced a world, what happens when this build did not, and when a cached
/// passage stops being the passage its inputs produce.
///
/// The three are one subject. A seed is not a regeneration recipe — a rule change or the model's
/// own variance breaks that — so the materialised artefacts are what endure, and an artefact that
/// cannot name its producer is one nobody can reason about later. These tests are the assertion
/// that it can.
/// </summary>
public class ProvenanceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"wb-prov-{Guid.CreateVersion7()}");

    private static WorldView World(ulong seed = 42, int years = 50)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return WorldView.Build(sim.Log, seed);
    }

    private string Path_(string name)
    {
        Directory.CreateDirectory(_dir);
        return System.IO.Path.Combine(_dir, name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // ---- the header -------------------------------------------------------

    [Fact]
    public void TheEngineNamesItselfFromTheBuildRatherThanFromAConstant()
    {
        // A figure restated in source is a figure that goes stale silently. This one is an
        // assembly attribute, so it cannot disagree with what was actually built.
        Assert.False(string.IsNullOrWhiteSpace(Engine.Version));
        Assert.True(Version.TryParse(Engine.Version, out _));
    }

    [Fact]
    public void AWorldFileRecordsTheEngineAndRulesetThatWroteIt()
    {
        Simulation sim = new(42);
        sim.Run(10);

        string path = Path_("world-42.jsonl");
        JsonlIo.Write(path, sim.Log, 42);

        WorldHeader? header = JsonlIo.ReadHeader(path);

        Assert.NotNull(header);
        Assert.Equal(42UL, header.Seed);
        Assert.Equal(sim.Log.Count, header.Events);
        Assert.Equal(Engine.Version, header.EngineVersion);
        Assert.Equal(Ruleset.Version, header.RulesetVersion);
        Assert.False(header.ProvenanceUnknown);
    }

    [Fact]
    public void TwoRunsOfTheSameSeedWriteTheSameHeader()
    {
        // Nothing in the header may vary between runs. The determinism guarantee is checked by
        // comparing files, and a timestamp in the header would quietly end that.
        Simulation a = new(42);
        a.Run(10);
        Simulation b = new(42);
        b.Run(10);

        string first = Path_("a.jsonl");
        string second = Path_("b.jsonl");
        JsonlIo.Write(first, a.Log, 42);
        JsonlIo.Write(second, b.Log, 42);

        Assert.Equal(File.ReadAllText(first), File.ReadAllText(second));
    }

    [Fact]
    public void AWorldFileWrittenBeforeTheHeaderExistedStillReads()
    {
        // Every v1 artefact looks like this, including the hand-verified baseline. A reader that
        // throws on them is a reader that cannot open the only worlds that matter.
        string path = Path_("old.jsonl");
        File.WriteAllText(path, "{\"type\":\"world\",\"seed\":\"42\",\"events\":1}\n");

        WorldHeader? header = JsonlIo.ReadHeader(path);

        Assert.NotNull(header);
        Assert.Equal(42UL, header.Seed);
        Assert.True(header.ProvenanceUnknown);
    }

    [Fact]
    public void HeaderFieldsAreOmittedRatherThanWrittenEmpty()
    {
        // So a file carrying no provenance is never confusable with one carrying blank
        // provenance. Absent and empty are different, which is a rule this project keeps
        // relearning in other places.
        string line = new WorldHeader { Seed = 7, Events = 3 }.Serialise();

        Assert.Equal("{\"type\":\"world\",\"seed\":\"7\",\"events\":3}", line);
        Assert.DoesNotContain("engine_version", line, StringComparison.Ordinal);
    }

    // ---- the open policy --------------------------------------------------

    [Fact]
    public void AWorldFromANewerEngineBlocks()
    {
        WorldHeader header = new()
        {
            Seed = 42, Events = 10, EngineVersion = "2.0.0", RulesetVersion = "1.2.0",
        };

        WorldFileCheck check = WorldCompatibility.Check(header, "1.2.0", "1.2.0");

        Assert.True(check.Blocks);
        Assert.Contains(check.Notes, n => n.Contains("--accept-newer", StringComparison.Ordinal));
    }

    [Fact]
    public void AWorldFromAnOlderEngineOpensAndSaysSo()
    {
        WorldHeader header = new()
        {
            Seed = 42, Events = 10, EngineVersion = "1.0.0", RulesetVersion = "1.2.0",
        };

        WorldFileCheck check = WorldCompatibility.Check(header, "1.2.0", "1.2.0");

        Assert.False(check.Blocks);
        Assert.NotEmpty(check.Notes);
    }

    [Fact]
    public void AChangedRulesetIsReportedAsLostRegenerationNotAsAnUnreadableFile()
    {
        // The log is the artefact and it is complete. What a ruleset change costs is the ability
        // to rebuild this world from its seed, which is a different loss and must read as one.
        WorldHeader header = new()
        {
            Seed = 42, Events = 10, EngineVersion = "1.2.0", RulesetVersion = "1.1.0",
        };

        WorldFileCheck check = WorldCompatibility.Check(header, "1.2.0", "1.2.0");

        Assert.False(check.Blocks);
        Assert.Contains(check.Notes, n => n.Contains("regenerated from its seed", StringComparison.Ordinal));
    }

    [Fact]
    public void AMatchingWorldSaysNothingAtAll()
    {
        WorldHeader header = new()
        {
            Seed = 42, Events = 10, EngineVersion = "1.2.0", RulesetVersion = "1.2.0",
        };

        WorldFileCheck check = WorldCompatibility.Check(header, "1.2.0", "1.2.0");

        Assert.False(check.Blocks);
        Assert.Empty(check.Notes);
    }

    [Fact]
    public void AWorldWithNoProvenanceSaysSoWithoutBlocking()
    {
        WorldHeader header = new() { Seed = 42, Events = 10 };

        WorldFileCheck check = WorldCompatibility.Check(header, "1.2.0", "1.2.0");

        Assert.False(check.Blocks);
        Assert.NotEmpty(check.Notes);
    }

    // ---- cached-render invalidation ---------------------------------------

    [Fact]
    public void APackHashesEverythingTheModelIsShown()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        Assert.False(string.IsNullOrEmpty(pack.InputHash));
        Assert.Equal(ContextPackBuilder.InputHashOf(pack.Body), pack.InputHash);
    }

    [Fact]
    public void TheSamePackHashesTheSameAcrossTwoBuildsOfTheWorld()
    {
        Assert.Equal(
            ContextPackBuilder.Year(World(), 24).InputHash,
            ContextPackBuilder.Year(World(), 24).InputHash);
    }

    [Fact]
    public void ChangedFactsOverUnchangedEventsMissTheCache()
    {
        // The defect this whole rule exists for. The events are identical, so the pack key is
        // identical, and a cache keyed on that alone would serve a passage restating a figure
        // that has since been computed differently — permanently, because renders are canon.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path_("renders.json"));
        store.Put(new Render
        {
            PackKey = pack.Key,
            InputHash = "0123456789abcdef",
            PromptVersion = Prompts.VersionFor(pack.Kind),
            Model = "scripted",
            Text = "Written from figures that have since changed.",
            Year = 24,
        });

        Assert.False(store.TryGet(
            pack.Key, pack.InputHash, Prompts.VersionFor(pack.Kind), "scripted", out _));

        Assert.True(store.TryGet(
            pack.Key, "0123456789abcdef", Prompts.VersionFor(pack.Kind), "scripted", out Render stale));
        Assert.Equal("Written from figures that have since changed.", stale.Text);
    }

    [Fact]
    public async Task AStaleEntryIsLeftStandingRatherThanOverwritten()
    {
        // Same rule as a prompt or model change: the new render is a new entry. Overwriting
        // would destroy the record of what the world used to say, which is the thing the cache
        // exists to preserve.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path_("renders.json"));
        store.Put(new Render
        {
            PackKey = pack.Key,
            InputHash = "0123456789abcdef",
            PromptVersion = Prompts.VersionFor(pack.Kind),
            Model = "scripted",
            Text = "The old telling.",
            Year = 24,
        });

        ScriptedLlmClient client = new(_ => "The new telling.");
        await new Chronicler(client, store, new RenderJournal(Path_("renders.jsonl")))
            .RenderAsync(pack);

        Assert.Equal(1, client.Calls);
        Assert.Equal(2, store.ForPack(pack.Key).Count);
    }

    [Fact]
    public async Task AnUnchangedPackStillHitsTheCache()
    {
        // The other half, and the one that keeps the cache worth having: a rule change that
        // touches no pack must invalidate nothing.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path_("renders.json"));
        RenderJournal journal = new(Path_("renders.jsonl"));

        ScriptedLlmClient first = new(_ => "Told once.");
        await new Chronicler(first, store, journal).RenderAsync(pack);

        ScriptedLlmClient second = new(_ => "Told again.");
        RenderOutcome outcome = await new Chronicler(second, store, journal).RenderAsync(pack);

        // The first renders — how many attempts it takes is the retry path's business, not this
        // test's. The second must not call at all.
        Assert.True(first.Calls > 0);
        Assert.Equal(0, second.Calls);
        Assert.True(outcome.FromCache);
        Assert.Equal("Told once.", outcome.Render.Text);
    }

    [Fact]
    public async Task ACacheEntryWithNoInputHashIsServedAndCounted()
    {
        // The v1 cache is entirely like this, and it is the hand-verified one. Refusing it would
        // discard the baseline; accepting it silently would claim more than the cache can show.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path_("renders.json"));
        store.Put(new Render
        {
            PackKey = pack.Key,
            PromptVersion = Prompts.VersionFor(pack.Kind),
            Model = "scripted",
            Text = "Rendered before inputs were hashed.",
            Year = 24,
        });

        ScriptedLlmClient client = new(_ => "Should not be called.");
        RenderOutcome outcome = await new Chronicler(client, store, new RenderJournal(Path_("renders.jsonl")))
            .RenderAsync(pack);

        Assert.Equal(0, client.Calls);
        Assert.True(outcome.FromCache);
        Assert.Equal("Rendered before inputs were hashed.", outcome.Render.Text);
        Assert.Equal(1, store.UnpinnedHits);
    }

    [Fact]
    public void AStoreOfPreHashEntriesRewritesByteForByte()
    {
        // The v1 renders.json is an archived artefact with a recorded sha256. A re-check that
        // silently reformats it invalidates the thing it was verifying.
        string path = Path_("renders.json");
        const string line =
            "{\"packKey\":\"year-0123456789abcdef\",\"promptVersion\":\"v1\",\"model\":\"m\",\"year\":24," +
            "\"status\":\"Generated\",\"promptTokens\":1,\"outputTokens\":2,\"elapsedMs\":3,\"text\":\"Old.\"}\n";
        File.WriteAllText(path, line);

        RenderStore store = new(path);
        Assert.Equal(1, store.Count);

        // Any write rewrites the whole file, so one unrelated addition proves the round-trip.
        store.Put(new Render
        {
            PackKey = "year-ffffffffffffffff",
            InputHash = "abcdefabcdefabcd",
            PromptVersion = "v1",
            Model = "m",
            Text = "New.",
            Year = 25,
        });

        string[] written = File.ReadAllLines(path);
        Assert.Contains(written, l => l == line.TrimEnd('\n'));
        Assert.Contains(written, l => l.Contains("\"inputHash\":\"abcdefabcdefabcd\"", StringComparison.Ordinal));
    }
}
