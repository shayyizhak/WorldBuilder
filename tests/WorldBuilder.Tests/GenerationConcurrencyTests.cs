using System.Net;
using System.Text;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The generation path is bounded, and exceeding the bound fails loudly.
///
/// A Stage 15 finding that arrived early and cheaply. The local model wedged twice under concurrent
/// load from a headless measurement panel, and both times the symptom was a request that never
/// returned — no error, no diagnosis, a run that looked slow. Stage 15's economics rest entirely on
/// the render cache and Stage 10 scales to 2,000+ actors, at which point rendering is the
/// bottleneck and concurrency stops being optional, so a generation path that wedges under
/// concurrent load is load-bearing.
///
/// Recorded on the Stage 15 card, mitigated here, and deliberately not investigated further this
/// phase.
///
/// <b>Entered through <see cref="ILlmClient.CompleteAsync"/></b>, which is the only method any
/// caller in this engine uses. A test against the semaphore would pass while the client bypassed
/// it, which is the shape of two of this project's silent paths.
/// </summary>
public class GenerationConcurrencyTests
{
    /// <summary>
    /// A handler that answers when told to, so a call can be held in flight while a second is made.
    ///
    /// No sleeps and no timing assumptions: the first call blocks on a gate the test opens, so the
    /// second call meets a genuinely occupied slot rather than a hoped-for one.
    /// </summary>
    private sealed class HeldHandler : HttpMessageHandler
    {
        private readonly SemaphoreSlim _release = new(0);

        /// <summary>Signalled once a request has actually arrived and is being held.</summary>
        public SemaphoreSlim Arrived { get; } = new(0);

        public void Release() => _release.Release();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Arrived.Release();
            await _release.WaitAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"response":"a sentence","done_reason":"stop","eval_count":3}""",
                    Encoding.UTF8, "application/json"),
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _release.Dispose(); Arrived.Dispose(); }
            base.Dispose(disposing);
        }
    }

    private static LlmRequest Anything => new() { System = "s", Prompt = "p" };

    [Fact]
    public void TheBoundIsOneCallByDefault()
    {
        // The default matters more than the mechanism: nothing in the engine issues concurrent
        // calls today, so an accidental default of "unbounded" would go unnoticed until the first
        // thing that parallelised rendering wedged the machine again.
        Assert.Equal(1, LlmOptions.Default.MaxConcurrentCalls);
        Assert.True(LlmOptions.Default.ConcurrencyWaitSeconds > 0);
    }

    [Fact]
    public async Task ASecondConcurrentCallFailsLoudlyRatherThanWaiting()
    {
        using HeldHandler handler = new();
        using HttpClient http = new(handler) { Timeout = Timeout.InfiniteTimeSpan };

        LlmOptions options = LlmOptions.Default with
        {
            MaxConcurrentCalls = 1,

            // Seconds rather than minutes, so the test measures the refusal and not the wait. The
            // production default is 300s for the reason stated on the option: a slow render ahead
            // in the queue must not be mistaken for a wedge.
            ConcurrencyWaitSeconds = 1,
        };

        using OllamaClient client = new(options, http);

        Task<LlmResult> first = client.CompleteAsync(Anything);

        // The first call is genuinely in flight and holding the slot before the second is made.
        Assert.True(await handler.Arrived.WaitAsync(TimeSpan.FromSeconds(30)),
            "the first call never reached the handler");

        LlmUnavailableException refused =
            await Assert.ThrowsAsync<LlmUnavailableException>(() => client.CompleteAsync(Anything));

        // Loud means diagnosable. The message names the bound, the wait and the endpoint, because
        // the actual cause both times was something else saturating the machine and nothing in the
        // output said so.
        Assert.Contains("already in flight", refused.Message, StringComparison.Ordinal);
        Assert.Contains("1s", refused.Message, StringComparison.Ordinal);
        Assert.Contains(options.Endpoint, refused.Message, StringComparison.Ordinal);
        Assert.Contains("wedged", refused.Message, StringComparison.Ordinal);

        handler.Release();
        LlmResult answered = await first;
        Assert.Equal("a sentence", answered.Text);
    }

    [Fact]
    public async Task TheSlotIsReleasedAfterACallThatFailed()
    {
        // A bound that leaks a slot on failure converts one bad call into a wedge of its own, and
        // the failure would present as the thing the bound exists to prevent.
        using HttpClient http = new(new FailingHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        using OllamaClient client = new(LlmOptions.Default with { ConcurrencyWaitSeconds = 1 }, http);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            LlmUnavailableException thrown =
                await Assert.ThrowsAsync<LlmUnavailableException>(() => client.CompleteAsync(Anything));

            // Every attempt must fail on reaching the endpoint, never on waiting for a slot: a
            // slot still held from the previous attempt is exactly what this asserts is absent.
            Assert.DoesNotContain("already in flight", thrown.Message, StringComparison.Ordinal);
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("nothing is listening");
    }

    [Fact]
    public void ABoundOfZeroIsRefusedAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OllamaClient(LlmOptions.Default with { MaxConcurrentCalls = 0 }));
    }

    /// <summary>
    /// One deadline, and it is the configured one.
    ///
    /// <c>HttpClient</c>'s own default is 100 seconds, so a client built with the engine's 900
    /// second timeout was cancelled at 100 and then reported "did not answer within 900s". A wrong
    /// figure in an error message is the same family as an unlabelled one — nothing questions it —
    /// and this one fires routinely, because a 2,000-token pack costs about 80 seconds in prompt
    /// evaluation before a word is generated.
    /// </summary>
    [Fact]
    public void TheClientDoesNotCarryASecondShorterDeadline()
    {
        using OllamaClient client = new(LlmOptions.Default);

        // Reaching the HttpClient the client made for itself is the only way to assert this, and
        // the alternative — waiting 100 seconds for the wrong message — is not a test anyone runs.
        object? http = typeof(OllamaClient)
            .GetField("_http", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(client);

        Assert.Equal(Timeout.InfiniteTimeSpan, Assert.IsType<HttpClient>(http).Timeout);
    }
}
