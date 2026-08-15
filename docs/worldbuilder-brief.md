# Project Brief: Simulated World Builder

## What this is

A worldbuilding tool where the world **runs** rather than sitting in a wiki.

The existing landscape splits into two camps, and neither is what I want:

- **World Anvil / LegendKeeper** — structured wikis with templates. The user does 100% of the creative work; the tool just organises it.
- **Dwarf Fortress legends mode** — a genuine history simulator that generates centuries of world events. The user does 0% of the work and can't intervene.

I want the thing in between: a world that simulates its own history and consequences, that I can still steer and author into.

## Core architectural principle

**The LLM renders. It does not simulate.**

Most AI worldbuilding tools ask a model to *be* the world. This fails as soon as the world outgrows the context window — the model contradicts itself and the world turns to mush.

Invert it:

- The world runs headless as a cheap **symbolic simulation** with zero model calls. Rules-based, deterministic, fast.
- The LLM is a **lazy rendering layer**, invoked only on the parts the user actually looks at.

This is the same relationship a game engine has with its renderer, and it's what makes the project tractable: simulate 400 agents across 300 years for free, then spend tokens only on the tavern the user clicked on.

## State model

**Event-sourced.** Append-only event log; world state is a fold over that log.

This buys three things that are otherwise painful:

- **Time travel** — inspect world state at any year by replaying to that point.
- **Causality** — "why is this duchy poor?" is a graph traversal over the log, not a hallucination.
- **Determinism** — seeded RNG means `seed + intervention log` fully reproduces a world. A whole world is shareable as a small text file.

Entities are deliberately thin: **actors, places (hierarchical), factions, resources**. Depth should emerge from interaction rules, not from rich schemas. Resist the urge to add fields.

## The epistemic layer (the differentiator)

Facts have a truth value **and** a per-agent knowledge state. An event occurs; some agents witness it; knowledge propagates along social and trade edges, distorting as it travels.

Every NPC then narrates from *their own knowledge state* rather than from world truth. Secrets, rumour, misinformation, and useful spies become emergent properties rather than authored content.

This is the feature that produces the "wait, how does it know that?" reaction — a merchant three cities away holding a garbled version of an assassination because the simulation actually propagated it.

Not in v0, but the data model should not preclude it.

## The LLM contract

**The model never writes to state.** It emits structured proposals against a schema; the engine validates them against world rules and accepts, repairs, or rejects. Three roles:

| Role | Job | What it needs |
|---|---|---|
| **Render** | Event or entity → prose, on demand. Cached as canon once generated. | Prose quality. Fully async, so latency is irrelevant. |
| **Adjudicate** | Freeform authored intervention ("a plague hits the southern ports") → validated state deltas. | Schema reliability, not creativity. |
| **Query** | Natural language over the log, answered from *retrieved events*, never from model memory. | Retrieval quality and long context. Mostly a RAG problem. |

## Bidirectional authoring

The feature that separates this from both competitors: **you can write a fact into a world that is already running.**

Declare that a city is ruled by a merchant council, and the engine absorbs it, back-propagates plausible causes into recorded history, and continues forward. When an assertion contradicts derived state, the engine **surfaces the conflict and offers reconciliations** rather than silently overwriting.

That's the actual product: a world that does the tedious work but that I can still steer.

## Technical decisions already made

**Engine: C#.** Deterministic simulation and event sourcing both sit well in .NET, and it's my strongest language.

**Client: Flutter**, if and when there is one. Not a v0 concern.

**Inference: local, via Ollama.** Ollama exposes an OpenAI-compatible HTTP endpoint, so the engine talks to it over HTTP and models are swappable without code changes. Drop to llama.cpp's server directly if raw GBNF control is needed; vLLM if batch-render throughput becomes the bottleneck.

Local is a deliberate choice, not just a privacy preference: rendering is lazy and cached, so the whole century's events can be batch-rendered overnight at zero marginal cost. This is precisely the workload where per-token pricing hurts and owning the GPU wins.

**Constrained decoding, not prompt-begging.** "Respond only in JSON" is a request; a grammar is a constraint. Use Ollama's structured outputs / llama.cpp GBNF grammars so malformed output is *mechanically impossible*.

Two consequences that shape the design:

1. GBNF isn't limited to JSON. Consider grammar-constraining directly to a **world-delta DSL** (`FOUND city AT region:7 BY actor:112`) rather than generating JSON and parsing it into commands. Fewer layers, and the grammar doubles as schema documentation.
2. The validator's job shrinks to **semantics only** — syntax is guaranteed, so it just checks whether a delta is legal given world rules.

Known caveat: heavy constraints degrade quality on reasoning-type tasks because the model can't think out loud before committing to structure. Adjudication should therefore be **two calls** — reason freely first, then extract into the constrained schema.

## Licensing constraints

**Apache-2.0 models only.** Qwen's main releases qualify. Some Mistral models do; others ship under Mistral's own research licence, so check per model.

**Exclude Llama and Gemma.** Not because they forbid commercial use — the Llama Community License permits it, gated only by a 700M MAU threshold that is irrelevant at this scale. They're excluded because both carry custom source-available terms with attribution strings, incorporated acceptable-use policies that can be updated after adoption, and, in Llama's case, a prohibition on using outputs to improve competing foundation models.

That last clause has a concrete design consequence worth honouring regardless of model choice: **do not build a fine-tuning corpus from another model's outputs.** Keep the training set derived from my own accepted/edited renders so the base model stays swappable.

**Log every accepted render from day one.** World rendering is a narrow, repetitive task with a house style — a LoRA on a small model will likely beat a much larger general one later. The training set should build itself passively from the start. This is the one v2+ concern that needs a v0 hook.

## Scope for this session: v0 only

**v0 contains no AI whatsoever.**

- ~20 actors, 3 factions, one region
- Tick forward 50 years
- Dump the resulting event log as raw plain-text lines
- No prose rendering, no model calls, no UI

**The only question v0 answers: is the symbolic history interesting to read?**

If the raw event log is boring, no amount of LLM prose will save it — prose rendering makes dull events *florid*, not interesting. Projects like this routinely skip v0 and discover the problem in month four. I want to find out in week two.

Success criterion: I read 50 years of raw log output and want to know what happens next.

## Roadmap beyond v0 (context only — do not build)

- **v1** — LLM rendering of any event or entity; NL query over the log
- **v2** — authored interventions; contradiction detection and reconciliation
- **v3** — the knowledge/rumour propagation layer
- **v4** — export adapters (campaign notes, wiki, game data)

## Non-goals and failure modes to avoid

- **Do not** add LLM calls to v0 "just to see."
- **Do not** enrich the entity schemas. Interesting history should come from interaction rules between thin entities. If v0 is boring, the fix is better *rules*, not more *fields*.
- **Do not** let the LLM mutate state at any later version. Proposals → validation → engine writes. Always.
- **Do not** build UI before the simulation is proven interesting.
- **Do not** design for a specific consumer (TTRPG, novel, game). This is a general worldbuilder; consumers are v4 export adapters.

## What I want from planning mode

1. Interrogate the v0 simulation design — specifically, **which interaction rules are most likely to produce readable, causally-legible history** from thin entities. This is the whole risk of the project and the part I've thought least about.
2. A concrete v0 architecture: event schema, entity model, tick loop, RNG/determinism strategy, log output format.
3. Flag anywhere the v0 design would paint me into a corner for v2 (interventions) or v3 (per-agent knowledge), since those are the two features the data model must not preclude.
4. A build order with a clear stopping point where I evaluate the "is this interesting?" question before writing any more code.

Push back on anything above that seems wrong. The architecture is reasoned but unvalidated.
