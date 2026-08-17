# World Builder — project reference

Standing context for this project. Durable, not a status update — the *Current status* section is the only part that goes stale quickly.

---

## 1. What this is

A persistent, AI-assisted simulated world builder. It generates deep, internally consistent history — factions rising and falling, rulers murdered and succeeded, wars, famines, plagues, secessions — and lets you read that history as prose, query it in natural language, and eventually author into it and render it onto maps.

A hobby project, built in C#/.NET, run locally.

### The distinguishing idea

**The simulation is a cheap deterministic symbolic engine. The language model is only a rendering layer over it.**

The world runs headless with zero model calls. The LLM is invoked lazily, only on the parts you actually look at — the same relationship a game engine has with its renderer. That is what makes the whole thing tractable: a century of history exists for near-nothing and is narrated on demand.

Most projects in this space put the model *in* the simulation loop. This one deliberately does not, and that is the interesting part of the architecture — the thing worth leading with in any README or write-up.

---

## 2. Working arrangement

Shay builds on a separate device using Claude Code. Claude in chat provides design guidance and reviews uploaded artefacts — event logs, chronicles, sidecar JSON, agent reports. **Claude has no visibility into the code.**

The loop:

1. Claude writes a brief or a loop-prompt to `/mnt/user-data/outputs`
2. Shay copies it to Claude Code, which executes
3. Shay uploads results (logs, chronicles, JSON, a report)
4. Claude reviews against the record and writes the next brief

Later rounds moved from per-round briefs to **loop-prompts**: a document with explicit halt conditions that Claude Code runs unattended until the conditions hold or an abort triggers. This works well for anything machine-checkable and halts for anything requiring prose judgement.

**The abort is the feature, not the failure.** The baseline archive took three revisions and aborted twice. Both aborts were correct, and the second one caught a defect that had been sitting in the tree unnoticed since before version control existed. A loop that halts and reports beats one that resolves ambiguity by guessing. Revisions supersede rather than replace: each report is left standing, because the earlier one is still the record of what was searched for.

**Escalated decisions come back as questions, not as choices already made.** When a loop hits something requiring prose judgement, its report ends with a numbered list of what a human has to decide. That list is the interface between the two halves of this arrangement.

**Claude has no visibility into the code, but it does have the record.** Twice now a question posed as "someone has to read the passage" turned out answerable from the event log plus the round reports. Worth trying before assuming a decision needs eyes on source.

**GitHub access, if it is ever wanted.** Claude's sandbox can reach `github.com` and clone a *public* repo. Private repos are unreachable — no connector, no authentication. The repo is currently private, which is right; uploading specific files on demand costs less context than cloning a C# tree each session, and the line-anchored reports Claude Code produces carry most of the value of direct access.

---

## 3. Architecture (settled — do not relitigate without reason)

### Core principles

- **Event-sourced state.** An append-only event log; world state is a fold over the log. This buys time-travel (replay to any year), real causality (why-questions are graph traversals over recorded `causes` edges, not model guesses), and determinism.
- **Thin entities, rich interactions.** Actors, places, factions, resources are deliberately shallow. Depth comes from interaction rules, not elaborate schemas. Properties, not identities.
- **The model renders. It never simulates and never writes state.** It emits structured proposals the engine validates.
- **Cached renders are canon.** Anything accepted becomes world text and is stored, never silently regenerated. Same rule for externally generated artefacts (imported maps).
- **The structured event always survives.** Prose is a view, never a replacement. Later milestones operate on structure, not text.

### The three LLM roles

| Role | Input | Output | Milestone |
|---|---|---|---|
| **Render** | facts | prose | v1.0 / v1.1 |
| **Query** | a question + retrieved events | an answer | v1.2 |
| **Adjudicate** | authored prose | validated state deltas | v2 |

### Stack

- **Engine:** C#/.NET, currently version `1.2.0`. Event sourcing and deterministic simulation fit it well.
- **Inference:** Ollama, local, OpenAI-compatible, model-swappable. Currently `qwen3.6:latest`.
- **Model licensing: Apache-2.0 only.** Qwen qualifies. This is deliberate — it keeps the base model swappable and makes a future house-style LoRA legally clean to release. Llama and Gemma 1–3 were excluded for custom source-available terms; **Gemma 4 shipped Apache-2.0 in April 2026 and is now eligible.**
- **Client:** Flutter if a UI is ever built for users. Not a current concern.
- **Techniques:** constrained decoding for structured output; the two-call pattern (reason freely, then extract to schema) for anything needing structure.

### A note on model choice

Qwen is coding-tuned, and the prose was better than expected. There is a temptation to swap to a prose-tuned model. Resist it without testing: five rounds of render work converged on instructions to be *less* creative and more literal — don't invent motive, don't embellish, render a missing input as omission. A model that writes gorgeous fiction is a model that fills gaps beautifully, which is the primary failure mode. Coding-tuned literalism is plausibly why the prose was controllable at all. Any swap must also hold on the constrained-decoding side.

### Zero-inference paths are structural, not observational

`wb book --check-only` holds a `CacheOnlyLlmClient` whose `CompleteAsync` throws. A cache miss surfaces as the missing render it is, rather than being repaired by generating a passage nobody has verified.

The design rule this encodes: **"no call was observed" is not a proof that none was possible; "the call cannot be constructed" is.** The alternative considered and rejected was pointing `--endpoint` at a dead port — which does work, and is worth keeping as a *test* of render-cache completeness, but makes correctness depend on a misconfiguration.

---

## 4. Lessons (hard-won — these are the real value of this document)

### On rendering

**Particulars vs patterns.** The key conceptual move of the whole project. Inventing *particulars* — names, places, dates, numbers, motives, actions — is forbidden. Characterising *patterns* across the supplied records — frequency, escalation, how a period ended — is **required**. Clamping fabrication without this distinction kills aggregation and produces one-sentence-per-event transliteration.

**Counting is the engine's job, prose is the model's.** All statistics computed in C# and passed in as structured facts the model may only restate. The model is unreliable at counting across a long list.

**Wrong engine figures are worse than wrong model figures**, because nothing questions them.

**Statistics carry a scope.** A faction-lifetime figure restated inside a reign passage is a live defect class — it recurred three times across rounds.

**Supplied figures must be stated, not summarised.** "Hundreds died" when the record says 474 discards the only content the model can state with certainty. Vagueness is not a safe default.

**A missing input renders as omission, never as plausible connective text.** The model invents to fill gaps.

**Ambiguous engine labels are a fabrication vector independent of the model.** A reason code that doesn't name its party, an outcome buried in a prose clause, a duration in two conventions — each produced fabrications that no prompt fix could hold.

**Skewed outcome distributions are a latent fabrication risk.** Where one outcome dominates, the model scores well by guessing the majority case — and gets the rare case confidently wrong. Worth auditing which event types have that shape.

**Statistics need a population.** Suppress them below a threshold, or a one-year reign produces an absurd stat block.

**Prompt fixes decay.** Fixes made in the prompt held for one or two rounds and then regressed. This is the entire argument for the checker.

### On the checker and tests

**The silent-path family — appeared five times.** In every case *the rule was correct and the input never reached it*:

1. `"included"` missing from the partiality-marker list (only `"including"` was there)
2. `people`, `exiles`, `returns` absent from the countables lexicon
3. Normalisation not stripping possessives, so `"Realsis Leirpu's"` yielded a subject matching nobody
4. An early return discarding 32 of 33 extracted assertions
5. `unresolvable` conflating "could not look it up" with "looked and it isn't there"

Assume it will appear again. It is silent by nature — a gap that presents as a pass.

**The family is not confined to rules.** Two more instances turned up in the filesystem during the baseline archive: `*.jsonl` and `*.log` ignored globally would have dropped two of ten baseline artefacts while the directory still looked complete; and a hash computed over the working tree rather than over what git stores would have been CRLF-dependent. Both have the same shape — a gap that presents as a pass — and neither is in a checker rule.

**`coverage-sound` — two invariants, both required:**

```
ACCOUNTING   extracted == checked + unresolvable    (per rule, per scope)
FLOOR        extracted >= previous_extracted        (per rule, per scope)
```

ACCOUNTING says nothing is dropped *after* extraction. FLOOR says nothing is dropped *before* it. **Each is trivially satisfiable alone** — one round satisfied ACCOUNTING by collapsing extraction from 33 to 2; the next did the reverse. Re-baselining the floor must be an explicit human action, never something that happens by rerunning.

**Coverage reporting.** Every run emits per-scope `extracted / checked / unresolvable / fired` per rule. A rule extracting nothing from a scope containing the relevant construction emits `rule-inert`. This converts silent inertness into a loud failure.

**Test entry points.** Every rule test enters at the outermost callable production uses. Two tests once passed while the code failed — one hand-fed an event kind the planner never emits, the other called an inner method the public entry point bypassed. **A test feeding an input the production caller never produces is worse than no test**: it converts silence into false confidence.

**Assert extraction, not just absence of failure.** A test asserting "no finding fired" passes when the rule is inert.

**Construction-gated rules cannot be required to always extract.** `partition-sum` needs a partition; a two-sentence answer doesn't have one. Loosening extraction to satisfy a blanket coverage requirement is how false positives get manufactured — one attempt cost seven true chronicle sections. Inert is a finding only when the construction is present.

**Completeness rules on fragments are a trap.** They work on whole sections and misfire on short answers.

### On artefacts and provenance

*Added after the baseline archive rounds. This is the newest cluster and probably the least worked-out.*

**A derived artefact drifts silently unless something records what produced it.** The archived `chronicle-42.findings.json` was written by a pre-v1.2 checker and committed in the same commit as the post-v1.2 checker that supersedes it. Git could not show the inconsistency because it was *inside the first commit*. Nothing in the file said which checker wrote it.

The fix is a **fingerprint over the producing code**, stored with the artefact. This generalises: it is the same question Stage 3 asks about cached renders, arriving early in a different costume.

**Prose reproducing byte-identically proves nothing about its sidecar.** `chronicle-42.md`, the suspect-token count and the held-out sections all matched exactly while the coverage accounting had drifted on three scopes. Any check comparing the document would have passed cleanly. **It took the machine-readable block to see it** — which is the argument for the query-side sidecar, made from the other side.

**Hash what the repository stores, not what the working tree holds.** Line endings make a working-tree hash a property of the checkout. `QuerySuite.cs` hashes two different values depending on `core.autocrlf`; the same trap nearly reappeared one field over, in the checker fingerprint itself.

**A stale figure in a reference document behaves exactly like a wrong engine figure.** The checker rule count sat at 17 in a summary doc, propagated into a loop-prompt, and was only caught by someone enumerating `RuleNames.All`. Nothing questioned it, because it was written down. **This document is not exempt from its own lesson** — figures here are as fallible as any other engine output, and the manifest is authoritative over any count restated in prose.

**Verified and derived are different, and only one of them is precious.** The v1 hand-verification attaches to the *prose* — figures, ruler lists, tenure spans, named years. The findings sidecar is derived: a pure function of `(renders.json, checker code)`, recomputable at zero inference cost. That distinction is what let the sidecar be replaced without weakening the baseline, and it is worth asking of every artefact before treating it as irreplaceable.

**Fingerprint the artefact that was actually used, not the inputs that notionally produced it.** The render cache hashes the pack body — what the model is literally shown — rather than the facts the pack was built from. A body hash cannot drift from what was rendered; an input hash can, wherever something transforms inputs into the pack. Same principle as the zero-inference entry point: prefer a guarantee that holds by construction over one that holds if maintained.

**Backward compatibility for unverifiable artefacts is a permanent commitment, not a migration step.** Cache entries predating the input hash are served and counted, never refused, because the v1 cache is entirely of that kind and it is the hand-verified one. A policy that eventually refuses them eventually strands the baseline. State the weaker claim — *these entries' inputs are unverified* — and keep reading them.

**A world is a log plus whatever it cannot be read without.** "The materialised event log is the durable artefact" held for as long as everything in the world was in the log. An imported map broke that half: the log records which cell each place stands on, and a cell index means nothing without the board it indexes into. So a world is a directory, the header carries a hash per stored artefact, and opening verifies them.

**A mismatched artefact hash is the one provenance failure that refuses.** Every other mismatch this engine reports leaves a readable world behind it — an older ruleset, absent provenance, a superseded engine. A board whose hash has moved leaves a world that reads perfectly and is about somewhere else, because every distance in it silently changed. Refuse where the failure has no symptom; note where it has one.

**Hash the file, and name the artefact inside the record too.** The bundle header hashes the map beside the world; the genesis event carries the board's fingerprint. They catch different things — a file that changed under a world, and a world opened beside the wrong map entirely — and only having both makes a log safe to carry away from its bundle.

### On mechanics and reachability

*The engine dynamics phase, ruleset 1 → 3. Five mechanic changes and a great deal found on the way.*

**Skewed outcome distributions are a simulation lesson, not only a rendering one.** It was written down as a fabrication risk — a model scores well guessing the majority case and gets the rare case confidently wrong. It is also how a mechanic becomes decorative: coups 100% exposed, raids 80% beaten off, tribute 82% refused, heirs 82% set aside. A branch that almost never fires is a thing that happens constantly and changes nothing. Audit every outcome-bearing kind on every ruleset change.

**An invariant that cannot vary is not an invariant.** `CoupDecidedPct` had a numerator no code path could reach and reported a plausible zero for months while a threshold was tuned against it. Every ratio metric must assert that at least one path can move it, and must fail at definition time if none can.

**A metric can report zero without meaning none.** `LifecycleChainPct` was integer division: one lifecycle chain in 156 rounded to 0% and the invariant passed on a world containing exactly the construction it forbids. Metrics asserting absence assert a **count**, and carry a constructed positive control — a survey cannot distinguish "does not happen" from "cannot happen".

**A label with no emitter is worse than a dead branch.** `Title.Heir` was set once in worldgen and never again, while four rules read it and three attached weight to it. The designation stopped happening; the weight stayed. Two successive counterweights did nothing because both were attached to an act nobody performed. A dead branch is at least visibly dead; a live rule reading a fiction looks like it works.

**Two subsystems can hold incompatible theories of the same thing and stay invisible while one is disconnected.** A house names its most loyal member; the contest rewards ambition. Nothing noticed for as long as the designation was never consulted. Expect this wherever one component produces an input another consumes — a disconnection hides disagreement.

**A counterweight anti-correlated with its own situation is no counterweight.** The heir carried legitimacy, and a disputed succession is exactly when legitimacy is lowest — the event itself applies −8. Whatever decides a contest must be able to move under the conditions that open it. Prefer a quantity derived from a recorded past act, monotone in elapsed time, over a live state the crisis depresses.

**Hypothesis, three data points: causal variety tracks how many mechanics have genuinely reachable branches.** `distinct deep-chain shapes` on seed 7 fell 54 → 44 during the raid work and rose 44 → 55 across two later changes that did not target it. More reachable branches is the only intervention that has moved it, and it moved it without being aimed at it. If that holds, "is it interesting?" and "does every branch fire?" are closer to the same question than anyone assumed. **Not a conclusion** — worth testing deliberately when Stage 6 adds mechanics.

**Parking is a decision, not neglect.** Not every defect clears the bar. The bar is whether it makes the world less interesting to read or less able to support a campaign — not simulation correctness. Below it, a finding is diagnosed, categorised and parked in `KnownFailing` with its reasoning, watched by the harness. Without an explicit bar, each fix's discoveries set the agenda forever and the roadmap never resumes.

**Create-only beats a human gate, where the property wanted is "this cannot move by rerun."** The archive directory refuses to be overwritten. Replacement requires deliberately moving the old one aside, which is the explicit act. A gate that depends on remembering to be a human is weaker than one that depends on the filesystem.

### On geography and calibration

*Added after Stage 6, ruleset 3 → 4. Four mechanics gained a distance input and no threshold moved.*

**Express a new input as a percentage of what the world already is, and no existing threshold has to move.** Proximity is 100 at a typical separation, and every consumer multiplies by it and divides by a hundred. A pair at an ordinary distance therefore scores exactly what it scored before geography existed, so four mechanics inherited their calibration rather than being re-fitted. The alternative — pick a distance constant and tune it — is how the raid roll acquired its undocumented flat 25, which cost a phase to find and had no defence when found.

**A scale is only self-calibrating if it is calibrated against the population that actually occurs.** Proximity was first defined against the board's median separation over all land cells: arithmetically correct, and useless, because places are sited deliberately far apart and no world ever contained two at that distance. Every proximity came out below 100 and four mechanics documented as "centred" were discounted everywhere. **The defect was invisible in the code and unmissable in the metric** — war declaration reported 0 near and 29 far across the whole panel, a branch that cannot fire wearing a percentage. Same class as `CoupDecidedPct`. Ask what the distribution of the thing being measured actually looks like, not what the container's is.

**Direction matching is not evidence between mechanisms that both predict the direction.** This is the sharpest methodological lesson the project has produced, and it cost two phases to learn. Stage 6 pre-registered a prediction that causal variety would fall, measured a rise on four seeds of five, and recorded the pre-registered alternative — *distance makes which neighbour you fight a stable fact, and stable facts let chains grow long* — as the surviving explanation. It was not. A control that replaced every proximity with a fresh draw from the same distribution, with **no stability and no spatial structure whatever**, moved the metric at least as far. Both mechanisms predict a rise; confirming the direction separated nothing. A pre-registered prediction that comes true is not self-validating — ask what else would have produced the same result, and build the control that tells them apart.

**The reference panel is not the measurement panel.** Five seeds exist because *hand verification* is expensive — five worlds is about as much prose as a person will read against a record. A statistical comparison needs no hand verification at all: headless simulation, zero model calls, every figure computed in C#. It costs compute and nothing else. **Sizing the second by the cost of the first is what produced a claim the data could not support**, and it went unnoticed for three phases because the number five was never a decision anybody made — it was inherited from a different constraint. The measurement panel is now 207 seeds, and the reference seeds are excluded from it so the two cannot merge again.

**Seen data sizes the next experiment; it does not decide the last one.** The paired variance of the five reference seeds was available and would have made the geography result look better. It was used only to compute N, and that restriction was written down before it was computed. Re-analysing seen data with a newly chosen variance is how a dead result comes back to life wearing better statistics.

**A comparative rule needs a stated minimum panel range, or its rank arm is a coin flip.** Seed 99 was adjudicated against a rank criterion over five separation-spread figures spanning 34–37%; a rank over a degenerate population carries no information, and the rule read as "partially explained" when the population made that arm meaningless. The general form: state, before measuring, the range below which the rank arm is void and the rule falls to its absolute arm. Applied to the controls, the same guard fired immediately — three arm medians spanning 19 points against within-arm spreads of 38 and 44, so **n=5 cannot discriminate them.** Every comparative claim made across this panel is subject to it, including "geography improved four seeds of five".

**A small n read against an unstated baseline manufactures a silent path that is not there.** Alliance moved 0 of 13 and was reported as suggestive of a decorative branch at "about one in eight". The baseline behind that figure was never named; against the rate distance moves anything else, 6%, the true figure is about **45%** — near a coin flip. Inspection then showed the term is live in every one of the thirteen evaluations. The silent-path family has a mirror image: a healthy mechanism diagnosed as dead because nobody wrote down what normal would look like. Both are failures to state the denominator.

**Adding an input can raise causal variety as much as adding a branch — but this is now unsupported.** The standing hypothesis was that variety tracks how many mechanics have genuinely reachable branches. Stage 6's rise looked like evidence for a wider form, that variety tracks how many distinct, stable configurations a world can be in. The redraw control removed the support: stability is not required to produce the rise. **Recorded as open, not as knowledge.**

**Repetition can be a missing input rather than a missing brake.** `verbatim repeat rate` survived two rounds that diagnosed it correctly and fixed the wrong thing, and was parked as unattributed. Geography closed it without aiming at it. A house with no map picks its rival by grievance alone, and grievance is sticky, so the same two names transact forever. Distance did not stop them repeating — it gave the world more than one plausible pairing to repeat with. Before adding a cooldown, ask whether the choice has enough inputs to vary.

**One idea implemented twice is one idea fixed once.** "A fixture pinned to a seed is a ruleset-scoped artefact" was learned at ruleset 2 and applied to the test suite's corpus fixture. `wb test corpus` held the same idea in its own copy and was never touched, so it threw on a missing scope for two rulesets in a place nobody ran. This is the silent-path family outside a checker rule for the third recorded time. When a lesson is applied, grep for the second implementation.

**A metric written this round found this round's own defect.** Both the proximity mis-calibration and the doubled panel denominator were found by instrumentation added in the same phase as the code it caught. That is the argument for adding the metric before believing the mechanic, not after.

**Instrumentation invariance is a standing property.** Attaching a measurement must not change the world, asserted by hashing the full event log with and without it across the whole seed panel — and asserted alongside a check that the instruments actually fire, or invariance is satisfied by instruments that never ran. Every probe adopts it. Nothing weaker detects the failure: a run with a perturbed stream is a perfectly plausible run.

**RNG draw order is load-bearing, and this is a constraint on Stage 3's determinism guarantee.** Reproducibility is not a property of the rules alone; it is a property of the rules *and the order in which they consume the stream*. A pure refactor can change every world from that year on. The worked example: a site reading `won && margin > bar && rng.Chance(p) && holder == defender` throws its die *before* testing the holder, so hoisting that test into the guard — obviously equivalent, and what anybody would write — stops the draw in exactly those cases and re-sequences everything after. Every test stayed green; the log hash was the only detector. **A refactor at a short-circuiting site is a behavioural change until a hash says otherwise**, and any diagnostic needing a second value must take it from a stream of its own.

**A control needs an identity arm before its results mean anything.** A synthetic replacement for a real input is only interpretable if the machinery that substitutes it consumes nothing from the streams the rules are drawing on — otherwise the measured difference is confounded with re-sequencing, which the constraint above establishes changes worlds by itself. So the first control built is the one that hands back the real value, and it must reproduce the real world exactly. Its first run failed, and the failure was informative: 897 of 898 events matched, and the one that did not was the marker recording that the run was a control.

### On measurement

**Read the record, never the presentation view.** The `.log` hides ~341 bookkeeping rows (the yearly accounts) out of 1,035. Much of the economy's causal influence runs through them. This produced two confidently wrong measurements — economy coupling reported as 18 of 524 when it was 142 of 850.

**A filter that drops rows must fail loudly.** Both measurement errors had the same shape: a scan silently omitting rows. If an edge's target is missing, that is a dangling-reference failure, not a row to skip.

**Ranking by raw event count systematically under-represents things that ended.** A power destroyed in year 20 had eighteen years to accumulate events; a survivor had fifty. Scope selection by "weightiest" therefore drops exactly the powers whose stories conclude. This matters again at Stage 8's significance threshold, where dropping is mandatory — rate rather than total, or a floor for any power that held land or was destroyed.

### On the model itself

**Generation is not reproducible run to run**, despite temperature 0 and a fixed sampling seed. Evidence: the same question with a byte-identical request body was classified causal in one run and factual in another, changing retrieval from three records to one. This is Ollama's own variance.

Consequences: a single-question CLI call is not a valid proxy for a suite run; "16 of 16" is a sample, not a proof; two consecutive identical runs is the honest evidence standard.

**Re-checking is not generation.** Running the checker over a cached `renders.json` involves no inference and *is* reproducible — five identical runs across two builds. The distinction matters: it is what makes a findings sidecar a derived artefact rather than an unrepeatable one.

**The planner mistypes verbatim fields.** Three slips in sixteen on a field it was instructed to copy exactly, and both surviving retrieval files misspell "Hadale Commune" differently. Resolve verbatim fields against the question text or the record — never fuzzy-match the planner's string, because a miss is recoverable and a confident resolution to the wrong entity is not. Years matter most: a mistyped year produces no failure signal, just a plausible answer about the wrong decade.

**Absent vs withheld must be distinguishable.** The same conflation appeared as `unresolvable`-vs-fired in the checker and as one empty-result sentence covering three different situations in the query layer. This is not just phrasing — the v3 epistemic layer's entire premise is that not-known and not-true are different, so the query path has to be able to express it.

---

## 5. Roadmap

Stages 1 and 2 are complete (v1 render and query). The board is at **https://trello.com/b/Ovwt583e/world-builder** with lists Done → In flight → Foundations → Simulation depth → Scale → Release.

| # | Stage | Notes |
|---|---|---|
| 1 | Finish v1 render | ✅ done |
| 2 | v1.2 query | ✅ done |
| — | Archive the v1 golden baseline | ✅ done — see §8 |
| 3 | Determinism & versioning | ✅ done — decided *and* built |
| 4 | Automated quality harness | ✅ done — five layers, 456 tests |
| — | Engine dynamics phase | ✅ done — ruleset 3; see §5a |
| 5 | Workbench UI | Instrumentation for the builder, not product |
| 6 | **World substrate: geography, then economy** | Geography ✅ — ruleset 4; see §5b. Economy is the next phase |
| 7 | v2 adjudication & interventions | Prospective first, retroactive last |
| 8 | LOD contract | **Design only** — keep running 20 actors |
| 9 | Complexity mechanisms | naming → religion → resources/trade → creatures → tech diffusion |
| 10 | Scale-up | 20 → 200 → 2,000 → statistical millions |
| 11 | v3 epistemic layer | Facts get knowers |
| 12 | Campaign loop | The thing it'll actually be used for |
| 13 | v4 export adapters | Markdown, wiki, map rendering, JSON |
| 14 | Open source release | Licence, NOTICE, README, contributor non-negotiables |
| 15 | Hosted tool | Inference cost is the whole problem |

### Stage detail worth carrying

**Stage 3 — determinism. Settled and built.**

Two things break "seed + intervention log reproduces the world": rule changes (every later stage changes rules) and model variance (proven at v1.2). Both point at **the materialised event log as the durable artefact**; seed is provenance, not a regeneration recipe. Replay stays available within a ruleset version as a debugging tool. **V2 corollary:** the intervention log stores *accepted deltas*, never the prompt that produced them.

**The header.** `JsonlIo.Header` carries `engine_version`, `engine_commit` and `ruleset_version` alongside type, seed and event count. Version and commit are read from assembly metadata rather than a source constant — the stale-figure lesson applied to the thing that records provenance. Nothing time- or machine-dependent, so two runs of a seed stay byte-identical. Empty fields are omitted rather than written blank, so a headerless file can't be confused with one carrying empty provenance.

**Opening policy: a newer engine refuses; everything else opens and says so.** Same build, silent. Older engine, changed ruleset, or no provenance at all (every v1 artefact) — opens with a note. Newer refuses with exit 1, overridable by `--accept-newer`, which says that it did.

The asymmetry is deliberate. An unknown event kind already throws loudly; a *new field on an existing kind* would be dropped in silence. Newer-engine is the one direction where the failure is invisible, so it is the one direction that refuses.

**Render invalidation keys on the pack body, not on versions.** `ContextPack.InputHash` is a sha256 over literally what the model is shown, so it cannot fall out of step with what was rendered. This was a live defect rather than a hypothesis: `ContextPack.Key` hashed event content keys only, so a change to how a statistic is computed left the events untouched, the key unchanged, and served a passage restating a now-wrong figure — permanently, since cached renders are canon.

Keying on the body rather than on notional inputs is the stronger form of the same idea, and the same move as `CacheOnlyLlmClient`: make the guarantee structural rather than maintained. A ruleset bump touching no pack invalidates nothing, which is what keeps the cache worth having as LoRA corpus and Stage 15 cost lever. A mismatched hash is never served; the stale entry stands while a new one is written beside it.

**Legacy cache entries are served and counted, never refused — and this should stay true.** The entire v1 cache predates the hash, and it is the hand-verified one. A policy that eventually refuses legacy entries is a policy that eventually strands the baseline everything else is measured against. `wb book` reports how many entries came from unhashed cache — the weaker claim stated rather than assumed, which is sufficient. If strictness is ever wanted, a `--strict-cache` flag off by default gives it for new work without being retroactive.

**Two items carried forward:**

- **`ruleset_version` — resolved, and now doing real work.** Settled as two counters that coincide: the ruleset carries its own sequence, deliberately not matching any engine version. It is at `4` while `engine_version` stays `1.2.0`. Layer 5 reads it and skips a baseline cut under a different ruleset.
- **The header's artefact hashes and render-cache fingerprint — built at Stage 6.** Correctly deferred until something produced a bundle for them to describe. An imported map was that artefact, and `WorldBundle` now writes and verifies both. Opening is the one entry point every reader goes through, and a hash mismatch throws.

**Stage 6 — geography. Built; see §5b.** Import the physical layer only (terrain, biomes, adjacency, travel cost); simulate the political layer on top. Azgaar gives a cell-adjacency graph free; Watabou MFCG has a de-facto JSON API. **Generators are NOT reproducible across versions** — treat generation as one-time, store the artefact in the world file, hash it into the header, never regenerate from seed. **Watabou TownGeneratorOS is GPL-3.0 — do not embed it**; outputs are permissive, so use the hosted tool. Nothing is embedded: the Azgaar importer consumes an export and no generator code is linked or vendored.

**The stored board is currently made rather than imported.** No Azgaar export was available on the build machine and the generator is a browser application that cannot be driven headlessly, so `maps/board-1.wbmap.json` came from `wb map make` and says so in its own provenance. The importer for the real path is built and tested and the artefact format is identical, so dropping in a real export is a swap — but every world simulated against a board records its hash, so replacing the board means new worlds rather than changed ones.

**Stage 7 — the collision to resolve.** "Cached renders are canon" and "retroactive authoring back-propagates causes into the past" cannot both hold unconditionally, because back-propagation rewrites events that already have canon prose about them. Decide deliberately in design. Stage 3's invalidation rule sets the precedent this will be argued from.

**Stage 8 — the sequencing trap.** Complexity is cheapest to iterate at 20 actors, but LOD changes how entities are *represented*, so every mechanism written before the contract exists gets rewritten after. Write the contract now, defer the population scale-up to Stage 10. Three tiers: statistical populations → named entities → simulated individuals. Crystallisation deterministic via `(world_seed, entity_id, query)`. Every Stage 9 mechanism must be expressible at all three tiers.

**Stage 9 — naming first.** Names carry nearly all the felt sense of distinct cultures, and retrofitting a naming system after the log is full of names is miserable. Religion second — highest yield, because it gives succession disputes *reasons* rather than dice.

**Stage 15 — inference cost.** Local Ollama is free; hosted is not. Bring-your-own-key, hosted inference with quotas, or hosted simulation with client-side rendering. The render cache is the cost lever: cached renders are canon, which means they are also *paid for once*. The lazy-rendering architecture chosen for tractability turns out to be the business model.

### 5a. The engine dynamics phase (ruleset 1 → 3)

Unscheduled, and it came from the harness rather than the roadmap: Layer 1's first run found `coup success` at 0% on every seed, and the invariant meant to catch it had been counting exposures as successes.

**Five mechanic changes**, in order: raid outcome odds and raid target memory; coup plots attach to a **seat** rather than to a person; the covert leak roll becomes three-way (expose / strike / defer) with readiness rising alongside exposure; tribute compliance becomes a gradient rather than a cliff; and `Title.Heir` gains a runtime emitter, with the designation carrying the age of the act that made it.

**What it was worth.** Covert seizure exists where it was structurally impossible — 29 seizures across the panel, success varying 7–35% by seed. The rate is asserted pooled, because fourteen plots cannot support a percentage.

**What it cost, and the discipline that made it stoppable.** Each fix revealed the next; without a bar, a phase like this runs forever. The bar is §4's — does the defect make the world less interesting to read or less able to support a campaign — plus a hard budget of mechanic changes, with a fifth requiring escalation rather than a decision.

**The working-method lesson.** The first four rounds were one brief each, which cost a round-trip per finding, and most of what came back between them was derivable from the record. A phase loop carries the method and the pre-committed decision rules, extends its own queue, and halts only for questions of **semantic intent** — what a mechanic is *for* — which is the one thing that cannot be derived. Write phases, not rounds.

### 5b. Stage 6 — the geography substrate (ruleset 3 → 4)

Run as a phase loop rather than as rounds, and it worked the way the previous phase said it would: no escalation was needed, and the two things worth escalating over — a fifth mechanic and a threshold change — never arose.

**Step 0 first: a ruleset-3 anchor for all five seeds.** Layer 5 had skipped since ruleset 2, so two rulesets of change had landed with no golden anchor, and this phase was about to change the world more than either. Five baselines at `baselines/ruleset-3/seed-*`, all `stability-anchor-only`, Layer 5 passing 0/0 on every one. `wb baseline cut` reads the producing engine out of the world file's own header rather than from the build running it, and its checker fingerprint reproduced the v1 baseline's hand-computed `60f5b325` exactly.

**A world became a bundle.** The map is a stored artefact, hashed into the header; opening verifies and refuses on a mismatch. The genesis event names the board separately, so a log carried away from its bundle still knows which map its cell indices mean.

**Four mechanics gained a distance input and no threshold moved** — raid targeting, war declaration, conquest holdability, and the pairing rules. The budget held: nothing else gained one, and the two parked findings that look adjacent (tribute target selection, heir selection) were noted and left.

**What it was worth.** Conflict acquired a place. A war is now declared over somewhere and fought there — Threi Cut three years running — rather than wandering the map; a conquest is next to what you already hold; a house's enemies are its neighbours. That is a structural property of individual events and it stands.

> ~~Causal variety rose on four seeds of five, by up to +33, and `verbatim repeat rate` cleared everywhere.~~ **RETIRED.** Measured on a 207-seed panel with all four arms paired, geography − redraw is **−0.53, 95% CI [−2.70, +1.65]** — a precise null, not a failure to detect. Geography's effect on causal variety is not distinguishable from structureless perturbation, and on that panel it is not distinguishable from no distance at all. The five-seed figures were real numbers about five worlds and were never evidence about the engine. See §5c.

**Positions changed nothing else, and that was checkable.** Siting draws on an RNG purpose of its own, so all five seeds produced a byte-identical history to ruleset 3 with only the `cell` fields and the board fingerprint added. Geography present and inert is a real state, and being able to stand in it is what made the four attributions afterwards mean anything.

**The prediction was falsified and that was the most useful part** — see §4's geography cluster. The phase also cost one full re-measurement of its own headline result, because the metric written to check the calibration found the calibration wrong.

### 5c. The controls, and the panel that settled them (ruleset 4, no rule change)

Two phases of measurement after the geography build, neither of which changed a rule. Between them they retired the geography build's headline claim and replaced the project's idea of how big a measurement is.

**The controls.** A synthetic distance model replaces what the four mechanics are told, drawn from each world's own realised proximity distribution so the distribution and the clamp exposure are unchanged and only the *origin* of the values differs. Four of them: `identity` (the board, through the machinery), `flat` (everything typical — reproduces ruleset 3 exactly), `shuffle` (one value per place-pair, fixed at worldgen: stable, no spatial structure) and `redraw` (fresh per decision: no stability, no structure).

**The identity arm is what makes the others readable.** It must reproduce the real world exactly, or a control's result is confounded with RNG re-sequencing — which changes worlds on its own. It failed on first run: 897 of 898 events matched, and the one that did not was the marker recording that the run was a control. The assertion was tightened rather than loosened.

**The result, at N=207 with all four arms paired on the same seeds and boards:**

| contrast | mean | 95% CI | p |
|---|---|---|---|
| geography − redraw | **−0.53** | [−2.70, +1.65] | 0.63 |
| geography − shuffle | +0.62 | [−1.46, +2.71] | 0.56 |
| shuffle − redraw | −1.15 | [−3.16, +0.86] | 0.26 |

None clears the pre-registered 5-point minimum effect; none survives Holm. **These are precise nulls rather than failures to detect** — the intervals exclude the MDE in both directions. Realised paired σ was 15.87 against 16.48 estimated, so the panel was sized correctly.

**Geography does not move causal variety.** Not relative to structureless perturbation, and on that panel not relative to no distance at all (arm means: flat 64.4, geography 63.1, shuffle 62.4, redraw 63.6 — the geography − flat contrast was *not* pre-registered and is reported as description, not as a test).

**What is untouched by this.** Geography's design rationale was never a claim about causal-variety deltas: distance gates conflict, trade, alliance and — at Stage 11 — rumour. Wars are fought where they are declared. The census of what distance decides stands: 680 decisions consulted a proximity, 555 had room to be moved, 34 were.

**Board geometry is not a first-class variable.** With one board per panel seed and the same seeds re-run on a shared board, var(board) came out negative — the board adds nothing measurable to the discriminating share. Stated with the limitation it needs: **this demonstrates sensitivity strongly and insensitivity only weakly**, because every board sampled came from one generator and may share characteristics an Azgaar export does not.

**The discriminating share per mechanic, on 207 boards:** marriage 24%, alliance 8%, conquest 7%, raid targeting 5%, war declaration 1.5%. Alliance's panel figure of 8% independently confirms the correction to the 0-of-13 finding — at 8%, seeing none in thirteen happens a third of the time.

### Two cross-cutting concerns

**The render cache is both asset and liability.** Every accepted render is training data for a future house-style LoRA — log from day one, cheap now and impossible to reconstruct later. Every accepted render is also a thing that must stay consistent with any future edit to its underlying events.

**Every stage's exit criterion should be a harness number, not a feeling.** "Is it interesting?" only became answerable once chain shapes and repeat rates were measured.

---

## 6. The checker

Prose that fails validation is kept out of canon rather than corrected by hand. Failed chronicle passages go to `chronicle-{seed}.unverified.md` with their findings.

### Tiers

- **Tier 1 — internal consistency.** Needs the rendered text only. Count vs enumeration, partition sums, internal date agreement, summary vs body. Cheapest and catches the most.
- **Tier 2 — statement validation against events.** Action, succession, outcome, departure, tenure, quantity, date.
- **Tier 3 — coverage.** Mandatory event classes within a scope's window: collapse, conquest, secession, war/peace, every battle, deaths of seat-holders.

### The 16 rules

`RuleNames.All` yields sixteen distinct owners:

```
action     coined-term   count-enumeration   count-narration
coverage   date          date-agreement      departure
naming     outcome       partition-sum       quantity
shape      succession    summary-body        tenure
```

**Sixteen, not seventeen.** `unsupported-link` is a finding kind that maps onto `action` rather than a rule of its own, and `name`/`number` verdicts from the vocabulary scan map onto `naming`. Both folds are why a wrong count of 17 circulated. `coverage` and `shape` are completeness rules, gated off on the answer path.

### Sidecar format

`{rule, scope, span, detail, blocking, fatal}` plus a per-scope `coverage` block with `extracted / checked / unresolvable / fired / accounted`. Exclusions appear as findings with `fatal: true`. `unresolved` entries carry their span.

### Disposal differs between chronicle and query

A chronicle has fifteen sections and can drop one with a note in its place. **A query answer has one answer and nowhere to put a warning.** A fatal finding on an answer returns the retrieved facts plainly, or an admission — never annotated prose carrying a known fabrication.

### The query path has no sidecar

`CmdSuite` prints findings, withheld notes and the coverage table to stdout and writes no file. The only writer of a `*.findings.json` is the chronicle path.

**This is a live gap, not a formatting nicety.** `departure` extraction went 4 → 0 between two v1.2 rounds and nothing caught it, because there was no machine-readable block to diff. The chronicle path had one, and diffing it is the only reason the sidecar drift in §8 was ever visible. Stage 4 backlog.

---

## 7. Test suite (Stage 4 — specced, not fully built)

Five layers, increasing cost:

1. **Dynamics invariants** — log metrics as assertions. Dangling refs = 0; repeat rate < 10%; single-actor chains = 0%; max causal depth ≥ 8; distinct two-step shapes ≥ 60; collapses per faction ≤ 1; coup success > 15%; covert coup success > 0; ECONOMY→non-ECONOMY ≥ 10% of edges; cross-domain ≥ 25%. Run across all five seeds.
2. **Checker rule unit tests** — synthetic passages, positive and negative per rule, plus lexicon-completeness tests (every marker in every list fires its rule on an identical sentence with only the marker swapped).
3. **Regression corpus** — 31 hand-verified fabrications from the render rounds, each mapped to the rule that should catch it. Cases fixed and then regressed are the highest-value entries.
4. **Chronicle verified against the log** — ruler lists, departure manner, tenure spans, raid counts (three outcomes), battle counts, killing counts split internal/external, marriage counts, every named year, every proper noun.
5. **Golden diff** — current output against the stored baseline in §8. Any figure that moves is a failure. **Diff the coverage block too** — extraction counts are far more stable than prose, and a rule going non-zero to zero is the signature of the silent-path family.

**Layer 4 deliberately duplicates the checker.** The checker decides what enters canon; the suite decides whether the checker works. A checker that silently stops firing is invisible without an independent verifier. If they ever share an implementation, that property is lost.

### Stage 4 backlog

Carried from v1.2 and from the archive rounds:

- **A supplied figure going unused is caught by nothing** — one answer omitted "504 fled" while the pack supplied it.
- **A bare count in an answer is verified by no rule** — the vocabulary scan skips numbers under three digits, and `count-vs-list` needs an enumerated list, not four citations.
- **`departure` 4 → 0 went uncaught.** FLOOR was specified but not in that round's halt list.
- **Pattern characterisation lands on the easy shape** (records sharing a year and a target) and not the harder one (records sharing a source across differing years).
- **Query-side findings sidecar** — same `{rule, scope, span, detail, blocking, fatal}` shape plus the per-scope coverage block. The single highest-value item here; see §6.
- **Split retrieval sets from the planner echo.** Event-ID lists are deterministic, diffable and checkable forever; the echo line is a generation artefact. One echo line is the entire reason retrieval reproduction is permanently skipped.
- **Emit the question set as data.** It is currently a C# literal in `QuerySuite.ForSeed42`, so archiving it means archiving source.
- **Keep the dead-endpoint trick as a render-cache completeness test.** Rejected as an archive path, genuinely useful as a test.

---

## 8. The v1 golden baseline

Sealed at `baselines/v1/seed-42/`, create-only. **`manifest.json` is authoritative** for contents and hashes — deliberately not duplicated here, per the stale-figure lesson in §4.

**Contents:** the chronicle and its unverified passages; the findings sidecar; `renders.json`; the query answers, retrieval sets and question set; the record and the `.log` view. Plus `manifest.json`, `BASELINE.md`, `.sealed`, and the archive report.

**What is verified versus derived.** The prose is hand-verified — figures, ruler lists, tenure spans, counts, named years. The findings sidecar is *derived*, reproduced from `renders.json` by `wb book --check-only` rather than copied, and pinned as the anchor. The superseded pre-v1.2 sidecar sits beside it as `chronicle-42.findings.pre-v1.2.json`, role `historical-not-anchor`; it must never be used as a diff target.

**Why that file is still there.** It is the evidence for the §4 provenance lesson: a tree internally inconsistent inside its own first commit, where version control cannot show it. Five coverage deltas across 3 of 15 scopes, all explained by the two v1.2 raid-extraction fixes and the `name` → `naming` fold; 163 findings unchanged, 8 real, 4 blocking.

**Recorded deficiencies** (both in the manifest and in `BASELINE.md`):

- `query-coverage-unstructured` — v1's query-side coverage exists only as captured stdout. A rule going non-zero to zero on the query path cannot be detected by a golden diff against this baseline.
- `retrieval-contains-generated-echo-line` — one planner echo makes the retrieval file not fully deterministic.

**Rules for use.** Create-only: a new baseline requires moving this directory aside under a new name first. `verification: hand-verified` is correct only for seed 42 — baselines for seeds 7, 99, 1234 and 2025 must carry `stability-anchor-only`, since a golden diff needs its anchor stable, not correct, but the distinction must stay legible. `baselines/**` is pinned `-text` and force-included in `.gitignore`; a fresh clone reproduces every hash.

---

## 9. Seed 42 reference facts

Verified by hand across many rounds. Useful for any future test, question suite, or sanity check. **694 events in the `.log` view; 1,035 in the record.**

**Powers:** Wurn League (f:1), Kebarrow Compact (f:2), Griwick Compact (f:3), Sworn Men of Meigate (f:4), Sworn Men of Laehiford (f:5), Hadale Commune (f:6), Vea Lode Covenant (f:7).

**Secessions:** Meigate Y19, Laehiford Y20, Hadale Y27 (all from Kebarrow); Vea Lode Y29 from Griwick.

**Collapses:** Wurn League Y20 (Kebarrow took Hadale, leaving it landless), Griwick Compact Y35, Sworn Men of Meigate Y50.

**Griwick plague:** Y26–28. 185 + 133 + 156 = **474 dead**; 296 + 208 = **504 fled**.

**Paernmel Has:** four *failed* attempts on him (Stonand Ker Y43, Keithfal Naell Y45, Throll Kell Y46, Drouldthas Stour Y49), one *successful* killing of him (Wuldweald Valdrith Y51), and two killings *he ordered* (Veillpea Dourn Y46, Thres Thrild Y47). Seven assassination records name him; role and outcome both decide the count.

**Vea Lode rulers:** Stald Gearngoll 29–45, Veillpea Dourn 45–46, Thres Thrild 46–47, Gatros Hearn 47–48, Keithfal Naell 48–50, Herpeim Raern 50–.

**Recurring false premises:** Stonand Ker never held a seat. Hehum Skul was a named heir whose claim was set aside; he never ruled. The Kebarrow Compact never took Griwick — Vea Lode did, in Y35.

**Secrets:** 77 `[secret]` events. `e:639` (Y35, Gatros Hearn's failed attempt on Sothkel Sald) is the canonical withheld-not-absent test case.

**Benchmark chronicle scopes:** Kebarrow Compact 2–21 and 22–41, Sworn Men of Meigate, the Wurn League, the Heth Fal reign.

**Raid prose shape**, since two extraction bugs lived here: raids name a *place* as target — "the Kebarrow Compact raids Hadale and kills 16, but takes nothing" (`e:278`, Y19) — while the event carries both a target faction and a place. A chronicle sentence naming the raided *power* was told no such raid existed, and the phrase reader once ran four words past the end of a name (`"hadale killed 16 but"`). Both fixed, both pinned in `CheckerCorpusTests.cs`.

---

## 10. Current status

**v1 is complete and archived.**

- **Chronicle:** fifteen scopes, every power covered, figures verify, bad passages excluded automatically with precise diagnostics.
- **Query:** 16/16 suite questions correct, zero secret leakage, zero fatal findings, retrieval byte-identical across runs, 330 tests green.
- **Baseline:** sealed at `baselines/v1/seed-42/`, verified from a fresh clone.

**Stage 3, Stage 4, the engine dynamics phase and Stage 6's geography half are complete.** Ruleset `4`, engine `1.2.0`, 501 tests green.

- **Harness:** five layers built. Its first act was to find a live simulation defect that months of hand review had missed, and it has since found two of its own instrumentation's.
- **Engine:** no mechanic is decorative. Every distribution varies; every distance-consuming mechanic acts both near and far. The tightest is war declaration at 86% one way against a bar of 90, on n=23.
- **Instrumentation is trustworthy:** every rate reports its `n`, percentages are asserted only where `n` supports them, every absence-asserting metric has a constructed counter-example, every ratio's numerator is asserted reachable.

**Regression protection is live again.** Five sealed ruleset-3 baselines at `baselines/ruleset-3/seed-*`, Layer 5 passing 0/0 on all of them and correctly skipping under ruleset 4. The v1 baseline is untouched and still verifies. All five ruleset-3 baselines carry `verification: stability-anchor-only`; only the sealed v1 seed-42 baseline is hand-verified.

**Judgement on file, and it has been read down.** Geography made the history read better *structurally* — a war is now declared over somewhere and fought there rather than wandering, and the map ends as a connected block. That reading of one chronicle scope stands. **The quantitative claim does not.** A redraw control with no spatial structure and no stability moved causal variety at least as far as geography did, and at n=5 the two cannot be told apart. Layer 1 did go from three failures to one and `verbatim repeat rate` did clear everywhere; what is no longer supported is the attribution of those to distance rather than to perturbation of a similar size. **Open, not knowledge.**

**One metric still fails, and it is the same one.** `distinct deep-chain shapes` sits at 45 on seed 7 against a bar of 60, improved from 42, with seed 2025 now clearing it. Category two, a real loss, recovering; parked, watched, not chased. Seed 99 went the other way this phase, 74 → 69, which is recorded and not diagnosed.

**Two parked findings** remain, each in `KnownFailing` with category, rationale and owning round: tribute target selection (houses demand of whoever they resent, not whoever is weak, so most demands are between near-equals) and heir selection criteria (loyalty names the candidate, ambition wins the contest). Both look adjacent to distance now and neither was touched.

**One new exposure, reported and unbuilt.** Places have terrain and positions and the render pack carries neither, so no prose contains geography — measured at one figurative use of "borders" in 12,180 words. **No checker rule reads a cell or a terrain**, so the moment a pack carries either, the model gains a vocabulary it can be wrong in with nothing watching. A fabricated distance is a new fabrication class with zero coverage.

**Next: Stage 6's economy half**, then Stage 5's workbench. Geography deliberately landed before economy, because distance gates trade the way it gates conflict.
