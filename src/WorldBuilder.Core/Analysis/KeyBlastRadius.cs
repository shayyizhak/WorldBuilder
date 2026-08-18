using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// How many cached renders an inserted event costs, against how many of them are about anything
/// that changed.
///
/// <b>The finding this measures.</b> <see cref="Event.Key"/> exists so that a cached render
/// survives a shift in log position — its own doc comment says so: "stable across re-simulation
/// and independent of position in the log … so a cached render survives a v2 retcon that shifts
/// every downstream <c>Id</c>". That is true of a retcon and false of an insertion, because the
/// key is FNV over (year, kind, participants, <b>sequence</b>) and sequence counts emissions
/// within the year. Insert one event in year 12 and every later event *in year 12* is rekeyed.
///
/// <b>Why it is measured now and not fixed now.</b> It does not bite a ruleset bump: worlds
/// change, so every render regenerates anyway. It bites when a world is *kept* and events are
/// added to it, which is Stage 7's retroactive authoring — already carrying the collision between
/// "cached renders are canon" and "back-propagation rewrites the past". This is a second,
/// independent mechanism by which that cache invalidates for reasons unrelated to content, and
/// the cheap moment to size it is before anything depends on the answer.
///
/// <b>What the number means.</b> A pack's cache key is a hash over the <see cref="Event.Key"/> of
/// every event in it, so a pack loses its identity if *any* of its events was rekeyed — while the
/// only pack whose content actually changed is one that contains the inserted event. The ratio of
/// those two is the waste.
/// </summary>
public static class KeyBlastRadius
{
    public sealed record Report
    {
        public required ulong Seed { get; init; }
        public required int Events { get; init; }
        public required int InsertedAtYear { get; init; }
        public required int InsertedAtIndex { get; init; }

        /// <summary>Events whose key changed although nothing about them did.</summary>
        public required int Rekeyed { get; init; }

        /// <summary>Events in the same year as the insertion — the ceiling on <see cref="Rekeyed"/>.</summary>
        public required int InThatYear { get; init; }

        /// <summary>Events whose content genuinely changed. One, always: the inserted one.</summary>
        public required int Changed { get; init; }

        /// <summary>
        /// Events *before* the insertion point whose recomputed key does not match the stored one.
        ///
        /// Must be zero, and is reported rather than assumed. A non-zero value does not mean the
        /// blast radius is worse; it means this measurement's model of how the key is computed is
        /// wrong, and the figure beside it is not to be believed.
        /// </summary>
        public required int Unexplained { get; init; }

        public int WastePerRealChange => Changed == 0 ? Rekeyed : Rekeyed / Changed;

        public string Line =>
            string.Create(CultureInfo.InvariantCulture,
                $"seed {Seed}: inserting at year {InsertedAtYear} (index {InsertedAtIndex} of {Events}) " +
                $"rekeys {Rekeyed} event(s) — {InThatYear} sit in that year — " +
                $"against {Changed} whose content changed")
            + (Unexplained == 0
                ? ""
                : $"  [{Unexplained.ToString(CultureInfo.InvariantCulture)} UNEXPLAINED — " +
                  "do not believe this row]");
    }

    /// <summary>
    /// Inserts one synthetic event in the middle of the middle year and counts the damage.
    ///
    /// The insertion point is the median year rather than a chosen one, so the figure is a
    /// property of the log's shape and not of where somebody decided to poke it.
    /// </summary>
    public static Report Measure(EventLog log, ulong seed)
    {
        if (log.Count == 0)
        {
            return new Report
            {
                Seed = seed, Events = 0, InsertedAtYear = 0, InsertedAtIndex = 0,
                Rekeyed = 0, InThatYear = 0, Changed = 0, Unexplained = 0,
            };
        }

        int midYear = log.Events[log.Count / 2].Year;

        List<int> inThatYear = [];
        for (int i = 0; i < log.Count; i++)
            if (log.Events[i].Year == midYear) inThatYear.Add(i);

        // Mid-year, so there is something before it as well as after it. An insertion at the start
        // of a year rekeys all of it and would report the ceiling as the finding.
        int at = inThatYear[inThatYear.Count / 2];

        // Recompute every key in that year as it would stand with one more emission before it.
        // Nothing else in the log can move: the key takes no input from any other year.
        int rekeyed = 0, unexplained = 0;
        int sequence = 0;

        foreach (int i in inThatYear)
        {
            Event e = log.Events[i];
            bool after = i >= at;

            if (i == at) sequence++;   // the synthetic event takes this slot

            string wouldBe = EventFactory.ComputeKey(e.Year, e.Kind, e.Participants, sequence);
            sequence++;

            if (wouldBe == e.Key) continue;
            if (after) rekeyed++;
            else unexplained++;
        }

        return new Report
        {
            Seed = seed,
            Events = log.Count,
            InsertedAtYear = midYear,
            InsertedAtIndex = at,
            Rekeyed = rekeyed,
            InThatYear = inThatYear.Count,
            Changed = 1,
            Unexplained = unexplained,
        };
    }
}
