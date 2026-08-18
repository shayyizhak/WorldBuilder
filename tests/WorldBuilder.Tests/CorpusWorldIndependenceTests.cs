using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Which Layer 3 rows depend on world state, established rather than assumed.
///
/// The carry-forward brief calls the regression corpus world-independent and asks for that to be
/// confirmed by running it against ruleset 4. It is half true, and the half that is not is the
/// interesting half: a row with no scope is checked by Tier 1 alone and reads no world at all,
/// while a scoped row is checked against a context pack rebuilt from a particular log. Seed 42 at
/// ruleset 4 is a different history, not a stale one, so a scoped row asks its question of a
/// different world and may get a different answer.
///
/// That is not a defect in the corpus. The rows are pinned to the sealed v1 record on purpose —
/// ruleset 2 moved the world under all thirty-four at once and 56 fixtures failed together — and
/// this measures how much of the corpus that pinning is actually load-bearing for.
/// </summary>
public class CorpusWorldIndependenceTests
{
    private static string Directory => Corpus.FindDirectory(AppContext.BaseDirectory);

    /// <summary>
    /// The ruleset-4 machine baseline's world, opened the way every reader opens one.
    ///
    /// Through <see cref="WorldBundle.Open"/> rather than a raw read, so the board beside it is
    /// verified against the header and the genesis fingerprint. From ruleset 4 a world is a log and
    /// its board, and reading the log alone would answer distance questions about no map at all.
    /// </summary>
    private static WorldView Ruleset4Seed42()
    {
        string? path = Corpus.SealedWorld("ruleset-4", 42,
            AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory());

        Assert.NotNull(path);

        BundleOpen opened = WorldBundle.Open(path);
        return WorldView.Build(opened.Log, opened.Seed);
    }

    private static readonly Lock Gate = new();
    private static WorldView? _ruleset4;

    private static WorldView Ruleset4(ulong seed)
    {
        Assert.Equal(42UL, seed);        // no row names another seed; one that did would be silent

        lock (Gate) return _ruleset4 ??= Ruleset4Seed42();
    }

    /// <summary>
    /// A row with no scope reads no world, and produces the same verdict against either.
    ///
    /// The structural claim, asserted rather than reasoned about: these rows are checked by
    /// <see cref="SelfConsistency"/>, which is handed text and a coverage ledger and nothing else.
    /// </summary>
    [Fact]
    public void EveryTextOnlyRowIsWorldIndependent()
    {
        List<CorpusCase> textOnly = [.. Corpus.Load(Directory).Where(static c => c.Scope is null)];
        Assert.NotEmpty(textOnly);

        List<string> differed = [];

        foreach (CorpusCase one in textOnly)
        {
            CorpusResult onV1 = Corpus.Run(one, BaselineWorld.ForSeed);
            CorpusResult onRuleset4 = Corpus.Run(one, Ruleset4);

            if (onV1.Fired == onRuleset4.Fired
                && onV1.CorrectedClean == onRuleset4.CorrectedClean
                && string.Equals(onV1.Detail, onRuleset4.Detail, StringComparison.Ordinal))
            {
                continue;
            }

            differed.Add($"{one.Id}: v1 \"{onV1.Detail}\" against ruleset 4 \"{onRuleset4.Detail}\"");
        }

        Assert.True(differed.Count == 0,
            "a row with no scope read a world after all:\n  " + string.Join("\n  ", differed));
    }

    /// <summary>
    /// Every scoped row is run against ruleset 4, and the outcome is recorded rather than required.
    ///
    /// <b>The classification is the assertion.</b> A row that passes on both worlds is testing the
    /// checker; a row that passes only on v1 is testing the checker against a fact of the v1 world,
    /// and its pinning is load-bearing. Both are legitimate, and the corpus is correct either way —
    /// what would not be legitimate is not knowing which is which, since that is the state the whole
    /// reference-set rebuild exists to get out of.
    ///
    /// Pinned as a count so a future ruleset change that moves a row between the two groups shows up
    /// as a difference. The row lists are in the phase report; the numbers are here, because a
    /// figure restated in prose is a figure that goes stale in one of the two places.
    /// </summary>
    [Fact]
    public void TheScopedRowsAreClassifiedByWhetherTheyDependOnTheWorld()
    {
        List<CorpusCase> scoped = [.. Corpus.Load(Directory).Where(static c => c.Scope is not null)];

        List<string> both = [], v1Only = [], neither = [], reasons = [];

        foreach (CorpusCase one in scoped)
        {
            bool onV1 = Corpus.Run(one, BaselineWorld.ForSeed).Passed;

            bool onRuleset4;
            try
            {
                CorpusResult r4 = Corpus.Run(one, Ruleset4);
                onRuleset4 = r4.Passed;
                if (!onRuleset4) reasons.Add($"{one.Id} -> {r4.Detail}");
            }
            catch (InvalidDataException ex)
            {
                reasons.Add($"{one.Id} -> SCOPE GONE: {ex.Message}");
                // The scope itself is gone from the new history — a war that was never declared, a
                // reign nobody held. Counted with the rows that fail there rather than thrown,
                // because it is the same finding: the row is about the v1 world.
                onRuleset4 = false;
            }

            List<string> bucket = (onV1, onRuleset4) switch
            {
                (true, true) => both,
                (true, false) => v1Only,
                _ => neither,
            };

            bucket.Add(one.Id);
        }

        // Nothing may fail on v1. That is Layer 3's actual contract and CorpusTests asserts it row
        // by row; asserted again here so this test cannot report a tidy classification of a broken
        // corpus.
        Assert.True(neither.Count == 0, "these rows fail on the sealed v1 world:\n  " + string.Join("\n  ", neither));

        Assert.Equal(scoped.Count, both.Count + v1Only.Count);

        // The split, pinned. Not a bar either way — a row moving between the groups is a fact about
        // the new ruleset's history, not a regression — but it is a fact that should be noticed, and
        // the reasons come with it so the next reader does not have to re-derive them.
        //
        // Eight rows still catch their fabrication in a world nobody wrote them about, which is a
        // stronger statement about the checker than the corpus was ever asked for. The other twenty
        // fail on names and events the new history does not contain, or on a scope that no longer
        // exists — five reigns and faction windows among them.
        string why = "\n  " + string.Join("\n  ", reasons);

        // Eight of the twenty fail because the *scope* is gone from the new history — no reign of
        // Heth Fal over the Sworn Men of Laehiford 39–51, no Sworn Men of Meigate 19–51 — and twelve
        // because the passage names people and events the new history does not contain. Both are the
        // same finding about the row and are counted together.
        Assert.Equal(8, reasons.Count(static r => r.Contains("SCOPE GONE", StringComparison.Ordinal)));

        Assert.Equal(8, both.Count);
        Assert.True(v1Only.Count == 20,
            $"{v1Only.Count} rows now depend on the v1 world rather than 20:{why}");
    }
}
