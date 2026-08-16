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
    private static readonly Lock Gate = new();
    private static WorldView? _seed42;

    /// <summary>The v1 seed-42 world, ruleset 1, as archived. Loaded once for the whole run.</summary>
    public static WorldView Seed42()
    {
        lock (Gate)
        {
            if (_seed42 is not null) return _seed42;

            (EventLog log, ulong seed) = JsonlIo.Read(Path.Combine(Directory(), "world-42.jsonl"));
            _seed42 = WorldView.Build(log, seed);
            return _seed42;
        }
    }

    /// <summary>
    /// Seeds other than 42 have no archived world, so they are simulated.
    ///
    /// No corpus case uses one. Left working so a future case can, with the caveat that such a
    /// case is asserting about the current ruleset rather than about a pinned world — which is a
    /// thing to know when it starts failing.
    /// </summary>
    public static WorldView ForSeed(ulong seed)
    {
        if (seed == 42) return Seed42();

        Simulation sim = new(seed);
        sim.Run(50);
        return WorldView.Build(sim.Log, seed);
    }

    public static string Directory()
    {
        for (DirectoryInfo? at = new(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            string candidate = Path.Combine(at.FullName, "baselines", "v1", "seed-42");
            if (System.IO.Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException($"no baselines/v1/seed-42 above {AppContext.BaseDirectory}");
    }
}
