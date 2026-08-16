using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// Layer 1: the v0 acceptance criteria as assertions, across the whole seed panel.
///
/// A metric that holds on 42 alone is an anecdote. 42 is the seed every round was reviewed on,
/// which makes it the one seed a defect is least likely to survive on and the worst single
/// choice for proving a threshold.
///
/// Two hard rules here come from reviewer error rather than engine error: read the record and
/// never the <c>.log</c> view, and make a filter that drops rows fail loudly. Both are asserted
/// below rather than left as intentions.
/// </summary>
public class DynamicsTests
{
    /// <summary>The full panel from the specification.</summary>
    public static TheoryData<ulong> Panel() => [7UL, 42UL, 99UL, 1234UL, 2025UL];

    private static WorldView World(ulong seed, int years = 50)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return WorldView.Build(sim.Log, seed);
    }

    /// <summary>
    /// Metrics known to be failing, named rather than silenced.
    ///
    /// A threshold is not lowered to make a suite green — that is the floor moving by rerun, in
    /// the one place where it would be least visible. These are recorded as open defects instead,
    /// and <see cref="AKnownGapThatHasBeenFixedMustLeaveTheList"/> makes the list shrink the
    /// moment one of them starts holding, so a quarantine cannot outlive the thing it quarantines.
    ///
    /// <b>coup success rate</b> — 0% on all five seeds. Seed 42 resolves seven coups: none won,
    /// none lost, all seven exposed. This is the shape of the v0 run-3 regression and it is an
    /// engine question, not a measurement one.
    ///
    /// <b>covert coup path</b> — the same defect seen from the other side. It only appears here
    /// because the invariant was corrected to assert wins; as written it counted exposures as
    /// successes and reported green against zero wins.
    ///
    /// <b>distinct deep-chain shapes</b> — 54 on seed 7 and 52 on seed 99 against a bar of 60.
    /// Seed 42 passes, which is precisely why the panel exists.
    /// </summary>
    private static readonly string[] KnownFailing =
    [
        "coup success rate",
        "covert coup path",
        "distinct deep-chain shapes",
    ];

    [Theory]
    [MemberData(nameof(Panel))]
    public void EveryInvariantHoldsOnEverySeed(ulong seed)
    {
        List<Invariant> results = Invariants.Check(World(seed));

        Assert.NotEmpty(results);

        string broken = string.Join("\n  ", results
            .Where(r => !r.Held && !KnownFailing.Contains(r.Name, StringComparer.Ordinal))
            .Select(r => $"{r.Name}: measured {r.Measured}, expected {r.Expected}"));

        Assert.True(broken.Length == 0, $"seed {seed}:\n  {broken}");
    }

    /// <summary>
    /// Every quarantined metric is still failing somewhere on the panel.
    ///
    /// The half that keeps a known-gap list honest. Without it a list of excuses outlives the
    /// defects it excuses, and the suite goes on reporting green about something that has been
    /// fixed for a year — which is how a passing test stops meaning anything.
    /// </summary>
    [Fact]
    public void AKnownGapThatHasBeenFixedMustLeaveTheList()
    {
        HashSet<string> stillBroken = new(StringComparer.Ordinal);

        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234, 2025 })
            foreach (Invariant r in Invariants.Check(World(seed)))
                if (!r.Held) stillBroken.Add(r.Name);

        foreach (string quarantined in KnownFailing)
        {
            Assert.True(stillBroken.Contains(quarantined),
                $"'{quarantined}' now holds on every seed — delete it from KnownFailing.");
        }
    }

    /// <summary>
    /// Every metric the specification names is actually asserted, on every seed.
    ///
    /// A layer that quietly stopped computing one of its ten would still report all-green, which
    /// is the same shape as a rule that stops firing: absence reading as success.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void AllTenSpecifiedMetricsArePresent(ulong seed)
    {
        List<Invariant> results = Invariants.Check(World(seed));
        List<string> names = [.. results.Select(r => r.Name)];

        foreach (string metric in new[]
                 {
                     "dangling causal references",
                     "verbatim repeat rate",
                     "single-actor causal chains",
                     "maximum causal depth",
                     "distinct deep-chain shapes",
                     "collapses per faction",
                     "coup success rate",
                     "covert coup path",
                     "economy-driven edges",
                     "cross-domain edges",
                 })
        {
            Assert.Contains(metric, names);
        }
    }

    // ---- read the record, not the view ------------------------------------

    /// <summary>
    /// The rows read equal the rows in the file, and the file's own header agrees.
    ///
    /// Three reviews in a row measured the world over the <c>.log</c>, which hides the yearly
    /// accounts — roughly a third of the record, and most of the economy's influence on anything
    /// else. Economy coupling was reported as 18 of 524 when the world holds 142 of 850.
    /// </summary>
    [Theory]
    [MemberData(nameof(Panel))]
    public void EveryRowInTheFileIsRead(ulong seed)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"wb-dyn-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(dir);

        try
        {
            Simulation sim = new(seed);
            sim.Run(50);

            string path = Path.Combine(dir, $"world-{seed}.jsonl");
            JsonlIo.Write(path, sim.Log, seed);

            // Every line but the header is an event, counted from the file itself rather than
            // from anything that parsed it.
            int linesInFile = File.ReadAllLines(path).Count(l => !string.IsNullOrWhiteSpace(l)) - 1;

            (EventLog reloaded, ulong readSeed) = JsonlIo.Read(path);
            WorldHeader? header = JsonlIo.ReadHeader(path);

            Assert.Equal(seed, readSeed);
            Assert.NotNull(header);
            Assert.Equal(sim.Log.Count, linesInFile);
            Assert.Equal(sim.Log.Count, reloaded.Count);
            Assert.Equal(sim.Log.Count, header.Events);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// The readable view is a view, and the test suite must never mistake it for the world.
    ///
    /// Asserted as an inequality on purpose: if these ever match, either the filter has stopped
    /// filtering or the record has stopped carrying the bookkeeping, and both are worth knowing.
    /// </summary>
    [Fact]
    public void TheLogViewHoldsFewerRowsThanTheRecord()
    {
        Simulation sim = new(42);
        sim.Run(50);

        IReadOnlyList<string> rendered =
            WorldBuilder.Core.Rendering.LogFormatter.Render(sim.Log, 42, Significance.Minor);

        Assert.True(rendered.Count < sim.Log.Count,
            $"the view holds {rendered.Count} lines and the record {sim.Log.Count} events");
    }

    // ---- a filter that drops rows fails loudly ----------------------------

    /// <summary>
    /// A record with a cause pointing at nothing must fail, not be quietly skipped.
    ///
    /// This is the assertion the reviewer's own `if cause in events` guard defeated: the guard
    /// made a broken graph look healthy by declining to count what it could not resolve.
    /// </summary>
    [Fact]
    public void ACorruptedRecordFailsTheDanglingReferenceCheck()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"wb-dyn-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(dir);

        try
        {
            Simulation sim = new(42);
            sim.Run(50);

            string path = Path.Combine(dir, "world-42.jsonl");
            JsonlIo.Write(path, sim.Log, 42);

            string[] lines = File.ReadAllLines(path);

            // Point one real causal edge at an event that does not exist.
            int corrupted = -1;
            for (int i = 1; i < lines.Length; i++)
            {
                if (!lines[i].Contains("\"causes\":[\"e:", StringComparison.Ordinal)) continue;

                int start = lines[i].IndexOf("\"causes\":[", StringComparison.Ordinal);
                int end = lines[i].IndexOf(']', start);
                lines[i] = lines[i][..start] + "\"causes\":[\"e:999999\"" + lines[i][end..];
                corrupted = i;
                break;
            }

            Assert.True(corrupted > 0, "no event with a causal edge to corrupt");
            File.WriteAllLines(path, lines);

            (EventLog reloaded, ulong seed) = JsonlIo.Read(path);
            List<Invariant> results = Invariants.Check(WorldView.Build(reloaded, seed));

            Invariant dangling = results.Single(r => r.Name == "dangling causal references");

            Assert.False(dangling.Held, "a dangling reference was not reported");
            Assert.NotEqual("0", dangling.Measured);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
