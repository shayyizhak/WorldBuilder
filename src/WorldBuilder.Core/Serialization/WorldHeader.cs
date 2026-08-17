using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WorldBuilder.Core.Serialization;

/// <summary>
/// The first line of a world file: what this world is, and what produced it.
///
/// The header exists because the event log is the durable artefact and a seed is not a
/// regeneration recipe. Two things break "seed reproduces the world" — a rule change, and the
/// model's own run-to-run variance — so a world that cannot say which engine and which ruleset
/// wrote it is a world nobody can reason about later.
///
/// <c>engine_version</c> and <c>ruleset_version</c> are two counters, not one written twice.
/// The engine version says which build wrote the file and therefore what can read it; the
/// ruleset version says whether this build, given the same seed, would produce the same world.
/// They coincided at 1.2.0 for as long as rules only changed alongside releases, and the ruleset
/// now carries its own sequence starting at "1" precisely so that the coincidence cannot be read
/// as a constraint — see <see cref="Ruleset"/>.
///
/// Nothing here varies between two runs of the same seed on the same build, which is deliberate:
/// the determinism guarantee is checked by comparing files, and a timestamp or a machine name in
/// the header would break that for no gain. Provenance that changes when nothing happened is
/// noise, and noise in a header is how a byte-comparison stops being usable as a test.
/// </summary>
public sealed record WorldHeader
{
    public required ulong Seed { get; init; }
    public required int Events { get; init; }

    /// <summary>Empty for a file written before the header carried provenance.</summary>
    public string EngineVersion { get; init; } = "";
    public string EngineCommit { get; init; } = "";
    public string RulesetVersion { get; init; } = "";

    /// <summary>
    /// The artefacts stored beside this world, each with the sha256 of its bytes.
    ///
    /// Deferred at Stage 3 and correctly so: <c>wb run</c> knew nothing about renders and no
    /// world bundle existed, so there was nothing for a hash to be a hash of. An imported map is
    /// the artefact that forces the issue. It is not derivable from the seed, it decides every
    /// distance the simulation measures, and a world carrying the wrong one produces a history
    /// that is internally consistent and about a different map.
    ///
    /// Empty is the ordinary case for a bare event log, and the field is omitted entirely when
    /// empty, so a world with no artefacts is not confusable with one whose artefact list was
    /// lost.
    /// </summary>
    public IReadOnlyList<StoredArtefact> Artefacts { get; init; } = [];

    /// <summary>
    /// Which passages this world's prose consists of — see <see cref="WorldBundle"/>.
    ///
    /// A fingerprint over the cache's contents rather than a hash of the file, because the cache
    /// is appended to constantly and the question worth answering is "is this the same prose",
    /// not "is this the same file". Empty where no render cache travels with the world.
    /// </summary>
    public string RenderCacheFingerprint { get; init; } = "";

    /// <summary>
    /// The synthetic distance model this world ran under, and empty for every real world.
    ///
    /// A control world is a diagnostic artefact: its rules were told fabricated distances so that
    /// two explanations which make the same prediction could be told apart. It is a perfectly
    /// valid history of nowhere, and on disk it is a <c>world-42.jsonl</c> like any other — which
    /// is exactly why it has to say so here. Nothing that archives, seals or renders a world may
    /// accept one.
    /// </summary>
    public string Control { get; init; } = "";

    /// <summary>True where this world's distances were fabricated rather than measured.</summary>
    public bool IsControl => Control.Length > 0;

    /// <summary>
    /// True where the file predates provenance in the header. Not an error — the v1 artefacts are
    /// all like this, and they are the hand-verified ones — but it is a thing a reader must be
    /// told rather than left to assume.
    /// </summary>
    public bool ProvenanceUnknown => EngineVersion.Length == 0;

    /// <summary>A header describing a world this build is producing now.</summary>
    public static WorldHeader ForThisBuild(ulong seed, int events) => new()
    {
        Seed = seed,
        Events = events,
        EngineVersion = Engine.Version,
        EngineCommit = Engine.Commit,
        RulesetVersion = Ruleset.Version,
    };

    /// <summary>
    /// Written by hand so field order is fixed, for the same reason the events are: the file is
    /// compared byte-for-byte. Provenance fields are omitted entirely when empty rather than
    /// written as <c>""</c>, so a file this build writes is never confusable with one that
    /// carried no provenance at all.
    /// </summary>
    public string Serialise()
    {
        StringBuilder sb = new();
        sb.Append("{\"type\":\"world\"");
        sb.Append(",\"seed\":\"").Append(Seed.ToString(CultureInfo.InvariantCulture)).Append('"');
        sb.Append(",\"events\":").Append(Events.ToString(CultureInfo.InvariantCulture));

        if (EngineVersion.Length > 0) Append(sb, "engine_version", EngineVersion);
        if (EngineCommit.Length > 0) Append(sb, "engine_commit", EngineCommit);
        if (RulesetVersion.Length > 0) Append(sb, "ruleset_version", RulesetVersion);
        if (Control.Length > 0) Append(sb, "control", Control);
        if (RenderCacheFingerprint.Length > 0) Append(sb, "render_cache", RenderCacheFingerprint);

        if (Artefacts.Count > 0)
        {
            sb.Append(",\"artefacts\":[");
            for (int i = 0; i < Artefacts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":").Append(JsonSerializer.Serialize(Artefacts[i].Name))
                  .Append(",\"sha256\":\"").Append(Artefacts[i].Sha256).Append("\"}");
            }
            sb.Append(']');
        }

        sb.Append('}');
        return sb.ToString();
    }

    public static WorldHeader Parse(JsonElement root)
    {
        List<StoredArtefact> artefacts = [];
        if (root.TryGetProperty("artefacts", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
            foreach (JsonElement a in list.EnumerateArray())
                artefacts.Add(new StoredArtefact(Text(a, "name"), Text(a, "sha256")));

        return new WorldHeader
        {
            Seed = ulong.Parse(root.GetProperty("seed").GetString()!, CultureInfo.InvariantCulture),
            Events = root.TryGetProperty("events", out JsonElement n) && n.TryGetInt32(out int count) ? count : 0,
            EngineVersion = Text(root, "engine_version"),
            EngineCommit = Text(root, "engine_commit"),
            RulesetVersion = Text(root, "ruleset_version"),
            Control = Text(root, "control"),
            RenderCacheFingerprint = Text(root, "render_cache"),
            Artefacts = artefacts,
        };
    }

    private static void Append(StringBuilder sb, string name, string value) =>
        sb.Append(",\"").Append(name).Append("\":").Append(JsonSerializer.Serialize(value));

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement e) ? e.GetString() ?? "" : "";
}

/// <summary>
/// A file that travels with the world, and the sha256 of its bytes at the moment it was stored.
///
/// The name is relative to the bundle directory, never an absolute path: a bundle that records
/// where its files were on the machine that wrote it is a bundle that cannot be moved.
/// </summary>
public sealed record StoredArtefact(string Name, string Sha256);

/// <summary>What opening this world file under this build means.</summary>
public sealed record WorldFileCheck
{
    /// <summary>
    /// Whether opening should stop. True in exactly one case — a world written by a newer engine
    /// than this one — because that is the only case where reading quietly produces a wrong
    /// answer rather than an incomplete one.
    /// </summary>
    public required bool Blocks { get; init; }

    /// <summary>Everything a reader has to be told, in the order it should be said. Empty is the ordinary case.</summary>
    public required IReadOnlyList<string> Notes { get; init; }
}

/// <summary>
/// The policy for opening a world file that this build did not write.
///
/// The rule is that a mismatch is reported and only one kind of mismatch refuses.
///
/// <b>A newer engine blocks.</b> An older engine reading a newer file cannot know what it is
/// failing to understand; unknown event kinds already throw, but a field added to an existing
/// kind would simply be dropped, and a world silently missing part of itself is exactly the
/// shape of failure this project keeps finding — a gap that presents as a pass. Overriding it
/// has to be a deliberate act, not a default.
///
/// <b>Everything else opens and says so.</b> A ruleset change in particular does not make a
/// world unreadable: the log is the artefact, and it is complete. What a ruleset change costs
/// is the ability to regenerate that world from its seed, which is a different loss and is worth
/// naming as one.
/// </summary>
public static class WorldCompatibility
{
    public static WorldFileCheck Check(WorldHeader header) => Check(header, Engine.Version, Ruleset.Version);

    /// <summary>Overload taking the current versions explicitly, so the policy is testable without a rebuild.</summary>
    public static WorldFileCheck Check(WorldHeader header, string engineVersion, string rulesetVersion)
    {
        List<string> notes = [];
        bool blocks = false;

        if (header.ProvenanceUnknown)
        {
            notes.Add(
                "this world file carries no engine or ruleset provenance — it predates the world " +
                "header. It is readable, but nothing records what produced it.");
        }
        else
        {
            if (Version.TryParse(header.EngineVersion, out Version? wrote)
                && Version.TryParse(engineVersion, out Version? running))
            {
                if (wrote > running)
                {
                    blocks = true;
                    notes.Add(
                        $"this world was written by engine {header.EngineVersion} and this is " +
                        $"{engineVersion}. A newer file may carry fields this build would drop " +
                        "without noticing. Pass --accept-newer to open it anyway.");
                }
                else if (wrote < running)
                {
                    notes.Add($"written by engine {header.EngineVersion}; this is {engineVersion}.");
                }
            }
            else if (!string.Equals(header.EngineVersion, engineVersion, StringComparison.Ordinal))
            {
                notes.Add($"written by engine '{header.EngineVersion}'; this is '{engineVersion}'.");
            }

            if (header.RulesetVersion.Length > 0
                && !string.Equals(header.RulesetVersion, rulesetVersion, StringComparison.Ordinal))
            {
                notes.Add(
                    $"written under ruleset {header.RulesetVersion} and this build runs " +
                    $"{rulesetVersion}. The log is complete and reads normally; what it can no " +
                    "longer do is be regenerated from its seed by this build.");
            }
        }

        return new WorldFileCheck { Blocks = blocks, Notes = notes };
    }
}
