using System.Text;

namespace WorldBuilder.Inference;

/// <summary>One scope's verdict and the record of how it was reached.</summary>
/// <param name="Scope">
/// What was checked. A section heading on the chronicle path; the question itself on the answer
/// path, because an answer's scope is the thing it was asked.
/// </param>
public sealed record FindingScope(string Scope, IReadOnlyList<Fabrication> Findings, Coverage Coverage)
{
    /// <summary>
    /// True where the whole scope was kept out of canon, which makes every blocking finding in it
    /// fatal. The chronicle's case: a section with no verified account.
    /// </summary>
    public bool Excluded { get; init; }

    /// <summary>
    /// Individual findings that were acted on, where the scope as a whole survived. The answer
    /// path's case: an answer has one answer and nowhere to put a warning, so a fatal finding
    /// costs the prose rather than the scope.
    /// </summary>
    public IReadOnlySet<Fabrication>? Fatal { get; init; }

    internal bool IsFatal(Fabrication f) =>
        (Excluded && f.BlocksCanon) || (Fatal?.Contains(f) ?? false);
}

/// <summary>
/// Findings and coverage as JSON, in one shape for both paths.
///
/// The chronicle has emitted this since v1. The answer path emitted nothing at all, and the cost
/// of that is on the record: <c>departure</c> extraction went 4 → 0 between two v1.2 rounds and
/// nothing caught it, because there was no machine-readable block to diff. The chronicle path had
/// one, and diffing it is the only reason the v1 sidecar drift was ever visible.
///
/// One writer rather than two, so the two paths cannot drift into different shapes and a golden
/// diff can read both with one parser. A shape that exists twice is a shape that will disagree
/// with itself eventually.
/// </summary>
public static class FindingsSidecar
{
    public static void Write(string path, IReadOnlyList<FindingScope> scopes)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Json(scopes));
    }

    /// <summary>
    /// Findings as JSON, one record per check that fired, plus what every rule read.
    ///
    /// The inert entries matter as much as the findings. A rule that extracted nothing is
    /// recorded rather than omitted, because zero here means nothing on its own and a great deal
    /// beside last round's six — which is the comparison the golden layer exists to make.
    /// </summary>
    public static string Json(IReadOnlyList<FindingScope> scopes)
    {
        StringBuilder sb = new();
        sb.Append("{\n  \"findings\": [\n");
        bool first = true;

        foreach (FindingScope scope in scopes)
        {
            List<Fabrication> all = [.. scope.Findings, .. scope.Coverage.Inert()];

            foreach (Fabrication f in all)
            {
                if (!first) sb.Append(",\n");
                first = false;

                sb.Append("    {\"rule\":").Append(Quote(f.Kind))
                  .Append(",\"scope\":").Append(Quote(scope.Scope))
                  .Append(",\"span\":").Append(Quote(f.Token))
                  .Append(",\"detail\":").Append(Quote(f.Context))
                  .Append(",\"blocking\":").Append(f.BlocksCanon ? "true" : "false")
                  .Append(",\"fatal\":").Append(scope.IsFatal(f) ? "true" : "false")
                  .Append('}');
            }
        }

        sb.Append("\n  ],\n  \"scopes\": [\n");

        for (int i = 0; i < scopes.Count; i++)
        {
            FindingScope scope = scopes[i];
            if (i > 0) sb.Append(",\n");

            sb.Append("    {\"scope\":").Append(Quote(scope.Scope)).Append(",\"coverage\":{");

            IReadOnlyDictionary<string, RuleCounts> rules = scope.Coverage.Rules;
            bool firstRule = true;

            foreach (string name in scope.Coverage.Names)
            {
                RuleCounts counts = rules[name];
                if (!firstRule) sb.Append(',');
                firstRule = false;

                sb.Append(Quote(name))
                  .Append(":{\"extracted\":").Append(counts.Extracted)
                  .Append(",\"checked\":").Append(counts.Checked)
                  .Append(",\"unresolvable\":").Append(counts.Unresolvable)
                  .Append(",\"fired\":").Append(counts.Fired)
                  .Append(",\"accounted\":").Append(counts.Accounted ? "true" : "false");

                // Why, where anything was dropped. The count says a rule discarded a third of
                // what it read; only the reason says whether that is prose it should leave alone
                // or a resolution it should have managed.
                IReadOnlyList<(string Reason, int Count)> why = scope.Coverage.Reasons(name);

                if (why.Count > 0)
                {
                    sb.Append(",\"unresolved\":{");
                    for (int r = 0; r < why.Count; r++)
                    {
                        if (r > 0) sb.Append(',');
                        sb.Append(Quote(why[r].Reason)).Append(':').Append(why[r].Count);
                    }
                    sb.Append('}');
                }

                sb.Append('}');
            }

            sb.Append("}}");
        }

        sb.Append("\n  ]\n}\n");
        return sb.ToString();
    }

    /// <summary>
    /// One scope per question.
    ///
    /// A chronicle section and an answer are not the same kind of thing, and the difference shows
    /// here: a section that fails is dropped from the document with a note in its place, so its
    /// findings are fatal to the scope. An answer has one answer and nowhere to put a warning, so
    /// a fatal finding is recorded against the finding rather than against the question.
    /// </summary>
    public static List<FindingScope> ForAnswers(IReadOnlyList<QuerySuite.Scored> scored)
    {
        List<FindingScope> scopes = [];

        foreach (QuerySuite.Scored s in scored)
        {
            // A scope here is an answer the checker read. A refusal, a rejected premise and an
            // empty result are all sentences the engine wrote itself from the records, and no
            // rule ever ran on them — their coverage is empty, and entering them would put
            // fourteen zeroes in the file for prose no rule was offered. Zero extraction is the
            // signal that a rule never saw its input, and spending it on rules that were never
            // given one makes it worth less everywhere.
            //
            // Nothing hides: a question that stops being answered drops out of the sidecar
            // entirely, and a missing scope is louder in a golden diff than a row of zeroes.
            if (s.Result.Fabrication.Coverage.Names.Count == 0) continue;

            scopes.Add(new FindingScope(
                s.Question.Text,
                s.Result.Fabrication.Findings,
                s.Result.Fabrication.Coverage)
            {
                Fatal = s.Result.Fatal.Count == 0 ? null : new HashSet<Fabrication>(s.Result.Fatal),
            });
        }

        return scopes;
    }

    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
