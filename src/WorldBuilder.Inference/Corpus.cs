using System.Text.Json;
using System.Text.Json.Serialization;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>One hand-verified fabrication that reached canon, with the correct answer known.</summary>
public sealed record CorpusCase
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("seed")] public ulong Seed { get; init; } = 42;

    /// <summary>The chronicle heading the passage came from, or null for a text-only case.</summary>
    [JsonPropertyName("scope")] public string? Scope { get; init; }

    [JsonPropertyName("passage")] public string Passage { get; init; } = "";
    [JsonPropertyName("expect_rule")] public string ExpectRule { get; init; } = "";
    [JsonPropertyName("expect_span")] public string ExpectSpan { get; init; } = "";

    /// <summary>The same passage written true. Must fire nothing.</summary>
    [JsonPropertyName("corrected")] public string Corrected { get; init; } = "";

    /// <summary>
    /// Whether the passage is a whole section rather than an excerpt of one.
    ///
    /// Only the completeness rules read it, and only two rows are about completeness. Every
    /// other row is three sentences lifted out of a section and must not be asked whether it
    /// told the whole story.
    /// </summary>
    [JsonPropertyName("whole_section")] public bool WholeSection { get; init; }

    [JsonPropertyName("note")] public string Note { get; init; } = "";
}

/// <summary>What running one case produced.</summary>
public sealed record CorpusResult(CorpusCase Case, bool Fired, bool CorrectedClean, string Detail)
{
    public bool Passed => Fired && CorrectedClean;
}

/// <summary>
/// Layer 3: eleven rounds of hand review, made into a test.
///
/// This is the most valuable thing the project has produced and until now it lived only in a
/// conversation. Each row is a false sentence that was rendered, accepted, and only later found
/// by a person reading carefully — with the true answer beside it.
///
/// Both halves of a row are load-bearing. The passage must fire its rule, or the checker has a
/// hole. The corrected passage must fire nothing, or the checker has a false positive, and a
/// checker that flags everything costs the chronicle real content until people stop reading it.
/// Round 10 excluded seven correct sections from canon and that is the failure this half exists
/// to prevent.
///
/// Rows 10, 25 and 26 were each fixed once and came back. They are the reason the corpus is a
/// file on disk rather than a memory of a review.
/// </summary>
public static class Corpus
{
    /// <summary>
    /// The finding kinds that satisfy each rule named in the corpus.
    ///
    /// The corpus names rules the way a reader does — "this is a date error" — while the checker
    /// emits the specific kind it happened to reach the conclusion by. One conceptual rule can
    /// be reached several ways, and which way is an implementation detail the corpus must not be
    /// coupled to, or every refactor rewrites 31 files.
    /// </summary>
    private static readonly Dictionary<string, string[]> Families = new(StringComparer.Ordinal)
    {
        ["succession"] = ["never-held-the-seat", "false-succession", "wrong-seat", "unshared-pair"],
        ["outcome"] = ["wrong-direction", "wrong-role", "wrong-ender", "hedged-outcome", "unshared-pair"],
        ["tenure"] = ["wrong-seat", "never-held-the-seat", "missing-ruler", "no-such-event", "relative-time", "outside-the-window"],
        ["departure"] = ["wrong-fate"],
        ["action"] = ["no-such-event", "wrong-collapse", "wrong-direction", "unsupported-manner"],
        ["quantity"] = ["vague-quantity", "count-vs-narration", "count-vs-list", "partition-sum", "no-such-event", "wrong-scope-total", "outside-the-reign"],
        ["date"] = ["wrong-year", "date-disagreement", "relative-time"],
        ["duplicate"] = ["event-told-twice"],
        ["coverage"] = ["incomplete-enumeration"],
        ["particular"] = ["invented-particular", "forty-nine", "unsupported-manner", "vague-quantity"],
        ["ordering"] = ["out-of-order"],
        ["1.1"] = ["hedged-exhaustive-list", "count-vs-list", "count-vs-narration", "incomplete-enumeration"],
        ["1.4"] = ["self-contradiction", "wrong-killer"],
    };

    public static IReadOnlyDictionary<string, string[]> RuleFamilies => Families;

    /// <summary>
    /// Rules that judge a whole section rather than a sentence.
    ///
    /// A corpus row is three sentences lifted out of a section, and no excerpt can name every
    /// ruler of a twenty-year window or cover every year in it. These fire on both halves of
    /// nearly every row and mean nothing there — except on the two rows that are about coverage,
    /// where the passage is section-shaped and the finding is the point.
    /// </summary>
    private static readonly string[] Coverage = ["missing-ruler", "incomplete-enumeration"];

    /// <summary>Every case on disk, in file order.</summary>
    public static List<CorpusCase> Load(string directory)
    {
        List<CorpusCase> cases = [];
        if (!Directory.Exists(directory)) return cases;

        string[] files = Directory.GetFiles(directory, "*.json");
        Array.Sort(files, StringComparer.Ordinal);

        foreach (string file in files)
        {
            CorpusCase? one = JsonSerializer.Deserialize<CorpusCase>(File.ReadAllText(file));
            if (one is null) throw new InvalidDataException($"{file} did not parse as a corpus case");
            cases.Add(one);
        }

        return cases;
    }

    /// <summary>
    /// The corpus directory, found by walking up from wherever the caller runs.
    ///
    /// The test host runs from <c>bin/Debug/net10.0</c> and the CLI from the solution root, and
    /// both need the same files. Walking up is less brittle than either a copy step or a path
    /// relative to one of them.
    /// </summary>
    public static string FindDirectory(string from)
    {
        for (DirectoryInfo? at = new(from); at is not null; at = at.Parent)
        {
            string candidate = Path.Combine(at.FullName, "tests", "corpus");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException($"no tests/corpus above {from}");
    }

    public static CorpusResult Run(CorpusCase one, Func<ulong, WorldView> world)
    {
        if (!Families.TryGetValue(one.ExpectRule, out string[]? kinds))
            return new CorpusResult(one, false, false, $"unknown rule '{one.ExpectRule}'");

        List<string> onPassage = [.. Findings(one, one.Passage, world).Select(f => f.Kind)];
        List<Fabrication> corrected = Findings(one, one.Corrected, world);
        List<string> onCorrected = [.. corrected.Select(f => f.Kind)];

        bool fired = onPassage.Any(k => kinds.Contains(k, StringComparer.Ordinal));

        // What the correction must not do is introduce a finding the original did not have.
        //
        // A corpus row is an excerpt of a section, and the whole-section rules — every ruler
        // named, every year covered — cannot be satisfied by three sentences. Those fire on both
        // halves of nearly every row and say nothing about the rewrite. Holding the correction
        // only to findings it adds keeps the assertion sharp: a rule that fires on the true
        // telling but not the false one is a false positive, and that is the whole point.
        //
        // Style findings are exempt on both sides. They do not block canon, so a corpus stricter
        // than the chronicle would be a bar nothing has to clear.
        List<string> before = [.. onPassage];
        List<string> substantive = [];

        foreach (string kind in onCorrected)
        {
            if (!Fabrication.Blocks(kind)) continue;
            if (before.Remove(kind)) continue;          // present before the rewrite too
            if (Coverage.Contains(kind) && !kinds.Contains(kind, StringComparer.Ordinal)) continue;
            substantive.Add(kind);
        }

        string detail = fired
            ? substantive.Count == 0
                ? "ok"
                : "the corrected passage still fires " + string.Join("; ",
                    corrected.Where(f => substantive.Contains(f.Kind)).Select(f => $"{f.Kind} — {f.Context}"))
            : $"expected {one.ExpectRule} ({string.Join("/", kinds)}); got " +
              (onPassage.Count == 0 ? "nothing" : string.Join(", ", onPassage));

        return new CorpusResult(one, fired, substantive.Count == 0, detail);
    }

    private static List<Fabrication> Findings(CorpusCase one, string text, Func<ulong, WorldView> world)
    {
        if (one.Scope is null) return [.. SelfConsistency.Check(text)];

        WorldView view = world(one.Seed);
        ContextPack? pack = ChronicleAudit.PackFor(view, one.Scope);

        if (pack is null) throw new InvalidDataException($"{one.Id}: no scope matches \"{one.Scope}\"");

        return [.. FabricationCheck.Check(pack, text, one.WholeSection).Findings];
    }
}
