using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Positive controls for the Layer 1 metrics that pass at zero.
///
/// A metric asserting absence passes identically whether the engine no longer produces the thing
/// or could never produce it. A survey across the panel cannot separate those two, and the
/// difference is the whole of what the metric is worth — <c>CoupDecidedPct</c> spent months
/// reporting a plausible figure it was structurally incapable of moving.
///
/// So each zero-asserting metric gets a constructed counter-example: a world containing the
/// thing, and an assertion that the detector sees it. This is Layer 2's trick — synthetic
/// fixtures written to contain the construction — applied one layer down.
///
/// The construction is injected into a real simulated log rather than assembled from nothing.
/// A hand-built log with no genesis events is not a world the detector would ever meet, and a
/// control that feeds an input production never produces is the failure this suite exists to
/// catch, one level up.
/// </summary>
public class PositiveControlTests
{
    private static (EventLog Log, WorldState State) Simulated(ulong seed = 42, int years = 50)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return (sim.Log, sim.State);
    }

    /// <summary>An event that cites one predecessor and names one actor, appended to a live log.</summary>
    private static Event Link(EventLog log, EntityId actor, EventId because, int year, EventKind kind)
    {
        EventId id = log.NextId;

        return log.Append(new Event
        {
            Id = id,
            Key = $"control-{id.Value}",
            Year = year,
            Kind = kind,
            Significance = Significance.Major,
            Participants = [new Participant(Role.Subject, actor)],
            Causes = because.IsNone ? [] : [because],
        });
    }

    // ---- single-actor causal chains == 0% ---------------------------------

    /// <summary>
    /// The detector catches a chain that is one actor's own doings, end to end.
    ///
    /// This was a v0 failure: causal depth that looked impressive and turned out to be one
    /// person's biography rather than the world acting on itself. The metric has read 0% ever
    /// since, and until now nothing established that 0% was a finding rather than a blind spot.
    /// </summary>
    [Fact]
    public void ASingleActorChainIsDetected()
    {
        (EventLog log, WorldState state) = Simulated();

        int before = Audit.Compute(WorldView.Build(log, 42)).LifecycleChains;

        // Six events, each caused by the last, every one naming the same actor and nobody else.
        EntityId actor = state.Actors[0].Id;
        EventId previous = EventId.None;

        for (int i = 0; i < 6; i++)
            previous = Link(log, actor, previous, 60 + i, EventKind.PolityCourtsSupport).Id;

        Audit after = Audit.Compute(WorldView.Build(log, 42));

        Assert.True(after.LifecycleChains > before,
            $"a six-deep single-actor chain was appended and the count did not move ({before} -> " +
            $"{after.LifecycleChains})");

        // Deliberately not asserted on the percentage. This control is what showed that the
        // percentage rounds a single lifecycle chain away to 0% — the metric now asserts the
        // count, and asserting the rate here would re-import the defect into its own control.
    }

    /// <summary>
    /// The rounding that hid it, pinned so it cannot come back.
    ///
    /// One lifecycle chain among a hundred and fifty is 0.67%, which integer division reports as
    /// 0%. For as long as the invariant read the percentage, a world containing exactly the
    /// construction the metric forbids reported clean.
    /// </summary>
    [Fact]
    public void ASingleLifecycleChainRoundsAwayInThePercentage()
    {
        (EventLog log, WorldState state) = Simulated();

        EntityId actor = state.Actors[0].Id;
        EventId previous = EventId.None;
        for (int i = 0; i < 6; i++)
            previous = Link(log, actor, previous, 60 + i, EventKind.PolityCourtsSupport).Id;

        Audit after = Audit.Compute(WorldView.Build(log, 42));

        Assert.True(after.LifecycleChains > 0);
        Assert.True(after.DeepChains > 100, $"only {after.DeepChains} deep chains; the rounding needs a big denominator");
        Assert.Equal(0, after.LifecycleChainPct);
    }

    /// <summary>
    /// And the invariant fails when the world contains one, rather than merely reporting it.
    ///
    /// The control has to reach the assertion, not just the counter. A detector that sees the
    /// construction while the invariant goes on passing is the same defect one step later.
    /// </summary>
    [Fact]
    public void TheInvariantFailsOnAWorldContainingASingleActorChain()
    {
        (EventLog log, WorldState state) = Simulated();

        EntityId actor = state.Actors[0].Id;
        EventId previous = EventId.None;
        for (int i = 0; i < 6; i++)
            previous = Link(log, actor, previous, 60 + i, EventKind.PolityCourtsSupport).Id;

        Invariant chains = Invariants.Check(WorldView.Build(log, 42))
            .Single(r => r.Name == "single-actor causal chains");

        Assert.False(chains.Held, "the invariant passed on a world with a single-actor chain in it");
    }

    // ---- dangling causal references == 0 ----------------------------------

    /// <summary>
    /// The detector catches an edge pointing at an event that is not there.
    ///
    /// Already covered from the file side by the corrupted-record test; this is the in-memory
    /// twin, so the control does not depend on the serialiser being the thing under test.
    /// </summary>
    [Fact]
    public void ADanglingCausalEdgeIsDetected()
    {
        (EventLog log, WorldState state) = Simulated();

        Link(log, state.Actors[0].Id, new EventId(999_999), 60, EventKind.PolityCourtsSupport);

        Invariant dangling = Invariants.Check(WorldView.Build(log, 42))
            .Single(r => r.Name == "dangling causal references");

        Assert.False(dangling.Held);
        Assert.NotEqual("0", dangling.Measured);
    }

    // ---- collapses per faction <= 1 ---------------------------------------

    /// <summary>
    /// The detector catches a faction collapsing twice.
    ///
    /// Not a zero-assertion but the same shape: a ceiling nothing can reach is a ceiling that
    /// proves nothing. The zombie-faction bug of v0 was exactly a house collapsing more than
    /// once, and the metric has read 1 or less ever since.
    /// </summary>
    [Fact]
    public void AFactionCollapsingTwiceIsDetected()
    {
        (EventLog log, WorldState state) = Simulated();

        int before = Audit.Compute(WorldView.Build(log, 42)).MaxCollapsesPerFaction;
        EntityId faction = state.Factions[0].Id;

        for (int i = 0; i < 2; i++)
        {
            EventId id = log.NextId;
            log.Append(new Event
            {
                Id = id,
                Key = $"control-collapse-{id.Value}",
                Year = 60 + i,
                Kind = EventKind.PolityCollapse,
                Significance = Significance.Major,
                Participants = [new Participant(Role.Faction, faction)],
            });
        }

        Audit after = Audit.Compute(WorldView.Build(log, 42));

        Assert.True(after.MaxCollapsesPerFaction > before);
        Assert.True(after.MaxCollapsesPerFaction >= 2);

        Invariant collapses = Invariants.Check(WorldView.Build(log, 42))
            .Single(r => r.Name == "collapses per faction");

        Assert.False(collapses.Held);
    }

    // ---- the controls are controls ----------------------------------------

    /// <summary>
    /// Every Layer 1 metric that passes at zero or at a ceiling has a control in this file.
    ///
    /// Listed by name so that adding such a metric without a control is a failing test rather
    /// than an omission nobody notices. A metric asserting absence with nothing to prove the
    /// detector works is a metric that looks verified and is not.
    /// </summary>
    [Fact]
    public void EveryAbsenceAssertingMetricHasAControl()
    {
        string[] controlled =
        [
            "single-actor causal chains",
            "dangling causal references",
            "collapses per faction",
        ];

        List<Invariant> results = Invariants.Check(BaselineWorld.Seed42());

        // The metrics whose pass condition is "nothing happened" — zero, or a ceiling of one.
        List<string> absenceAsserting =
        [
            .. results.Where(r => r.Expected is "0" or "0%" or "<= 1").Select(r => r.Name),
        ];

        Assert.NotEmpty(absenceAsserting);

        foreach (string metric in absenceAsserting)
        {
            Assert.True(controlled.Contains(metric, StringComparer.Ordinal),
                $"'{metric}' passes by absence and has no positive control — it looks verified " +
                "and is not. Add one to PositiveControlTests.");
        }
    }
}
