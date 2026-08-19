using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Rendering;
using WorldBuilder.Core.Rules;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Ruleset 7 — <c>GoalBook</c> enters the fold.
///
/// Three assertions, from §3 of <c>docs/brief-goalbook-phase-2.md</c>, and they are three because no
/// two of them can substitute for each other:
///
/// <list type="number">
/// <item><b>Key-level record extension</b> — the simulation did not change. Every event of every
/// sealed ruleset-6 baseline still appears in the same order with every payload key it had, and the
/// only new events are the two goal kinds. This is the replacement for additive-only, which cannot
/// hold once transitions ride existing events as new keys.</item>
/// <item><b>Replay reproduces goals</b> — the deliverable. It is asserted in
/// <see cref="ReplayTests"/> because that is where the property lives.</item>
/// <item><b>The off-switch</b> — switch goal recording off and the ruleset-6 logs come back event for
/// event and key for key. Stronger than instrumentation invariance: that says a measurement did not
/// disturb the world, this says a mechanics change touched nothing outside its own rules.</item>
/// </list>
/// </summary>
public class GoalRecordTests
{
    private static readonly ulong[] Panel = ReferencePanel.Current;

    /// <summary>The two kinds ruleset 7 adds, and the only events allowed to be new.</summary>
    private static bool IsGoalRow(Event e) =>
        e.Kind is EventKind.GoalsFormed or EventKind.GoalsEnded;

    private static string Hash(IEnumerable<Event> events)
    {
        StringBuilder sb = new();
        foreach (Event e in events) sb.Append(JsonlIo.Serialise(e)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static EventLog Sealed(ulong seed)
    {
        // Ruleset 6 by name, not by Ruleset.Version: this test compares against the previous
        // ruleset's seal for as long as it exists, and reading the version would make it silently
        // start comparing ruleset 7 against itself the moment the new baselines are cut.
        string path = WorldBuilder.Inference.Corpus.SealedWorld("ruleset-6", seed,
                          AppContext.BaseDirectory, Directory.GetCurrentDirectory())
                      ?? throw new FileNotFoundException($"no sealed baselines/ruleset-6/seed-{seed}");

        return JsonlIo.Read(path).Log;
    }

    // ---- §3.3 the off-switch -----------------------------------------------

    /// <summary>
    /// Goal recording off reproduces ruleset 6 exactly — every event, every key, every byte.
    ///
    /// The strong form of the standing rule. Note what it rules out that a weaker check would not:
    /// the rules were restructured at forty-six call sites, the perception phase now batches its
    /// decisions through a cap test it did not previously make for itself, and the challenge site had
    /// a transition moved earlier in the tick. Any one of those could have changed a draw order or a
    /// refusal, and none of them shows up in a test that only looks at goals.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TurningGoalRecordingOffGivesBackRuleset6(ulong seed)
    {
        // Ruleset 8's severance guard is off too. Reaching a ruleset-6 log means turning off every
        // record change since then, which is the design the per-mechanic switches were chosen for:
        // a comparison reaches back as far as the switches it turns off.
        Simulation sim = new(seed, recordGoals: false, guardSeverances: false);
        sim.Run(50);

        EventLog archived = Sealed(seed);

        // The arm marker is stripped, and only it. An arm world stamps `arm=record-no-goals` into its
        // genesis event by design — the whole point of the marking — so comparing it verbatim would
        // fail on the one difference that is supposed to be there. Same strip the termination arms use.
        EventLog mine = Divergence.WithoutArmMarker(sim.Log);

        Assert.Equal(archived.Count, mine.Count);
        Assert.Equal(Hash(archived.Events), Hash(mine.Events));

        // Byte equality above is the strong claim; the divergence report is what says *where* if it
        // ever stops holding, and a hash alone would only say "somewhere".
        Divergence.Report report = Divergence.Between(archived, mine, seed);
        Assert.True(report.FirstDifference < 0,
            $"seed {seed.ToString(CultureInfo.InvariantCulture)}: goal recording switched off still " +
            $"differs from ruleset 6 at index {report.FirstDifference.ToString(CultureInfo.InvariantCulture)} " +
            $"(year {report.FirstDifferenceYear.ToString(CultureInfo.InvariantCulture)}). Something " +
            "outside goal recording changed, and the record-extension claim is void.");
    }

    /// <summary>
    /// An arm world says so, in the header and in the record, and a baseline cut refuses it.
    ///
    /// Without this the off-switch is a way to produce a diagnostic artefact that looks exactly like a
    /// history — the failure mode the proximity control and the termination arms are both marked
    /// against, and the header's own comment warns that a second flag beside <c>IsControl</c> leaves
    /// every existing check answering the narrower question.
    /// </summary>
    [Fact]
    public void AWorldThatDidNotRecordItsGoalsIsMarkedDiagnostic()
    {
        Simulation off = new(42, recordGoals: false);
        off.Run(5);

        Assert.Equal(GoalRecord.OffArm, off.Log.Events[0].GetString("arm"));

        WorldHeader header = WorldHeader.ForThisBuild(42, off.Log.Count) with
        {
            Arm = TerminationArms.NameOf(off.Arm, off.RecordsGoals),
        };

        Assert.True(header.IsDiagnostic);
        Assert.Contains(GoalRecord.OffArm, header.DiagnosticReason, StringComparison.Ordinal);

        // And a real world is not marked, which is the half that fails if the flag is inverted.
        Simulation on = new(42);
        on.Run(5);

        Assert.Null(on.Log.Events[0].GetString("arm"));
        Assert.False((WorldHeader.ForThisBuild(42, on.Log.Count) with
        {
            Arm = TerminationArms.NameOf(on.Arm, on.RecordsGoals),
        }).IsDiagnostic);
    }

    // ---- §3.1 key-level record extension -----------------------------------

    /// <summary>
    /// Ruleset 7 is a record extension, stated at the level of payload keys.
    ///
    /// <b>Why not additive-only.</b> Step one could say "the diff is insertions only" because
    /// <c>DIPLO.ALLIANCE_BROKEN</c> was a whole new event beside an unchanged one. Goal transitions
    /// ride their host events as new keys, so existing events genuinely change content and the old
    /// form of the claim is unavailable. What survives is stricter than "something changed" and weaker
    /// than byte-identity: same events, same order, every old key present and equal, new keys allowed,
    /// and no new events except the two goal kinds.
    ///
    /// <b>Compared through the alignment, not by position.</b> Inserting a <c>GOALS.FORMED</c> row
    /// renumbers every later id and rekeys the rest of that year, so <see cref="Event.Id"/> and
    /// <see cref="Event.Key"/> are excluded by construction — the alignment walks both logs and skips
    /// the new rows, which is the only comparison an insertion can survive.
    ///
    /// <b>Ruleset 8's severance guard is switched off, because this theory is about 6 → 7.</b> That
    /// bump only added keys, which is what "every old key present and equal" asserts; ruleset 8
    /// removes some, so leaving its switch on would make this fail for a reason from a later ruleset
    /// and would take a proven property of 6 → 7 off the board with it. Ruleset 8's own key-level
    /// property lives in <c>PhantomMutationTests</c> and runs the subsequence the other way.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void Ruleset7ExtendsTheRuleset6RecordAndChangesNoKeyInIt(ulong seed)
    {
        EventLog archived = Sealed(seed);

        Simulation sim = new(seed, guardSeverances: false);
        sim.Run(50);

        List<string> complaints = [];
        int old = 0;
        int inserted = 0;

        foreach (Event now in sim.Log.Events)
        {
            if (IsGoalRow(now)) { inserted++; continue; }

            if (old >= archived.Count)
            {
                complaints.Add($"ruleset 7 has an extra {EventKinds.Name(now.Kind)} in Y{now.Year} " +
                               "that is not a goal row");
                break;
            }

            Event was = archived.Events[old++];

            if (was.Kind != now.Kind || was.Year != now.Year)
            {
                complaints.Add($"at baseline position {old}: was {EventKinds.Name(was.Kind)} " +
                               $"Y{was.Year}, now {EventKinds.Name(now.Kind)} Y{now.Year}");
                break;
            }

            // Same participants, same outcome, same significance, same visibility, same arc.
            if (Shape(was) != Shape(now))
                complaints.Add($"{EventKinds.Name(was.Kind)} Y{was.Year}: shape moved");

            // Every key the baseline carried is still there, with the same value, in the same order.
            // New keys may be interleaved anywhere — that is what "extension" means.
            //
            // <b>Stated as a subsequence, not as a lookup per key, and the difference is not
            // pedantic.</b> `EventDraft.Set` appends rather than replaces, so one event legitimately
            // carries the same key more than once — an `ECONOMY.YIELD` moves grain into and out of the
            // same store — and a per-key lookup resolves every occurrence to the first. The first
            // version of this check did exactly that and reported five seeds' worth of changed yield
            // figures on a record that had not changed at all. A subsequence walk cannot make that
            // mistake, because it never looks a key up.
            if (!IsSubsequence(was.Data, now.Data, out string why))
                complaints.Add($"{EventKinds.Name(was.Kind)} Y{was.Year}: {why}");

            // Causes, through the alignment: the same count and the same relative targets. Ids
            // renumber on insertion, so what is compared is which baseline position each cause
            // points at.
            if (was.Causes.Count != now.Causes.Count)
            {
                complaints.Add($"{EventKinds.Name(was.Kind)} Y{was.Year}: " +
                               $"{was.Causes.Count} cause(s) became {now.Causes.Count}");
            }

            if (complaints.Count > 8) break;
        }

        Assert.True(complaints.Count == 0,
            $"seed {seed.ToString(CultureInfo.InvariantCulture)}: " + string.Join("; ", complaints));

        // The baseline must be fully consumed, or ruleset 7 dropped events rather than adding them.
        Assert.Equal(archived.Count, old);

        // And something must actually have been inserted, or this theory is satisfied by a change
        // that did not happen — the shape of a test that reports coverage it does not have.
        Assert.True(inserted > 0, "no goal rows were inserted at all");
    }

    private static string Shape(Event e)
    {
        StringBuilder sb = new();
        sb.Append(e.Kind).Append('|').Append(e.Year).Append('|')
          .Append(e.Outcome).Append('|').Append(e.Significance).Append('|')
          .Append(e.Scope).Append('|').Append(e.Arc);

        foreach (Participant p in e.Participants) sb.Append('|').Append(p.Role).Append(':').Append(p.Id);
        return sb.ToString();
    }

    /// <summary>
    /// Whether every pair of <paramref name="old"/> appears in <paramref name="now"/>, in order.
    ///
    /// Compared pair by pair rather than key by key, so duplicate keys are handled and no read goes
    /// through <see cref="Event.GetString"/> — which would report every one of them to
    /// <see cref="EventFieldReadLog"/> and fill the schema sweep's record with reads no consumer makes.
    /// </summary>
    private static bool IsSubsequence(
        IReadOnlyList<KeyValuePair<string, string>> old,
        IReadOnlyList<KeyValuePair<string, string>> now,
        out string why)
    {
        int at = 0;

        foreach (KeyValuePair<string, string> want in old)
        {
            bool found = false;
            while (at < now.Count)
            {
                KeyValuePair<string, string> here = now[at++];
                if (here.Key != want.Key) continue;               // an inserted key: skip it

                if (!string.Equals(here.Value, want.Value, StringComparison.Ordinal))
                {
                    why = $"'{want.Key}' was '{want.Value}', now '{here.Value}'";
                    return false;
                }

                found = true;
                break;
            }

            if (!found)
            {
                why = $"lost key '{want.Key}' (value '{want.Value}')";
                return false;
            }
        }

        why = "";
        return true;
    }

    // ---- §1.2 the count agrees with the label ------------------------------

    /// <summary>
    /// Every <c>GOALS.ENDED</c> and <c>GOALS.FORMED</c> row's total agrees with its own breakdown, and
    /// with the number of goal keys it carries.
    ///
    /// <c>ACollapseSaysWhatItEnded</c>'s assertion, for the new rows. A bare total is an unlabelled
    /// figure, and this project treats an unlabelled figure as a fabrication vector regardless of who
    /// reads it next; a breakdown nothing checks is decoration that will drift from the total it is
    /// supposed to explain.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void EveryGoalRowsCountAgreesWithItsBreakdown(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);

        int formed = 0, ended = 0;

        foreach (Event e in sim.Log.Events)
        {
            if (!IsGoalRow(e)) continue;

            bool forming = e.Kind == EventKind.GoalsFormed;
            string countKey = forming ? GoalRecord.FormedCount : GoalRecord.EndedCount;
            string labelKey = forming ? GoalRecord.FormedKinds : GoalRecord.EndedReasons;
            string keyPrefix = forming ? "goalAdd:" : "goalEnd:";

            int total = e.GetInt(countKey, -1);
            Assert.True(total > 0, $"{EventKinds.Name(e.Kind)} Y{e.Year} has no '{countKey}'");

            int fromLabel = 0;
            string label = e.GetString(labelKey) ?? "";
            foreach (string part in label.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] half = part.Split(':');
                Assert.Equal(2, half.Length);
                fromLabel += int.Parse(half[1], CultureInfo.InvariantCulture);
            }

            int keys = 0;
            foreach (KeyValuePair<string, string> kv in e.Data)
                if (kv.Key.StartsWith(keyPrefix, StringComparison.Ordinal)) keys++;

            Assert.Equal(total, fromLabel);
            Assert.Equal(total, keys);

            if (forming) formed++; else ended++;
        }

        // Both kinds must actually occur, or the assertion above is vacuous for one of them.
        Assert.True(formed > 0, "no GOALS.FORMED rows");
        Assert.True(ended > 0, "no GOALS.ENDED rows");
    }

    /// <summary>
    /// Every <see cref="GoalEnd"/> has a route, and each route is reachable.
    ///
    /// The compile-time guard is <see cref="GoalRecord.Route"/>'s exhaustive switch — a new label with
    /// no route fails the build. This is the other half: that the three routes are not a taxonomy with
    /// an unused branch. Reachability is checked against the panel for the two that fire on it, and
    /// against the routing table for every label, so a label whose route this panel never exercises is
    /// reported by the audit as unexercised rather than passing as covered.
    /// </summary>
    [Fact]
    public void EveryRouteIsReachedAndEveryEndingHasOne()
    {
        Dictionary<GoalRecord.GoalRoute, int> declared = [];
        foreach (GoalEnd why in Enum.GetValues<GoalEnd>())
        {
            GoalRecord.GoalRoute route = GoalRecord.Route(why);
            declared[route] = declared.GetValueOrDefault(route) + 1;
        }

        foreach (GoalRecord.GoalRoute route in Enum.GetValues<GoalRecord.GoalRoute>())
            Assert.True(declared.GetValueOrDefault(route) > 0, $"no GoalEnd routes {route}");

        // And the panel reaches all three in practice.
        GoalCensus census = new();
        Simulation sim = new(42);
        sim.State.Goals.Watch = census;
        sim.Run(50);

        HashSet<GoalRecord.GoalRoute> exercised = [];
        foreach (GoalEnd why in census.Ended.Keys) exercised.Add(GoalRecord.Route(why));

        foreach (GoalRecord.GoalRoute route in Enum.GetValues<GoalRecord.GoalRoute>())
            Assert.Contains(route, exercised);
    }

    /// <summary>
    /// A goal key naming a goal the book does not hold is refused, not skipped.
    ///
    /// The runtime half of the guard, and it is load-bearing rather than decorative: it is what caught
    /// the defection site double-recording an ending the reducer already owned. A fold that shrugged at
    /// an unknown goal id would have produced a world internally consistent and about somebody else.
    /// </summary>
    [Fact]
    public void AFoldRefusesAGoalKeyItCannotResolve()
    {
        Simulation sim = new(42);
        sim.Run(10);

        Event real = sim.Log.Events[0];
        Event bogus = EventFactory.Create(
            id: new EventId(1),
            year: real.Year,
            kind: EventKind.GoalsEnded,
            participants: [],
            data: [new KeyValuePair<string, string>("goalEnd:99999", nameof(GoalEnd.Expired))]);

        WorldState fresh = new() { Seed = 42 };

        InvalidOperationException thrown =
            Assert.Throws<InvalidOperationException>(() => EventReducer.Apply(fresh, bogus));

        Assert.Contains("99999", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("diverged", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The record's own census of goal transitions equals the live one, on every seed.
    ///
    /// <see cref="ReplayTests"/> asserts the resulting state matches. This asserts the *path* matched:
    /// the same creations, the same advances, the same endings under the same labels. A record could
    /// in principle arrive at the right final book by a different route — an ending recorded under the
    /// wrong reason, an advance folded twice and another dropped — and end-state equality would not
    /// see it.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheFoldMakesTheSameTransitionsTheRulesDid(ulong seed)
    {
        GoalAuditSeed audit = GoalAudit.Run(seed, 50);

        Assert.Equal(audit.Live.Created, audit.Folded.Created);
        Assert.Equal(audit.Live.Advanced, audit.Folded.Advanced);
        Assert.Equal(audit.Live.Ended, audit.Folded.Ended);
        Assert.Equal(audit.Live.Attachments, audit.Folded.Attachments);

        // No removal may name a goal that has already gone. Fifteen did at ruleset 6, and all
        // fifteen were counted as endings by an audit that could not tell.
        Assert.Equal(0, audit.Live.TotalVanished);
        Assert.Equal(0, audit.Folded.TotalVanished);

        // And the census is not empty, so none of the above is satisfied by two zeroes.
        Assert.True(audit.Live.TotalCreated > 0);
        Assert.True(audit.Live.TotalEnded > 0);
    }
}
