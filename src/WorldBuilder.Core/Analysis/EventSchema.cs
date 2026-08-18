namespace WorldBuilder.Core.Analysis;

/// <summary>One field name a consumer read, and whether the emitter has ever written it.</summary>
public sealed record SchemaRead(EventKind Kind, string Field, bool EmittedOnThisKind, bool EmittedAnywhere)
{
    /// <summary>
    /// A name the emitter writes nowhere at all. The <c>loot</c> class: the read cannot ever
    /// succeed, so the assertion built on it cannot ever fail.
    /// </summary>
    public bool DeadRead => !EmittedAnywhere;

    /// <summary>
    /// A real field name, read on a kind that does not carry it.
    ///
    /// Not automatically a defect. A consumer that walks every event and asks each one for
    /// <c>deaths</c> is asking a question most kinds have no answer to, and skipping the nulls is
    /// the correct shape for that. Reported, never asserted on: the assertion is
    /// <see cref="DeadRead"/>, which has no innocent reading.
    /// </summary>
    public bool OffKind => EmittedAnywhere && !EmittedOnThisKind;
}

/// <summary>
/// The emitter's field vocabulary, taken from the emitter.
///
/// <b>The property being asserted: every field name a consumer reads exists in the emitter's
/// vocabulary.</b> Two of the last four defects were field-name mismatches, and the sharper one
/// was inside the independent verifier itself — Layer 4 read <c>took</c>, <c>haul</c> and
/// <c>plunder</c> from raids, the engine writes <c>loot</c>, and the three-way raid split was
/// two-way for as long as the layer had existed with nothing failing.
///
/// <b>From the record, not from documentation.</b> The vocabulary is read off real logs rather
/// than off a declared table, because a declared table is a second thing to keep in step with the
/// rules and the rules are the ones that decide. That makes the vocabulary as complete as the
/// panel it was derived from and no more, which is why the assertion built on it is
/// <see cref="SchemaRead.DeadRead"/> — a name absent from five whole histories is not a rare
/// branch, it is a name nothing writes.
///
/// <b>Structured delta keys reduce to their prefix.</b> <c>pop:p:3</c>, <c>rel:a:1:a:2:Kin</c> and
/// <c>stock:p:7:Grain</c> are one vocabulary entry each — <c>pop</c>, <c>rel</c>, <c>stock</c> —
/// because the tail is an entity id, and a vocabulary that enumerated every id would grow with the
/// world and match nothing.
/// </summary>
public static class EventSchema
{
    /// <summary>
    /// The vocabulary entry a payload key belongs to.
    ///
    /// Deltas carry their target after a colon; plain fields have none. Splitting on the first
    /// colon is what <see cref="EventReducer"/> itself does to route them, so the two agree by
    /// construction rather than by convention.
    /// </summary>
    public static string Name(string key)
    {
        int colon = key.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? key : key[..colon];
    }

    /// <summary>What each event kind writes, across one record.</summary>
    public static Dictionary<EventKind, SortedSet<string>> Emitted(EventLog log)
    {
        Dictionary<EventKind, SortedSet<string>> vocabulary = [];

        foreach (Event e in log.Events)
        {
            if (!vocabulary.TryGetValue(e.Kind, out SortedSet<string>? fields))
                vocabulary[e.Kind] = fields = new SortedSet<string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> kv in e.Data) fields.Add(Name(kv.Key));
        }

        return vocabulary;
    }

    /// <summary>The same, folded across several records, so a panel is one vocabulary.</summary>
    public static Dictionary<EventKind, SortedSet<string>> Emitted(IEnumerable<EventLog> logs)
    {
        Dictionary<EventKind, SortedSet<string>> vocabulary = [];

        foreach (EventLog log in logs)
            foreach ((EventKind kind, SortedSet<string> fields) in Emitted(log))
            {
                if (!vocabulary.TryGetValue(kind, out SortedSet<string>? all))
                    vocabulary[kind] = all = new SortedSet<string>(StringComparer.Ordinal);
                all.UnionWith(fields);
            }

        return vocabulary;
    }

    /// <summary>Every name the emitter writes on any kind.</summary>
    public static SortedSet<string> Anywhere(IReadOnlyDictionary<EventKind, SortedSet<string>> vocabulary)
    {
        SortedSet<string> all = new(StringComparer.Ordinal);
        foreach (SortedSet<string> fields in vocabulary.Values) all.UnionWith(fields);
        return all;
    }

    /// <summary>
    /// Every read a recorder saw, resolved against the vocabulary.
    ///
    /// One row per (kind, field) pair rather than per field, because the same name can be
    /// perfectly good on one kind and meaningless on another, and collapsing that loses the
    /// distinction the report is being written to make.
    /// </summary>
    public static List<SchemaRead> Resolve(
        EventFieldReads reads, IReadOnlyDictionary<EventKind, SortedSet<string>> vocabulary)
    {
        SortedSet<string> anywhere = Anywhere(vocabulary);
        List<SchemaRead> rows = [];

        foreach (EventKind kind in reads.Kinds)
            foreach (string field in reads.On(kind))
            {
                bool onKind = vocabulary.TryGetValue(kind, out SortedSet<string>? fields)
                              && fields.Contains(field);

                rows.Add(new SchemaRead(kind, field, onKind, anywhere.Contains(field)));
            }

        return rows;
    }

    /// <summary>Reads that name a field the emitter writes nowhere. Empty is the only passing answer.</summary>
    public static List<SchemaRead> DeadReads(
        EventFieldReads reads, IReadOnlyDictionary<EventKind, SortedSet<string>> vocabulary) =>
        [.. Resolve(reads, vocabulary).Where(static r => r.DeadRead)];

    /// <summary>The vocabulary as lines, for a report.</summary>
    public static List<string> Render(IReadOnlyDictionary<EventKind, SortedSet<string>> vocabulary)
    {
        List<EventKind> kinds = [.. vocabulary.Keys];
        kinds.Sort();

        List<string> lines = ["| event kind | fields the emitter writes |", "|---|---|"];

        foreach (EventKind kind in kinds)
        {
            SortedSet<string> fields = vocabulary[kind];
            lines.Add($"| {EventKinds.Name(kind)} | {(fields.Count == 0 ? "—" : string.Join(", ", fields))} |");
        }

        return lines;
    }
}
