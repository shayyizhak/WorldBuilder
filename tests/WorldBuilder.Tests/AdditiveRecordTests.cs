using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// <b>Adding a record of something the engine already did must not change what the engine does.</b>
///
/// <c>DIPLO.ALLIANCE_BROKEN</c> is emitted where an alliance was already being destroyed — inside
/// the war declaration's payload, silently, since alliances existed. So the step that adds it has a
/// property no later step in this phase can have: the worlds should be *the same worlds*, and the
/// sealed ruleset-4 baselines are still on disk to check that against. Any change made before this
/// one destroys the opportunity, which is why it is taken alone and first.
///
/// <b>What "unchanged" can mean, and what it cannot.</b> The brief asks that every baseline event
/// appear in the new log unchanged. Taken literally that is unachievable for any insertion, and the
/// reason is worth writing down rather than working around: <see cref="Event.Id"/> is the log
/// position, and <see cref="Event.Key"/> is FNV over (year, kind, participants, <i>sequence</i>)
/// where sequence counts emissions within the year. Inserting one event therefore renumbers every
/// later id and rekeys every later event in that year. Both are artefacts of position, and
/// <see cref="Event.Causes"/> is expressed in the same renumbered ids.
///
/// So the comparison is on world content — year, kind, participants, outcome, scope, significance,
/// origin, arc, payload and witnesses — and the causal edges are compared *through the alignment*,
/// which is stricter than comparing them literally: it demands not merely that a baseline event
/// still cites two things, but that it cites the same two events it always did.
///
/// <b>What a failure would mean.</b> Not that the emitter is wrong. That emission is drawing from
/// the RNG stream, or reaching a rule that scans the log by position — a Stage 3 determinism
/// finding about the engine, reported as such rather than absorbed as a phase failure.
/// </summary>
public class AdditiveRecordTests
{
    private static readonly ulong[] Panel = [7, 42, 99, 1234, 2025];

    /// <summary>
    /// Everything about an event except where it sits in the log.
    ///
    /// Causes are excluded here and checked separately, because they are ids and ids move. Data and
    /// witnesses are in, and they are what make this a real comparison rather than a shape check —
    /// the payload carries every state delta the event applied, so an event whose <c>relDel</c> or
    /// <c>leg:</c> keys moved reads as a different event.
    /// </summary>
    private static string Content(Event e)
    {
        StringBuilder sb = new();
        sb.Append(e.Year).Append('|').Append(EventKinds.Name(e.Kind)).Append('|')
          .Append(e.Significance).Append('|').Append(e.Scope).Append('|')
          .Append(e.Outcome).Append('|').Append(e.Origin).Append('|').Append(e.Arc).Append('|');

        foreach (Participant p in e.Participants) sb.Append(p.Role).Append(':').Append(p.Id).Append(',');
        sb.Append('|');
        foreach (EntityId w in e.Witnesses) sb.Append(w).Append(',');
        sb.Append('|');
        foreach (KeyValuePair<string, string> kv in e.Data) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(',');

        return sb.ToString();
    }

    private static Dictionary<EventId, int> Positions(EventLog log)
    {
        Dictionary<EventId, int> at = [];
        for (int i = 0; i < log.Events.Count; i++) at[log.Events[i].Id] = i;
        return at;
    }

    private static EventLog Fresh(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);
        return sim.Log;
    }

    private static EventLog Sealed(ulong seed)
    {
        string path = WorldBuilder.Inference.Corpus.SealedWorld("ruleset-4", seed,
                          AppContext.BaseDirectory, Directory.GetCurrentDirectory())
                      ?? throw new FileNotFoundException($"no sealed baselines/ruleset-4/seed-{seed}");

        (EventLog archived, ulong archivedSeed) = JsonlIo.Read(path);
        Assert.Equal(seed, archivedSeed);
        return archived;
    }

    /// <summary>
    /// The alignment: baseline index to new-log index, or a failure naming the first divergence.
    ///
    /// A two-pointer walk is the right shape precisely because insertion is the only edit being
    /// permitted. Anything else — a reordering, a changed payload, a dropped event — presents here
    /// as a baseline event that cannot be found before the next one is, and the assertion names the
    /// year and kind rather than reporting that two hashes differ.
    /// </summary>
    private static int[] Align(EventLog baseline, EventLog fresh, out List<Event> inserted)
    {
        int[] map = new int[baseline.Count];
        inserted = [];
        int j = 0;

        for (int i = 0; i < baseline.Count; i++)
        {
            string want = Content(baseline.Events[i]);

            while (j < fresh.Count && Content(fresh.Events[j]) != want)
            {
                inserted.Add(fresh.Events[j]);
                j++;
            }

            Assert.True(j < fresh.Count,
                $"baseline event {i} ({baseline.Events[i].Year}, " +
                $"{EventKinds.Name(baseline.Events[i].Kind)}) has no match in the new log — " +
                "the world moved, this is not an additive record change");

            map[i] = j;
            j++;
        }

        for (; j < fresh.Count; j++) inserted.Add(fresh.Events[j]);
        return map;
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(99UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheRecordGrowsAndTheWorldDoesNot(ulong seed)
    {
        EventLog baseline = Sealed(seed);
        EventLog fresh = Fresh(seed);

        int[] map = Align(baseline, fresh, out List<Event> inserted);

        // Every insertion is the one kind this step adds. Without this the alignment would accept
        // a world that had gained a battle as readily as one that had gained a record.
        foreach (Event e in inserted)
            Assert.True(e.Kind == EventKind.DiploAllianceBroken,
                $"unexpected insertion in year {e.Year}: {EventKinds.Name(e.Kind)}");

        Assert.Equal(baseline.Count + inserted.Count, fresh.Count);
    }

    /// <summary>
    /// Every baseline event still cites the same events it cited, through the renumbering.
    ///
    /// The half that would otherwise go unchecked. Content equality says each event still carries
    /// the payload it carried; this says the causal graph over those events is the same graph, which
    /// is the part every downstream chain measurement actually reads.
    /// </summary>
    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(99UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheCausalGraphOverTheBaselineEventsIsUnchanged(ulong seed)
    {
        EventLog baseline = Sealed(seed);
        EventLog fresh = Fresh(seed);

        int[] map = Align(baseline, fresh, out _);
        Dictionary<EventId, int> oldAt = Positions(baseline);
        Dictionary<EventId, int> newAt = Positions(fresh);

        for (int i = 0; i < baseline.Count; i++)
        {
            Event was = baseline.Events[i];
            Event now = fresh.Events[map[i]];

            List<int> expected = [];
            foreach (EventId c in was.Causes)
                expected.Add(oldAt.TryGetValue(c, out int at) ? map[at] : -1);

            List<int> actual = [];
            foreach (EventId c in now.Causes)
                actual.Add(newAt.TryGetValue(c, out int at) ? at : -1);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// The emitter fires, and fires on every seed.
    ///
    /// Otherwise both theories above are satisfied by an emitter that never runs — the exact shape
    /// this project has now met often enough to test for by reflex.
    /// </summary>
    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(99UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheAllianceBreakIsRecordedOnEverySeed(ulong seed)
    {
        EventLog fresh = Fresh(seed);

        List<Event> breaks = [];
        foreach (Event e in fresh.Events)
            if (e.Kind == EventKind.DiploAllianceBroken) breaks.Add(e);

        Assert.NotEmpty(breaks);

        foreach (Event e in breaks)
        {
            // Record-only. A state delta here would mean the deletion had moved off the war, and
            // the war's payload is what the baselines hold.
            Assert.DoesNotContain(e.Data, kv => kv.Key.StartsWith("rel", StringComparison.Ordinal));
            Assert.True(e.Arc.IsNone, "the break is not part of the war's arc");

            // The war that caused it always resolves; the pact that created it is cited when it
            // resolves and omitted when it does not, never guessed.
            Assert.NotEmpty(e.Causes);
            foreach (EventId c in e.Causes) Assert.True(fresh.TryGet(c, out _), "dangling cause");
        }
    }
}
