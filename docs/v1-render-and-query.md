# v1 — Render and Query

## Where we are

v0 is complete and validated. The symbolic engine produces 50-year histories across five seeds with no AI involvement. After one round of dynamics fixes, the measured results are:

- 328 distinct deep-chain shapes across five seeds (up from 78)
- 0% of deep chains are single-actor lifecycle sequences (previously dominant)
- 66% of deep chains span three or more event domains
- Max causal depth 9–18 (up from 7)
- Verbatim repeat rate 8.9% (down from 11–22%)
- Coup attempts down from 173 to 57, success rate up from 13% to 28%, "come to nothing" outcomes eliminated

The engine produces genuine emergent history — e.g. a man exiled in Y37 returns in Y49 to murder the ruler who took the seat he was denied, and the faction partitions over it. That was not authored.

**The architecture is settled. Do not restructure the engine, the event schema, or the log format in this phase.**

---

## What v1 is

**v1 makes the event log readable as history.** Two capabilities, nothing else:

1. **Render** — structured events become prose
2. **Query** — natural language questions answered from the log

### Explicit non-goals

Do not build any of the following in v1. They are v2 and v3.

- Authored interventions / prose-to-events (the "adjudicate" role)
- Contradiction detection or reconciliation
- Texture extraction (absorbing model-invented detail back into world state)
- Knowledge propagation, rumour, or per-agent epistemic state
- Map generator integration
- Any UI beyond what is needed to read output

If you find yourself building "the model proposes a change to world state," you have drifted out of scope. Stop and flag it.

---

## Inference setup

- **Runtime:** Ollama, local, OpenAI-compatible HTTP endpoint at `localhost:11434`
- **Model:** `qwen3.6` — use `qwen3.6:27b` (17GB) or `qwen3.6:35b` (24GB MoE) depending on available VRAM. 256K context window. Apache 2.0 licensed, which is a deliberate constraint (see below).
- **Model must be swappable via configuration.** No model-specific logic anywhere in the codebase. The tag is a config value.

### Licensing constraint (settled decision, do not revisit)

The project is restricted to Apache-2.0 models. Qwen3.6 qualifies. This is why: it keeps the base model swappable and avoids custom source-available terms, attribution requirements, and clauses restricting use of outputs.

**Consequence to honour now:** do not build a fine-tuning corpus from another model's outputs. Training data must derive from the user's own accepted and edited renders.

### Structured output

Use Ollama's structured output support so malformed responses are mechanically impossible rather than prompt-discouraged. Validate semantics separately after the shape is guaranteed.

Where a task needs real reasoning (chain selection, query planning), use the **two-call pattern**: let the model reason freely in the first call, then extract into the constrained schema in the second. Heavy constraints degrade reasoning quality, so do not constrain the thinking step.

---

## Build order

### v1.0 — Render a single event

Ollama client, prompt scaffold, cache, render log. Feed one event plus the entities it references; get back one or two sentences of prose.

The feature is deliberately trivial. The work here is infrastructure — get it boring and correct before moving on.

### v1.1 — Aggregate (this is where the value is)

Rendering event-by-event just produces a wordier log, which is arguably worse than what already exists, since the raw log is terse and scannable. **The win is many events to one passage.**

Aggregation units to support: a year, a reign, a war, a faction's rise and fall, and a causal chain.

Benchmark case: the world-42 chain spanning Y24–Y50 is 17 events — a refused tribute, two failed conspiracies, a revolt, a legitimacy crisis, a successful coup, and an exile returning twelve years later to murder the man who took the throne. As a single chronicle passage this should read as a page of history. If it doesn't clearly beat the raw event list, the prompt needs work, not a bigger model.

### v1.2 — Query

Natural language over the event log, answered by **retrieval, then generation**. Retrieve relevant events from the log first; render an answer from those events only.

The model must never answer from its own memory of what it rendered earlier. Do not simply stuff the world into the context window — that will work at 50 years and fabricate confidently the moment the world exceeds it.

Two query shapes to support:
- Causal: "why did the Sworn Men of Laehiford partition?" — walk causal edges backward, render the chain
- Factual: "who has ruled Kebarrow?" — filter and list

---

## Four constraints to hold throughout

### 1. No invented texture in v1

The model may not invent weather, gestures, sensory detail, motivations, or any fact not present in the input events. Every proper noun, date, place, and outcome in rendered prose must trace to a source event.

This produces flatter prose. That is intentional. The extraction pass that would let invented detail be safely absorbed into world state is a v2 feature; until it exists, invented texture becomes canon on cache and will contradict engine state later.

### 2. Cached renders are canon

Once a passage is generated and accepted, it is the world's text. Store it. Never silently regenerate — a model swap or prompt change must not rewrite existing history.

This is the same rule already applied to externally generated artefacts: anything generated becomes world state the moment it is observed.

### 3. The structured event always survives

Rendering is a view, never a replacement. Every event remains retrievable in its original structured form permanently. v2 adjudication and v3 epistemic state both operate on structure, not prose.

### 4. Log every render pair from day one

Persist: input events, prompt version, model tag, raw output, and whether the user accepted, edited, or rejected it. Store edits alongside originals.

This is the fine-tuning corpus for a later LoRA on the narrow house-style render task. It costs nothing now and cannot be reconstructed later. Do not defer this.

---

## Determinism

The engine's determinism guarantee must not be weakened by adding a nondeterministic component.

- LLM output is nondeterministic; **renders are cached, so world reproducibility is preserved through the cache, not through the model.**
- **No rendered output may ever feed back into simulation state in v1.** The render layer is strictly read-only with respect to the world. If prose can influence the sim, replay breaks.
- Existing determinism tests must continue to pass unchanged. Same seed, byte-identical event log.

---

## Evaluation criteria

v0's bar was "is the raw log interesting?" v1 has two bars, and both must be met.

### 1. Fabrication rate near zero

Build this as an automated test, not a manual read. Take rendered passages, extract every factual assertion, and check each against the source events. Any name, place, date, relationship, or outcome not present in the input is a failure. Report the rate.

### 2. The chronicle must beat the log

Read a rendered reign side by side with the raw events it came from. If the log is clearer or more informative, aggregation is not earning its keep.

**Name this risk plainly: the raw log is already good.** Prose that merely restates it is a regression. v1 succeeds only if reading the chronicle tells you something the event list did not.

---

## Carry-over from v0

**Fix before v1.2.** `LIFE.MARRIAGE` events still record spurious causal edges — in world-42, 18 of 39 marriages cite another marriage as their cause with zero shared actors between them. This is "previous event from the same generator loop" recorded as causation. The fix-3 audit in the last round did not reach this event type.

This must be fixed before query ships, because causal queries traverse these edges and will produce nonsense answers about why marriages happened. Verified: removing these edges does not change max causal depth in any seed, so the depth metrics above are real.

**Deferred deliberately.** The economic layer does not currently feed the political layer — deep chains are almost entirely POLITY, CONFLICT, and DIPLO. Famines, harvests, and trade pacts run as a parallel system. This is a known gap and the highest-value next addition to the sim, but **do not fix it in v1.** Render the political layer first; reading it as narrative will show much more precisely what the economic layer needs to do.

---

## What I want from you

Build v1.0 and v1.1 first, then stop and show me rendered output before starting v1.2. The aggregation quality question needs a human read before query is worth building.

Push back before implementing if you disagree. In particular: if the no-texture constraint in v1.1 would make aggregation output too flat to evaluate fairly, or if the retrieval design in v1.2 needs to be settled earlier than I've scheduled it, say so now rather than working around it.
