using System.Security.Cryptography;
using System.Text;

namespace WorldBuilder.Core.Serialization;

/// <summary>Raised when a bundle's contents do not match what its header says they are.</summary>
public sealed class BundleIntegrityException(string message) : Exception(message);

/// <summary>
/// A world that has been opened: its log, and everything a reader has to be told about how it
/// got here. <paramref name="Header"/> is null only for a file written before headers existed.
/// </summary>
public sealed record BundleOpen(
    EventLog Log,
    ulong Seed,
    WorldHeader? Header,
    IReadOnlyList<string> Notes);

/// <summary>
/// A world and the artefacts it cannot be read without.
///
/// The event log has been the durable artefact since Stage 3, on the reasoning that a seed is
/// provenance rather than a regeneration recipe. An imported map breaks the "log alone" half of
/// that: the log records which cell each place stands on, and a cell index means nothing without
/// the board it indexes into. The world is therefore a directory rather than a file, and the
/// header carries a hash for every file in it.
///
/// <b>Opening verifies, and a mismatch refuses.</b> Not a note, not a warning — an exception.
/// The whole reason a hash is in the header is that a map which does not match it is a map whose
/// distances are wrong, and a world simulated against a different board than the one it is being
/// read with is internally consistent, plausible, and about somewhere else. That failure has no
/// symptom, which is exactly the profile of the defects this project keeps finding late.
///
/// The opening policy from Stage 3 is unchanged and runs first: a newer ruleset refuses, an older
/// one opens with a note, no provenance opens with a note. Integrity is checked after
/// compatibility, so the more informative message wins when both are wrong.
/// </summary>
public static class WorldBundle
{
    /// <summary>The board artefact's name inside a bundle. One board per world.</summary>
    public const string BoardName = "board.wbmap.json";

    /// <summary>The render cache's name inside a bundle, where one travels with it.</summary>
    public const string RenderCacheName = "renders.json";

    public static string WorldPath(string directory, ulong seed) =>
        Path.Combine(directory, $"world-{seed.ToString(System.Globalization.CultureInfo.InvariantCulture)}.jsonl");

    /// <summary>
    /// Writes the world file with a header naming every artefact beside it.
    ///
    /// The artefacts themselves are not copied or moved — they are already in the directory, put
    /// there by whatever produced them. This hashes what is actually on disk at the moment of
    /// writing, which is the only reading of "the artefact that was used" that cannot drift.
    /// </summary>
    public static void Write(
        string directory,
        EventLog log,
        ulong seed,
        IReadOnlyList<string> artefactNames,
        string renderCacheFingerprint = "")
    {
        Directory.CreateDirectory(directory);

        List<StoredArtefact> artefacts = [];
        foreach (string name in Ordered(artefactNames))
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path))
                throw new FileNotFoundException($"bundle artefact '{name}' is not in {directory}.", path);

            artefacts.Add(new StoredArtefact(name, HashOf(path)));
        }

        WorldHeader header = WorldHeader.ForThisBuild(seed, log.Count) with
        {
            Artefacts = artefacts,
            RenderCacheFingerprint = renderCacheFingerprint,
        };

        using StreamWriter writer = new(WorldPath(directory, seed), append: false, new UTF8Encoding(false));
        writer.NewLine = "\n";

        writer.WriteLine(header.Serialise());
        foreach (Event e in log.Events) writer.WriteLine(JsonlIo.Serialise(e));
    }

    /// <summary>
    /// Checks every artefact the header names against what is on disk.
    ///
    /// Returns the complaints rather than throwing, so a caller that wants to report all of them
    /// can. <see cref="VerifyOrThrow"/> is the one every read path uses.
    /// </summary>
    public static List<string> Verify(string worldPath, WorldHeader header)
    {
        List<string> failures = [];
        string directory = Path.GetDirectoryName(worldPath) ?? ".";

        foreach (StoredArtefact artefact in header.Artefacts)
        {
            string path = Path.Combine(directory, artefact.Name);

            if (!File.Exists(path))
            {
                failures.Add($"{artefact.Name}: named in the header and not in the bundle");
                continue;
            }

            string actual = HashOf(path);
            if (string.Equals(actual, artefact.Sha256, StringComparison.OrdinalIgnoreCase)) continue;

            failures.Add(
                $"{artefact.Name}: header says {Short(artefact.Sha256)}, the file is {Short(actual)}");
        }

        return failures;
    }

    /// <summary>
    /// Verifies, and refuses to proceed on a mismatch.
    ///
    /// Loud by design. The alternative — a note, and carry on — was considered and is exactly
    /// wrong here: every other provenance mismatch this engine reports leaves a readable world
    /// behind it, and this one does not. A board whose hash has moved gives every rule that
    /// consults it a different answer, and nothing downstream would look unusual.
    /// </summary>
    public static void VerifyOrThrow(string worldPath, WorldHeader header)
    {
        List<string> failures = Verify(worldPath, header);
        if (failures.Count == 0) return;

        StringBuilder sb = new();
        sb.Append("this bundle does not match its header:\n");
        foreach (string failure in failures) sb.Append("    ").Append(failure).Append('\n');
        sb.Append("    a stored artefact that is not the one the header records is not an artefact to proceed with.");

        throw new BundleIntegrityException(sb.ToString());
    }

    /// <summary>
    /// Opening a world: compatibility, then integrity, then the log.
    ///
    /// One entry point, used by every command that reads a world, so that no caller can acquire a
    /// log without both checks having run. The alternative — a helper each command remembers to
    /// call — is the arrangement that produced two of this project's silent paths, and a test
    /// against the helper would pass while a command that skipped it shipped.
    /// </summary>
    public static BundleOpen Open(string worldPath, bool acceptNewer = false)
    {
        if (!File.Exists(worldPath))
            throw new FileNotFoundException($"no world at '{worldPath}' — run `wb run --seed <n>` first.", worldPath);

        WorldHeader? header = JsonlIo.ReadHeader(worldPath);

        List<string> notes;
        if (header is null)
        {
            notes = ["this world file has no header at all; nothing records what produced it."];
        }
        else
        {
            WorldFileCheck check = WorldCompatibility.Check(header);
            notes = [.. check.Notes];

            if (check.Blocks && !acceptNewer)
                throw new FormatException($"refusing to open '{worldPath}' — {string.Join(" ", check.Notes)}");

            // Integrity after compatibility, so the more informative complaint is heard first
            // when both are wrong.
            VerifyOrThrow(worldPath, header);
        }

        (EventLog log, ulong seed) = JsonlIo.Read(worldPath);
        return new BundleOpen(log, seed, header, notes);
    }

    /// <summary>sha256 of a file's bytes, hex, lower case. Bytes, never text: a hash that
    /// depends on how a line ending was interpreted is a hash of the checkout.</summary>
    public static string HashOf(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    public static string HashOfBytes(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Sorted, so two bundles holding the same files hash their headers the same way
    /// whatever order the caller happened to list them in.</summary>
    private static List<string> Ordered(IReadOnlyList<string> names)
    {
        List<string> sorted = [.. names];
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }

    private static string Short(string hash) => hash.Length <= 12 ? hash : hash[..12];
}
