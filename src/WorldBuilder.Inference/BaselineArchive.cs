using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorldBuilder.Inference;

/// <summary>One file in a baseline, and how it got there.</summary>
public sealed record BaselineArtefact(string Filename, string SourcePath, string Sha256, long Bytes, string Role);

/// <summary>
/// Cutting a golden baseline: an archive of one run, sealed, with everything needed to say what
/// produced it.
///
/// The v1 baseline was assembled by hand over three rounds, two of which aborted, and both aborts
/// were correct. What they were correct about is the reason this is code now: the tree turned out
/// to be internally inconsistent inside its own first commit, and nothing in the artefacts said
/// which checker had written which file. A baseline that cannot name its producer is a baseline
/// that drifts silently.
///
/// Three properties, each learned the hard way:
///
/// <b>Create-only.</b> A new baseline requires moving the old directory aside first. The property
/// wanted is "this cannot move by rerun", and a gate that depends on somebody remembering to be
/// careful is weaker than one that depends on the filesystem.
///
/// <b>The producer is read from the artefacts, never from this build.</b> The engine version and
/// commit come out of the world file's own header — the file the run wrote — so cutting a
/// baseline later, from a build with new code in it, cannot quietly attribute the artefacts to
/// the wrong engine. This tool records; it does not produce.
///
/// <b>The checker fingerprint hashes what git stores.</b> Not the working tree. Line endings make
/// a working-tree hash a property of the checkout, and this project has already nearly shipped
/// that exact bug twice — once in the query suite's hash and once in the fingerprint field
/// itself.
/// </summary>
public static class BaselineArchive
{
    /// <summary>
    /// The checker's source, as one list, in one place.
    ///
    /// Restated nowhere else. A figure written down in two places is a figure that goes stale in
    /// one of them, and the count of checker rules has already done exactly that in a summary
    /// document — sat at 17 for weeks and propagated into a loop prompt because nothing
    /// questioned it.
    /// </summary>
    public static readonly IReadOnlyList<string> CheckerSources =
    [
        "src/WorldBuilder.Inference/Claims.cs",
        "src/WorldBuilder.Inference/Coverage.cs",
        "src/WorldBuilder.Inference/FabricationCheck.cs",
        "src/WorldBuilder.Inference/RuleNames.cs",
        "src/WorldBuilder.Inference/SelfConsistency.cs",
    ];

    public const string FingerprintMethod =
        "For each path below, sha256 of the file content as stored in git at engine_commit (LF, " +
        "checkout-independent). One line per file, 'path  sha256\\n', sorted by path. The " +
        "fingerprint is the sha256 of that listing.";

    /// <summary>
    /// The checker fingerprint at a commit, with the per-file hashes that made it.
    ///
    /// Shells out to git rather than reading the working tree, and fails where git cannot answer
    /// rather than falling back. A fallback here would be the silent-path shape exactly: a
    /// fingerprint computed a different way, recorded under the same field name, that nobody can
    /// tell apart from the real one afterwards.
    /// </summary>
    public static (string Fingerprint, List<(string Path, string Sha256)> Inputs) CheckerFingerprint(
        string repositoryRoot, string commit)
    {
        List<(string Path, string Sha256)> inputs = [];
        List<string> paths = [.. CheckerSources];
        paths.Sort(StringComparer.Ordinal);

        foreach (string path in paths)
        {
            byte[] blob = GitBlob(repositoryRoot, commit, path);
            inputs.Add((path, Convert.ToHexString(SHA256.HashData(blob)).ToLowerInvariant()));
        }

        StringBuilder listing = new();
        foreach ((string path, string sha) in inputs)
            listing.Append(path).Append("  ").Append(sha).Append('\n');

        string fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(listing.ToString()))).ToLowerInvariant();

        return (fingerprint, inputs);
    }

    /// <summary>The file the ruleset counter lives in, read out of git to check a seal's claim.</summary>
    public const string RulesetSource = "src/WorldBuilder.Core/Provenance.cs";

    /// <summary>
    /// The ruleset version a commit contains, or null where git cannot read it there.
    ///
    /// <b>Why a seal has to be able to answer this.</b> A baseline's manifest names an engine commit,
    /// and the whole purpose of naming it is that a reader can go to that commit and regenerate the
    /// world. A manifest claiming ruleset 8 whose commit contains ruleset 6 names a build that would
    /// produce a different history — the seal verifies, every hash matches, and the one thing it is
    /// for does not hold.
    ///
    /// It has happened three times, always the same way: the ruleset was bumped in the working tree,
    /// the baselines were cut before committing, and the build stamped HEAD. Rulesets 5 and 7 exist in
    /// no commit at all as a result, so those two sets can never be re-cut correctly — the code that
    /// produced them was never stored. That is why <see cref="Cut"/> now refuses rather than leaving
    /// this to be noticed later.
    ///
    /// Read from git rather than the working tree, for the reason the checker fingerprint is: the
    /// question is what the repository stores at that commit, and the working tree is a different
    /// thing that happens to be nearby.
    /// </summary>
    public static string? RulesetAt(string repositoryRoot, string commit)
    {
        byte[] blob;
        try
        {
            blob = GitBlob(repositoryRoot, commit, RulesetSource);
        }
        catch (InvalidOperationException)
        {
            // The commit predates the file, or git cannot reach it. Null is "cannot say", which the
            // caller has to handle as its own case rather than as a mismatch.
            return null;
        }

        Match match = Regex.Match(
            Encoding.UTF8.GetString(blob), @"const\s+string\s+Version\s*=\s*""([^""]+)""");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static byte[] GitBlob(string repositoryRoot, string commit, string path)
    {
        ProcessStartInfo start = new("git", ["cat-file", "blob", $"{commit}:{path}"])
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("could not start git; the checker fingerprint needs it.");

        using MemoryStream buffer = new();
        process.StandardOutput.BaseStream.CopyTo(buffer);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git could not read {commit}:{path} — {error.Trim()}. The fingerprint hashes what " +
                "the repository stores, so an uncommitted checker cannot be fingerprinted.");
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Copies a run's artefacts into a new sealed baseline directory and writes its manifest.
    ///
    /// <paramref name="verification"/> is the caller's claim about the prose, and it is the field
    /// most worth getting right: only seed 42's v1 chronicle was ever read by a human, so every
    /// other baseline carries <c>stability-anchor-only</c>. A golden diff needs its anchor stable
    /// rather than correct — but the two are different claims, and a baseline that overstates
    /// which one it makes will eventually be trusted for the wrong thing.
    /// </summary>
    public static BaselineCut Cut(BaselineRequest request)
    {
        if (Directory.Exists(request.To))
        {
            throw new IOException(
                $"{request.To} already exists. A baseline is create-only: establishing a new one " +
                "means moving this directory aside under a new name first, which is the act that " +
                "stops a floor moving by rerun.");
        }

        string worldName = $"world-{request.Seed.ToString(CultureInfo.InvariantCulture)}.jsonl";
        string worldPath = Path.Combine(request.From, worldName);

        if (!File.Exists(worldPath))
            throw new FileNotFoundException($"no world at {worldPath}; there is nothing to archive.", worldPath);

        // The producer, read from the artefact rather than from the running build.
        Core.Serialization.WorldHeader header = Core.Serialization.JsonlIo.ReadHeader(worldPath)
            ?? throw new FormatException(
                $"{worldName} has no header, so nothing records what produced it. A baseline whose " +
                "engine cannot be named is one nobody can reproduce a judgement about.");

        // A control world is a perfectly valid history of nowhere: its rules were told fabricated
        // distances so that two explanations could be told apart. Sealing one would put a
        // diagnostic artefact in the place the whole suite measures against, and on disk it looks
        // like any other world file — which is why the refusal is here rather than in a habit.
        if (header.IsDiagnostic)
        {
            throw new InvalidOperationException(
                $"{worldName} ran under {header.DiagnosticReason}, so it is a diagnostic artefact " +
                "rather than a world, and a baseline is the thing everything else is measured " +
                "against. It cannot be sealed.");
        }

        if (header.EngineCommit.Length == 0)
        {
            throw new FormatException(
                $"{worldName} records no engine commit. The checker fingerprint is taken at that " +
                "commit, so a baseline cannot be cut from a build that did not record one.");
        }

        // The commit has to contain the ruleset the manifest is about to claim.
        //
        // A seal names an engine commit so a reader can go there and regenerate the world; a manifest
        // saying ruleset 8 whose commit holds ruleset 6 names a build that would produce a different
        // history, and every hash in it still matches. The failure is invisible from inside the
        // artefact, which is why it went unnoticed three times: bump the ruleset in the working tree,
        // cut before committing, and the build stamps HEAD. Rulesets 5 and 7 exist in no commit at
        // all because of it, so those two sets can never be re-cut correctly.
        //
        // Refused rather than warned. The cheap fix is to commit first and cut second, and a warning
        // at this point is a note nobody reads until the set it describes is already sealed.
        if (header.RulesetVersion.Length > 0 &&
            RulesetAt(request.RepositoryRoot, header.EngineCommit) is { } sealedRuleset &&
            !string.Equals(sealedRuleset, header.RulesetVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{worldName} was produced under ruleset {header.RulesetVersion}, and its engine " +
                $"commit {header.EngineCommit[..12]} contains ruleset {sealedRuleset}. A seal names " +
                "the commit so the world can be regenerated from it, and this one cannot be. Commit " +
                "the ruleset, rebuild so the header carries the new commit, re-run the world, and " +
                "cut from that.");
        }

        // Which board this history happened on, taken from its own record. A ruleset-4 world
        // that does not carry its board is not a world, so the archive refuses rather than
        // producing a sealed, hash-verified, unreadable directory.
        string boardFingerprint = BoardNamedBy(Core.Serialization.JsonlIo.Read(worldPath).Log);

        Directory.CreateDirectory(request.To);

        List<BaselineArtefact> artefacts = [];
        foreach ((string name, string role, bool required) in Contents(request.Seed, boardFingerprint.Length > 0))
        {
            string source = Path.Combine(request.From, name);
            if (!File.Exists(source))
            {
                if (required)
                    throw new FileNotFoundException($"{name} is missing from {request.From}.", source);
                continue;
            }

            string destination = Path.Combine(request.To, name);
            File.Copy(source, destination);

            string sha = Core.Serialization.WorldBundle.HashOf(destination);

            // The archived board must be the board this history happened on, not merely *a*
            // board. The fingerprint on the genesis event is the sha256 of the map's canonical
            // bytes, so the two are directly comparable — and a baseline sealed around the wrong
            // map would be internally consistent and about somewhere else.
            if (role == "board" && !string.Equals(sha, boardFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{name} hashes to {sha[..12]} and this world was simulated on board " +
                    $"{boardFingerprint[..12]}. Archiving the wrong map would seal a world nobody " +
                    "can read the distances of.");
            }

            artefacts.Add(new BaselineArtefact(
                name,
                source.Replace('\\', '/'),
                sha,
                new FileInfo(destination).Length,
                role));
        }

        (string fingerprint, List<(string Path, string Sha256)> inputs) =
            CheckerFingerprint(request.RepositoryRoot, header.EngineCommit);

        string manifest = Manifest(request, header, artefacts, fingerprint, inputs);
        string manifestPath = Path.Combine(request.To, "manifest.json");
        File.WriteAllText(manifestPath, manifest, new UTF8Encoding(false));

        // The seal is the sha256 of the manifest, and the manifest is authoritative for
        // everything else. One file to check before trusting the directory.
        string seal = Core.Serialization.WorldBundle.HashOf(manifestPath);
        File.WriteAllText(Path.Combine(request.To, ".sealed"), seal + "\n", new UTF8Encoding(false));

        return new BaselineCut(request.To, seal, fingerprint, artefacts, header);
    }

    /// <summary>
    /// What goes into a baseline, and whether its absence is a failure.
    ///
    /// <b>From ruleset 4, a world is a log and its board.</b> That is a definition rather than a
    /// checklist item: a cell index means nothing without the board it indexes into, so an
    /// archive holding the log alone does not hold the world, and it is incomplete by definition
    /// rather than by oversight. Anything claiming to archive a ruleset-4 world without both is
    /// wrong about what a world is.
    ///
    /// The board is therefore required exactly when the log names one, and not otherwise — the
    /// ruleset-3 baselines predate boards entirely and must keep verifying.
    ///
    /// The unverified passages are optional because a run in which nothing was held out of canon
    /// legitimately has none — and the difference between "no file" and "an empty file" is the
    /// absent-versus-withheld distinction this project has now met in four places.
    /// </summary>
    private static IEnumerable<(string Name, string Role, bool Required)> Contents(ulong seed, bool hasBoard)
    {
        string stem = $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}";
        string world = $"world-{seed.ToString(CultureInfo.InvariantCulture)}";

        yield return ($"{stem}.md", "artefact", true);
        yield return ($"{stem}.findings.json", "anchor", true);
        yield return ($"{stem}.unverified.md", "artefact", false);
        yield return ("renders.json", "artefact", true);
        yield return ($"{world}.jsonl", "artefact", true);
        yield return ($"{world}.log", "artefact", true);

        if (hasBoard) yield return (Core.Serialization.WorldBundle.BoardName, "board", true);
    }

    /// <summary>
    /// The board fingerprint a world's own record names, or empty where it names none.
    ///
    /// Read from the genesis event rather than from the bundle header, deliberately. The header
    /// records what was sitting beside the file; the record says what the history actually
    /// happened on, and those are the two different things the pair of them exists to tell apart.
    /// </summary>
    private static string BoardNamedBy(Core.EventLog log)
    {
        if (log.Count == 0) return "";

        Core.Event genesis = log.Events[0];
        return genesis.Kind != Core.EventKind.GenesisWorld ? "" : genesis.GetString("board") ?? "";
    }

    private static string Manifest(
        BaselineRequest request,
        Core.Serialization.WorldHeader header,
        IReadOnlyList<BaselineArtefact> artefacts,
        string fingerprint,
        IReadOnlyList<(string Path, string Sha256)> inputs)
    {
        StringBuilder sb = new();
        sb.Append("{\n");
        sb.Append("  \"baseline_id\": ").Append(Json(request.Id)).Append(",\n");
        sb.Append("  \"created_utc\": ").Append(Json(
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))).Append(",\n");
        sb.Append("  \"seed\": ").Append(request.Seed.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        sb.Append("  \"verification\": ").Append(Json(request.Verification)).Append(",\n");
        sb.Append("  \"engine_version\": ").Append(Json(header.EngineVersion)).Append(",\n");
        sb.Append("  \"engine_commit\": ").Append(Json(header.EngineCommit)).Append(",\n");
        sb.Append("  \"ruleset_version\": ").Append(Json(header.RulesetVersion)).Append(",\n");
        sb.Append("  \"checker_fingerprint\": ").Append(Json(fingerprint)).Append(",\n");
        sb.Append("  \"checker_fingerprint_method\": ").Append(Json(FingerprintMethod)).Append(",\n");

        sb.Append("  \"checker_fingerprint_inputs\": [\n");
        for (int i = 0; i < inputs.Count; i++)
        {
            sb.Append("    { \"path\": ").Append(Json(inputs[i].Path))
              .Append(", \"sha256\": ").Append(Json(inputs[i].Sha256)).Append(" }")
              .Append(i == inputs.Count - 1 ? "\n" : ",\n");
        }
        sb.Append("  ],\n");

        sb.Append("  \"checker_rule_count\": ").Append(RuleNames.All.Count
            .ToString(CultureInfo.InvariantCulture)).Append(",\n");

        sb.Append("  \"checker_rules\": [");
        for (int i = 0; i < RuleNames.All.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(Json(RuleNames.All[i]));
        }
        sb.Append("],\n");

        sb.Append("  \"inference\": {\n");
        sb.Append("    \"runtime\": \"ollama\",\n");
        sb.Append("    \"model\": ").Append(Json(request.Model)).Append(",\n");
        sb.Append("    \"model_digest\": ").Append(Json(request.ModelDigest)).Append('\n');
        sb.Append("  },\n");

        sb.Append("  \"notes\": [\n");
        for (int i = 0; i < request.Notes.Count; i++)
            sb.Append("    ").Append(Json(request.Notes[i])).Append(i == request.Notes.Count - 1 ? "\n" : ",\n");
        sb.Append("  ],\n");

        sb.Append("  \"artefacts\": [\n");
        for (int i = 0; i < artefacts.Count; i++)
        {
            BaselineArtefact a = artefacts[i];
            sb.Append("    { \"filename\": ").Append(Json(a.Filename))
              .Append(", \"source_path\": ").Append(Json(a.SourcePath))
              .Append(", \"sha256\": ").Append(Json(a.Sha256))
              .Append(", \"bytes\": ").Append(a.Bytes.ToString(CultureInfo.InvariantCulture))
              .Append(", \"role\": ").Append(Json(a.Role)).Append(" }")
              .Append(i == artefacts.Count - 1 ? "\n" : ",\n");
        }
        sb.Append("  ]\n}\n");

        return sb.ToString();
    }

    /// <summary>
    /// Checks a sealed baseline against itself: the seal against the manifest, and the manifest's
    /// hashes against the files.
    ///
    /// Cheap, and the only thing that makes the seal worth having. A recorded hash nothing ever
    /// compares against is a comment.
    /// </summary>
    public static List<string> Check(string directory)
    {
        List<string> failures = [];
        string manifestPath = Path.Combine(directory, "manifest.json");
        string sealPath = Path.Combine(directory, ".sealed");

        if (!File.Exists(manifestPath)) return ["manifest.json is missing; the baseline says nothing about itself"];
        if (!File.Exists(sealPath)) return [".sealed is missing; nothing pins the manifest"];

        string expected = File.ReadAllText(sealPath).Trim();
        string actual = Core.Serialization.WorldBundle.HashOf(manifestPath);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            failures.Add($".sealed says {expected[..12]} and manifest.json hashes to {actual[..12]}");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!doc.RootElement.TryGetProperty("artefacts", out JsonElement list)) return failures;

        foreach (JsonElement artefact in list.EnumerateArray())
        {
            string name = artefact.GetProperty("filename").GetString()!;
            string sha = artefact.GetProperty("sha256").GetString()!;
            string path = Path.Combine(directory, name);

            if (!File.Exists(path)) { failures.Add($"{name}: in the manifest and not in the directory"); continue; }

            string found = Core.Serialization.WorldBundle.HashOf(path);
            if (!string.Equals(found, sha, StringComparison.OrdinalIgnoreCase))
                failures.Add($"{name}: manifest says {sha[..12]}, the file is {found[..12]}");
        }

        return failures;
    }

    private static string Json(string value) => JsonSerializer.Serialize(value);
}

/// <summary>What to archive, where from, where to, and what claim is being made about it.</summary>
public sealed record BaselineRequest
{
    public required ulong Seed { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Id { get; init; }
    public required string RepositoryRoot { get; init; }

    /// <summary>
    /// <c>hand-verified</c> only where a person has read the prose against the record. Everything
    /// else is <c>stability-anchor-only</c>: enough for a golden diff, which needs its anchor
    /// stable rather than correct, and not enough to be quoted as truth about the world.
    /// </summary>
    public string Verification { get; init; } = "stability-anchor-only";

    public string Model { get; init; } = "";
    public string ModelDigest { get; init; } = "";
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record BaselineCut(
    string Directory,
    string Seal,
    string CheckerFingerprint,
    IReadOnlyList<BaselineArtefact> Artefacts,
    Core.Serialization.WorldHeader Header);
