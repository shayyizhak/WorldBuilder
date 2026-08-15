using System.Text;
using System.Text.RegularExpressions;

namespace WorldBuilder.Inference;

public sealed record RenderOutcome
{
    public required Render Render { get; init; }
    public required FabricationReport Fabrication { get; init; }
    public required bool FromCache { get; init; }
    public required ContextPack Pack { get; init; }
}

/// <summary>
/// Turns a context pack into prose: cache, prompt, model, check, store.
///
/// Strictly read-only with respect to the world. Nothing here touches <c>WorldState</c> or the
/// event log — if rendered text could influence the simulation, replay would stop reproducing
/// the world and the engine's determinism guarantee would be worth nothing.
/// </summary>
public sealed partial class Chronicler(
    ILlmClient client, RenderStore store, RenderJournal journal)
{
    private readonly ILlmClient _client = client;
    private readonly RenderStore _store = store;
    private readonly RenderJournal _journal = journal;

    /// <summary>
    /// Renders a pack, or returns the cached passage unchanged. A cached render is canon and is
    /// never silently regenerated; <paramref name="force"/> exists for deliberate re-rendering
    /// and still writes under the same key only if prompt and model are unchanged.
    /// </summary>
    public async Task<RenderOutcome> RenderAsync(ContextPack pack, bool force = false, CancellationToken ct = default)
    {
        if (!force && _store.TryGet(pack.Key, Prompts.VersionFor(pack.Kind), _client.ModelTag, out Render cached))
        {
            FabricationReport recheck = FabricationCheck.Check(pack, cached.Text, wholeSection: true);

            // The text is canon; the verdict on it is not. A passage checked by an earlier,
            // coarser checker keeps whichever status that checker gave it, and a section held
            // out of the document over a finding since shown to be spurious would stay held out
            // forever. So the machine verdict is recomputed on read — and only the machine
            // verdict: a human's Accepted, Edited or Rejected stands.
            Render judged = cached.Status is RenderStatus.Generated or RenderStatus.Suspect
                ? cached with { Status = recheck.Truthful ? RenderStatus.Generated : RenderStatus.Suspect }
                : cached;

            if (judged.Status != cached.Status) _store.Put(judged);

            return new RenderOutcome
            {
                Render = judged,
                Fabrication = recheck,
                FromCache = true,
                Pack = pack,
            };
        }

        string system = Prompts.System;
        string prompt = Prompts.For(pack);

        // Two attempts, and the second is told what was wrong with the first.
        //
        // The check used to report and then cache the passage anyway, which made it an
        // observation rather than a guard: a paragraph that contradicted itself about who ended
        // whose rule went into the document with a warning on stderr that the document did not
        // carry. A finding is now acted on. Only a clean passage becomes canon; one that fails
        // twice is stored as Suspect so the run stays reproducible and the failure inspectable.
        LlmResult result = await Attempt(prompt, ct);
        string text = Clean(result.Text);
        FabricationReport check = FabricationCheck.Check(pack, text, wholeSection: true);

        // Retried where the passage is false, and also where it is shapeless.
        //
        // Falsehood and shape are separate judgements and only one of them decides canon. A
        // section that names one of its eleven rulers goes in either way; it is still worth one
        // more call, because that defect and its opposite — a sentence per year in log order —
        // had been alternating every round under prompt wording alone, and a measurement the
        // retry can act on is what stops the oscillation.
        if (check.Retryable.Count > 0)
        {
            LlmResult second = await Attempt(prompt + Correction(check), ct);
            string retried = Clean(second.Text);
            FabricationReport recheck = FabricationCheck.Check(pack, retried, wholeSection: true);

            // Truth first, then shape, so a retry can never trade a true section for a
            // livelier false one.
            bool better = recheck.Blocking.Count < check.Blocking.Count
                || (recheck.Blocking.Count == check.Blocking.Count
                    && recheck.Retryable.Count < check.Retryable.Count);

            if (better)
            {
                result = second;
                text = retried;
                check = recheck;
            }
        }

        Render render = new()
        {
            PackKey = pack.Key,
            PromptVersion = Prompts.VersionFor(pack.Kind),
            Model = result.Model,
            Text = text,
            Year = pack.ToYear,
            Status = check.Truthful ? RenderStatus.Generated : RenderStatus.Suspect,
            PromptTokens = result.PromptTokens,
            OutputTokens = result.OutputTokens,
            ElapsedMs = result.ElapsedMs,
        };

        _store.Put(render);
        _journal.Record(pack, Prompts.VersionFor(pack.Kind), system, prompt, render);

        return new RenderOutcome
        {
            Render = render,
            Fabrication = check,
            FromCache = false,
            Pack = pack,
        };

        async Task<LlmResult> Attempt(string body, CancellationToken token)
        {
            LlmResult r = await _client.CompleteAsync(new LlmRequest
            {
                System = system,
                Prompt = body,
                MaxTokens = Budget(pack.Kind),
            }, token);

            // A render cut off by the token budget is not a passage, and caching one makes an
            // unfinished sentence permanent. Nothing is stored and nothing is journalled.
            if (r.Truncated)
            {
                throw new RenderTruncatedException(
                    $"the model ran out of tokens rendering {pack.Kind} \"{pack.Title}\" " +
                    $"({pack.Events.Count} events, {r.OutputTokens} tokens produced). " +
                    "Not cached. Raise the budget or narrow the pack.");
            }

            return r;
        }
    }

    /// <summary>
    /// The findings, written back into the prompt as instructions rather than as complaints.
    /// The model is not told it failed; it is told which specific things the records do not
    /// support, which is the part it can act on.
    /// </summary>
    private static string Correction(FabricationReport check)
    {
        StringBuilder sb = new();
        sb.Append("\n\nA first attempt at this passage contained the following, none of which\n");
        sb.Append("the records above support. Write the passage again without them:\n");

        foreach (Fabrication f in check.Blocking)
            sb.Append("  - \"").Append(f.Token).Append("\": ").Append(f.Context).Append('\n');

        sb.Append("Every other rule still applies. Prose only.\n");
        return sb.ToString();
    }

    /// <summary>Records a human verdict. Edits are stored beside the original, never over it.</summary>
    public void Judge(ContextPack pack, RenderStatus status, string? editedText = null)
    {
        if (!_store.TryGet(pack.Key, Prompts.VersionFor(pack.Kind), _client.ModelTag, out Render existing)) return;

        Render updated = existing with
        {
            Status = status,
            Text = editedText ?? existing.Text,
            Original = editedText is null ? existing.Original : existing.Original ?? existing.Text,
        };

        _store.Put(updated);
        _journal.Record(pack, Prompts.VersionFor(pack.Kind), Prompts.System, "(verdict)", updated);
    }

    /// <summary>
    /// Token budgets, raised so a section-length render has room to finish. Truncation is now
    /// an error rather than a silent cache write, so a budget that is too small fails loudly.
    /// </summary>
    private static int Budget(PackKind kind) => kind switch
    {
        PackKind.SingleEvent => 200,
        PackKind.Year => 600,
        _ => 1600,
    };

    /// <summary>
    /// Strips the two things the model reliably adds despite instruction: a "Here is..."
    /// preamble, and entity codes copied out of the records.
    /// </summary>
    private static string Clean(string text)
    {
        text = EntityCode().Replace(text, "");
        text = Preamble().Replace(text, "");
        text = ExtraSpace().Replace(text, " ");
        return text.Trim();
    }

    [GeneratedRegex(@"\s*\((?:[apfwe]:\d+(?:,\s*)?)+\)|\s\b[apfwe]:\d+\b")]
    private static partial Regex EntityCode();

    [GeneratedRegex(@"^\s*(?:Here (?:is|follows)[^\n:]*:|Passage:|Chronicle:)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex Preamble();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex ExtraSpace();
}

/// <summary>Thrown when generation stopped on the token budget rather than on a stop token.</summary>
public sealed class RenderTruncatedException(string message) : Exception(message);

/// <summary>
/// A deterministic stand-in for the model, so the pipeline, the cache and the fabrication
/// checker can all be tested without inference running — and so CI never depends on a 24 GB
/// model being resident.
/// </summary>
public sealed class ScriptedLlmClient(
    Func<LlmRequest, string> reply, string model = "scripted", string stopReason = "stop") : ILlmClient
{
    public string ModelTag { get; } = model;

    public int Calls { get; private set; }

    public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(new LlmResult
        {
            Text = reply(request),
            Model = ModelTag,
            PromptTokens = request.Prompt.Length / 4,
            OutputTokens = 40,
            ElapsedMs = 0,
            StopReason = stopReason,
        });
    }
}
