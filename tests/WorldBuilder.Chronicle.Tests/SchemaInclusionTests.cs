using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using Xunit;

namespace WorldBuilder.Chronicle.Tests;

/// <summary>
/// Layer 4's own reads, against the emitter's vocabulary.
///
/// <b>This is where the defect was.</b> <see cref="RecordFacts"/> read <c>took</c>, <c>haul</c>
/// and <c>plunder</c> off a raid; the engine writes <c>loot</c>. Every successful raid came back
/// as zero, the three-way split had been two-way since the layer was written, and nothing failed
/// — because every assertion was about the accounting rather than about the values. The
/// independent verifier had been reporting on nothing, for that one figure, for its whole life.
///
/// <b>Asserted from this side, deliberately.</b> The same check runs in the main test assembly
/// over the checker, the pack builder, query retrieval and the archive. It cannot cover this
/// layer: this assembly does not reference <c>WorldBuilder.Inference</c> and asserts that it
/// cannot, so a sweep driven from over there would run Layer 4's reads through the implementation
/// Layer 4 exists to be independent of. Two checks, no shared implementation — the same trade the
/// duplication itself is making.
/// </summary>
public class SchemaInclusionTests
{
    /// <summary>
    /// What the emitter writes, from both sealed baselines this layer runs against.
    ///
    /// Both, because Layer 4 verifies both documents and a name written only under ruleset 4 is
    /// not a dead read in a check that meets it there.
    /// </summary>
    private static Dictionary<EventKind, SortedSet<string>> Vocabulary() =>
        EventSchema.Emitted(SealedBaselines.All.Select(b => SealedBaselines.World(b).Log));

    /// <summary>
    /// Every figure this layer derives, computed with a recorder attached.
    ///
    /// The entry points the layer's own tests call, not an inner step of them. A sweep that
    /// entered somewhere else would record the reads of a path the layer never takes.
    /// </summary>
    private static EventFieldReads WhatLayerFourReads(BaselineUnderTest baseline)
    {
        WorldView view = SealedBaselines.World(baseline);
        EventFieldReads reads = new();

        using (EventFieldReadLog.Record(reads))
        {
            foreach (Faction f in view.State.Factions)
            {
                RecordFacts.SeatHistory(view, f.Id);
                RecordFacts.RaidsSent(view, f.Id, view.FirstYear, view.LastYear);
                RecordFacts.RaidsSuffered(view, f.Id, view.FirstYear, view.LastYear);
                RecordFacts.Battles(view, f.Id, view.FirstYear, view.LastYear);
                RecordFacts.Killings(view, f.Id, view.FirstYear, view.LastYear);
                RecordFacts.Marriages(view, f.Id, view.FirstYear, view.LastYear);
                RecordFacts.YearsNamed(view, f.Id, view.FirstYear, view.LastYear);
            }

            RecordFacts.AllNameWords(view);

            foreach (Place p in view.State.Places)
                RecordFacts.HeldBy(view, p.Id, view.LastYear);
        }

        return reads;
    }

    public static TheoryData<BaselineUnderTest> Baselines()
    {
        TheoryData<BaselineUnderTest> data = [];
        foreach (BaselineUnderTest one in SealedBaselines.All) data.Add(one);
        return data;
    }

    [Theory]
    [MemberData(nameof(Baselines))]
    public void ThisLayerReadsNoFieldNameTheEmitterNeverWrites(BaselineUnderTest baseline)
    {
        EventFieldReads reads = WhatLayerFourReads(baseline);

        // The recorder saw something. Without this the assertion below passes on a run in which
        // the layer read nothing at all, which is the same failure shape one level up.
        Assert.True(reads.Count > 0, $"{baseline}: the recorder saw no payload read at all");

        List<SchemaRead> dead = EventSchema.DeadReads(reads, Vocabulary());

        Assert.True(dead.Count == 0,
            $"{baseline}: these names are read here and the emitter writes them nowhere:\n  " +
            string.Join("\n  ", dead.Select(r => $"{EventKinds.Name(r.Kind)}.{r.Field}").Distinct()));
    }

    /// <summary>
    /// The haul figure is read from the name the record actually carries.
    ///
    /// Pinned by name rather than only by behaviour, because the behavioural assertion — that some
    /// raid came away with something — is one true sentence away from passing again on a second
    /// wrong name. The three names that were read instead are asserted absent from the vocabulary
    /// so that a future emitter change which introduces one of them is visible here.
    /// </summary>
    [Theory]
    [MemberData(nameof(Baselines))]
    public void TheHaulFigureIsReadFromLootAndFromNothingElse(BaselineUnderTest baseline)
    {
        Dictionary<EventKind, SortedSet<string>> vocabulary = Vocabulary();

        Assert.Contains("loot", vocabulary[EventKind.ConflictRaid]);

        SortedSet<string> anywhere = EventSchema.Anywhere(vocabulary);
        foreach (string invented in (string[])["took", "haul", "plunder"])
            Assert.DoesNotContain(invented, anywhere);

        // And this layer asks for it on raids. A figure read from a name nothing writes is exactly
        // what a recorder can see and an assertion about the total cannot.
        Assert.Contains("loot", WhatLayerFourReads(baseline).On(EventKind.ConflictRaid));
    }
}
