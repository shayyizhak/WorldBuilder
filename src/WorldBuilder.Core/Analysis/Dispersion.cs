using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Which dispersion or interval statistic a figure is. There is no zero-valued "unknown" member
/// on purpose: <see cref="Dispersion"/> is a reference type, so an absent one is null and loud
/// rather than a plausible <c>sd=0</c>.
/// </summary>
public enum DispersionKind : byte
{
    /// <summary>Standard deviation, in the data's own units.</summary>
    Sd = 1,

    /// <summary>Coefficient of variation: the standard deviation as a percentage of the mean.</summary>
    Cv = 2,

    /// <summary>Lowest and highest observed.</summary>
    Range = 3,

    /// <summary>A 95% confidence interval.</summary>
    Ci95 = 4,

    /// <summary>Standard deviation squared, in squared units.</summary>
    Variance = 5,

    // No Iqr member. The interquartile range is the obvious fourth kind and nothing in this
    // engine computes one — GeographyAudit says why it reports a coefficient of variation
    // instead — and §4's lesson is that a label with no emitter is worse than a dead branch.
    // Add it here at the same time as the first thing that emits one, not before.
}

/// <summary>
/// A dispersion or interval statistic that carries what kind it is, so it cannot be read as
/// another kind further downstream.
///
/// <b>Third instance of a verdict reported under an ambiguity in an engine figure.</b> A plague
/// duration in two conventions; an unnamed 0-of-13 denominator; and then a pair of figures
/// written as "spread", meaning max − min, carried forward into a decision rule that read them as
/// standard deviations. All three were caught, each by a different route, and every catch came
/// from re-deriving the figure rather than re-reading the sentence.
///
/// The project already held this lesson from the other side — <i>ambiguous engine labels are a
/// fabrication vector independent of the model</i>, filed under rendering as something the engine
/// does to the model. It generalises: <b>an ambiguous figure is a fabrication vector regardless of
/// who reads it next.</b> The reader in the third case was a person and the effect was identical,
/// a plausible conclusion resting on a quantity whose meaning was never pinned.
///
/// So the fix is in the emitter rather than in anyone's discipline, the same argument as the
/// countables lexicon: the failure is silent, and a rule that depends on remembering to write
/// "sd" is a rule with a date on it. <see cref="ToString"/> cannot render without the kind.
///
/// <b>An interval prints both ends and its width.</b> That is the specific ambiguity that
/// happened: a width alone — 38 — reads exactly like a standard deviation, and both readings of
/// the figure were arithmetically sensible. <c>range=[38, 76] width=38</c> cannot be misread.
///
/// A reference type, so an unset one is null. A struct would default to a valid-looking
/// <c>sd=0.00</c>, which is the absent-versus-zero conflation this project has now met in the
/// checker, the query layer, the plot ledger and the ratio metrics.
/// </summary>
public sealed record Dispersion
{
    private Dispersion(DispersionKind kind, double low, double high, int n)
    {
        Kind = kind;
        Low = low;
        High = high;
        N = n;
    }

    public DispersionKind Kind { get; }

    /// <summary>The lower end of an interval, or the figure itself for a single-figure kind.</summary>
    public double Low { get; }

    /// <summary>The upper end of an interval. Equal to <see cref="Low"/> for a single-figure kind.</summary>
    public double High { get; }

    /// <summary>How many observations it was computed over, or 0 where the caller did not say.</summary>
    public int N { get; }

    public bool IsInterval => Kind is DispersionKind.Range or DispersionKind.Ci95;

    /// <summary>How wide the interval is. Meaningful only for an interval kind.</summary>
    public double Width => High - Low;

    /// <summary>
    /// The figure, for the kinds that are one number.
    ///
    /// Throws on an interval rather than returning <see cref="Low"/>, because reading one end of
    /// an interval as though it were the whole statistic is the mistake this type exists to stop.
    /// </summary>
    public double Figure => IsInterval
        ? throw new InvalidOperationException($"{Label} is an interval; read Low and High, or Width.")
        : Low;

    /// <summary>The short name that always precedes the value: <c>sd</c>, <c>cv</c>, <c>range</c>…</summary>
    public string Label => Kind switch
    {
        DispersionKind.Sd => "sd",
        DispersionKind.Cv => "cv",
        DispersionKind.Range => "range",
        DispersionKind.Ci95 => "ci95",
        DispersionKind.Variance => "var",

        // Not a fallback. A kind added without a label would otherwise emit a bare number, which
        // is the entire defect class, so it fails at the point of emission instead.
        _ => throw new InvalidOperationException(
            $"DispersionKind {(int)Kind} has no label; a dispersion figure may not be emitted unlabelled."),
    };

    /// <summary>
    /// The figure with its kind attached. The only way to render one, deliberately: there is no
    /// property that returns the number alone in a form a report could interpolate.
    /// </summary>
    public override string ToString() => Kind switch
    {
        // One decimal always, rather than "0.#". Two coefficients of variation printed as 36% and
        // 35.3% look like figures of different precision and get compared as though one were
        // rounder than the other; these are ranked against each other, so they are formatted alike.
        DispersionKind.Cv => string.Create(CultureInfo.InvariantCulture, $"cv={Low:0.0}%"),

        DispersionKind.Ci95 => string.Create(CultureInfo.InvariantCulture,
            $"ci95=[{Low:+0.00;-0.00}, {High:+0.00;-0.00}]"),

        DispersionKind.Range => string.Create(CultureInfo.InvariantCulture,
            $"range=[{Low:0.##}, {High:0.##}] width={Width:0.##}"),

        _ => string.Create(CultureInfo.InvariantCulture, $"{Label}={Low:0.00}"),
    };

    /// <summary>The same figure padded to a column width, for a table. Still labelled.</summary>
    public string Padded(int width) => ToString().PadRight(width);

    // ---- construction -----------------------------------------------------

    /// <summary>The sample standard deviation of a set of observations, on n−1.</summary>
    public static Dispersion Sd(IReadOnlyList<int> values)
    {
        double sd = SampleSd(values);
        return new Dispersion(DispersionKind.Sd, sd, sd, values.Count);
    }

    /// <summary>A standard deviation the caller has already computed — the t-test needs it anyway.</summary>
    public static Dispersion Sd(double sd, int n = 0) => new(DispersionKind.Sd, sd, sd, n);

    /// <summary>
    /// The coefficient of variation, as a percentage of the mean.
    ///
    /// Dimensionless on purpose, so two populations on different scales can be compared on how
    /// unequal they are rather than on how large.
    /// </summary>
    public static Dispersion Cv(IReadOnlyList<int> values)
    {
        if (values.Count < 2) return new Dispersion(DispersionKind.Cv, 0, 0, values.Count);

        double mean = values.Average();
        return new Dispersion(DispersionKind.Cv,
            mean <= 0 ? 0 : 100 * PopulationSd(values) / mean, 0, values.Count);
    }

    /// <summary>A coefficient of variation the caller has already computed, as a percentage.</summary>
    public static Dispersion Cv(double percent, int n = 0) => new(DispersionKind.Cv, percent, 0, n);

    /// <summary>Lowest and highest, stated as both ends and a width so no end can pass for the whole.</summary>
    public static Dispersion Range(double low, double high, int n = 0) =>
        new(DispersionKind.Range, low, high, n);

    /// <summary>The lowest and highest of a set of observations.</summary>
    public static Dispersion Range(IReadOnlyList<int> values) =>
        values.Count == 0
            ? new Dispersion(DispersionKind.Range, 0, 0, 0)
            : new Dispersion(DispersionKind.Range, values.Min(), values.Max(), values.Count);

    /// <summary>A confidence interval, which is an interval and says so.</summary>
    public static Dispersion Ci95(double low, double high, int n = 0) =>
        new(DispersionKind.Ci95, low, high, n);

    /// <summary>A variance, in squared units, which is why it is never printed beside a mean unlabelled.</summary>
    public static Dispersion Variance(double variance, int n = 0) =>
        new(DispersionKind.Variance, variance, variance, n);

    /// <summary>The variance of a set of observations, on n−1.</summary>
    public static Dispersion Variance(IReadOnlyList<int> values)
    {
        double sd = SampleSd(values);
        return new Dispersion(DispersionKind.Variance, sd * sd, sd * sd, values.Count);
    }

    // ---- arithmetic -------------------------------------------------------

    private static double SampleSd(IReadOnlyList<int> values)
    {
        if (values.Count < 2) return 0;

        double mean = values.Average();
        double sum = 0;
        foreach (int v in values) sum += (v - mean) * (v - mean);
        return Math.Sqrt(sum / (values.Count - 1));
    }

    /// <summary>
    /// The population form, on n rather than n−1.
    ///
    /// Correct for a coefficient of variation over every pair of places in a world: those pairs
    /// are the whole population, not a sample of one, and this is the figure the separation
    /// profile has always reported.
    /// </summary>
    private static double PopulationSd(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return 0;

        double mean = values.Average();
        double sum = 0;
        foreach (int v in values) sum += (v - mean) * (v - mean);
        return Math.Sqrt(sum / values.Count);
    }
}
