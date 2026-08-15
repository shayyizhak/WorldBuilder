namespace WorldBuilder.Core;

/// <summary>
/// Identifies what a random draw is *for*. Part of the stream key, so a change to how
/// many mortality rolls happen in a year cannot shift the harvest rolls of that same year.
/// Values are explicit and must never be renumbered — they are part of world reproducibility.
/// </summary>
public enum RngPurpose : ulong
{
    Genesis = 1,
    Naming = 2,
    Traits = 3,
    Mortality = 10,
    Birth = 11,
    ComingOfAge = 12,
    Marriage = 13,
    Harvest = 20,
    Disease = 21,
    Trade = 22,
    GoalFormation = 30,
    ActionChoice = 31,
    ActionTarget = 32,
    Succession = 40,
    Coup = 41,
    Unrest = 42,
    Diplomacy = 50,
    Battle = 51,
    Assassination = 52,
    Raid = 53,
}

/// <summary>
/// A deterministic random stream derived from (seed, year, entity, purpose).
///
/// There is deliberately no shared, sequential generator anywhere in the engine. A single
/// global stream would make every draw depend on the exact order and count of every prior
/// draw, so inserting an event into the past — which v2's back-propagation does by design —
/// would re-roll all subsequent history. Deriving an independent stream per call site means
/// a retcon in year 12 leaves year 40's unrelated draws byte-identical.
/// </summary>
public struct Rng
{
    private ulong _state;

    private Rng(ulong state) => _state = state;

    /// <summary>Derive the stream for one entity's draws of one purpose in one year.</summary>
    public static Rng For(ulong seed, int year, EntityId entity, RngPurpose purpose)
    {
        ulong h = Mix(seed ^ 0xA0761D6478BD642FUL);
        h = Mix(h ^ ((ulong)(uint)year * 0x9E3779B97F4A7C15UL));
        h = Mix(h ^ entity.Bits);
        h = Mix(h ^ (ulong)purpose);
        return new Rng(h);
    }

    /// <summary>Derive a world-level stream not attached to any single entity.</summary>
    public static Rng For(ulong seed, int year, RngPurpose purpose) =>
        For(seed, year, EntityId.None, purpose);

    /// <summary>
    /// Derive a sub-stream from this one by a caller-supplied discriminator, without
    /// consuming a draw. Used when one entity needs several independent streams of the
    /// same purpose (e.g. one per candidate heir).
    /// </summary>
    public readonly Rng Branch(long discriminator) =>
        new(Mix(_state ^ ((ulong)discriminator * 0xD6E8FEB86659FD93UL)));

    /// <summary>splitmix64 finalizer — a strong avalanche mix for the stream key.</summary>
    private static ulong Mix(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    public ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, maxExclusive). Rejection-sampled, so no modulo bias.</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 1) return 0;

        ulong bound = (ulong)maxExclusive;
        ulong limit = ulong.MaxValue - (ulong.MaxValue % bound) - 1;
        ulong r;
        do { r = NextUInt64(); } while (r > limit);
        return (int)(r % bound);
    }

    /// <summary>Uniform in [minInclusive, maxExclusive).</summary>
    public int Range(int minInclusive, int maxExclusive) =>
        maxExclusive <= minInclusive ? minInclusive : minInclusive + Next(maxExclusive - minInclusive);

    /// <summary>True with the given probability in percent.</summary>
    public bool Chance(int percent) => Next(100) < percent;

    /// <summary>
    /// True with the given probability in basis points (1/10000). The engine keeps all
    /// probabilities as integers — no float ever enters simulation state.
    /// </summary>
    public bool ChanceBp(int basisPoints) => Next(10_000) < basisPoints;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    /// <summary>
    /// Weighted choice. Weights are clamped at zero; returns -1 if every weight is zero,
    /// which callers treat as "this actor does nothing this year".
    /// </summary>
    public int PickIndexWeighted(ReadOnlySpan<int> weights)
    {
        long total = 0;
        foreach (int w in weights) total += Math.Max(0, w);
        if (total <= 0) return -1;

        long roll = (long)(NextUInt64() % (ulong)total);
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= Math.Max(0, weights[i]);
            if (roll < 0) return i;
        }
        return weights.Length - 1;
    }
}
