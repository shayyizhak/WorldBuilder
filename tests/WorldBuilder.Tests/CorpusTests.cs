using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Layer 3, run as data rather than as code.
///
/// The rows live in <c>tests/corpus/*.json</c> so that adding the next round's findings is a
/// file, not a rebuild — the corpus is going to keep growing for as long as anyone reads the
/// chronicle, and a format that makes growing it cheap is the only one that will actually grow.
///
/// Each row is asserted twice, and the second assertion is the one people forget: the corrected
/// passage must come back clean. A checker that fires on everything passes the first half of
/// every row in this file and is worthless.
/// </summary>
public class CorpusTests
{
    private static readonly Dictionary<ulong, WorldView> Worlds = [];
    private static readonly Lock Gate = new();

    /// <summary>
    /// One simulation per seed for the whole class. Thirty-one rows each re-running fifty years
    /// took longer than every other test put together.
    /// </summary>
    private static WorldView World(ulong seed)
    {
        lock (Gate)
        {
            if (Worlds.TryGetValue(seed, out WorldView? cached)) return cached;

            Simulation sim = new(seed);
            sim.Run(50);
            WorldView view = WorldView.Build(sim.Log, seed);
            Worlds[seed] = view;
            return view;
        }
    }

    public static TheoryData<string> Cases()
    {
        TheoryData<string> data = [];
        foreach (CorpusCase one in Corpus.Load(Directory)) data.Add(one.Id);
        return data;
    }

    private static string Directory => Corpus.FindDirectory(AppContext.BaseDirectory);

    [Theory]
    [MemberData(nameof(Cases))]
    public void ACorpusCaseFiresItsRuleAndItsCorrectionDoesNot(string id)
    {
        CorpusCase one = Corpus.Load(Directory).Single(c => c.Id == id);
        CorpusResult result = Corpus.Run(one, World);

        Assert.True(result.Fired, $"{id}: {result.Detail}\n  {one.Note}");
        Assert.True(result.CorrectedClean, $"{id}: {result.Detail}\n  {one.Note}");
    }

    /// <summary>
    /// Every row names a rule the runner knows, and every span it claims is in its passage.
    ///
    /// Cheap, and it catches the typo that would otherwise make a row silently untestable — a
    /// row that cannot fail is worse than no row, because it reads as coverage.
    /// </summary>
    [Fact]
    public void EveryRowIsWellFormed()
    {
        List<CorpusCase> cases = Corpus.Load(Directory);
        Assert.Equal(31, cases.Count);

        foreach (CorpusCase one in cases)
        {
            Assert.True(Corpus.RuleFamilies.ContainsKey(one.ExpectRule),
                $"{one.Id}: no rule family named '{one.ExpectRule}'");
            Assert.Contains(one.ExpectSpan, one.Passage, StringComparison.Ordinal);
            Assert.NotEqual(one.Passage, one.Corrected);
            Assert.False(string.IsNullOrWhiteSpace(one.Note), $"{one.Id} has no note");
        }

        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct().Count());
    }
}
