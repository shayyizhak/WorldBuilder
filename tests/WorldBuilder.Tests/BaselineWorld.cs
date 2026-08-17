using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Serialization;

namespace WorldBuilder.Tests;

/// <summary>
/// The sealed v1 world for seed 42, loaded from its archived record rather than re-simulated.
///
/// <b>A fixture pinned to a seed is a ruleset-scoped artefact, and nothing said so.</b> The
/// regression corpus, the checker cases and the render and retrieval fixtures all assert facts
/// about one particular world — that Paernmel Has was the target of four failed attempts, that
/// the raid on Hadale in 19 killed 16. Every one of them re-ran the simulation to obtain that
/// world, which quietly made them assertions about *whatever the current rules produce*. The
/// first genuine ruleset change moved the world under them and 56 of them failed at once.
///
/// That is the same lesson as the golden anchor, in a place nobody had scoped: a derived artefact
/// must name its inputs. These cases were always about the v1 world, so they now read the v1
/// world — the create-only file in the sealed baseline, whose sha256 is recorded in a manifest
/// and which no rule change can move.
///
/// Tests about the *engine* rather than about that world keep simulating: determinism, replay,
/// the dynamics panel, the plot ledger. They are assertions about what the rules do now, and
/// pinning them to an archived world would be the opposite mistake.
/// </summary>
public static class BaselineWorld
{
    /// <summary>The v1 seed-42 world, ruleset 1, as archived. Loaded once for the whole run.</summary>
    public static WorldView Seed42() => ForSeed(42);

    /// <summary>
    /// The world a fixture is about, from the one function that decides it.
    ///
    /// <b>This used to be a second implementation of that decision and that is what broke.</b>
    /// The policy — seed 42 from the sealed record, everything else simulated — lived here and
    /// again inside <c>wb test corpus</c>, this copy was fixed at ruleset 2 and the other was
    /// not, and the command spent two rulesets throwing on a scope that no longer existed. Two
    /// copies of one idea is one idea that gets fixed once, so there is now one.
    ///
    /// Layer 4 duplicating the *checker* is deliberate and stays: duplicated verification is the
    /// property being bought there. Duplicated implementation is not the same thing.
    /// </summary>
    public static WorldView ForSeed(ulong seed) => WorldBuilder.Inference.Corpus.WorldFor(seed);

    /// <summary>
    /// The sealed baseline directory, resolved by the same function <c>wb test corpus</c> uses.
    ///
    /// Shared deliberately. There were two resolvers, this one was fixed at ruleset 2 and the
    /// command's was not, and the command spent two rulesets throwing on a scope that no longer
    /// existed. One idea implemented twice is one idea that gets fixed once.
    /// </summary>
    public static string Directory() =>
        Path.GetDirectoryName(
            WorldBuilder.Inference.Corpus.SealedSeed42(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()))
        ?? throw new DirectoryNotFoundException($"no baselines/v1/seed-42 above {AppContext.BaseDirectory}");
}
