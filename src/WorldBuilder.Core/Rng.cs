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

    /// <summary>
    /// Choosing which cell of the board a place stands on.
    ///
    /// A purpose of its own rather than another draw on <see cref="Genesis"/>, and the reason is
    /// worth keeping: a stream is keyed on its purpose, so siting the world consumes nothing that
    /// the population, yield and treasury draws were consuming. Every ruleset-3 world therefore
    /// keeps the history it had, and the only thing that changed about it is that its places now
    /// have somewhere to be. That is a checkable claim rather than a hopeful one — and it made
    /// the difference between "geography was added" and "geography was added and everything
    /// moved, for reasons nobody can now separate".
    /// </summary>
    Placement = 4,
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

    /// <summary>
    /// Experimental controls. <b>No rule may ever draw on this.</b>
    ///
    /// A control replaces an input a rule reads and must not disturb the stream that rule is
    /// consuming, or the measured difference is confounded with re-sequencing — which the
    /// constraint above establishes changes worlds on its own. Numbered far from the rule
    /// purposes so the separation is visible at a glance.
    /// </summary>
    Control = 90,
}

/// <summary>
/// A deterministic random stream derived from (seed, year, entity, purpose).
///
/// There is deliberately no shared, sequential generator anywhere in the engine. A single
/// global stream would make every draw depend on the exact order and count of every prior
/// draw, so inserting an event into the past — which v2's back-propagation does by design —
/// would re-roll all subsequent history. Deriving an independent stream per call site means
/// a retcon in year 12 leaves year 40's unrelated draws byte-identical.
///
/// <b>Determinism constraint: draw order within a stream is load-bearing.</b> The per-site
/// keying above bounds the damage but does not remove it — within one stream, the nth draw is
/// still the nth draw. So reproducibility is not a property of the rules alone. It is a property
/// of the rules <i>and the order in which they consume the stream</i>, and a change that alters
/// no logic can still change every world.
///
/// The case that proves it is a short-circuit. A site reading
/// <code>
/// won &amp;&amp; margin &gt; bar &amp;&amp; rng.Chance(p) &amp;&amp; holder == defender
/// </code>
/// throws its die <i>before</i> testing the holder, so a battle on ground the defender had
/// already lost consumes a draw anyway. Hoisting that last test into the guard — obviously
/// equivalent, and what anybody would write — stops the draw in exactly those cases and
/// re-sequences everything after. It was found by hashing the log, not by reading the code, and
/// no test in the suite noticed.
///
/// Two consequences worth carrying: a refactor at a short-circuiting site is a behavioural
/// change until a log hash says otherwise, and any diagnostic that needs a second value must
/// take it from a stream of its own rather than from the one the rule is using — which is what
/// <see cref="WouldPick"/> and <see cref="RngPurpose.Control"/> exist for.
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
    public int PickIndexWeighted(ReadOnlySpan<int> weights) => PickIndexWeighted(weights, out _, out _);

    /// <summary>
    /// Weighted choice, also reporting where in the distribution the draw landed.
    ///
    /// <paramref name="roll"/> and <paramref name="total"/> exist so a counterfactual can ask
    /// what the same draw would have chosen from a different set of weights — the geography probe
    /// re-picks with proximity held flat, at the same relative position, and reports whether the
    /// winner moves. Exposing the draw is what lets that happen without taking a second one, and
    /// a second draw would move the RNG stream and change the world being measured.
    /// </summary>
    public int PickIndexWeighted(ReadOnlySpan<int> weights, out long roll, out long total)
    {
        total = 0;
        foreach (int w in weights) total += Math.Max(0, w);
        if (total <= 0) { roll = 0; return -1; }

        roll = (long)(NextUInt64() % (ulong)total);

        long remaining = roll;
        for (int i = 0; i < weights.Length; i++)
        {
            remaining -= Math.Max(0, weights[i]);
            if (remaining < 0) return i;
        }
        return weights.Length - 1;
    }

    /// <summary>
    /// Which index a draw at the same relative position would have chosen from other weights.
    ///
    /// Takes no draw of its own. "The same relative position" is the only sound way to compare
    /// two weighted picks whose totals differ, and saying so here rather than at the call sites
    /// keeps the counterfactual one definition rather than four.
    /// </summary>
    public static int WouldPick(ReadOnlySpan<int> weights, long roll, long total)
    {
        long other = 0;
        foreach (int w in weights) other += Math.Max(0, w);
        if (other <= 0 || total <= 0) return -1;

        long remaining = roll * other / total;
        for (int i = 0; i < weights.Length; i++)
        {
            remaining -= Math.Max(0, weights[i]);
            if (remaining < 0) return i;
        }
        return weights.Length - 1;
    }
}
