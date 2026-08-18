using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// One paired contrast, with everything needed to read it honestly.
///
/// The two dispersion figures are <see cref="Dispersion"/> rather than bare doubles, so neither
/// can reach a report without saying which it is. A standard deviation and the half-width of an
/// interval are the same order of magnitude here and were confused once already.
/// </summary>
public sealed record Contrast(string Name, int N, double Mean, Dispersion Sd, double T, double P, Dispersion Ci95)
{
    public double StandardError => N <= 1 ? 0 : Sd.Figure / Math.Sqrt(N);

    /// <summary>Whether the whole confidence interval clears a minimum effect, in either direction.</summary>
    public bool ClearsMde(double mde) => Math.Abs(Mean) >= mde && Ci95.Low * Ci95.High > 0;

    public string Line() =>
        string.Create(CultureInfo.InvariantCulture,
            $"{Name,-24} n={N,4}  mean={Mean,7:+0.00;-0.00}  {Sd.Padded(11)}  " +
            $"{Ci95.Padded(24)}  t={T,6:0.00}  p={P:0.0000}");
}

/// <summary>
/// The arithmetic for a paired comparison, and nothing else.
///
/// Kept in one place and out of the command that prints it, for the reason every figure in this
/// project ends up in one place: a statistic restated at its call site is a statistic that goes
/// stale in one of them. There is no simulation state here and no determinism requirement, so
/// this is the one corner of the engine where doubles are allowed.
///
/// <b>The t distribution is computed rather than approximated.</b> A normal approximation would
/// be within about a percent at these degrees of freedom and it would still be the wrong habit:
/// a p-value that is nearly right is the sort of engine figure nothing questions.
/// </summary>
public static class PairedStats
{
    /// <summary>A paired contrast between two arms measured on the same units.</summary>
    public static Contrast Compare(string name, IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        if (a.Count != b.Count)
            throw new ArgumentException($"{name}: {a.Count} against {b.Count} — a paired contrast needs both arms on the same units.");

        int n = a.Count;
        if (n < 2)
            return new Contrast(name, n, 0, Dispersion.Sd(0, n), 0, 1, Dispersion.Ci95(0, 0, n));

        double[] d = new double[n];
        for (int i = 0; i < n; i++) d[i] = a[i] - b[i];

        double mean = d.Average();
        double variance = 0;
        foreach (double x in d) variance += (x - mean) * (x - mean);
        variance /= n - 1;

        double sd = Math.Sqrt(variance);
        double se = sd / Math.Sqrt(n);
        int df = n - 1;

        double t = se == 0 ? 0 : mean / se;
        double p = se == 0 ? 1 : TwoSidedP(t, df);
        double critical = InverseT(0.975, df);

        return new Contrast(name, n, mean, Dispersion.Sd(sd, n), t, p,
            Dispersion.Ci95(mean - critical * se, mean + critical * se, n));
    }

    /// <summary>
    /// Holm–Bonferroni, in place of a plain Bonferroni: uniformly more powerful and just as
    /// strict on the family error rate. Returns the same contrasts with a verdict per contrast.
    /// </summary>
    public static List<(Contrast Contrast, bool Survives, double Threshold)> Holm(
        IReadOnlyList<Contrast> contrasts, double alpha = 0.05)
    {
        List<Contrast> ordered = [.. contrasts];
        ordered.Sort(static (x, y) => x.P.CompareTo(y.P));

        List<(Contrast, bool, double)> verdicts = [];
        bool stillRejecting = true;

        for (int i = 0; i < ordered.Count; i++)
        {
            double threshold = alpha / (ordered.Count - i);

            // Once one fails, everything above it fails too — that step-down is the whole of
            // Holm, and stopping the cascade is what keeps the family error rate at alpha.
            if (stillRejecting && ordered[i].P > threshold) stillRejecting = false;
            verdicts.Add((ordered[i], stillRejecting, threshold));
        }

        return verdicts;
    }

    /// <summary>Required N per arm for a paired design, given a minimum effect and a σ.</summary>
    public static int RequiredN(double sigma, double mde, double power = 0.80, double alpha = 0.05)
    {
        double z = InverseNormal(1 - alpha / 2) + InverseNormal(power);
        return (int)Math.Ceiling(z * z * sigma * sigma / (mde * mde));
    }

    // ---- distributions ----------------------------------------------------

    private static double TwoSidedP(double t, int df) =>
        IncompleteBeta(df / 2.0, 0.5, df / (df + t * t));

    /// <summary>Student's t quantile, by bisection on its own CDF. Slow and obviously correct.</summary>
    private static double InverseT(double p, int df)
    {
        double low = 0, high = 1000;
        for (int i = 0; i < 200; i++)
        {
            double mid = (low + high) / 2;
            double cdf = 1 - TwoSidedP(mid, df) / 2;
            if (cdf < p) low = mid; else high = mid;
        }
        return (low + high) / 2;
    }

    /// <summary>Acklam's rational approximation to the normal quantile. Ample for sizing a panel.</summary>
    private static double InverseNormal(double p)
    {
        double[] a = [-39.69683028665376, 220.9460984245205, -275.9285104469687,
                      138.3577518672690, -30.66479806614716, 2.506628277459239];
        double[] b = [-54.47609879822406, 161.5858368580409, -155.6989798598866,
                      66.80131188771972, -13.28068155288572];
        double[] c = [-0.007784894002430293, -0.3223964580411365, -2.400758277161838,
                      -2.549732539343734, 4.374664141464968, 2.938163982698783];
        double[] d = [0.007784695709041462, 0.3224671290700398, 2.445134137142996, 3.754408661907416];

        const double split = 0.02425;

        if (p < split)
        {
            double q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (p > 1 - split) return -InverseNormal(1 - p);

        double r = p - 0.5;
        double s = r * r;
        return (((((a[0] * s + a[1]) * s + a[2]) * s + a[3]) * s + a[4]) * s + a[5]) * r /
               (((((b[0] * s + b[1]) * s + b[2]) * s + b[3]) * s + b[4]) * s + 1);
    }

    /// <summary>Regularised incomplete beta, by the standard continued fraction.</summary>
    private static double IncompleteBeta(double a, double b, double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        double front = Math.Exp(
            LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x));

        return x < (a + 1) / (a + b + 2)
            ? front * BetaContinuedFraction(a, b, x) / a
            : 1 - front * BetaContinuedFraction(b, a, 1 - x) / b;
    }

    private static double BetaContinuedFraction(double a, double b, double x)
    {
        const double tiny = 1e-30;
        double qab = a + b, qap = a + 1, qam = a - 1;
        double c = 1, d = 1 - qab * x / qap;

        if (Math.Abs(d) < tiny) d = tiny;
        d = 1 / d;
        double h = d;

        for (int m = 1; m <= 300; m++)
        {
            int m2 = 2 * m;

            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + aa * d;
            if (Math.Abs(d) < tiny) d = tiny;
            c = 1 + aa / c;
            if (Math.Abs(c) < tiny) c = tiny;
            d = 1 / d;
            h *= d * c;

            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + aa * d;
            if (Math.Abs(d) < tiny) d = tiny;
            c = 1 + aa / c;
            if (Math.Abs(c) < tiny) c = tiny;
            d = 1 / d;

            double delta = d * c;
            h *= delta;

            if (Math.Abs(delta - 1) < 1e-14) break;
        }

        return h;
    }

    /// <summary>Lanczos approximation to log Γ.</summary>
    private static double LogGamma(double x)
    {
        double[] g = [76.18009172947146, -86.50532032941677, 24.01409824083091,
                      -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];

        double y = x, tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);

        double series = 1.000000000190015;
        for (int j = 0; j < 6; j++) series += g[j] / ++y;

        return -tmp + Math.Log(2.5066282746310005 * series / x);
    }
}
