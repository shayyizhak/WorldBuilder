namespace WorldBuilder.Core.Analysis;

/// <summary>
/// How a decision that consulted distance would have gone if distance had said nothing.
/// </summary>
/// <param name="Mechanic">Which of the four consumers made it.</param>
/// <param name="Kind">A ranking over candidates, or a single roll against a line.</param>
/// <param name="Candidates">How many options were open. One means there was nothing to choose between.</param>
/// <param name="Range">
/// The proximity range across those options — the width distance had to work with. Zero-width for
/// a roll, and zero-width for a ranking whose candidates all sit the same distance away.
///
/// A <see cref="Dispersion"/> rather than an int width, which is the same figure this field used
/// to hold under the name <c>Spread</c>. Nothing prints it today, and that is exactly when to
/// name it properly: the ambiguity is created where the number is made, not where it is read.
/// </param>
/// <param name="Discriminated">
/// Whether the outcome actually differed with proximity held flat at
/// <see cref="Geography.Geography.Neutral"/>, holding the random draw fixed.
/// </param>
public sealed record DistanceDecision(
    string Mechanic,
    DecisionKind Kind,
    int Candidates,
    Dispersion Range,
    bool Discriminated);

public enum DecisionKind : byte
{
    /// <summary>An argmax or a weighted pick over several candidates.</summary>
    Ranking = 0,

    /// <summary>A single score or chance compared against a line.</summary>
    Roll = 1,
}

/// <summary>
/// What distance actually decided, as opposed to what it was consulted about.
///
/// <b>A counterfactual, not a survey.</b> Counting how often a rule read a proximity says
/// nothing: every raid reads one. The question is how often the answer mattered — how often the
/// decision would have gone the other way had proximity been held flat at 100 — and that can
/// only be answered by evaluating both and comparing.
///
/// It exists because two seeds went the wrong way while three went right, and "the board is too
/// uniform for distance to discriminate" is a hypothesis about this number and nothing else.
/// Without it the question is answered by looking at outcomes and telling a story about them,
/// which is the shape of reasoning this project has spent two phases learning to distrust.
///
/// <b>Instrumentation that changes the world is not instrumentation.</b> Nothing here is read by
/// any rule, the probe is null on an ordinary run, and — the part that actually takes care — no
/// counterfactual draws a random number. Each site was restructured to take its single draw into
/// a variable and compare it against both lines, so attaching a probe cannot move the RNG stream
/// by one call. The same discipline <see cref="PlotLedger"/> is held to, and for the same reason:
/// a measurement that perturbs its subject measures the perturbation.
/// </summary>
public sealed class GeographyProbe
{
    private readonly List<DistanceDecision> _decisions = [];

    public IReadOnlyList<DistanceDecision> Decisions => _decisions;

    /// <summary>Records a decision that ranged over several candidates.</summary>
    /// <param name="nearest">The closest candidate's proximity.</param>
    /// <param name="furthest">The furthest candidate's proximity. Equal to <paramref name="nearest"/>
    /// where every candidate sits the same distance away, which is a real and common case.</param>
    public void Ranked(string mechanic, int candidates, int nearest, int furthest, bool discriminated) =>
        _decisions.Add(new DistanceDecision(mechanic, DecisionKind.Ranking, candidates,
            Dispersion.Range(nearest, furthest, candidates), discriminated));

    /// <summary>Records a decision that compared one figure against a line.</summary>
    public void Rolled(string mechanic, bool discriminated) =>
        _decisions.Add(new DistanceDecision(mechanic, DecisionKind.Roll, 1,
            Dispersion.Range(0, 0, 1), discriminated));

    private readonly Dictionary<string, (int Total, int Absorbed)> _absorption = new(StringComparer.Ordinal);

    /// <summary>
    /// Records whether a clamp downstream of the distance term could absorb it entirely.
    ///
    /// <b>A mechanical property, not a statistical one.</b> "Alliance moved 0 of 13" will not
    /// resolve at any seed count worth spending — at a 6% flip rate, seeing none in thirteen
    /// happens about 45% of the time. So the question is asked of the mechanism instead: given
    /// this evaluation, could <i>any</i> distance value in the world's realised range have
    /// changed the figure that actually gets used? That is a yes or no per evaluation and needs
    /// no sample at all.
    ///
    /// This is the third appearance of the same family — a correct rule whose input never
    /// arrives — after the checker's five and the covert coup's structural zero, and the first on
    /// the mechanics side that an invariant was reporting green.
    /// </summary>
    public void Absorption(string mechanic, bool absorbed)
    {
        (int total, int wasAbsorbed) = _absorption.GetValueOrDefault(mechanic);
        _absorption[mechanic] = (total + 1, wasAbsorbed + (absorbed ? 1 : 0));
    }

    /// <summary>Per mechanic: how many evaluations a clamp could swallow the distance term in.</summary>
    public IReadOnlyDictionary<string, (int Total, int Absorbed)> Absorbed => _absorption;

    /// <summary>Per mechanic, and over the whole run.</summary>
    public List<DiscriminationSummary> Summarise()
    {
        Dictionary<string, (int Total, int Discriminated, int Open)> byMechanic = new(StringComparer.Ordinal);

        foreach (DistanceDecision d in _decisions)
        {
            (int total, int discriminated, int open) = byMechanic.GetValueOrDefault(d.Mechanic);

            // "Open" counts only the decisions distance could have moved: a ranking over one
            // candidate, or one whose candidates are all equidistant, had no room to differ and
            // counting it would dilute the share with decisions nobody could have made otherwise.
            bool couldHaveMattered = d.Kind == DecisionKind.Roll || (d.Candidates > 1 && d.Range.Width > 0);

            byMechanic[d.Mechanic] =
                (total + 1, discriminated + (d.Discriminated ? 1 : 0), open + (couldHaveMattered ? 1 : 0));
        }

        List<DiscriminationSummary> summaries = [];
        foreach ((string mechanic, (int total, int discriminated, int open)) in byMechanic)
            summaries.Add(new DiscriminationSummary(mechanic, total, open, discriminated));

        summaries.Sort(static (a, b) => string.CompareOrdinal(a.Mechanic, b.Mechanic));
        return summaries;
    }

    /// <summary>The panel figure: every mechanic pooled.</summary>
    public DiscriminationSummary Overall()
    {
        int total = 0, open = 0, discriminated = 0;
        foreach (DiscriminationSummary s in Summarise())
        {
            total += s.Consulted;
            open += s.Open;
            discriminated += s.Discriminated;
        }

        return new DiscriminationSummary("all four", total, open, discriminated);
    }
}

/// <param name="Consulted">Decisions that read a proximity at all.</param>
/// <param name="Open">Those distance had any room to move — more than one candidate, at more than one distance.</param>
/// <param name="Discriminated">Those where holding proximity flat changes the outcome.</param>
public sealed record DiscriminationSummary(string Mechanic, int Consulted, int Open, int Discriminated)
{
    /// <summary>
    /// The discriminating share: of the decisions distance could have moved, the share it did.
    ///
    /// Over <see cref="Open"/> rather than <see cref="Consulted"/>, deliberately. A raid on a
    /// house holding one place reads a proximity and had no alternative to weigh; including it
    /// would make the share a measure of how many single-option decisions a world happened to
    /// contain. Both figures are reported so the difference is visible rather than argued about.
    /// </summary>
    public int SharePct => Open == 0 ? 0 : Discriminated * 100 / Open;
}
