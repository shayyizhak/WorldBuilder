using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Behavioural checks on the rules that carry the history. These are deliberately stated as
/// properties of a whole run rather than as unit tests of individual functions: the question
/// v0 answers is whether the ruleset produces readable history, and that is only visible in
/// aggregate.
/// </summary>
public class RuleTests
{
    private static WorldView RunWorld(ulong seed = 42, int years = 60)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return WorldView.Build(sim.Log, seed);
    }

    private static int Count(WorldView view, EventKind kind)
    {
        int n = 0;
        foreach (Event e in view.Log.Events)
            if (e.Kind == kind) n++;
        return n;
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2025UL)]
    public void TheWholeVocabularyGetsExercised(ulong seed)
    {
        WorldView view = RunWorld(seed);

        // Rules that never fire are rules that are not really there. Each of these is a
        // distinct source of drama and all of them should appear within sixty years.
        Assert.True(Count(view, EventKind.PolitySuccession) > 0, "no successions");
        Assert.True(Count(view, EventKind.LifeDeathNatural) > 0, "nobody died of old age");
        Assert.True(Count(view, EventKind.EconomyFamine) > 0, "no famine");
        Assert.True(Count(view, EventKind.DiploWarDeclared) > 0, "no wars");
        Assert.True(Count(view, EventKind.ConflictBattle) > 0, "no battles");
        Assert.True(Count(view, EventKind.PolityExile) > 0, "nobody was exiled");
        Assert.True(Count(view, EventKind.ConflictAssassination) > 0, "no assassinations");
    }

    [Fact]
    public void SuccessionKeepsFactionsLedWhileTheyHoldGround()
    {
        WorldView view = RunWorld();

        foreach (Faction f in view.State.Factions)
        {
            if (view.State.HoldingsOf(f.Id).Count == 0) continue;
            Assert.False(f.Leader.IsNone, $"{f.Name} holds ground but has no leader");
            Assert.True(view.State.ActorOf(f.Leader).IsAlive, $"{f.Name} is led by a dead actor");
        }
    }

    [Fact]
    public void EveryDeadRulerIsFollowedBySomeoneOrTheFactionEnds()
    {
        WorldView view = RunWorld();

        // Each faction that ever lost a leader must show either a succession or a collapse
        // afterwards. A throne that simply stays empty is the bug this guards against.
        foreach (Faction f in view.State.Factions)
        {
            bool lostALeader = false;
            bool resolved = true;

            foreach (EventId id in view.Log.ForEntity(f.Id))
            {
                Event e = view.Log.Get(id);
                if (e.Kind is EventKind.LifeDeathNatural or EventKind.LifeDeathViolent
                    && e.Faction == f.Id
                    && e.GetInt("wasLeader") == 1)
                {
                    lostALeader = true;
                    resolved = false;
                }
                else if (e.Kind is EventKind.PolitySuccession or EventKind.PolityCollapse)
                {
                    resolved = true;
                }
            }

            Assert.True(!lostALeader || resolved, $"{f.Name} lost a leader and never replaced them");
        }
    }

    [Fact]
    public void GrievanceDecaysButRemembers()
    {
        SimConfig config = SimConfig.Default;
        Assert.InRange(config.GrievanceRetentionPct, 90, 99);

        // At the configured rate a serious grudge must still be materially alive fifteen years
        // on — that is the whole basis of the engine's long-range causality.
        int value = 100;
        for (int i = 0; i < 15; i++) value = value * config.GrievanceRetentionPct / 100;
        Assert.InRange(value, 40, 90);
    }

    [Fact]
    public void HistoryHasLongRangeCausality()
    {
        WorldStats stats = WorldStats.Compute(RunWorld());
        Assert.True(stats.LongestCausalSpan >= 15,
            $"longest causal chain spans only {stats.LongestCausalSpan} years");
    }

    [Fact]
    public void NoPlaceIsHeldByAFactionThatDoesNotExist()
    {
        WorldView view = RunWorld();

        foreach (Place p in view.State.Places)
        {
            if (p.Controller.IsNone) continue;
            Assert.InRange(p.Controller.Index, 1, view.State.Factions.Count);
        }
    }

    [Fact]
    public void StockpilesAndPopulationsNeverGoNegative()
    {
        WorldView view = RunWorld();

        foreach (Place p in view.State.Places)
        {
            Assert.True(p.Population >= 0, $"{p.Name} has negative population");
            foreach (Resource r in Resources.All)
                Assert.True(p.Stockpile[(int)r] >= 0, $"{p.Name} has negative {Resources.Name(r)}");
        }

        foreach (Faction f in view.State.Factions)
        {
            Assert.InRange(f.Legitimacy, 0, 100);
            Assert.True(f.Treasury >= 0);
        }
    }

    [Fact]
    public void TheWorldStillHasPeopleAtTheEnd()
    {
        // The first working build of this engine ended with eight living actors out of thirty-two
        // and three empty thrones. A world that runs out of people stops producing history.
        WorldView view = RunWorld();

        int living = 0;
        foreach (Actor a in view.State.LivingActors()) living++;

        Assert.True(living >= 15, $"only {living} actors alive after 60 years");
    }

    [Fact]
    public void NoIdenticalEventRepeatsYearAfterYear()
    {
        // Repetition is the main way a symbolic history becomes unreadable. Nothing should
        // produce the same sentence in three consecutive years.
        WorldView view = RunWorld();
        Dictionary<string, List<int>> byText = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Significance < Significance.Minor) continue;
            string text = view.Describe(e.Id);
            if (!byText.TryGetValue(text, out List<int>? years)) byText[text] = years = [];
            years.Add(e.Year);
        }

        foreach ((string text, List<int> years) in byText)
        {
            for (int i = 2; i < years.Count; i++)
            {
                Assert.False(years[i] - years[i - 2] <= 2,
                    $"repeated in {years[i - 2]}..{years[i]}: {text}");
            }
        }
    }
}
