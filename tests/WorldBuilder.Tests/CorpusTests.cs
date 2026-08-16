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
    /// <summary>
    /// The archived v1 world, not a fresh simulation.
    ///
    /// Every row here is a fabrication that reached canon in one particular world, with the true
    /// answer beside it. Re-simulating to obtain that world made each row an assertion about
    /// whatever the current rules happen to produce, and ruleset 2 moved the world under all
    /// thirty-four at once. They are about the v1 world, so they read it.
    /// </summary>
    private static WorldView World(ulong seed) => BaselineWorld.ForSeed(seed);

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
        Assert.Equal(34, cases.Count);

        foreach (CorpusCase one in cases)
        {
            Assert.True(Corpus.RuleFamilies.ContainsKey(one.ExpectRule),
                $"{one.Id}: no rule family named '{one.ExpectRule}'");
            Assert.Contains(one.ExpectSpan, one.Passage, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(one.Note), $"{one.Id} has no note");

            Assert.True(one.Kind is "must-fire" or "must-not-fire" or "extraction",
                $"{one.Id}: unknown kind '{one.Kind}'");

            // Only a must-fire row has a correction to hold; the other two assert about the
            // passage alone, and inventing a second passage for them would be a second thing
            // to keep true for no gain.
            if (one.Kind == "must-fire") Assert.NotEqual(one.Passage, one.Corrected);
        }

        // The three rows that were fixed and came back are the reason the corpus is a file on
        // disk rather than a memory of a review. Asserted by id so a rename cannot lose them.
        foreach (string repeat in new[]
                 {
                     "r08-r11-reign-given-a-faction-lifetime-count",
                     "r09-r11-held-the-seat-since-year-one",
                     "r09-r11-killing-149-men",
                 })
        {
            Assert.Contains(cases, c => c.Id == repeat);
        }

        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct().Count());
    }
}
