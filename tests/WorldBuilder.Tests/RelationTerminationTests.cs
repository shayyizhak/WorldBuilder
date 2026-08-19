using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Rules;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Relations end, and every ending is named.
///
/// <b>What replaces the additive-only assertion.</b> Step one could check its ruleset against
/// sealed baselines event for event, because the world did not move. Step two moves it, so that
/// property is gone and something has to stand in its place — otherwise a ruleset bump becomes a
/// licence for anything at all to have changed. Two things stand in for it here: divergence
/// begins exactly where the new mechanic first acts and nowhere earlier, and no tie disappears
/// without an event saying it did.
/// </summary>
public class RelationTerminationTests
{
    private static readonly ulong[] Panel = ReferencePanel.Sealed;

    public static TheoryData<ulong> Seeds()
    {
        TheoryData<ulong> data = [];
        foreach (ulong seed in Panel) data.Add(seed);
        return data;
    }

    /// <summary>
    /// A ruleset-6 world, for comparison against the ruleset-5 seal.
    ///
    /// <b>Goal recording is switched off, and that is what keeps this file about relations.</b> These
    /// theories anchor on the *ruleset-5* baselines, so every mechanics change since then has to be off
    /// for the comparison to isolate the three termination rules. Ruleset 7 records goal transitions,
    /// which inserts bookkeeping rows the ruleset-5 log cannot have — so with recording left on, these
    /// would fail for a reason that has nothing to do with terminations, which is exactly the confound
    /// the null arm exists to rule out.
    ///
    /// The off-switches composing like this is the point of having them per-mechanic rather than
    /// one "previous ruleset" flag: each later ruleset adds a switch, and a comparison reaches back as
    /// far as the switches it turns off. Ruleset 8 added the third — a severance is now written only
    /// where the tie is live, which drops keys the ruleset-5 log does have — so it is off here too,
    /// and the file stays about relations.
    /// </summary>
    private static EventLog Fresh(ulong seed)
    {
        Simulation sim = new(seed, recordGoals: false, guardSeverances: false);
        sim.Run(50);
        return sim.Log;
    }

    private static EventLog Sealed(string set, ulong seed)
    {
        string path = WorldBuilder.Inference.Corpus.SealedWorld(set, seed,
                          AppContext.BaseDirectory, Directory.GetCurrentDirectory())
                      ?? throw new FileNotFoundException($"no sealed baselines/{set}/seed-{seed}");

        (EventLog archived, ulong archivedSeed) = JsonlIo.Read(path);
        Assert.Equal(seed, archivedSeed);
        return archived;
    }

    /// <summary>
    /// The §5 property: identical to the previous ruleset up to the first termination, divergent
    /// after.
    ///
    /// A failure here is not a failure of the termination rules. It says something *else* moved
    /// the world, which is a finding about the engine and is reported as one — the same shape as
    /// step one's "additive-only failing means emission is drawing from the RNG stream".
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NothingMovesBeforeTheFirstTermination(ulong seed)
    {
        Divergence.Report report = Divergence.Between(
            Sealed("ruleset-5", seed), Divergence.WithoutArmMarker(Fresh(seed)), seed);

        Assert.True(report.FirstTermination >= 0,
            $"seed {seed} never terminates a relation, so there is nothing to anchor divergence to");

        Assert.True(report.Holds, report.Verdict);
    }

    /// <summary>
    /// Every tie that ends does so inside an event that says a tie ended.
    ///
    /// <b>Two shapes count as naming it</b>, and the second is a recorded compromise rather than a
    /// convenience. Either the event carries an <c>endCause</c>, or an event of the kind
    /// <see cref="RelationEnds.Names"/> gives that relation kind stands in the same year naming the
    /// same two parties. The second exists because the alliance deletion was deliberately left on
    /// the war declaration — moving it onto <c>DIPLO.ALLIANCE_BROKEN</c> would change the war's
    /// payload, and the war precedes the break it causes, so the divergence check above would fire
    /// before the first termination on every seed with a war in it.
    ///
    /// What is not allowed is a tie vanishing with nothing in that year saying so, which is the
    /// defect this phase exists to close.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void NoTieEndsWithoutAnEventSayingSo(ulong seed)
    {
        EventLog log = Fresh(seed);
        RelationTrajectory.Report report = RelationTrajectory.Of(log, seed);

        Assert.NotEmpty(report.Terminations);

        foreach (Termination t in report.Terminations)
        {
            if (t.Cause != Termination.Unnamed) continue;

            EventKind? names = RelationEnds.Names(t.Kind);
            Assert.True(names is not null,
                $"seed {seed}: a {t.Kind} tie ended in year {t.Year} at {t.At} and no event kind " +
                "names the end of that relation kind at all");

            bool named = false;
            foreach (Event e in log.Events)
            {
                if (e.Year != t.Year || e.Kind != names) continue;
                if (!Touches(e, t.From) || !Touches(e, t.To)) continue;
                named = true;
                break;
            }

            Assert.True(named,
                $"seed {seed}: a {t.Kind} tie between {t.From} and {t.To} vanished in year " +
                $"{t.Year} inside {EventKinds.Name(t.Via)} ({t.At}) and no " +
                $"{EventKinds.Name(names.Value)} that year says so");
        }

        static bool Touches(Event e, EntityId id)
        {
            foreach (Participant p in e.Participants)
                if (p.Id == id) return true;
            return false;
        }
    }

    /// <summary>
    /// A collapse says how many ties it ended and of what kinds.
    ///
    /// A bare "relations cleared" is an unlabelled figure, and a collapse that silently drops a
    /// dozen edges is the invisible transition this phase exists to repair. Asserted on the
    /// payload, so a renderer that stops printing it cannot make the record lose it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ACollapseSaysWhatItEnded(ulong seed)
    {
        EventLog log = Fresh(seed);
        int checkedAny = 0;

        foreach (Event e in log.Events)
        {
            if (e.Kind != EventKind.PolityCollapse) continue;

            bool severs = false;
            foreach (KeyValuePair<string, string> kv in e.Data)
                if (kv.Key.StartsWith("relDel:", StringComparison.Ordinal)) { severs = true; break; }

            if (!severs) continue;
            checkedAny++;

            Assert.Equal(RelationEnds.Collapse, e.GetString(RelationTrajectory.CauseField));

            int count = e.GetInt("tiesEnded");
            Assert.True(count > 0, $"seed {seed}: {e.Id} severs ties and reports {count}");

            string kinds = e.GetString("tiesEndedKinds") ?? "";
            Assert.False(string.IsNullOrWhiteSpace(kinds),
                $"seed {seed}: {e.Id} reports {count} ties ended and does not say of what kinds");

            // The label has to agree with the total, or it is decoration.
            int labelled = 0;
            foreach (string part in kinds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                labelled += int.Parse(part.Split(':')[^1], System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(count, labelled);
        }

        Assert.True(checkedAny > 0, $"seed {seed}: no collapse ended a single tie");
    }

    /// <summary>
    /// A trade collapse carries its cause, its origin and no dangling reference.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ATradeCollapseSaysWhatEndedIt(ulong seed)
    {
        EventLog log = Fresh(seed);
        int seen = 0;

        foreach (Event e in log.Events)
        {
            if (e.Kind != EventKind.EconomyTradeCollapse) continue;
            seen++;

            string? cause = e.GetString(RelationTrajectory.CauseField);
            Assert.True(cause is RelationEnds.War or RelationEnds.Disuse,
                $"seed {seed}: {e.Id} ended trade for '{cause}', which is not a stated rule");

            Assert.NotEmpty(e.Causes);
            foreach (EventId c in e.Causes)
                Assert.True(log.TryGet(c, out _), $"seed {seed}: {e.Id} cites {c}, which is not there");

            // What was lost, not merely that something was. Read off the edge before the fold
            // removed it, and unrecoverable afterwards.
            Assert.NotNull(e.GetString("made"));
            Assert.NotNull(e.GetString("worth"));
        }

        Assert.True(seen > 0, $"seed {seed}: trade never collapses");
    }

    /// <summary>
    /// A relation kind with no event to name its ending cannot be ended.
    ///
    /// The guard rather than the happy path. The way this defect returns is a rule that deletes an
    /// edge inline because no event existed to carry it, so the helper refuses instead of doing it
    /// quietly — and refuses loudly enough to name the fix.
    /// </summary>
    [Fact]
    public void AKindWithNoNamingEventCannotBeEnded()
    {
        Assert.Null(RelationEnds.Names(RelationKind.Grievance));
        Assert.Null(RelationEnds.Names(RelationKind.Kin));

        Assert.Equal(EventKind.EconomyTradeCollapse, RelationEnds.Names(RelationKind.Trade));
        Assert.Equal(EventKind.DiploAllianceBroken, RelationEnds.Names(RelationKind.Alliance));
    }

    /// <summary>
    /// <b>The null arm reproduces the previous ruleset exactly.</b>
    ///
    /// This is the assertion that makes every other arm's figure mean something, and it is not a
    /// result — it is the check on the instrument. Switching the three termination rules off must
    /// give back the ruleset-5 log event for event, or the switch itself moved the world and an
    /// attribution made with it is worthless. Same reasoning as the identity proximity control,
    /// which exists to prove the substitution machinery consumes nothing from the streams the
    /// rules draw on.
    ///
    /// It also says something about ruleset 6 that nothing else does: **the bump changed nothing
    /// outside these three rules.** Step one could prove that by comparing against sealed
    /// baselines because its worlds did not move; this is how the same claim is made once they do.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void TurningTheTerminationRulesOffGivesBackTheOldRuleset(ulong seed)
    {
        // Every switch off: ruleset 5 is three record-or-mechanics changes back, so reaching it means
        // turning off the termination rules, goal recording *and* the severance guard. See Fresh.
        Simulation sim = new(seed, arm: TerminationArm.None, recordGoals: false,
            guardSeverances: false);
        sim.Run(50);

        EventLog baseline = Sealed("ruleset-5", seed);
        Divergence.Report report = Divergence.Between(
            baseline, Divergence.WithoutArmMarker(sim.Log), seed);

        Assert.Equal(baseline.Count, sim.Log.Count);
        Assert.True(report.FirstDifference < 0,
            $"seed {seed}: the null arm differs from ruleset 5 at index {report.FirstDifference} " +
            "(year " + report.FirstDifferenceYear + "). Something outside the three termination " +
            "rules changed, and every arm attribution is void.");
    }

    /// <summary>
    /// An arm world says it is one, and nothing will seal it.
    ///
    /// A world that ran under a subset of its own ruleset is internally consistent and about
    /// nowhere, and on disk it is a `world-42.jsonl` like any other. That is exactly why the
    /// marking has to be in the file rather than in a habit.
    /// </summary>
    [Fact]
    public void AnArmWorldIsMarkedAsADiagnosticArtefact()
    {
        Simulation sim = new(7, arm: TerminationArm.War);
        sim.Run(10);

        string header = WorldBuilder.Core.Serialization.JsonlIo.Header(
            7, sim.Log.Count, "", TerminationArms.NameOf(TerminationArm.War));

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(header);
        WorldBuilder.Core.Serialization.WorldHeader parsed =
            WorldBuilder.Core.Serialization.WorldHeader.Parse(doc.RootElement);

        Assert.True(parsed.IsArm);
        Assert.True(parsed.IsDiagnostic);
        Assert.Contains("rule arm", parsed.DiagnosticReason, StringComparison.Ordinal);

        // And a real world is not swept up by the same check.
        using System.Text.Json.JsonDocument real =
            System.Text.Json.JsonDocument.Parse(WorldBuilder.Core.Serialization.JsonlIo.Header(7, 10));
        Assert.False(WorldBuilder.Core.Serialization.WorldHeader.Parse(real.RootElement).IsDiagnostic);
    }

    /// <summary>
    /// The timeout is a timeout: nothing is drawn from the stream to decide it.
    ///
    /// Asserted because the divergence check above depends on it. A rule that rolled for
    /// abandonment would shift every later draw in the year and diverge the log from the moment it
    /// first ran rather than from the moment it first fired.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void DisuseCostsNoDraw(ulong seed)
    {
        EventLog log = Fresh(seed);

        foreach (Event e in log.Events)
        {
            if (e.Kind != EventKind.EconomyTradeCollapse) continue;
            if (e.GetString(RelationTrajectory.CauseField) != RelationEnds.Disuse) continue;

            // Twenty years of no dealings, and the record has to be able to show it.
            int held = e.GetInt("held");
            Assert.True(held >= RelationEnds.DisusedAfterYears,
                $"seed {seed}: {e.Id} abandoned a tie held {held} years, under the " +
                $"{RelationEnds.DisusedAfterYears}-year rule");
        }
    }
}
