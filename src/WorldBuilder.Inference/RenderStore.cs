using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WorldBuilder.Inference;

/// <summary>How a render came to hold the text it holds.</summary>
public enum RenderStatus
{
    Generated,
    Accepted,
    Edited,
    Rejected,

    /// <summary>
    /// Generated, checked, and found to contain something the records do not support — after a
    /// second attempt with the findings handed back. Kept so the run is reproducible and the
    /// failure is inspectable, but it is explicitly not canon and the document says so.
    /// </summary>
    Suspect,
}

public sealed record Render
{
    public required string PackKey { get; init; }
    public required string PromptVersion { get; init; }
    public required string Model { get; init; }
    public required string Text { get; init; }
    public required int Year { get; init; }
    public RenderStatus Status { get; init; } = RenderStatus.Generated;

    /// <summary>The model's own words, kept when a human has since edited <see cref="Text"/>.</summary>
    public string? Original { get; init; }

    public int PromptTokens { get; init; }
    public int OutputTokens { get; init; }
    public int ElapsedMs { get; init; }

    /// <summary>Cache identity. A new prompt or a new model produces a new entry, never an
    /// overwrite — swapping either must not rewrite history that already exists.</summary>
    public string Id => $"{PackKey}|{PromptVersion}|{Model}";
}

/// <summary>
/// The cache, and therefore the canon.
///
/// Once a passage exists it is the world's text. Nothing regenerates silently: a prompt change
/// or a model swap writes a new entry under a new key and leaves the old one standing. This is
/// also what preserves reproducibility — the engine is deterministic and the model is not, so
/// the world's prose is pinned by the cache rather than by the model.
/// </summary>
public sealed class RenderStore
{
    private readonly Dictionary<string, Render> _byId = [];
    private readonly string _path;

    public RenderStore(string path)
    {
        _path = path;
        Load();
    }

    public int Count => _byId.Count;

    public bool TryGet(string packKey, string promptVersion, string model, out Render render) =>
        _byId.TryGetValue($"{packKey}|{promptVersion}|{model}", out render!);

    /// <summary>All renders for a pack, whatever prompt or model produced them.</summary>
    public List<Render> ForPack(string packKey)
    {
        List<Render> found = [];
        foreach (Render r in _byId.Values)
            if (r.PackKey == packKey) found.Add(r);

        found.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
        return found;
    }

    public void Put(Render render)
    {
        _byId[render.Id] = render;
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;

        foreach (string line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement e = doc.RootElement;

            Render render = new()
            {
                PackKey = e.GetProperty("packKey").GetString()!,
                PromptVersion = e.GetProperty("promptVersion").GetString()!,
                Model = e.GetProperty("model").GetString()!,
                Text = e.GetProperty("text").GetString()!,
                Year = e.GetProperty("year").GetInt32(),
                Status = Enum.Parse<RenderStatus>(e.GetProperty("status").GetString()!),
                Original = e.TryGetProperty("original", out JsonElement o) ? o.GetString() : null,
                PromptTokens = Read(e, "promptTokens"),
                OutputTokens = Read(e, "outputTokens"),
                ElapsedMs = Read(e, "elapsedMs"),
            };
            _byId[render.Id] = render;
        }
    }

    private static int Read(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement v) && v.TryGetInt32(out int i) ? i : 0;

    private void Save()
    {
        List<Render> ordered = [.. _byId.Values];
        ordered.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));

        StringBuilder sb = new();
        foreach (Render r in ordered)
        {
            sb.Append('{');
            sb.Append("\"packKey\":").Append(Json(r.PackKey));
            sb.Append(",\"promptVersion\":").Append(Json(r.PromptVersion));
            sb.Append(",\"model\":").Append(Json(r.Model));
            sb.Append(",\"year\":").Append(r.Year.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"status\":").Append(Json(r.Status.ToString()));
            sb.Append(",\"promptTokens\":").Append(r.PromptTokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"outputTokens\":").Append(r.OutputTokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"elapsedMs\":").Append(r.ElapsedMs.ToString(CultureInfo.InvariantCulture));
            if (r.Original is not null) sb.Append(",\"original\":").Append(Json(r.Original));
            sb.Append(",\"text\":").Append(Json(r.Text));
            sb.Append("}\n");
        }

        string? dir = Path.GetDirectoryName(_path);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, sb.ToString());
    }

    private static string Json(string value) => JsonSerializer.Serialize(value);
}

/// <summary>
/// Append-only record of every render pair: what went in, what came back, and what the human
/// did about it.
///
/// This is the fine-tuning corpus, and it has to start accumulating on day one because it
/// cannot be reconstructed afterwards. It stays derived from this project's own accepted and
/// edited output — never from another model's — so the base model remains swappable.
/// </summary>
public sealed class RenderJournal(string path)
{
    private readonly string _path = path;

    public void Record(ContextPack pack, string promptVersion, string system, string prompt, Render render)
    {
        StringBuilder sb = new();
        sb.Append('{');
        sb.Append("\"at\":").Append(Json(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        sb.Append(",\"packKey\":").Append(Json(pack.Key));
        sb.Append(",\"packKind\":").Append(Json(pack.Kind.ToString()));
        sb.Append(",\"events\":").Append(pack.Events.Count.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"fromYear\":").Append(pack.FromYear.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"toYear\":").Append(pack.ToYear.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"promptVersion\":").Append(Json(promptVersion));
        sb.Append(",\"model\":").Append(Json(render.Model));
        sb.Append(",\"status\":").Append(Json(render.Status.ToString()));
        sb.Append(",\"promptTokens\":").Append(render.PromptTokens.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"outputTokens\":").Append(render.OutputTokens.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"elapsedMs\":").Append(render.ElapsedMs.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"system\":").Append(Json(system));
        sb.Append(",\"prompt\":").Append(Json(prompt));
        sb.Append(",\"output\":").Append(Json(render.Original ?? render.Text));
        if (render.Original is not null) sb.Append(",\"edited\":").Append(Json(render.Text));
        sb.Append("}\n");

        string? dir = Path.GetDirectoryName(_path);
        if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
        File.AppendAllText(_path, sb.ToString());
    }

    private static string Json(string value) => JsonSerializer.Serialize(value);
}
