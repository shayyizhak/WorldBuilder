namespace WorldBuilder.Core;

/// <summary>
/// Which payload fields a consumer asked an event for.
///
/// Thread-safe, because the consumers being watched include an async render pipeline and the
/// recorder has to survive a continuation landing on another thread.
/// </summary>
public sealed class EventFieldReads
{
    private readonly Lock _gate = new();
    private readonly Dictionary<EventKind, HashSet<string>> _byKind = [];
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    internal void Note(EventKind kind, string key)
    {
        lock (_gate)
        {
            if (!_byKind.TryGetValue(kind, out HashSet<string>? keys))
                _byKind[kind] = keys = new HashSet<string>(StringComparer.Ordinal);

            keys.Add(key);
            _names.Add(key);
        }
    }

    /// <summary>Every field name any consumer asked for, whatever the kind it asked on.</summary>
    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_gate)
            {
                List<string> names = [.. _names];
                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }
    }

    /// <summary>Field names asked for on one event kind.</summary>
    public IReadOnlyList<string> On(EventKind kind)
    {
        lock (_gate)
        {
            if (!_byKind.TryGetValue(kind, out HashSet<string>? keys)) return [];
            List<string> names = [.. keys];
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }

    public IReadOnlyList<EventKind> Kinds
    {
        get
        {
            lock (_gate)
            {
                List<EventKind> kinds = [.. _byKind.Keys];
                kinds.Sort();
                return kinds;
            }
        }
    }

    public int Count
    {
        get { lock (_gate) return _names.Count; }
    }
}

/// <summary>
/// Records what consumers read out of event payloads, while a recorder is attached.
///
/// <b>Why this is instrumentation rather than a scan.</b> A verifier that reads a field name the
/// engine does not write cannot fail — Layer 4 read <c>took</c>, <c>haul</c> and <c>plunder</c>
/// while the engine wrote <c>loot</c>, so the three-way raid split was two-way from the day it was
/// written and every assertion passed, because every assertion was about the accounting rather
/// than about the values. Two of the last four defects were field-name mismatches.
///
/// The reads all pass through <see cref="Event.GetString"/>, which makes that the one place they
/// can be observed without a consumer having to declare anything. A declared list of reads is a
/// list that goes stale, and a stale list of what a consumer reads is exactly the artefact this
/// defect class is made of.
///
/// <b>It cannot change the world.</b> Nothing here draws from the RNG, writes state or appends to
/// a log, and no recorder is attached unless a caller attaches one; the ordinary path is a single
/// null check on an <see cref="AsyncLocal{T}"/>. Instrumentation invariance is asserted for it
/// anyway rather than argued for, on the standing rule that attaching a measurement must not
/// change the world.
/// </summary>
public static class EventFieldReadLog
{
    private static readonly AsyncLocal<EventFieldReads?> Current = new();

    /// <summary>Attaches a recorder for the lifetime of the returned scope.</summary>
    public static IDisposable Record(EventFieldReads sink)
    {
        EventFieldReads? previous = Current.Value;
        Current.Value = sink;
        return new Scope(previous);
    }

    internal static void Note(EventKind kind, string key) => Current.Value?.Note(kind, key);

    private sealed class Scope(EventFieldReads? previous) : IDisposable
    {
        private bool _closed;

        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            Current.Value = previous;
        }
    }
}
