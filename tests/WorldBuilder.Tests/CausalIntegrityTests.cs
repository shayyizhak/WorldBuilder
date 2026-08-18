using WorldBuilder.Core;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The causal graph has to be sound, because three later features read it and none of them can
/// repair it: v1 assembles render context by walking causes, v2 back-propagates along them, and
/// <c>why</c> is nothing but a traversal. A broken edge here is invisible until it is expensive.
/// </summary>
public class CausalIntegrityTests
{
    private static EventLog Run(ulong seed = 42, int years = 60)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return sim.Log;
    }

    /// <summary>
    /// Events that are *decided* must say what prompted them. Events that merely happen —
    /// dying of old age, a plague arriving, two people marrying — may legitimately have no
    /// parent, and inventing one for them is what corrupted the causal graph in the first
    /// place. An empty cause list is honest; a fabricated edge is not.
    /// </summary>
    private static bool MustHaveCause(EventKind kind) => kind switch
    {
        EventKind.GenesisWorld or EventKind.GenesisPlace or EventKind.GenesisFaction
            or EventKind.GenesisActor => false,
        EventKind.LifeBirth or EventKind.LifeComingOfAge or EventKind.LifeMarriage
            or EventKind.LifeDeathNatural => false,
        EventKind.EconomyYield or EventKind.EconomyPlague => false,
        _ => true,
    };

    [Fact]
    public void EveryDecidedEventHasACause()
    {
        EventLog log = Run();
        List<string> orphans = [];

        foreach (Event e in log.Events)
        {
            if (e.Significance < Significance.Minor) continue;
            if (!MustHaveCause(e.Kind)) continue;
            if (e.Causes.Count > 0) continue;
            orphans.Add($"{e.Id} [Y{e.Year}] {EventKinds.Name(e.Kind)}");
        }

        Assert.True(orphans.Count == 0,
            $"{orphans.Count} decided event(s) have no recorded cause: {string.Join(", ", orphans.Take(10))}");
    }

    [Fact]
    public void MarriagesAreNotCausedByOtherPeoplesMarriages()
    {
        // Each cross-faction match strengthens the alliance edge between the two houses, so
        // citing that edge's latest cause chained one wedding to the last — couples with no
        // person in common, linked into long runs that inflated apparent causal depth.
        EventLog log = Run();

        foreach (Event e in log.Events)
        {
            if (e.Kind != EventKind.LifeMarriage) continue;

            HashSet<EntityId> here = [];
            foreach (Participant p in e.Participants)
                if (p.Id.Kind == EntityKind.Actor) here.Add(p.Id);

            foreach (EventId cause in e.Causes)
            {
                Event parent = log.Get(cause);
                if (parent.Kind != EventKind.LifeMarriage) continue;

                bool shared = false;
                foreach (Participant p in parent.Participants)
                    if (p.Id.Kind == EntityKind.Actor && here.Contains(p.Id)) shared = true;

                Assert.True(shared, $"marriage {e.Id} cites marriage {cause} with no person in common");
            }
        }
    }

    [Fact]
    public void NobodyIsBornBecauseSomebodyDied()
    {
        // A regression guard on the specific class of fabricated edge that made causal depth
        // look real: a political or military decision citing an unrelated life event that
        // merely happened to be the last thing touching that faction.
        EventLog log = Run();

        foreach (Event e in log.Events)
        {
            if (e.Kind is not (EventKind.ConflictRaid or EventKind.ConflictBattle
                or EventKind.DiploWarDeclared or EventKind.DiploInsult
                or EventKind.PolityLegitimacyCrisis or EventKind.EconomyTradePact))
            {
                continue;
            }

            foreach (EventId cause in e.Causes)
            {
                EventKind parent = log.Get(cause).Kind;
                Assert.False(
                    parent is EventKind.LifeBirth or EventKind.LifeComingOfAge or EventKind.LifeDeathNatural,
                    $"{EventKinds.Name(e.Kind)} {e.Id} cites {EventKinds.Name(parent)} {cause} as its cause");
            }
        }
    }

    [Fact]
    public void CausesAlwaysPointBackwardsInTime()
    {
        // Guarantees the graph is acyclic without needing a traversal: an event may only cite
        // parents that already exist, so cycles are structurally impossible.
        foreach (Event e in Run().Events)
        {
            foreach (EventId cause in e.Causes)
            {
                Assert.False(cause.IsNone);
                Assert.True(cause.Value < e.Id.Value, $"{e.Id} cites {cause}, which is not older than it.");
            }
        }
    }

    /// <summary>
    /// Every cause must resolve inside the file that carries it. Fails the build rather than
    /// warning: a log with holes in its causal graph silently corrupts everything downstream —
    /// depth metrics measured with unresolvable edges quietly dropped, and a renderer handed a
    /// reference it cannot look up.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheWrittenLogHasNoDanglingCauses(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);

        string path = Path.Combine(Path.GetTempPath(), $"wb-ri-{Guid.CreateVersion7()}.jsonl");
        try
        {
            WorldBuilder.Core.Serialization.JsonlIo.Write(path, sim.Log, seed);
            (EventLog reloaded, _) = WorldBuilder.Core.Serialization.JsonlIo.Read(path);

            List<string> dangling = [];
            foreach (Event e in reloaded.Events)
                foreach (EventId cause in e.Causes)
                    if (!reloaded.TryGet(cause, out _)) dangling.Add($"{e.Id} -> {cause}");

            Assert.True(dangling.Count == 0,
                $"{dangling.Count} dangling causes: {string.Join(", ", dangling.Take(8))}");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CausesAndParticipantsAllResolve()
    {
        EventLog log = Run();
        Simulation sim = new(42);
        sim.Run(60);
        WorldState state = sim.State;

        foreach (Event e in log.Events)
        {
            foreach (EventId cause in e.Causes) Assert.True(log.TryGet(cause, out _));

            foreach (Participant p in e.Participants)
            {
                int limit = p.Id.Kind switch
                {
                    EntityKind.Actor => state.Actors.Count,
                    EntityKind.Place => state.Places.Count,
                    EntityKind.Faction => state.Factions.Count,
                    EntityKind.Arc => state.Arcs.Count,
                    _ => 0,
                };
                Assert.InRange(p.Id.Index, 1, limit);
            }
        }
    }

    [Fact]
    public void ArcReferencesResolve()
    {
        Simulation sim = new(42);
        sim.Run(60);

        foreach (Event e in sim.Log.Events)
            if (!e.Arc.IsNone) Assert.InRange(e.Arc.Index, 1, sim.State.Arcs.Count);
    }

    [Fact]
    public void WitnessesAreRecordedForTheKnowledgeLayerToUseLater()
    {
        // v0 never reads these. v3 cannot reconstruct them after the fact, which is the only
        // reason they are populated now — so a regression that silently stops writing them
        // would not surface until the feature that needs them is being built.
        int witnessed = 0;
        foreach (Event e in Run().Events)
            if (e.Witnesses.Count > 0) witnessed++;

        Assert.True(witnessed > 100, $"only {witnessed} events recorded witnesses");
    }

    [Fact]
    public void SecretsAreSeenByFewerPeopleThanPublicEvents()
    {
        int secret = 0, secretWitnesses = 0;
        int publicCount = 0, publicWitnesses = 0;

        foreach (Event e in Run().Events)
        {
            if (e.Scope == Visibility.Secret) { secret++; secretWitnesses += e.Witnesses.Count; }
            else if (e.Scope == Visibility.Public && e.Significance >= Significance.Minor)
            {
                publicCount++;
                publicWitnesses += e.Witnesses.Count;
            }
        }

        Assert.True(secret > 0 && publicCount > 0);
        Assert.True(secretWitnesses / secret < publicWitnesses / publicCount,
            "secret events should have a narrower audience than public ones");
    }
}
