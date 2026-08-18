using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Every field name a consumer reads exists in the emitter's vocabulary.
///
/// <b>The defect.</b> Layer 4 read <c>took</c>, <c>haul</c> and <c>plunder</c> off a raid; the
/// engine has only ever written <c>loot</c>. Every successful raid therefore came back as zero,
/// the three-way raid split had been two-way since the layer was written, and nothing failed —
/// because every assertion was about the accounting rather than about the values. A verifier that
/// reads a field name the engine does not write cannot fail. Two of the last four defects were
/// field-name mismatches.
///
/// <b>Why a standing test rather than a scan.</b> A one-time scan finds today's mismatches and
/// says nothing about tomorrow's. And a hand-declared list of what each consumer reads is a second
/// artefact to keep in step with the first, which is the same shape as the lexicons that produced
/// five of the silent-path family. So the reads are observed instead: every payload read goes
/// through <see cref="Event.GetString"/>, the consumers are run at the entry points the product
/// calls, and what they asked for is compared with what five whole records contain.
///
/// <b>What is asserted, and what is only reported.</b> A name the emitter writes nowhere is a dead
/// read and fails. A real name read on a kind that does not carry it is reported and not failed:
/// walking every event and asking each for <c>deaths</c> is a legitimate shape, and asserting on
/// it would manufacture false positives — which is how one attempt at a blanket coverage rule cost
/// seven true chronicle sections.
/// </summary>
public class SchemaInclusionTests
{
    private static readonly ulong[] Panel = [7, 42, 99, 1234, 2025];

    // ---- the emitter's vocabulary -------------------------------------------

    private static readonly Lock Gate = new();
    private static Dictionary<EventKind, SortedSet<string>>? _vocabulary;

    /// <summary>
    /// What the engine writes, from five whole ruleset-4 records and the sealed v1 one.
    ///
    /// v1 is in it because consumers pinned to the sealed record read that world, and a field the
    /// current rules no longer emit is not a dead read in a consumer that only ever meets it
    /// there.
    /// </summary>
    private static Dictionary<EventKind, SortedSet<string>> Vocabulary()
    {
        lock (Gate)
        {
            if (_vocabulary is not null) return _vocabulary;

            List<EventLog> logs = [];
            foreach (ulong seed in Panel) logs.Add(Record("ruleset-4", seed));
            logs.Add(Record("v1", 42));

            return _vocabulary = EventSchema.Emitted(logs);
        }
    }

    private static EventLog Record(string set, ulong seed)
    {
        string path = Corpus.SealedWorld(set, seed, AppContext.BaseDirectory, Directory.GetCurrentDirectory())
                      ?? throw new FileNotFoundException($"no sealed baselines/{set}/seed-{seed}");

        (EventLog log, ulong _) = JsonlIo.Read(path);
        return log;
    }

    private static WorldView World(string set, ulong seed) => WorldView.Build(Record(set, seed), seed);

    [Fact]
    public void TheVocabularyIsNonTrivialAndCoversTheKindsTheRulesEmit()
    {
        Dictionary<EventKind, SortedSet<string>> vocabulary = Vocabulary();

        // A vocabulary read off an empty log would make every read below dead, and a vocabulary
        // read off a log with one event would make every read below pass. Both are the shape of a
        // check that reports on nothing.
        Assert.True(vocabulary.Count > 20, $"only {vocabulary.Count} event kinds ever emitted");
        Assert.True(EventSchema.Anywhere(vocabulary).Count > 30,
            $"only {EventSchema.Anywhere(vocabulary).Count} field names in the whole vocabulary");

        // The name at the centre of the defect, and the three that were read instead of it. If
        // this ever inverts, the fix has been undone in the emitter rather than in the reader.
        Assert.Contains("loot", vocabulary[EventKind.ConflictRaid]);
        foreach (string invented in (string[])["took", "haul", "plunder"])
            Assert.DoesNotContain(invented, EventSchema.Anywhere(vocabulary));
    }

    /// <summary>Structured delta keys reduce to their prefix, so the vocabulary does not grow with the world.</summary>
    [Fact]
    public void ADeltaKeyIsOneVocabularyEntryRatherThanOnePerEntity()
    {
        Assert.Equal("pop", EventSchema.Name("pop:p:3"));
        Assert.Equal("rel", EventSchema.Name("rel:a:1:a:2:Kin"));
        Assert.Equal("stock", EventSchema.Name("stock:p:7:Grain"));
        Assert.Equal("loot", EventSchema.Name("loot"));

        // And the reduction is doing something: the raw keys really do carry entity ids.
        Assert.Contains(Record("ruleset-4", 42).Events,
            e => e.Data.Any(kv => kv.Key.StartsWith("pop:", StringComparison.Ordinal)));
    }

    // ---- what the consumers actually read -----------------------------------

    /// <summary>
    /// Every consumer named in the brief, run at the entry point the product calls, with a
    /// recorder attached.
    ///
    /// <see cref="SchemaSweep"/> does the running, and <c>wb schema --reads</c> calls the same
    /// function. Two lists of what counts as a consumer would drift, and a consumer missing from
    /// this side's copy is a consumer whose dead reads nothing checks.
    ///
    /// Layer 4 is not in it: it lives in an assembly that cannot reference the checker, and the
    /// same assertion runs there over its own reads. Running it from this side would route the
    /// independent verifier through the implementation it exists to be independent of.
    /// </summary>
    private static EventFieldReads WhatTheConsumersRead() => SchemaSweep.Run(
        World("ruleset-4", 42),
        Path.GetDirectoryName(
            Corpus.SealedWorld("ruleset-4", 42, AppContext.BaseDirectory, Directory.GetCurrentDirectory())));

    // ---- the assertion ------------------------------------------------------

    [Fact]
    public void NoConsumerReadsAFieldNameTheEmitterNeverWrites()
    {
        EventFieldReads reads = WhatTheConsumersRead();

        // The recorder saw something. Without this the assertion below is satisfied by a run in
        // which no consumer read anything at all — which is precisely the failure shape this whole
        // file is about, one level up.
        Assert.True(reads.Count > 20, $"the recorder saw only {reads.Count} distinct field name(s)");

        List<SchemaRead> dead = EventSchema.DeadReads(reads, Vocabulary());

        Assert.True(dead.Count == 0,
            "these names are read and the emitter writes them nowhere:\n  " +
            string.Join("\n  ", dead.Select(r => $"{EventKinds.Name(r.Kind)}.{r.Field}").Distinct()));
    }

    /// <summary>
    /// The recorder detects a dead read when there is one.
    ///
    /// Without this the test above is a test of the recorder's silence, and a recorder that never
    /// fires passes it forever. A read of an invented name is planted and must show up.
    /// </summary>
    [Fact]
    public void APlantedDeadReadIsCaught()
    {
        EventFieldReads reads = new();

        using (EventFieldReadLog.Record(reads))
        {
            Event raid = Record("ruleset-4", 42).Events.First(static e => e.Kind == EventKind.ConflictRaid);

            Assert.Equal(0, raid.GetInt("plunder"));    // the name Layer 4 used to read
            Assert.NotEqual(0, raid.GetInt("loot") + raid.GetInt("deaths"));
        }

        List<SchemaRead> dead = EventSchema.DeadReads(reads, Vocabulary());

        SchemaRead caught = Assert.Single(dead);
        Assert.Equal("plunder", caught.Field);
        Assert.Equal(EventKind.ConflictRaid, caught.Kind);
    }

    /// <summary>
    /// Reads on kinds that do not carry the field, reported rather than failed.
    ///
    /// Kept as an assertion on the *shape* of the report rather than on a count, so it records that
    /// the distinction exists without pinning a figure that moves whenever a consumer gains a
    /// branch.
    /// </summary>
    [Fact]
    public void AnOffKindReadIsDistinguishedFromADeadOne()
    {
        List<SchemaRead> rows = EventSchema.Resolve(WhatTheConsumersRead(), Vocabulary());

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.False(r.DeadRead && r.OffKind));   // the two are exclusive
        Assert.Contains(rows, static r => r.EmittedOnThisKind);
    }

    // ---- instrumentation invariance -----------------------------------------

    /// <summary>
    /// Attaching the recorder does not change the world.
    ///
    /// The standing rule, asserted rather than argued for. The recorder sits on the read path and
    /// the rules read payloads while they run, so "it obviously cannot matter" is exactly the
    /// argument the RNG-draw-order lesson says not to accept: a pure refactor at a
    /// short-circuiting site can change every world from that year on with every test green, and
    /// the with-and-without log hash is the only detector.
    /// </summary>
    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(99UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void RecordingReadsLeavesTheLogIdentical(ulong seed)
    {
        string bare = Hash(seed);

        EventFieldReads reads = new();
        string watched;
        using (EventFieldReadLog.Record(reads)) watched = Hash(seed);

        Assert.Equal(bare, watched);

        // And the recorder was attached for a run that actually reads payloads, so the equality
        // above is not the equality of two unwatched runs.
        Assert.True(reads.Count > 0, "the recorder saw nothing during a simulation");
    }

    /// <summary>
    /// A whole world, hashed. Whether a recorder is attached is decided by the caller's scope, not
    /// by an argument here — a flag would only be a second place for the two runs to differ.
    /// </summary>
    private static string Hash(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);

        System.Text.StringBuilder sb = new();
        foreach (Event e in sim.Log.Events) sb.Append(JsonlIo.Serialise(e)).Append('\n');

        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
