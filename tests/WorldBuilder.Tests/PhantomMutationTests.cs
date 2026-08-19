using System.Globalization;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Rendering;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Ruleset 8 — a declaration of war stops claiming it broke an alliance that was not there.
///
/// <b>Four properties, and the first is the one that would have caught the defect.</b> The other
/// three say the repair changed the record and nothing else: the same events in the same order with
/// the same payload minus the phantom keys, and a world that has not moved.
///
/// <b>Why the family test comes first.</b> This is the third instance of one shape —
/// <c>GoalBook.Remove</c> notified whether or not the book held the goal, then the relation graph did
/// the same with severances — and a repair of the third instance that leaves the family unmeasured
/// buys nothing. <see cref="MutationAudit"/> probes every mutation key the reducer applies, so the
/// fourth instance fails a test rather than waiting to be read out of a facts sheet.
/// </summary>
public class PhantomMutationTests
{
    private static readonly ulong[] Panel = ReferencePanel.Current;

    private static EventLog Sealed(ulong seed)
    {
        // Ruleset 7 by name, not by Ruleset.Version, for the reason GoalRecordTests states: reading
        // the version makes this silently compare ruleset 8 against itself the moment its own
        // baselines are cut.
        string path = WorldBuilder.Inference.Corpus.SealedWorld("ruleset-7", seed,
                          AppContext.BaseDirectory, Directory.GetCurrentDirectory())
                      ?? throw new FileNotFoundException($"no sealed baselines/ruleset-7/seed-{seed}");

        return JsonlIo.Read(path).Log;
    }

    /// <summary>Whether one payload key is a severance of an alliance — the only key this may drop.</summary>
    private static bool IsAllianceSeverance(string key) =>
        key.StartsWith("relDel:", StringComparison.Ordinal) &&
        key.EndsWith($":{RelationKind.Alliance}", StringComparison.Ordinal);

    // ---- §7.1 the family ---------------------------------------------------

    /// <summary>
    /// No event emits a mutation key for a change the state does not hold.
    ///
    /// <b>The check that covers both instances of the family and any future one.</b> `e:718` claimed
    /// to sever an alliance that had ended eleven years earlier and `e:92` was read as founding one
    /// that was already live; neither was visible from any figure the project computed, because a
    /// severance of an absent edge is dropped in silence and the arithmetic that would have caught it
    /// — created minus ended equals live — holds by construction whatever the labels say.
    ///
    /// <b>Non-vacuous by assertion, not by hope.</b> A site that stopped emitting its keys entirely
    /// would leave a clean audit and an unchanged world, which is the unfalsifiable-check shape this
    /// project has been caught by five times — so the number of keys examined is asserted too, and
    /// so is the presence of every family the reducer knows how to apply.
    ///
    /// <b>A clamped delta is not this defect and is not asserted against.</b> A legitimacy penalty
    /// levied on a house already at zero is a real mutation the floor absorbs; suppressing the key
    /// would delete the record of a penalty that was applied. Counted by the audit, reported, and
    /// deliberately not failed here.
    ///
    /// <b>A key naming the null entity is a different defect, and is bounded rather than failed.</b>
    /// See <see cref="TheOnlyKeysNamingNobodyAreTheTwoAlreadyKnown"/>: <c>leg:-</c> does not parse
    /// into the shape its reducer case wants, so repairing it means guarding where every delta passes
    /// rather than where severances are written, which is wider than ruleset 8's repair. It is
    /// asserted at its exact count so it cannot grow while it waits.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void NoEventClaimsAChangeTheStateDoesNotHold(ulong seed)
    {
        MutationAuditSeed audit = MutationAudit.Run(seed, 50);

        Assert.True(audit.Phantom == 0,
            $"seed {seed.ToString(CultureInfo.InvariantCulture)}: " +
            string.Join("; ", audit.Idle
                .Where(static k => k.Verdict == MutationVerdict.NoReferent)
                .Take(6)
                .Select(static k => $"{k.At} ({EventKinds.Name(k.Kind)}) '{k.Key}' {k.Note}")));

        // The probe looked at something. Without this the assertion above passes on a world whose
        // rules stopped writing payload keys at all.
        Assert.True(audit.Examined > 1000,
            $"only {audit.Examined} mutation key(s) examined; the audit is not covering the record");

        // And it looked at every family the reducer applies, so a family this audit cannot probe
        // cannot hide in it. `relDel` is named explicitly because it is the one that was wrong.
        HashSet<string> saw = [.. audit.Families.Select(static f => f.Family)];
        Assert.Contains("relDel", saw);
        Assert.Contains("rel", saw);
        Assert.Contains("goalEnd", saw);
    }

    /// <summary>
    /// The only mutation keys naming nobody are the two already known, and there are exactly two.
    ///
    /// <b>A bound, not a pass.</b> `LIFE.DEATH_VIOLENT` writes <c>leg:-</c> where the victim belonged
    /// to no house — once on seed 1, once on seed 1234, and nowhere else on the panel. The reducer
    /// drops it in silence because <c>leg:-</c> has two tokens where its case wants three, so it
    /// matches nothing at all: the same emitting-and-ignoring shape ruleset 8 repaired for severances,
    /// and deliberately left for its own change because guarding it belongs where every delta passes
    /// rather than where severances are written — which would put it beyond the reach of the severance
    /// off-switch and cost three earlier rulesets their byte-level reproduction.
    ///
    /// Asserted at the exact figure so the defect cannot spread while it waits, and asserted to be
    /// non-zero so that repairing it fails this test and forces the count down deliberately rather
    /// than letting a fix pass unnoticed.
    /// </summary>
    [Fact]
    public void TheOnlyKeysNamingNobodyAreTheTwoAlreadyKnown()
    {
        List<string> found = [];
        int total = 0;

        foreach (ulong seed in Panel)
        {
            MutationAuditSeed audit = MutationAudit.Run(seed, 50);
            total += audit.Nobody;

            foreach (MutationKey k in audit.Idle)
            {
                if (k.Verdict != MutationVerdict.NoEntity) continue;
                found.Add($"seed {seed.ToString(CultureInfo.InvariantCulture)} {k.At} " +
                          $"{EventKinds.Name(k.Kind)} '{k.Key}'");
            }
        }

        Assert.Equal(2, total);
        Assert.All(found, f => Assert.Contains("leg:-", f));
        Assert.All(found, f => Assert.Contains(EventKinds.Name(EventKind.LifeDeathViolent), f));
    }

    /// <summary>
    /// Every family the reducer can apply is a family the audit can probe.
    ///
    /// Guards the one way <see cref="NoEventClaimsAChangeTheStateDoesNotHold"/> could be quietly
    /// narrowed: a new payload key added to <c>EventReducer.ApplyDeltas</c> and not to
    /// <see cref="MutationAudit.Families"/> would be a mutation site the audit skips in silence,
    /// which is the same defect as the one being repaired, one level up.
    ///
    /// Read out of the reducer's source rather than a second hand-written list, because a
    /// hand-written list is what §4 of the project reference is about.
    /// </summary>
    [Fact]
    public void TheAuditProbesEveryKeyFamilyTheReducerApplies()
    {
        string? source = Find("src/WorldBuilder.Core/EventReducer.cs");
        Assert.NotNull(source);

        HashSet<string> applied = [];
        foreach (string line in File.ReadAllLines(source))
        {
            int at = line.IndexOf("case \"", StringComparison.Ordinal);
            if (at < 0) continue;

            int from = at + 6;
            int to = line.IndexOf('"', from);
            if (to > from) applied.Add(line[from..to]);
        }

        Assert.True(applied.Count > 10, $"only found {applied.Count} key families in the reducer");

        List<string> unprobed =
            [.. applied.Where(k => k != MutationAudit.Creation &&
                                   Array.IndexOf(MutationAudit.Families, k) < 0)];

        Assert.True(unprobed.Count == 0,
            "the reducer applies key families the mutation audit does not probe: " +
            string.Join(", ", unprobed) +
            $" — add them to MutationAudit.Families, or to {nameof(MutationAudit)}.{nameof(MutationAudit.Creation)} " +
            "with the reason they cannot be probed");
    }

    // ---- §1.3 the record loses keys ----------------------------------------

    /// <summary>
    /// Against each sealed ruleset-7 baseline: the same events, in the same order, with the same
    /// shape and the same causes — and the only payload difference is the loss of alliance
    /// severances the graph did not hold.
    ///
    /// <b>The subsequence runs the other way from ruleset 7's.</b> That bump added keys, so the
    /// baseline had to be a subsequence of the new record; this one removes them, so the new record
    /// has to be a subsequence of the baseline. Stated as a subsequence rather than a per-key lookup
    /// for the reason that check already carries: <c>EventDraft.Set</c> appends, one event
    /// legitimately holds the same key twice, and a lookup resolves every occurrence to the first.
    ///
    /// <b>And the omissions are checked, not just counted.</b> A record that dropped a real
    /// severance would satisfy "is a subsequence" perfectly.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void Ruleset8DropsOnlyPhantomSeverancesFromTheRuleset7Record(ulong seed)
    {
        EventLog archived = Sealed(seed);

        Simulation sim = new(seed);
        sim.Run(50);

        // No event added, none removed. Asserted before the walk, because a count mismatch makes
        // every position after it meaningless and the per-event complaints unreadable.
        Assert.Equal(archived.Count, sim.Log.Count);

        List<string> complaints = [];
        int dropped = 0;

        for (int i = 0; i < archived.Count; i++)
        {
            Event was = archived.Events[i];
            Event now = sim.Log.Events[i];

            // Same key means same year, kind, participants and sequence — computed over exactly
            // those and deliberately not over the payload, which is what makes it the alignment.
            if (was.Key != now.Key)
            {
                complaints.Add($"position {i}: event key {was.Key} became {now.Key} " +
                               $"({EventKinds.Name(was.Kind)} Y{was.Year} → " +
                               $"{EventKinds.Name(now.Kind)} Y{now.Year})");
                break;
            }

            if (was.Outcome != now.Outcome || was.Significance != now.Significance ||
                was.Scope != now.Scope || was.Arc != now.Arc)
            {
                complaints.Add($"{now.Id} {EventKinds.Name(now.Kind)} Y{now.Year}: shape moved");
            }

            if (was.Causes.Count != now.Causes.Count)
            {
                complaints.Add($"{now.Id} {EventKinds.Name(now.Kind)} Y{now.Year}: " +
                               $"{was.Causes.Count} cause(s) became {now.Causes.Count}");
            }

            dropped += ComparePayload(was, now, complaints);

            if (complaints.Count > 8) break;
        }

        Assert.True(complaints.Count == 0,
            $"seed {seed.ToString(CultureInfo.InvariantCulture)}: " + string.Join("; ", complaints));

        // Something must actually have been dropped, or this theory is satisfied by a change that
        // did not happen.
        Assert.True(dropped > 0, "no key was dropped at all, so this proves nothing about the repair");
    }

    /// <summary>
    /// Walks the baseline's payload against the new one and returns how many keys were dropped,
    /// complaining about any drop that is not an alliance severance and any reordering.
    /// </summary>
    private static int ComparePayload(Event was, Event now, List<string> complaints)
    {
        int at = 0;
        int dropped = 0;

        foreach (KeyValuePair<string, string> had in was.Data)
        {
            if (at < now.Data.Count && now.Data[at].Key == had.Key)
            {
                if (!string.Equals(now.Data[at].Value, had.Value, StringComparison.Ordinal))
                {
                    complaints.Add($"{now.Id} {EventKinds.Name(now.Kind)} Y{now.Year}: " +
                                   $"'{had.Key}' was '{had.Value}', now '{now.Data[at].Value}'");
                }

                at++;
                continue;
            }

            // Not here: either this key was dropped, or the payload has been reordered. Only one
            // kind of drop is allowed, and reordering is never allowed.
            if (!IsAllianceSeverance(had.Key))
            {
                complaints.Add($"{now.Id} {EventKinds.Name(now.Kind)} Y{now.Year}: lost '{had.Key}' " +
                               $"(value '{had.Value}'), which is not an alliance severance");
                continue;
            }

            if (now.Data.Any(kv => kv.Key == had.Key))
            {
                complaints.Add($"{now.Id} {EventKinds.Name(now.Kind)} Y{now.Year}: '{had.Key}' is " +
                               "still present but has moved — the payload was reordered, not shortened");
                continue;
            }

            dropped++;
        }

        // Nothing new may appear either. This bump adds no keys.
        if (at != now.Data.Count)
        {
            complaints.Add($"{now.Id} {EventKinds.Name(now.Kind)} Y{now.Year}: " +
                           $"{now.Data.Count - at} key(s) were added, and this ruleset adds none");
        }

        return dropped;
    }

    /// <summary>
    /// The log loses keys and the world does not move: the live ruleset-8 world equals the fold of
    /// the sealed ruleset-7 log on all 27 components.
    ///
    /// <b>The half of §1.3 that matters.</b> The key-level property says the record changed in one
    /// specific way; this says the change was not load-bearing anywhere. If a phantom severance had
    /// been holding some later branch in place, this is what would fail, and it would name the
    /// component.
    ///
    /// Folded against this world's own board, never the repository's — five separate sites once
    /// looked up the stored board here and were all correct only because the reference panel shares
    /// one.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void Ruleset8LeavesTheWorldWhereRuleset7LeftIt(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);

        WorldState before = Replay.Fold(Sealed(seed), seed, untilYear: null, board: sim.State.Board);

        List<string> differences = WorldFingerprint.Differences(before, sim.State);

        Assert.True(differences.Count == 0,
            $"seed {seed.ToString(CultureInfo.InvariantCulture)}: " + string.Join("; ", differences));

        // The comparison covered the whole state rather than a subset of it that happens to match.
        Assert.Equal(27, WorldFingerprint.Components(sim.State).Count);
    }

    // ---- §7.3 relation spans -----------------------------------------------

    /// <summary>
    /// No relation span opens after it closes, and no tie is deleted twice without a making between.
    ///
    /// The second clause is what the phantom severance broke in the record, and the reason it is
    /// stated as "without a making between" rather than "twice": a tie may legitimately be struck,
    /// broken, struck again and broken again — the Kebarrow–Griwick alliance does exactly that three
    /// times — and what cannot happen is a second ending with no beginning in front of it.
    ///
    /// Read through the fold, so a severance the graph ignored is not a termination at all. That is
    /// also why this could not have caught `e:718` on its own: a key that deletes nothing produces no
    /// termination to check. It is here for the inverse case, and the audit is here for that one.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void NoSpanOpensAfterItClosesAndNoTieEndsTwiceWithoutAMaking(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);

        RelationTrajectory.Report ties =
            RelationTrajectory.Of(sim.Log, seed, sim.State.Board);

        Assert.NotEmpty(ties.Terminations);

        // Per tie, in log order: each ending must carry a making, and that making must not predate
        // the previous ending of the same tie.
        Dictionary<(EntityId From, EntityId To, RelationKind Kind), int> closedAt = [];

        foreach (Termination t in ties.Terminations)
        {
            var key = (t.From, t.To, t.Kind);

            Assert.True(t.Made is not null,
                $"{t.Kind} {t.From}↔{t.To} ends at {t.At} Y{t.Year} with no making in the record");

            Assert.True(t.Made <= t.Year,
                $"{t.Kind} {t.From}↔{t.To} opens in {t.Made} and closes in {t.Year}");

            if (closedAt.TryGetValue(key, out int previous))
            {
                Assert.True(t.Made >= previous,
                    $"{t.Kind} {t.From}↔{t.To} ends twice with nothing making it between: closed in " +
                    $"{previous}, ends again at {t.At} Y{t.Year} opening in {t.Made}");
            }

            closedAt[key] = t.Year;
        }

        // A world where every tie ends at most once exercises the second clause vacuously. Not
        // asserted as a requirement — it is a property of the seed — but reported so a panel that
        // drifted into that shape is visible in the output rather than silently weaker.
        Assert.True(closedAt.Count <= ties.Terminations.Count);
    }

    // ---- §7.2 the role-and-outcome table -----------------------------------

    /// <summary>
    /// The role-and-outcome table's four columns account for every record naming a person, and the
    /// sponsor's columns split on outcome.
    ///
    /// <b>The partition is the weaker half and it held before the repair too.</b> Three columns also
    /// summed to the record count, which is exactly why the mislabelling survived: *killings they
    /// ordered* pooled a killing with a botched attempt, and no arithmetic could see it. So the
    /// second half of this test re-derives both sponsor columns from the record and requires them to
    /// match — a table whose third column is a total again would pass the sum and fail here.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheRoleAndOutcomeColumnsPartitionTheRecordCount(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);
        WorldView view = WorldView.Build(sim.Log, seed, sim.State.Board);

        List<AttemptTally> attempts = ReferenceSet.Attempts(view);
        Assert.NotEmpty(attempts);

        foreach (AttemptTally t in attempts)
        {
            Assert.True(t.Partitions,
                $"{t.Actor}: {t.Records} record(s) but {t.FailedAgainst} + {t.KilledBy} + " +
                $"{t.Ordered} + {t.OrderedFailed} = " +
                $"{t.FailedAgainst + t.KilledBy + t.Ordered + t.OrderedFailed}");

            // Re-derived from the record, not from the tally that produced the row.
            int ordered = 0, botched = 0;
            foreach (Event e in sim.Log.Events)
            {
                if (e.Kind != EventKind.ConflictAssassination || e.Subject != t.Actor) continue;
                if (e.Outcome == Outcome.Succeeded) ordered++; else botched++;
            }

            Assert.Equal(ordered, t.Ordered);
            Assert.Equal(botched, t.OrderedFailed);
        }

        // And the split is not decorative on this panel: somebody sponsored an attempt that failed,
        // which is the case the pooled column described wrongly.
        Assert.Contains(attempts, static t => t.OrderedFailed > 0);
    }

    private static string? Find(string relative)
    {
        foreach (string from in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (DirectoryInfo? at = new(from); at is not null; at = at.Parent)
            {
                string candidate = Path.Combine(at.FullName, relative);
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }
}
