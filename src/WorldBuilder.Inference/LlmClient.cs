using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WorldBuilder.Inference;

public sealed record LlmRequest
{
    public required string System { get; init; }
    public required string Prompt { get; init; }

    /// <summary>JSON Schema the reply must satisfy. When set, malformed output is impossible
    /// rather than merely discouraged.</summary>
    public string? Schema { get; init; }

    /// <summary>Overrides <see cref="LlmOptions.Think"/> for a single call — the reasoning half
    /// of the two-call pattern wants it on.</summary>
    public bool? Think { get; init; }

    public int? MaxTokens { get; init; }

    /// <summary>
    /// Moves this call off the configured sampling seed, without giving the caller the seed.
    ///
    /// An offset rather than a seed, so reproducibility survives: attempt <i>n</i> of a given
    /// question is still exactly reproducible; it is simply not attempt <i>n−1</i>.
    /// </summary>
    public int SeedOffset { get; init; }

    /// <summary>
    /// Overrides <see cref="LlmOptions.TemperatureCentis"/> for a single call.
    ///
    /// Zero is right for rendering and is the reason a re-render of a pack reproduces its
    /// passage — but at zero the decoding is greedy, which means a retry re-derives the answer
    /// it was just told was wrong. An answer rejected for writing "Hdale" came back from two
    /// retries still saying "Hdale": the correction named the word, the seed had moved, and
    /// neither mattered, because at temperature zero neither is consulted.
    ///
    /// A retry is the one call that must be allowed to differ. It is still deterministic — the
    /// seed is fixed — so a question asked twice gives the same answer both times.
    /// </summary>
    public int? TemperatureCentis { get; init; }
}

public sealed record LlmResult
{
    public required string Text { get; init; }
    public required string Model { get; init; }
    public int PromptTokens { get; init; }
    public int OutputTokens { get; init; }
    public int ElapsedMs { get; init; }

    /// <summary>Reasoning, when a thinking model produced any. Never treated as output.</summary>
    public string? Thinking { get; init; }

    /// <summary>
    /// Why generation stopped. "length" means the token budget ran out mid-sentence, which
    /// must never be cached — a section that ended on "In 38, Throll" became canon silently.
    /// </summary>
    public string StopReason { get; init; } = "stop";

    public bool Truncated => string.Equals(StopReason, "length", StringComparison.OrdinalIgnoreCase);
}

public interface ILlmClient
{
    string ModelTag { get; }
    Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default);
}

/// <summary>
/// Talks to Ollama's native endpoint, which is where structured output lives. Nothing in here
/// knows which model it is speaking to.
/// </summary>
public sealed class OllamaClient(LlmOptions options, HttpClient? http = null) : ILlmClient, IDisposable
{
    private readonly LlmOptions _options = options;
    private readonly HttpClient _http = http ?? new HttpClient();
    private readonly bool _ownsHttp = http is null;

    public string ModelTag => _options.Model;

    public async Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        string body = BuildBody(request);
        using StringContent content = new(body, Encoding.UTF8, "application/json");

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        long started = Environment.TickCount64;

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync($"{_options.Endpoint}/api/generate", content, timeout.Token);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmUnavailableException(
                $"cannot reach Ollama at {_options.Endpoint} — is it running? ({ex.Message})", ex);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmUnavailableException(
                $"model '{_options.Model}' did not answer within {_options.TimeoutSeconds}s.");
        }

        using (response)
        {
            string payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new LlmUnavailableException($"Ollama returned {(int)response.StatusCode}: {Trim(payload)}");

            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;

            return new LlmResult
            {
                Text = (root.TryGetProperty("response", out JsonElement r) ? r.GetString() : null)?.Trim() ?? "",
                Model = _options.Model,
                PromptTokens = Count(root, "prompt_eval_count"),
                OutputTokens = Count(root, "eval_count"),
                ElapsedMs = (int)(Environment.TickCount64 - started),
                Thinking = root.TryGetProperty("thinking", out JsonElement t) ? t.GetString() : null,
                StopReason = root.TryGetProperty("done_reason", out JsonElement d)
                    ? d.GetString() ?? "stop" : "stop",
            };
        }
    }

    private string BuildBody(LlmRequest request)
    {
        StringBuilder sb = new();
        sb.Append('{');
        sb.Append("\"model\":").Append(Json(_options.Model));
        sb.Append(",\"system\":").Append(Json(request.System));
        sb.Append(",\"prompt\":").Append(Json(request.Prompt));
        sb.Append(",\"stream\":false");
        sb.Append(",\"think\":").Append((request.Think ?? _options.Think) ? "true" : "false");
        sb.Append(",\"keep_alive\":").Append(Json(_options.KeepAlive));

        if (request.Schema is { Length: > 0 }) sb.Append(",\"format\":").Append(request.Schema);

        sb.Append(",\"options\":{");
        sb.Append("\"temperature\":")
          .Append(((request.TemperatureCentis ?? _options.TemperatureCentis) / 100.0)
              .ToString("0.00", CultureInfo.InvariantCulture));
        sb.Append(",\"seed\":")
          .Append((_options.Seed + request.SeedOffset).ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"num_ctx\":").Append(_options.ContextTokens.ToString(CultureInfo.InvariantCulture));
        // The model's shipped sampling defaults are tuned for chat, not for a record that has
        // to stay stable. Pinned explicitly so nothing is inherited.
        sb.Append(",\"top_p\":1,\"repeat_penalty\":1,\"presence_penalty\":0,\"frequency_penalty\":0");
        sb.Append(",\"num_predict\":")
          .Append((request.MaxTokens ?? _options.MaxTokens).ToString(CultureInfo.InvariantCulture));
        sb.Append('}');

        sb.Append('}');
        return sb.ToString();
    }

    private static int Count(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement e) && e.TryGetInt32(out int v) ? v : 0;

    private static string Json(string value) => JsonSerializer.Serialize(value);

    private static string Trim(string s) => s.Length <= 300 ? s : s[..300] + "…";

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}

/// <summary>
/// A client that answers nothing, so a command holding one can only read the cache.
///
/// The point is the guarantee rather than the behaviour: re-running the checker over an
/// archived render cache has to be provably free of inference, and "we did not observe a call"
/// is not a proof. Here a call is unreachable — the request cannot be made, so a cache miss
/// surfaces as the missing render it is instead of being repaired by generating a passage no
/// one has verified. It carries the model tag because cache identity includes the model: the
/// entries to be re-checked are the ones that model wrote.
/// </summary>
public sealed class CacheOnlyLlmClient(string model) : ILlmClient
{
    public string ModelTag { get; } = model;

    public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
        throw new LlmUnavailableException(
            $"nothing cached for this pack under model '{ModelTag}', and --check-only does not " +
            "generate. Re-check needs a render cache that already covers every section.");
}

public sealed class LlmUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
