using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;

namespace WorldBuilder.Chronicle.Tests;

/// <summary>
/// A sealed baseline Layer 4 runs against: the chronicle, the record that produced it, and the two
/// figures that are facts about that particular document rather than about any world.
/// </summary>
/// <param name="Set">
/// The baseline set under <c>baselines/</c>. <c>v1</c> is the hand-verified one; <c>ruleset-4</c> is
/// a machine baseline nobody has read.
/// </param>
/// <param name="Headings">Scopes the document declares, including the ones held out of canon.</param>
/// <param name="WithProse">Scopes carrying a verified passage. Both are asserted, because the
/// second alone would drift downwards as exclusions grew without anyone noticing.</param>
public sealed record BaselineUnderTest(string Set, ulong Seed, int Headings, int WithProse)
{
    public override string ToString() => Set;
}

/// <summary>
/// The baselines Layer 4 verifies, and the walk that finds them.
///
/// <b>Two baselines, and running against both is the point.</b> Layer 4 verified the v1 chronicle
/// for as long as v1 was the only chronicle that existed. Ruleset 4 produced a second one — a
/// machine baseline, nobody has read it — and the layer that exists to catch a checker going quiet
/// is worth exactly as much on the ruleset the engine currently runs as on the one it shipped.
///
/// <b>Seed 42 at ruleset 4 is a different world, not a stale one.</b> Positions are assigned at
/// worldgen and four mechanics consume distance, so the stream is consumed differently and the
/// history diverges: different powers, different windows, thirteen scopes rather than fifteen. So
/// nothing here is a re-verification of anything. Every figure below is derived from that world's
/// own record, and the two document counts are properties of that document.
///
/// The v1 baseline's prose is hand-verified. The ruleset-4 baseline's is not, and carries
/// <c>verification: stability-anchor-only</c>. That distinction is exactly why these checks are
/// worth running there: they are the ones that need no human, and they are all that can be said
/// about that document until somebody reads it.
/// </summary>
public static class SealedBaselines
{
    public static BaselineUnderTest V1 { get; } = new("v1", 42, Headings: 15, WithProse: 12);

    /// <summary>
    /// The ruleset-4 machine baseline: thirteen scopes, and six of them held out of canon.
    ///
    /// <b>Seven of thirteen is worse than v1's twelve of fifteen, and that is recorded rather than
    /// acted on.</b> Whether six exclusions on a thirteen-scope document is the checker working or
    /// the checker over-firing is a question about what the excluded passages say, which is prose
    /// judgement and belongs to a human. It is pinned here so the figure cannot drift while nobody
    /// is looking at it, and it is in the phase report as a question for Shay.
    /// </summary>
    public static BaselineUnderTest Ruleset4 { get; } = new("ruleset-4", 42, Headings: 13, WithProse: 7);

    public static IEnumerable<BaselineUnderTest> All => [V1, Ruleset4];

    public static string Directory(BaselineUnderTest baseline)
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            string candidate = Path.Combine(at.FullName, "baselines", baseline.Set,
                $"seed-{baseline.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

            if (System.IO.Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException(
            $"no baselines/{baseline.Set}/seed-{baseline.Seed} above {AppContext.BaseDirectory}");
    }

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, WorldView> Worlds = [];

    /// <summary>
    /// The record a baseline's chronicle was written from, folded once per run.
    ///
    /// The board comes with it where the log names one: from ruleset 4 a cell index means nothing
    /// without the board it indexes into, and <see cref="Rendering.Replay"/> refuses a board whose
    /// fingerprint is not the one on the genesis event rather than quietly attaching today's.
    /// </summary>
    public static WorldView World(BaselineUnderTest baseline)
    {
        lock (Gate)
        {
            if (Worlds.TryGetValue(baseline.Set, out WorldView? cached)) return cached;

            string file = Path.Combine(Directory(baseline),
                $"world-{baseline.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)}.jsonl");

            (EventLog log, ulong seed) = JsonlIo.Read(file);
            return Worlds[baseline.Set] = WorldView.Build(log, seed);
        }
    }

    public static string Markdown(BaselineUnderTest baseline) =>
        File.ReadAllText(Path.Combine(Directory(baseline),
            $"chronicle-{baseline.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)}.md"));

    public static List<Section> Sections(BaselineUnderTest baseline) =>
        ChronicleReader.Sections(Markdown(baseline));
}
