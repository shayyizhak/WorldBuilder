using System.Reflection;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// No dispersion figure reaches a report unlabelled.
///
/// Three assertions, and they catch three different failures. A kind added without a label. A
/// dispersion exposed as a bare number, so a report can interpolate it without its kind. A report
/// line that had the label and lost it in a refactor.
///
/// <b>Why the emitter and not the writer.</b> This is the third verdict this project has reported
/// under an ambiguity in one of its own figures — a plague duration in two conventions, an unnamed
/// 0-of-13 denominator, and a pair of widths written down as "spread" and read as standard
/// deviations. The last one changed which branch of a decision rule applied; both readings
/// happened to abort, so the conclusion survived by luck rather than by construction. A rule that
/// depends on remembering to write "sd" is a rule with a date on it, exactly like a prompt fix.
/// </summary>
public class DispersionTests
{
    /// <summary>
    /// Every kind has a distinct label, and renders with it.
    ///
    /// Exhaustive over the enum, so a kind added without a label fails here rather than emitting a
    /// bare number somewhere in a report. <see cref="Dispersion.Label"/> throws on an unlabelled
    /// kind, which is the loud half; this is the half that finds it before a run does.
    /// </summary>
    [Fact]
    public void EveryDispersionKindCarriesADistinctLabel()
    {
        Dictionary<string, DispersionKind> byLabel = new(StringComparer.Ordinal);

        foreach (DispersionKind kind in Enum.GetValues<DispersionKind>())
        {
            Dispersion one = Make(kind);

            Assert.False(string.IsNullOrWhiteSpace(one.Label), $"{kind} has no label");
            Assert.StartsWith(one.Label + "=", one.ToString(), StringComparison.Ordinal);

            Assert.False(byLabel.TryGetValue(one.Label, out DispersionKind clash),
                $"{kind} and {clash} both render as \"{one.Label}=\"");

            byLabel[one.Label] = kind;
        }

        // The kinds the harness actually emits. Asserted by name so that removing one is a
        // decision rather than a refactor, and so this test cannot pass by finding an empty enum.
        Assert.Equal(
            ["ci95", "cv", "range", "sd", "var"],
            byLabel.Keys.OrderBy(static l => l, StringComparer.Ordinal));
    }

    /// <summary>
    /// An interval states both ends and its width, and a single figure states neither.
    ///
    /// The specific ambiguity: a width of 38, quoted alone, reads exactly like a standard
    /// deviation of 38, and in the case that happened both readings were arithmetically sensible.
    /// </summary>
    [Fact]
    public void AnIntervalCannotBeMistakenForASingleFigure()
    {
        Assert.Equal("range=[38, 76] width=38", Dispersion.Range(38, 76).ToString());
        Assert.Equal("ci95=[-2.70, +1.65]", Dispersion.Ci95(-2.70, 1.65).ToString());
        Assert.Equal("sd=14.17", Dispersion.Sd(14.17).ToString());

        // Reading one end of an interval as the whole statistic is the mistake, so it throws
        // rather than quietly returning the lower bound.
        Assert.Throws<InvalidOperationException>(() => Dispersion.Range(38, 76).Figure);
        Assert.Throws<InvalidOperationException>(() => Dispersion.Ci95(-1, 1).Figure);

        Assert.Equal(14.17, Dispersion.Sd(14.17).Figure, 4);
    }

    /// <summary>
    /// No public member of the harness exposes a dispersion as a bare number.
    ///
    /// The structural half. A labelled <see cref="Dispersion"/> is only worth having if there is
    /// no second route to the figure: a property called <c>SpreadPct</c> returning an int is an
    /// invitation to interpolate it beside a mean, and that is how the emitted figure lost its
    /// kind the first time.
    ///
    /// Scoped to the analysis and checker assemblies, which is where every reported figure is
    /// computed, and to member names that can only mean a dispersion statistic.
    /// </summary>
    [Fact]
    public void NoPublicMemberExposesADispersionAsABareNumber()
    {
        // Words that can only mean a dispersion or interval statistic. "Spread" is included and is
        // the reason this list exists: it meant a coefficient of variation in one place, a max−min
        // width in a report, and the share of the commonest branch in a third. The third is now
        // called skew, and the word is retired.
        //
        // The two-letter abbreviations match a whole camel-case word and not a substring, which is
        // not fussiness: "CrossDomainEdges" and "WarsDeclared" both contain "sd", and a lexicon
        // that flagged them would be turned off within a round. The failure this test exists for
        // is silent, so it has to stay believable.
        string[] words = ["sd", "cv", "iqr", "ci95"];
        string[] substrings =
        [
            "stdev", "stddev", "standarddeviation", "deviation", "variance",
            "quartile", "percentile", "dispersion", "spread",
        ];

        List<string> offenders = [];

        foreach (Type type in Assemblies().SelectMany(static a => a.GetExportedTypes()))
        {
            if (type == typeof(Dispersion) || type == typeof(DispersionKind)) continue;

            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance
                         | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Type? returned = Returned(member);
                if (returned is null || !IsNumeric(returned)) continue;

                string name = member.Name.ToLowerInvariant();
                bool reads = substrings.Any(s => name.Contains(s, StringComparison.Ordinal))
                    || Tokens(member.Name).Any(t => words.Contains(t, StringComparer.Ordinal));

                if (!reads) continue;

                offenders.Add($"{type.Name}.{member.Name} returns {returned.Name}; a dispersion figure must be a Dispersion");
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The report lines the harness produces carry their labels.
    ///
    /// The behavioural half, entered where production enters: the same <c>Line()</c> the panel
    /// prints and the same <see cref="Invariants"/> row <c>wb test dynamics</c> prints. A test
    /// against <see cref="Dispersion.ToString"/> alone would pass while a report interpolated
    /// <c>.Low</c> instead.
    /// </summary>
    [Fact]
    public void ThePanelsContrastLineNamesBothOfItsDispersions()
    {
        Contrast contrast = PairedStats.Compare(
            "geography - redraw",
            [45, 99, 69, 97, 66],
            [62, 116, 58, 83, 52]);

        string line = contrast.Line();

        Assert.Contains("sd=", line, StringComparison.Ordinal);
        Assert.Contains("ci95=[", line, StringComparison.Ordinal);

        // The estimate this contrast's σ was sized against, reproduced from the pre-registration's
        // own five paired differences. It is here because the figure that was misread was this
        // one's neighbour, and a test that pins it makes the arithmetic checkable rather than
        // remembered.
        Assert.Equal(16.48, contrast.Sd.Figure, 2);
        Assert.Equal(1.00, contrast.Mean, 2);
    }

    /// <summary>
    /// The distance-can-vary invariant reports a labelled range.
    ///
    /// It used to print "38–76", which is the form that was read as a spread. Asserted on a real
    /// world through <see cref="Invariants.Check"/>, not on a constructed profile: the figure has
    /// to survive the path that actually prints it.
    /// </summary>
    [Fact]
    public void TheDistanceInvariantReportsALabelledRange()
    {
        Simulation sim = new(42);
        sim.Run(20);

        Invariant vary = Invariants.Check(WorldView.Build(sim.Log, 42))
            .Single(static r => r.Name == "distance can vary");

        Assert.StartsWith("range=[", vary.Measured, StringComparison.Ordinal);
        Assert.Contains("width=", vary.Measured, StringComparison.Ordinal);
        Assert.True(vary.Held, vary.Measured);
    }

    /// <summary>
    /// No invariant reports a figure whose kind a reader has to guess.
    ///
    /// Every measured value is either a count, a percentage that says so, or a labelled
    /// dispersion. Run across the reference panel so this is a property of the reports rather than
    /// of one world.
    /// </summary>
    [Fact]
    public void NoInvariantReportsAnUnlabelledInterval()
    {
        List<string> unlabelled = [];

        foreach (ulong seed in ReferencePanel.Current)
        {
            Simulation sim = new(seed);
            sim.Run(20);

            foreach (Invariant r in Invariants.Check(WorldView.Build(sim.Log, seed)))
            {
                // An en dash between two numbers is the shape a range used to be written in, and
                // the shape nothing may emit now: it is indistinguishable from a subtraction and
                // from a hyphenated single figure.
                int at = r.Measured.IndexOf('–', StringComparison.Ordinal);
                if (at <= 0 || at + 1 >= r.Measured.Length) continue;

                if (char.IsDigit(r.Measured[at - 1]) && char.IsDigit(r.Measured[at + 1]))
                    unlabelled.Add($"seed {seed}: {r.Name} reports \"{r.Measured}\"");
            }
        }

        Assert.True(unlabelled.Count == 0, string.Join("\n  ", unlabelled));
    }

    // ---- plumbing ---------------------------------------------------------

    private static IEnumerable<Assembly> Assemblies() =>
    [
        typeof(Dispersion).Assembly,
        typeof(WorldBuilder.Inference.RuleNames).Assembly,
    ];

    /// <summary>A member name split at its camel-case boundaries, lower-cased.</summary>
    private static IEnumerable<string> Tokens(string name)
    {
        int start = 0;

        for (int i = 1; i <= name.Length; i++)
        {
            if (i < name.Length && !char.IsUpper(name[i])) continue;
            if (i > start) yield return name[start..i].ToLowerInvariant();
            start = i;
        }
    }

    private static Type? Returned(MemberInfo member) => member switch
    {
        PropertyInfo p => p.PropertyType,
        FieldInfo f => f.FieldType,
        MethodInfo m when !m.IsSpecialName => m.ReturnType,
        _ => null,
    };

    private static bool IsNumeric(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(double)
        || type == typeof(float) || type == typeof(decimal) || type == typeof(short);

    private static Dispersion Make(DispersionKind kind) => kind switch
    {
        DispersionKind.Sd => Dispersion.Sd(14.17),
        DispersionKind.Cv => Dispersion.Cv(35.0),
        DispersionKind.Range => Dispersion.Range(38, 76),
        DispersionKind.Ci95 => Dispersion.Ci95(-2.70, 1.65),
        DispersionKind.Variance => Dispersion.Variance(251.86),

        // A kind added to the enum and not to this switch fails here, which is the point: the
        // exhaustiveness check is only exhaustive if constructing every kind is mandatory.
        _ => throw new NotSupportedException($"DispersionKind {kind} has no construction in this test"),
    };
}
