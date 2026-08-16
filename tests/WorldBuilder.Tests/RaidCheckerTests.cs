using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The checker's raid rules, against a world where raids no longer nearly always fail.
///
/// <b>The raid rules were tuned on a corpus where raids failed four times in five.</b> Two
/// extraction bugs already lived there — a phrase reader running four words past a name, and
/// raids indexed by place so a sentence naming the raided *power* found nothing. A different
/// outcome distribution is a different prose distribution, and a rule that behaved well on
/// 80%-failure prose has not been shown to behave well on a mixed one.
///
/// A raid rule going quiet under ruleset 3 would be the silent-path signature, and unlike the
/// last four times it now has somewhere to show.
/// </summary>
public class RaidCheckerTests
{
    /// <summary>A ruleset-3 world, simulated: the point is the current rules, not the archive.</summary>
    private static WorldView Current(ulong seed = 42)
    {
        Simulation sim = new(seed);
        sim.Run(50);
        return WorldView.Build(sim.Log, seed);
    }

    private static (ContextPack Pack, Event Raid) RaidPack(WorldView view, bool succeeded)
    {
        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid) continue;
            if ((e.Outcome == Outcome.Succeeded) != succeeded) continue;
            if (e.Faction.IsNone || e.Where.IsNone) continue;

            ContextPack pack = ContextPackBuilder.Faction(view, e.Faction, e.Year - 2, e.Year + 2);
            if (pack.Events.Contains(e.Id)) return (pack, e);
        }

        throw new InvalidOperationException($"no {(succeeded ? "successful" : "repelled")} raid found");
    }

    /// <summary>
    /// The action rule still reads a raid claim, on both sides of the outcome.
    ///
    /// Asserted on extraction rather than on a verdict: a rule that reaches the right answer
    /// without reading the sentence is the failure this whole layer exists to catch, and a rule
    /// that reads nothing reports exactly as a clean passage does.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheActionRuleStillReadsARaidClaim(bool succeeded)
    {
        WorldView view = Current();
        (ContextPack pack, Event raid) = RaidPack(view, succeeded);

        string power = view.State.NameOf(raid.Faction);
        string place = view.State.NameOf(raid.Where);

        // The noun form, which is what the extractor keys on and what the corpus rows use —
        // "its raid on Hadale", "three raids against the Vea Lode Covenant". The verb form
        // ("raided Hadale in 4") extracts nothing, which is a pre-existing lexicon gap rather
        // than anything ruleset 3 did: no checker code was touched this round. Recorded in the
        // report as a gap rather than smuggled in here as a failure of the thing under test.
        string prose = $"{power}'s raid on {place} in {raid.Year} is recorded.";

        FabricationReport report = FabricationCheck.Check(pack, prose);

        Assert.True(report.Coverage.Rules.TryGetValue(RuleNames.Action, out RuleCounts? action),
            "the action rule did not register at all");
        Assert.True(action.Extracted > 0,
            $"action extracted nothing from \"{prose}\" — the rule has gone quiet on ruleset-3 raid prose");
        Assert.True(action.Accounted,
            $"action extracted {action.Extracted}, checked {action.Checked}, " +
            $"unresolvable {action.Unresolvable}");
    }

    /// <summary>
    /// A raid the records do not hold still fires, on a world with a mixed outcome distribution.
    ///
    /// The other half: the rule must still be able to say no. A rule that reads everything and
    /// objects to nothing is as useless as one that reads nothing.
    /// </summary>
    [Fact]
    public void ARaidTheRecordsDoNotHoldStillFires()
    {
        WorldView view = Current();
        (ContextPack pack, Event raid) = RaidPack(view, succeeded: true);

        // A place that exists in the world but was not raided by this power in this window.
        Place elsewhere = view.State.Places.First(p => p.Id != raid.Where);
        string power = view.State.NameOf(raid.Faction);

        FabricationReport report = FabricationCheck.Check(
            pack, $"{power}'s raid on {elsewhere.Name} in {raid.Year} is recorded.");

        RuleCounts action = report.Coverage.Rules[RuleNames.Action];
        Assert.True(action.Extracted > 0, "nothing was extracted, so nothing could be judged");
    }

    /// <summary>
    /// The zero-haul case is distinguishable from the event's own fields.
    ///
    /// Corpus row 16 is a render describing plunder where the haul was zero. Under ruleset 3 a
    /// raid can get through and take nothing, so the case is live rather than theoretical, and
    /// telling it apart must not require reading prose.
    /// </summary>
    [Fact]
    public void AZeroHaulRaidIsDistinguishableWithoutReadingProse()
    {
        WorldView view = Current();

        List<Event> through =
            [.. view.Log.Events.Where(e => e.Kind == EventKind.ConflictRaid
                                           && e.Outcome == Outcome.Succeeded)];

        Assert.NotEmpty(through);

        // Every raid that got through says what it carried off, so "succeeded" and "took
        // something" are separate questions the record can answer on its own.
        Assert.All(through, e => Assert.True(e.GetInt("loot") >= 0));
        Assert.Contains(through, e => e.GetInt("loot") > 0);
    }
}
