using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The world bundle: a world plus the artefacts it cannot be read without.
///
/// Every test here enters at <see cref="WorldBundle.Open"/> or <see cref="WorldBundle.Write"/>,
/// which are what the commands call. That is deliberate and it is the lesson this suite has
/// already paid for twice: a test that reaches past the entry point verifies a helper, and a
/// caller that forgets to use the helper ships anyway.
/// </summary>
public class BundleTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "wb-bundle-" + Guid.NewGuid().ToString("n")[..12]);

    public BundleTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private EventLog World()
    {
        Simulation sim = new(42);
        sim.Run(4);
        return sim.Log;
    }

    private string WriteArtefact(string name, string content)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    [Fact]
    public void AWorldWithNoArtefactsWritesTheSameHeaderItAlwaysDid()
    {
        // The fields are omitted rather than written empty, so adding them to the header does not
        // move a single byte of any world that has none — which is what lets the ruleset-3
        // baselines stay comparable across this change.
        EventLog log = World();

        WorldBundle.Write(_dir, log, 42, []);

        string header = File.ReadLines(WorldBundle.WorldPath(_dir, 42)).First();

        Assert.DoesNotContain("artefacts", header, StringComparison.Ordinal);
        Assert.DoesNotContain("render_cache", header, StringComparison.Ordinal);
        Assert.Equal(JsonlIo.Header(42, log.Count), header);
    }

    [Fact]
    public void TheHeaderRecordsAHashForEveryStoredArtefact()
    {
        EventLog log = World();
        string board = WriteArtefact(WorldBundle.BoardName, "{\"format\":\"wb-board/1\"}");

        WorldBundle.Write(_dir, log, 42, [WorldBundle.BoardName], "cache-fingerprint");

        BundleOpen opened = WorldBundle.Open(WorldBundle.WorldPath(_dir, 42));

        StoredArtefact recorded = Assert.Single(opened.Header!.Artefacts);
        Assert.Equal(WorldBundle.BoardName, recorded.Name);
        Assert.Equal(WorldBundle.HashOf(board), recorded.Sha256);
        Assert.Equal("cache-fingerprint", opened.Header.RenderCacheFingerprint);
    }

    [Fact]
    public void OpeningABundleWhoseArtefactHasChangedFailsLoudly()
    {
        EventLog log = World();
        WriteArtefact(WorldBundle.BoardName, "{\"format\":\"wb-board/1\",\"cells\":[]}");
        WorldBundle.Write(_dir, log, 42, [WorldBundle.BoardName]);

        // One byte. A map that does not match its hash is not a map to proceed with, and the
        // reason this refuses rather than notes is that nothing downstream would look wrong: the
        // distances would simply all be different, and the history built from them consistent.
        WriteArtefact(WorldBundle.BoardName, "{\"format\":\"wb-board/1\",\"cells\":[ ]}");

        BundleIntegrityException thrown = Assert.Throws<BundleIntegrityException>(
            () => WorldBundle.Open(WorldBundle.WorldPath(_dir, 42)));

        Assert.Contains(WorldBundle.BoardName, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpeningABundleMissingAnArtefactFailsLoudly()
    {
        EventLog log = World();
        WriteArtefact(WorldBundle.BoardName, "{\"format\":\"wb-board/1\"}");
        WorldBundle.Write(_dir, log, 42, [WorldBundle.BoardName]);

        File.Delete(Path.Combine(_dir, WorldBundle.BoardName));

        BundleIntegrityException thrown = Assert.Throws<BundleIntegrityException>(
            () => WorldBundle.Open(WorldBundle.WorldPath(_dir, 42)));

        Assert.Contains("not in the bundle", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WritingABundleWhoseArtefactIsNotThereRefusesRatherThanRecordingNothing()
    {
        // The failure mode worth forbidding: a bundle that quietly records fewer artefacts than
        // it was asked to still looks complete, and the world it describes is missing a piece
        // nobody can name later.
        Assert.Throws<FileNotFoundException>(
            () => WorldBundle.Write(_dir, World(), 42, [WorldBundle.BoardName]));
    }

    [Fact]
    public void ArtefactsAreRecordedInASettledOrderWhateverOrderTheyAreGivenIn()
    {
        EventLog log = World();
        WriteArtefact("a.json", "one");
        WriteArtefact("b.json", "two");

        WorldBundle.Write(_dir, log, 42, ["b.json", "a.json"]);
        string first = File.ReadLines(WorldBundle.WorldPath(_dir, 42)).First();

        WorldBundle.Write(_dir, log, 42, ["a.json", "b.json"]);
        string second = File.ReadLines(WorldBundle.WorldPath(_dir, 42)).First();

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheOpeningPolicyStillApplies()
    {
        // A ruleset mismatch opens with a note; the log is complete and what it has lost is only
        // the ability to be rebuilt from its seed. Integrity checking must not have turned that
        // into a refusal.
        EventLog log = World();
        WorldBundle.Write(_dir, log, 42, []);

        string path = WorldBundle.WorldPath(_dir, 42);
        string[] lines = File.ReadAllLines(path);
        lines[0] = lines[0].Replace($"\"ruleset_version\":\"{Ruleset.Version}\"", "\"ruleset_version\":\"1\"",
            StringComparison.Ordinal);
        File.WriteAllLines(path, lines);

        BundleOpen opened = WorldBundle.Open(path);

        Assert.Contains(opened.Notes, n => n.Contains("ruleset 1", StringComparison.Ordinal));
        Assert.Equal(log.Count, opened.Log.Count);
    }

    [Fact]
    public void ANewerEngineStillRefusesUnlessToldOtherwise()
    {
        EventLog log = World();
        WorldBundle.Write(_dir, log, 42, []);

        string path = WorldBundle.WorldPath(_dir, 42);
        string[] lines = File.ReadAllLines(path);
        lines[0] = lines[0].Replace($"\"engine_version\":\"{Engine.Version}\"", "\"engine_version\":\"9.9.9\"",
            StringComparison.Ordinal);
        File.WriteAllLines(path, lines);

        Assert.Throws<FormatException>(() => WorldBundle.Open(path));
        Assert.Equal(log.Count, WorldBundle.Open(path, acceptNewer: true).Log.Count);
    }

    [Fact]
    public void AHeaderRoundTripsThroughItsOwnSerialisation()
    {
        WorldHeader header = new()
        {
            Seed = 7,
            Events = 3,
            EngineVersion = "1.2.0",
            EngineCommit = "abc",
            RulesetVersion = "4",
            RenderCacheFingerprint = "ffff",
            Artefacts = [new StoredArtefact("board.wbmap.json", "0011")],
        };

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(header.Serialise());
        WorldHeader read = WorldHeader.Parse(doc.RootElement);

        // Field by field rather than by record equality: the artefact list is a reference, so the
        // generated Equals compares two lists by identity and would pass on an empty one.
        Assert.Equal(header.Seed, read.Seed);
        Assert.Equal(header.Events, read.Events);
        Assert.Equal(header.EngineVersion, read.EngineVersion);
        Assert.Equal(header.EngineCommit, read.EngineCommit);
        Assert.Equal(header.RulesetVersion, read.RulesetVersion);
        Assert.Equal(header.RenderCacheFingerprint, read.RenderCacheFingerprint);
        Assert.Equal(header.Artefacts, read.Artefacts);
        Assert.Equal(header.Serialise(), read.Serialise());
    }
}
