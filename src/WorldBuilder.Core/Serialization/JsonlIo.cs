using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WorldBuilder.Core.Serialization;

/// <summary>
/// The machine-readable half of the output: one JSON object per event, written by hand so
/// field order is fixed and two runs of the same seed are byte-identical files. The whole
/// determinism guarantee is checked by hashing this, so nothing in here may depend on
/// dictionary ordering, culture, or floating point.
/// </summary>
public static class JsonlIo
{
    /// <summary>
    /// Writes a world file. <paramref name="control"/> names the synthetic distance model a
    /// diagnostic run used, and is empty for every real world — see <see cref="WorldHeader.Control"/>.
    /// </summary>
    public static void Write(string path, EventLog log, ulong seed, string control = "")
    {
        using StreamWriter writer = new(path, append: false, Encoding.UTF8);
        writer.NewLine = "\n";

        writer.WriteLine(Header(seed, log.Count, control));
        foreach (Event e in log.Events) writer.WriteLine(Serialise(e));
    }

    public static string Header(ulong seed, int count, string control = "") =>
        (WorldHeader.ForThisBuild(seed, count) with { Control = control }).Serialise();

    public static string Serialise(Event e)
    {
        StringBuilder sb = new();
        sb.Append('{');

        sb.Append("\"id\":").Append(e.Id.Value.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"key\":\"").Append(e.Key).Append('"');
        sb.Append(",\"year\":").Append(e.Year.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"kind\":\"").Append(EventKinds.Name(e.Kind)).Append('"');
        sb.Append(",\"sig\":\"").Append(e.Significance).Append('"');
        sb.Append(",\"scope\":\"").Append(e.Scope).Append('"');
        sb.Append(",\"outcome\":\"").Append(e.Outcome).Append('"');
        sb.Append(",\"origin\":\"").Append(e.Origin).Append('"');
        sb.Append(",\"arc\":\"").Append(e.Arc).Append('"');

        sb.Append(",\"parts\":[");
        for (int i = 0; i < e.Participants.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"r\":\"").Append(e.Participants[i].Role).Append("\",\"id\":\"")
              .Append(e.Participants[i].Id).Append("\"}");
        }
        sb.Append(']');

        AppendStringArray(sb, "causes", e.Causes.Select(static c => c.ToString()));
        AppendStringArray(sb, "witnesses", e.Witnesses.Select(static w => w.ToString()));

        sb.Append(",\"data\":{");
        for (int i = 0; i < e.Data.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Escape(e.Data[i].Key)).Append("\":\"").Append(Escape(e.Data[i].Value)).Append('"');
        }
        sb.Append('}');

        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendStringArray(StringBuilder sb, string name, IEnumerable<string> values)
    {
        sb.Append(",\"").Append(name).Append("\":[");
        bool first = true;
        foreach (string v in values)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(Escape(v)).Append('"');
        }
        sb.Append(']');
    }

    private static string Escape(string value)
    {
        if (value.AsSpan().IndexOfAny('"', '\\', '\n') < 0 && !value.Contains('\r', StringComparison.Ordinal))
            return value;

        StringBuilder sb = new(value.Length + 8);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // ---- reading ----------------------------------------------------------

    /// <summary>
    /// Reads a world file back into a log. Every query command goes through this rather than
    /// re-running the simulation, which means each invocation silently re-proves that the log
    /// alone is enough to reconstruct the world.
    /// </summary>
    public static (EventLog Log, ulong Seed) Read(string path)
    {
        EventLog log = new();
        ulong seed = 0;
        bool first = true;

        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;

            if (first)
            {
                first = false;
                if (IsHeader(root))
                {
                    seed = WorldHeader.Parse(root).Seed;
                    continue;
                }
            }

            log.Append(Deserialise(root));
        }

        return (log, seed);
    }

    /// <summary>
    /// The header alone, without reading a thousand events to get at it.
    ///
    /// Returns null for a file with no header rather than throwing. A headerless world file is a
    /// real thing — every v1 artefact is one — and the caller's job is to say so, not to fail.
    /// </summary>
    public static WorldHeader? ReadHeader(string path)
    {
        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using JsonDocument doc = JsonDocument.Parse(line);
            return IsHeader(doc.RootElement) ? WorldHeader.Parse(doc.RootElement) : null;
        }

        return null;
    }

    private static bool IsHeader(JsonElement root) =>
        root.TryGetProperty("type", out JsonElement type) && type.GetString() == "world";

    private static Event Deserialise(JsonElement root)
    {
        if (!EventKinds.TryParse(root.GetProperty("kind").GetString()!, out EventKind kind))
            throw new FormatException($"Unknown event kind '{root.GetProperty("kind").GetString()}'.");

        List<Participant> parts = [];
        foreach (JsonElement p in root.GetProperty("parts").EnumerateArray())
        {
            EntityId.TryParse(p.GetProperty("id").GetString(), out EntityId id);
            parts.Add(new Participant(Enum.Parse<Role>(p.GetProperty("r").GetString()!), id));
        }

        List<EventId> causes = [];
        foreach (JsonElement c in root.GetProperty("causes").EnumerateArray())
            if (EventId.TryParse(c.GetString(), out EventId cause)) causes.Add(cause);

        List<EntityId> witnesses = [];
        foreach (JsonElement w in root.GetProperty("witnesses").EnumerateArray())
            if (EntityId.TryParse(w.GetString(), out EntityId id)) witnesses.Add(id);

        List<KeyValuePair<string, string>> data = [];
        foreach (JsonProperty d in root.GetProperty("data").EnumerateObject())
            data.Add(new KeyValuePair<string, string>(d.Name, d.Value.GetString() ?? ""));

        EntityId.TryParse(root.GetProperty("arc").GetString(), out EntityId arc);

        return new Event
        {
            Id = new EventId(root.GetProperty("id").GetInt32()),
            Key = root.GetProperty("key").GetString()!,
            Year = root.GetProperty("year").GetInt32(),
            Kind = kind,
            Participants = parts,
            Causes = causes,
            Witnesses = witnesses,
            Data = data,
            Scope = Enum.Parse<Visibility>(root.GetProperty("scope").GetString()!),
            Significance = Enum.Parse<Significance>(root.GetProperty("sig").GetString()!),
            Outcome = Enum.Parse<Outcome>(root.GetProperty("outcome").GetString()!),
            Origin = Enum.Parse<Provenance>(root.GetProperty("origin").GetString()!),
            Arc = arc,
        };
    }
}
