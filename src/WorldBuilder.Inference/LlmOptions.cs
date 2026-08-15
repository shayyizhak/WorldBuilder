namespace WorldBuilder.Inference;

/// <summary>
/// Everything about the model, in one place and none of it in code.
///
/// The model tag is configuration and nothing anywhere branches on it. That is a licensing
/// requirement as much as an engineering one: the project is restricted to Apache-2.0 weights,
/// and the base model has to stay swappable for that to remain true.
/// </summary>
public sealed record LlmOptions
{
    public static readonly LlmOptions Default = new();

    public string Endpoint { get; init; } = "http://localhost:11434";

    /// <summary>The tag as it exists locally. Never hard-code a model's quirks against this.</summary>
    public string Model { get; init; } = "qwen3.6:latest";

    /// <summary>
    /// Reasoning is off for rendering. Qwen3.6 is a thinking model and will otherwise spend its
    /// whole token budget deliberating and return empty prose — measured, not assumed. The
    /// two-call pattern turns it back on for the reasoning call only.
    /// </summary>
    public bool Think { get; init; }

    public int MaxTokens { get; init; } = 700;

    /// <summary>
    /// Zero, with a fixed seed. Two renders of similar inputs disagreed on fact — one said a
    /// plotter was exiled, the other omitted it — and both would have been cached as canon.
    /// Sampling variance is not a feature here: the world's text must be a function of the
    /// world's events, and the model's shipped defaults (temperature 1) are wrong for that.
    /// </summary>
    public int TemperatureCentis { get; init; }

    /// <summary>Fixed sampling seed, so a re-render of the same pack reproduces the passage.</summary>
    public int Seed { get; init; } = 20260810;

    /// <summary>
    /// Context window to request.
    ///
    /// Raised from 8192 once the digests began enumerating what they count. The largest pack
    /// plus the shared rules plus a retry's correction now runs close to six thousand tokens,
    /// and generation that overruns the window is reported as a length stop — which the
    /// renderer correctly refuses to cache, so a scope simply produced nothing. Headroom is
    /// cheaper than a missing chapter.
    /// </summary>
    public int ContextTokens { get; init; } = 16384;

    /// <summary>
    /// Generous by default. On the reference machine a 2,000-token pack costs roughly 80
    /// seconds in prompt evaluation alone before a word is generated.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 900;

    /// <summary>Keeps the weights resident between calls; a cold load costs ~45 seconds.</summary>
    public string KeepAlive { get; init; } = "30m";

    public double Temperature => TemperatureCentis / 100.0;
}
