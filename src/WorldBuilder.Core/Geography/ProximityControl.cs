namespace WorldBuilder.Core.Geography;

/// <summary>What a world's rules are told when they ask how far apart two things are.</summary>
public enum ProximityControlKind : byte
{
    /// <summary>The board. The only setting a real world runs under.</summary>
    None = 0,

    /// <summary>
    /// The board, routed through the control machinery.
    ///
    /// Exists solely to be asserted equal to <see cref="None"/>. If a world under this setting
    /// hashes identically to one without a control at all, then attaching a control consumes
    /// nothing from the rules' own streams, and any later difference is attributable to the
    /// values rather than to re-sequencing. Without that check every control result is
    /// confounded, and the confounding is invisible.
    /// </summary>
    Identity = 1,

    /// <summary>
    /// Every distance answered as exactly typical.
    ///
    /// Reproduces pre-geography behaviour on a ruleset-4 build, because every consumer multiplies
    /// by a proximity and divides by a hundred — so a hundred everywhere leaves each of them
    /// computing what it computed before distance existed. That is asserted rather than argued:
    /// on the reference panel it returns the ruleset-3 figures exactly.
    ///
    /// It exists so the no-distance arm of a comparison can be run <i>on the same board, the same
    /// build and the same seed</i> as the others, instead of against an archived measurement from
    /// a different binary. A contrast whose arms came from different builds is a contrast with a
    /// second variable in it.
    /// </summary>
    Flat = 4,

    /// <summary>
    /// A fresh draw per question, from the world's own realised distribution of proximities.
    ///
    /// Same distribution, same clamp exposure, <b>no stability and no spatial structure</b>. Two
    /// houses are neighbours this year and strangers the next. This is the falsifier: if the
    /// causal-variety gain survives it, the gain was never about geography or about stability —
    /// it was about perturbation, and any noise of similar size would have done it.
    /// </summary>
    Redraw = 2,

    /// <summary>
    /// One draw per unordered pair of places, fixed at worldgen and stable for the run.
    ///
    /// Same distribution, same clamp exposure, <b>stable heterogeneity with no spatial
    /// structure</b>. Distances are consistent but they do not obey a triangle inequality and
    /// they do not come from anywhere. If the gain survives this, the mechanism is stability
    /// rather than geometry, and geography is one implementation of it rather than its source.
    /// </summary>
    Shuffle = 3,
}

/// <summary>
/// A synthetic replacement for the one distance function, for separating explanations that make
/// the same prediction.
///
/// <b>Why this exists.</b> Geography raised causal variety and cleared the repeat rate, and two
/// mechanisms explain that equally well: distance makes which neighbour you fight a stable fact
/// and stable facts let chains grow long; or thirty-four early flips cascade and any perturbation
/// of similar size would do the same. Both predict a rise. Both predict the repeat rate clearing.
/// A pre-registered prediction that was confirmed does not separate them, and that is the whole
/// reason these controls are worth building — direction matching is not evidence between
/// mechanisms that both predict the direction.
///
/// <b>Every control draws from a stream of its own.</b> <see cref="RngPurpose.Control"/>,
/// crystallised from the world seed, the site and a per-site decision counter. It never touches
/// the streams the rules consume, because re-sequencing those changes worlds on its own and the
/// difference would then be unattributable.
///
/// <b>A control world is a diagnostic artefact, not a world.</b> It is marked in the world file's
/// header and in its genesis event, and <c>wb baseline cut</c> refuses one. If a control world
/// can be mistaken for a real one on disk, that is a defect.
/// </summary>
public sealed class ProximityControl
{
    private readonly ProximityControlKind _kind;
    private readonly ulong _seed;
    private readonly int[] _empirical;
    private readonly Dictionary<long, int> _fixed = [];
    private readonly Dictionary<int, int> _counters = [];

    /// <param name="empirical">
    /// The proximities this world's places actually present to a rule — every unordered pair of
    /// sited places. Drawing from this rather than from a uniform range is what keeps the control
    /// honest: same distribution, same exposure to the clamps downstream, so the only thing that
    /// changed is where the values come from.
    /// </param>
    public ProximityControl(ProximityControlKind kind, ulong seed, IReadOnlyList<int> empirical)
    {
        _kind = kind;
        _seed = seed;
        _empirical = empirical.Count > 0 ? [.. empirical] : [Geography.Neutral];
        Array.Sort(_empirical);
    }

    public ProximityControlKind Kind => _kind;

    /// <summary>The name carried in the world header, and the empty string for a real world.</summary>
    public static string NameOf(ProximityControlKind kind) => kind switch
    {
        ProximityControlKind.None => "",
        ProximityControlKind.Identity => "identity",
        ProximityControlKind.Redraw => "redraw",
        ProximityControlKind.Flat => "flat",
        _ => "shuffle",
    };

    public static ProximityControlKind Parse(string name) => name.ToLowerInvariant() switch
    {
        "" or "none" => ProximityControlKind.None,
        "identity" => ProximityControlKind.Identity,
        "redraw" => ProximityControlKind.Redraw,
        "shuffle" => ProximityControlKind.Shuffle,
        "flat" => ProximityControlKind.Flat,
        _ => throw new FormatException(
            $"unknown proximity control '{name}' — try identity, flat, redraw or shuffle."),
    };

    /// <summary>
    /// What to tell a rule that asked about these two cells, given what the board would have said.
    ///
    /// <paramref name="site"/> separates the streams of the different call sites so that two
    /// mechanics asking in the same year do not receive correlated answers.
    /// </summary>
    public int Substitute(int site, int cellA, int cellB, int real)
    {
        switch (_kind)
        {
            case ProximityControlKind.Identity:
                return real;

            case ProximityControlKind.Flat:
                return Geography.Neutral;

            case ProximityControlKind.Shuffle:
            {
                // One value per unordered pair, decided the first time it is asked for and
                // thereafter remembered. Deciding lazily rather than at worldgen keeps the table
                // the size of what is actually consulted, and the key is order-independent so
                // asking the other way round cannot produce a different answer — a distance that
                // is not symmetric is the defect the board itself is verified against.
                long key = cellA <= cellB
                    ? ((long)cellA << 32) | (uint)cellB
                    : ((long)cellB << 32) | (uint)cellA;

                if (_fixed.TryGetValue(key, out int held)) return held;

                Rng rng = Rng.For(_seed, 0, EntityId.None, RngPurpose.Control).Branch(key);
                return _fixed[key] = _empirical[rng.Next(_empirical.Length)];
            }

            case ProximityControlKind.Redraw:
            {
                // A fresh answer every time, so nothing about the world is stable. The counter
                // is per site and advances only here, on a stream no rule can reach.
                int n = _counters.GetValueOrDefault(site);
                _counters[site] = n + 1;

                Rng rng = Rng.For(_seed, site, EntityId.None, RngPurpose.Control).Branch(n);
                return _empirical[rng.Next(_empirical.Length)];
            }

            default:
                return real;
        }
    }

    /// <summary>How many answers each site has been given. Reported, so a control that never
    /// fired cannot be mistaken for one that did.</summary>
    public IReadOnlyDictionary<int, int> Consulted => _counters;
}
