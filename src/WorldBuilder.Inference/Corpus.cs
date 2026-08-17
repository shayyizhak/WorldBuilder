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

    /// <summary>
    /// What this row asserts. <c>must-fire</c> by default, which is every row the corpus began
    /// with: a false passage that the named rule must catch.
    ///
    /// The other two exist because a corpus of only false passages measures one half of a
    /// checker. <c>must-not-fire</c> pins a true sentence a rule once accused — round 10 put
    /// seven correct sections out of canon, and a corpus that cannot express "this was fine"
    /// cannot stop that recurring. <c>extraction</c> pins a count rather than a verdict, for the
    /// case where the right answer is that a rule reads nothing at all.
    /// </summary>
    [JsonPropertyName("kind")] public string Kind { get; init; } = "must-fire";

    /// <summary>How many assertions the owning rule must build. Only read when <see cref="Kind"/> is <c>extraction</c>.</summary>
    [JsonPropertyName("expect_extraction")] public int ExpectExtraction { get; init; }

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

    /// <summary>
    /// The sealed v1 seed-42 record every corpus case is about, or null where no baseline is
    /// beside us.
    ///
    /// <b>One resolver, because there were two and only one of them was right.</b> A corpus row
    /// is a fabrication found by hand in prose about one particular world; re-simulating to get
    /// that world turns the row into an assertion about whatever the current rules produce, and
    /// the first genuine ruleset change moves the world out from under all of them. The test
    /// suite learned that at ruleset 2 and pinned its fixture. <c>wb test corpus</c> kept
    /// simulating, and was throwing on a missing scope from ruleset 2 until Stage 6 — one idea,
    /// implemented twice, fixed once, failing quietly in the copy nobody ran.
    ///
    /// Living here rather than in either caller so there is no second copy to forget again.
    /// </summary>
    public static string? SealedSeed42(params string[] searchFrom)
    {
        foreach (string from in searchFrom)
        {
            for (DirectoryInfo? at = new(from); at is not null; at = at.Parent)
            {
                string candidate = Path.Combine(at.FullName, "baselines", "v1", "seed-42", "world-42.jsonl");
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    private static readonly Lock WorldGate = new();
    private static readonly Dictionary<ulong, WorldView> Worlds = [];

    /// <summary>
    /// The world a corpus case is about.
    ///
    /// <b>The whole policy, in one place, because there were two of it.</b> Not just the path —
    /// the decision that seed 42 comes from the sealed record and everything else is simulated.
    /// Sharing only the path resolver left the two callers still able to disagree about what to
    /// do with it, which is the same defect one layer down.
    ///
    /// Seed 42 is the archived v1 world every row was written against. Re-simulating to obtain it
    /// makes each row an assertion about whatever the current rules produce, and ruleset 2 moved
    /// the world under all thirty-four at once. Other seeds have no archived record and are
    /// simulated — a row that used one would be asserting about the current ruleset, which is a
    /// thing to know when it starts failing. No row uses one today.
    /// </summary>
    public static WorldView WorldFor(ulong seed)
    {
        lock (WorldGate)
        {
            if (Worlds.TryGetValue(seed, out WorldView? cached)) return cached;

            if (seed == 42
                && SealedSeed42(AppContext.BaseDirectory, Directory.GetCurrentDirectory()) is string path)
            {
                (EventLog archived, ulong archivedSeed) = Core.Serialization.JsonlIo.Read(path);
                return Worlds[seed] = WorldView.Build(archived, archivedSeed);
            }

            Simulation sim = new(seed);
            sim.Run(50);
            return Worlds[seed] = WorldView.Build(sim.Log, seed);
        }
    }

    /// <summary>
    /// Every row, run. The one entry point for Layer 3, used by <c>wb test corpus</c> and by the
    /// test that keeps it honest, so the command and the suite cannot come to mean different
    /// things by "the corpus passes".
    ///
    /// A row whose scope no longer exists comes back as a failing row rather than an exception.
    /// It used to throw all the way out of the process, which reported a defect in one row as a
    /// defect in the tooling and took the other thirty-three with it.
    /// </summary>
    public static List<CorpusResult> RunAll(string? directory = null)
    {
        List<CorpusResult> results = [];

        foreach (CorpusCase one in Load(directory ?? FindDirectory(AppContext.BaseDirectory)))
        {
            try
            {
                results.Add(Run(one, WorldFor));
            }
            catch (InvalidDataException ex)
            {
                results.Add(new CorpusResult(one, false, false, ex.Message));
            }
        }

        return results;
    }

    public static CorpusResult Run(CorpusCase one, Func<ulong, WorldView> world)
    {
        if (!Families.TryGetValue(one.ExpectRule, out string[]? kinds))
            return new CorpusResult(one, false, false, $"unknown rule '{one.ExpectRule}'");

        // A true sentence a rule once accused. There is nothing to correct, so the second
        // assertion is the whole assertion: the family must stay silent.
        if (one.Kind == "must-not-fire")
        {
            List<Fabrication> found = Findings(one, one.Passage, world);
            List<Fabrication> wrong = [.. found.Where(f => kinds.Contains(f.Kind, StringComparer.Ordinal))];

            return new CorpusResult(one, wrong.Count == 0, true,
                wrong.Count == 0
                    ? "ok"
                    : $"{one.ExpectRule} accused a true sentence: " +
                      string.Join("; ", wrong.Select(f => $"{f.Kind} — {f.Context}")));
        }

        // A count rather than a verdict. Zero extraction is the right answer where the phrase a
        // rule used to read was never an assertion in the first place, and no finding-shaped
        // assertion can express that.
        if (one.Kind == "extraction")
        {
            Coverage cover = new();
            Findings(one, one.Passage, world, cover);

            string owner = RuleNames.Of(kinds.Length > 0 ? kinds[0] : one.ExpectRule);
            if (!Families.ContainsKey(one.ExpectRule)) owner = one.ExpectRule;
            if (cover.Names.Contains(one.ExpectRule, StringComparer.Ordinal)) owner = one.ExpectRule;

            int extracted = cover.Rules.TryGetValue(owner, out RuleCounts? counts) ? counts.Extracted : 0;
            bool matched = extracted == one.ExpectExtraction;

            return new CorpusResult(one, matched, true,
                matched ? "ok" : $"{owner} extracted {extracted}, expected {one.ExpectExtraction}");
        }

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

    private static List<Fabrication> Findings(
        CorpusCase one, string text, Func<ulong, WorldView> world, Coverage? cover = null)
    {
        if (one.Scope is null) return [.. SelfConsistency.Check(text, cover ?? new Coverage())];

        WorldView view = world(one.Seed);
        ContextPack? pack = ChronicleAudit.PackFor(view, one.Scope);

        if (pack is null) throw new InvalidDataException($"{one.Id}: no scope matches \"{one.Scope}\"");

        FabricationReport report = FabricationCheck.Check(pack, text, one.WholeSection);
        if (cover is not null) cover.Merge(report.Coverage);

        return [.. report.Findings];
    }
}
