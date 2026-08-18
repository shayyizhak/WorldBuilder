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

- **Engine:** C#/.NET. Event sourcing and deterministic simulation fit it well.
- **Inference:** Ollama, local, OpenAI-compatible, model-swappable. Currently `qwen3.6:latest`.
- **Model licensing: Apache-2.0 only.** Qwen qualifies. This is deliberate — it keeps the base model swappable and makes a future house-style LoRA legally clean to release. Llama and Gemma 1–3 were excluded for custom source-available terms; **Gemma 4 shipped Apache-2.0 in April 2026 and is now eligible.**
- **Client:** Flutter if a UI is ever built for users. Not a current concern.
- **Techniques:** constrained decoding for structured output; the two-call pattern (reason freely, then extract to schema) for anything needing structure.

### A note on model choice

Qwen is coding-tuned, and the prose was better than expected. There is a temptation to swap to a prose-tuned model. Resist it without testing: five rounds of render work converged on instructions to be *less* creative and more literal — don't invent motive, don't embellish, render a missing input as omission. A model that writes gorgeous fiction is a model that fills gaps beautifully, which is the primary failure mode. Coding-tuned literalism is plausibly why the prose was controllable at all. Any swap must also hold on the constrained-decoding side.

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

**`coverage-sound` — two invariants, both required:**

```
ACCOUNTING   extracted == checked + unresolvable    (per rule, per scope)
FLOOR        extracted >= previous_extracted        (per rule, per scope)
```

ACCOUNTING says nothing is dropped *after* extraction. FLOOR says nothing is dropped *before* it. **Each is trivially satisfiable alone** — one round satisfied ACCOUNTING by collapsing extraction from 33 to 2; the next did the reverse. Re-baselining the floor must be an explicit human action, never something that happens by rerunning.

**Coverage reporting.** Every run emits per-scope `extracted / checked / unresolvable / fired` per rule. A rule extracting nothing from a scope containing the relevant construction emits `rule-inert`. This converts silent inertness into a loud failure.

**A rule that fires without extracting has a floor of zero.** Such rules raise findings through word-scanning paths that never touch their extraction counter. FLOOR protects nothing for them: they can go silent forever and the golden layer will not notice.

Which rules, measured rather than reasoned about — `wb floors --set ruleset-5`, 5 seeds, 60 scopes, 2026-08-18:

| rule | scopes with a floor | of | reason |
|---|---|---|---|
| `action` | 11 | 60 | zero at baseline |
| `coined-term` | 60 | 60 | floored everywhere |
| `count-enumeration` | 9 | 60 | zero at baseline |
| `count-narration` | 28 | 60 | zero at baseline |
| `coverage` | 0 | 60 | no floor on this panel |
| `date` | 13 | 60 | zero at baseline |
| `date-agreement` | 24 | 60 | zero at baseline |
| `departure` | 47 | 60 | zero at baseline |
| `naming` | 60 | 60 | floored everywhere |
| `outcome` | 0 | 60 | no floor on this panel |
| `partition-sum` | 8 | 60 | zero at baseline |
| `quantity` | 3 | 60 | zero at baseline |
| `shape` | 0 | 60 | no floor on this panel |
| `succession` | 12 | 60 | zero at baseline |
| `summary-body` | 21 | 60 | zero at baseline |
| `tenure` | 6 | 60 | zero at baseline |

**Three, not six, and not three of the six that were listed.** The hand-written list named `action`, `date`, `quantity` and `tenure` — all of which carry floors in some scopes — and omitted `shape`, which carries none. Wrong in both directions, which is the signature of a list reasoned out rather than measured: a stale list drifts one way.

The three that remain — `coverage`, `outcome`, `shape` — are exactly the three with no `Extracted` call site anywhere in `WorldBuilder.Inference`. Their floors are unreachable rather than unreached, and no re-run changes that.

**The panel is the instrument, and one panel was not enough.** `tenure` reads 0 of 13 on ruleset-4 seed 42 alone and 2 of 60 across that panel — scope selection, not a rule gap. `quantity` read **0 of 60 across the whole ruleset-4 panel** and 3 of 60 at ruleset 5, so what looked like a call site no seed could reach was a call site that panel happened not to reach. A single document cannot separate "not instrumented" from "unreached", a single panel can be wrong about it, and the label therefore reports the reach and leaves the cause to be read off the source. **Eleven of the ruleset-4 panel's twenty holdouts were decided by a rule in this state**, and in all 22 such rows the sidecar carries a `rule-inert` line for the same rule in the same scope — saying both that it read nothing here and that a finding it owns kept the section out of canon. This is the exact configuration Stage 6 forbids on purpose for a premature geography rule, arrived at by accident. Unrepaired deliberately: correcting an extraction counter raises a floor, and re-baselining is an explicit human act.

**Test entry points.** Every rule test enters at the outermost callable production uses. Two tests once passed while the code failed — one hand-fed an event kind the planner never emits, the other called an inner method the public entry point bypassed. **A test feeding an input the production caller never produces is worse than no test**: it converts silence into false confidence.

**Assert extraction, not just absence of failure.** A test asserting "no finding fired" passes when the rule is inert.

**A test that one error cannot pass may still be passed by its opposite.** The ruler list asserted "no two neighbouring spells share a ruler" — satisfied both by collapsing a contested transfer correctly and by deleting a genuine second tenure, which are opposite errors. Same shape as the raid partition that summed while one of its three cells was structurally zero. Assert the collapse *and* the survival.

**Construction-gated rules cannot be required to always extract.** `partition-sum` needs a partition; a two-sentence answer doesn't have one. Loosening extraction to satisfy a blanket coverage requirement is how false positives get manufactured — one attempt cost seven true chronicle sections. Inert is a finding only when the construction is present.

**Completeness rules on fragments are a trap.** They work on whole sections and misfire on short answers.

### On measurement

**Read the record, never the presentation view.** The `.log` hides ~341 bookkeeping rows (the yearly accounts) out of 1,035. Much of the economy's causal influence runs through them. This produced two confidently wrong measurements — economy coupling reported as 18 of 524 when it was 142 of 850.

**A filter that drops rows must fail loudly.** Both measurement errors had the same shape: a scan silently omitting rows. If an edge's target is missing, that is a dangling-reference failure, not a row to skip.

**Ranking by raw event count systematically under-represents things that ended.** A power destroyed in year 20 had eighteen years to accumulate events; a survivor had fifty. Scope selection by "weightiest" therefore drops exactly the powers whose stories conclude. This matters again at Stage 8's significance threshold, where dropping is mandatory — rate rather than total, or a floor for any power that held land or was destroyed.

**Sidecars are the provenance record for findings.** A panel run that discards them cannot be interrogated afterwards — which emitter produced which row becomes unanswerable without a re-run. Retain by default.

### On the model itself

**Generation is not reproducible run to run**, despite temperature 0 and a fixed sampling seed. Evidence: the same question with a byte-identical request body was classified causal in one run and factual in another, changing retrieval from three records to one. This is Ollama's own variance.

Consequences: a single-question CLI call is not a valid proxy for a suite run; "16 of 16" is a sample, not a proof; two consecutive identical runs is the honest evidence standard.

**The planner mistypes verbatim fields.** Three slips in sixteen on a field it was instructed to copy exactly. Resolve verbatim fields against the question text or the record — never fuzzy-match the planner's string, because a miss is recoverable and a confident resolution to the wrong entity is not. Years matter most: a mistyped year produces no failure signal, just a plausible answer about the wrong decade.

**Absent vs withheld must be distinguishable.** The same conflation appeared as `unresolvable`-vs-fired in the checker and as one empty-result sentence covering three different situations in the query layer. This is not just phrasing — the v3 epistemic layer's entire premise is that not-known and not-true are different, so the query path has to be able to express it.

### On experiment design

**A pre-registered prediction shared by both competing mechanisms is not a test of either.** Pre-registration constrains the analyst; it does not discriminate for you. The geography prediction was pre-registered, confirmed, and separated nothing.

**A comparative decision rule needs a degeneracy guard** — a stated minimum panel range below which the rank arm is void and the rule falls to its absolute arm. Without it, a tight panel silently converts a rank criterion into a coin flip. Seed 99 was recorded as *failed* rather than mixed on exactly this basis: every seed's spread lay in a 34–37% band, so the rank arm carried no information.

**A guard covers the panel being too small; it does not cover the statistic being structurally constant.** The holdout brief's over-firing arm asked whether the heaviest rule's firing count on *surviving* scopes had risen. A blocking rule fires almost only where it causes a holdout — that is what blocking means — so the count is zero on both sides for every rule the arm is aimed at, and the arm cannot be taken however concentrated the holdouts are. Check that each arm's discriminating half can vary before committing to it.

**Each arm of a pre-committed decision rule must be shown reachable against existing data before the measurement.** That one rule carried two reachability defects, not one: the arm above, and a 20-point range criterion the population had never met — ruleset 3's own spread was `width=42`. A dry run over existing baselines catches both for free, and costs nothing but the reading. The degeneracy guard covers a statistic that varies too little; it does not cover one that cannot vary, and neither covers a threshold no arm could ever clear.

**Seen data sizes the next experiment; it does not decide the last one.** The paired variance figure could have rescued the geography result and was fenced to sizing N only, in writing, before it was computed.

**The reference panel is not the measurement panel.** Five reference seeds exist because hand verification is expensive; statistical comparison needs none. Sizing one by the cost of the other is how five seeds became a statistical claim.

**A contrast family closes when its verdicts are reported.** Enlarging a closed family moves Holm thresholds under already-published verdicts — so `flat − geography` was registered as its own family of one rather than a fourth member of the reported three.

**When a property holds on the panel by coincidence of the panel's construction, no test on that panel can detect it.** The general form of the silent-path family, one level out from the existing rules. Five separate sites re-folded a world's log against *the repository's stored board* rather than the world's own; all five were correct on the reference panel because its five seeds share a board, and all five refused every world on a panel where each seed has its own. The panel's construction was the thing making the tests pass. This is the argument for measurement panels being built differently from reference panels — not merely larger, but varying in the dimensions a reference panel holds fixed.

**A one-time verdict about a ruleset transition is a record, not a test.** Tests assert standing properties. `AdditiveRecordTests` asserted that ruleset 5 was additive over the sealed ruleset-4 worlds — true, proven, and unprovable by any engine that no longer contains ruleset 5, so it became a permanent red the moment ruleset 6 landed. Transition properties belong in the provenance chain, where they survive the transition that made them unprovable: the verdict now lives in `Provenance.cs` and `docs/phase-relation-termination-report.md`.

**Every mechanic ships with an off-switch that reproduces the prior ruleset exactly.** Stronger than instrumentation invariance, which only says a measurement did not disturb the world: this says a *mechanics change* touched nothing outside its own rules. `TurningTheTerminationRulesOffGivesBackTheOldRuleset` is the strong form — switch the three termination rules off and all five sealed ruleset-5 logs come back event for event, which is how the additive-only claim gets made once worlds do move. Where the previous ruleset is too far back for that, the weak form pins a characterisation figure and says which form it is (`TurningGeographyOffGivesTheSameFlatWorldEveryTime`).

**A regression guard is per-seed; an acceptance target is not.** `distinct deep-chain shapes ≥ 60` was both at once, and correlates with a world's length at `r = 0.871` — so half of ordinary worlds fail it and a world that merely ran shorter fails it for a reason unrelated to causal structure. Split: the target stays, and each seed carries a floor equal to *its own last accepted value*. A seed that has never met the target keeps the target and keeps failing, so a floor is never an excuse; and a floor can only be raised by hand after a run somebody judged good, never lowered by rerunning. Converting the bar to a rate was rejected — it would need a shapes-per-event constant chosen by fitting.

**Assert branches separately when they have different histories.** `BothOutcomesOfTheRollAreReached` asserted the covert-coup `seized` and `exposed` branches together, so when one world stopped uncovering conspiracies the obvious repair was to move both to panel level — dropping the protection on the branch that had actually been structurally zero once. Split instead: `seized` stays per-seed because that is the path discovery bought the assertion for, `exposed` goes panel-level with its per-seed figures recorded. Asserting two branches together makes the weaker assertion the price of the stronger one.

### On plumbing

**The silent-path family is a plumbing phenomenon, not a checker phenomenon.** Now seen in mechanics (covert coup structural zero) and in the independent verifier itself.

**A verifier that reads a field name the engine doesn't write cannot fail.** Layer 4 read `took`/`haul`/`plunder`; the engine writes `loot`, so the three-way raid split had been two-way since the layer was written, with nothing failing because every assertion was about the accounting. Two of the last four defects were field-name mismatches. Assert schema inclusion: every field name a consumer reads exists in the emitter's vocabulary. **Now a standing test rather than a scan** — the vocabulary comes off the records and the reads are *observed* at `Event.GetString` while the consumers run, because a declared list of what a consumer reads is the same artefact that produced five of the silent-path family. 84 names across 42 kinds emitted; 98 reads observed; **zero dead**.

**Four event kinds are declared and never emitted:** `ECONOMY.TRADE_COLLAPSE`, `DIPLO.ALLIANCE_BROKEN`, `CONFLICT.SIEGE`, `INTRIGUE.GRIEVANCE_SETTLED`. Each has a name and a render template and no emitter anywhere in the rules. The structural-zero family again, and the principle the dispersion work already states from the other side: **a label with no emitter is worse than a dead branch.** Substrate audited — see `docs/phase-four-dead-kinds-report.md`: three have the state they need and are emission-only, `CONFLICT.SIEGE` needs new persistent state and is carded.

**Instrumentation invariance.** Attaching a measurement must not change the world, asserted by log hash with and without, across all seeds. A standing property, not probe scaffolding.

**The engine still reproduces the sealed baselines, and that is now asserted.** `InstrumentationInvarianceTests` replays all five sealed ruleset-4 baselines event for event. Nothing asserted it before, and every measurement taken against those baselines rested on it. Same family as the rest of this list: a load-bearing property that no test held.

**RNG draw order is load-bearing.** A pure refactor at a short-circuiting site can change worlds with every test green. The with/without log hash is the only detector.

**An ambiguous figure is a fabrication vector regardless of who reads it next.** Previously filed under rendering as something the engine does to the model; it generalises. Three instances: plague duration in two conventions, an unnamed 0-of-13 denominator, a range read as a spread. Fixed mechanically — dispersion self-identifies at emission (`sd=`, `range=[a, b] width=`, `cv=`, `ci95=`, `var=`).

**A wrong figure in an error message is the same family as an unlabelled one.** `HttpClient` carried a 100s default, so a 900s call died at 100s and reported "did not answer within 900s". A message reporting a limit must print the limit that actually applied.

**A doc comment asserting a property the implementation lacks is the same family as an error message printing the wrong limit.** `Coverage.cs:36` states the read-versus-passed premise cleanly and nothing enforces it, so it misled a reader who had the code open. Both are a claim in the record that nothing checks — same failure, different surface.

**A list in the documentation describing a measurable property of the code should be emitted by the code.** The same family, one step further out. §4's list of rules lacking floor protection was hand-written and wrong in both directions — naming two rules that are protected and omitting one that is not — which is the signature of a list reasoned out rather than measured. It is now generated by `wb floors` and carried here with the command and date that produced it, exactly as the schema sweep is. A declared artefact describing a property of the code is a second copy of that property, kept in step by hand.

**From ruleset 4, a world is a log and its board.**

---

## 5. Roadmap

The board is at **https://trello.com/b/Ovwt583e/world-builder** with lists Done → In flight → Foundations → Simulation depth → Scale → Release.

| # | Stage | Notes |
|---|---|---|
| 1 | Finish v1 render | ✅ done |
| 2 | v1.2 query | ✅ done |
| 3 | Determinism & versioning decision | ✅ done in reduced form — provenance stamp shipped, versioning deferred |
| 4 | Automated quality harness | Five layers, built. Layer 5 passing 0/0 across five ruleset-4 baselines |
| 5 | Workbench UI | **Decision pending.** Instrumentation for the builder, not product |
| 6 | World substrate: geography, then economy | Geography ✅ built and measured. **Economy half not started** |
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

**Stage 3 — determinism. Settled.** Two things break "seed + intervention log reproduces the world": rule changes (every later stage changes rules) and model variance (proven at v1.2). Both point at **the materialised event log as the durable artefact**; seed is provenance, not a regeneration recipe.

**Versioning is deferred entirely.** Pre-release, single user, test worlds — when rules change, worlds get dumped. What shipped instead is a *provenance stamp*: **written, never read.** It exists because Stage 4's golden diff cannot otherwise distinguish engine change from ruleset change from pack change from Ollama variance. Revisit at Stage 14/15. **V2 corollary:** the intervention log stores *accepted deltas*, never the prompt that produced them.

**Stage 6 — geography. Half built.** Import the physical layer only (terrain, biomes, adjacency, travel cost); simulate the political layer on top. Board imported, stored, hashed, verified; positions assigned at worldgen; four mechanics consume distance. **Generators are NOT reproducible across versions** — treat generation as one-time, store the artefact in the world file, hash it into the header, never regenerate from seed. **Watabou TownGeneratorOS is GPL-3.0 — do not embed it**; outputs are permissive, so use the hosted tool.

**The geography result, recorded honestly.** Geography does **not** move causal variety relative to structureless perturbation. N=207, four arms paired on the same seeds and boards, MDE pre-registered at 5 points.

| contrast | mean | 95% CI | p |
|---|---|---|---|
| shuffle − redraw | −1.15 | [−3.16, +0.86] | 0.26 |
| geography − shuffle | +0.62 | [−1.46, +2.71] | 0.56 |
| geography − redraw | −0.53 | [−2.70, +1.65] | 0.63 |

Arm means: flat 64.4, geography 63.1, shuffle 62.4, redraw 63.6. Realised paired σ 15.87 against 16.48 predicted, so the sizing held. **These are precise nulls, not failures to detect** — the headline interval excludes the MDE in both directions.

*Retired:* the +33 causal-variety attribution to geography; `verbatim repeat rate` clearing as a geography result; any claim of the form "geography improved four seeds of five."

*Stands:* the **680 / 555 / 34** census (680 decisions consulted a proximity, 555 had room to be moved, 34 were), which rests on within-world decisions rather than on five worlds. Wars fought where declared. The proximity calibration fix. Alliance's distance term live in 13 of 13. All the engineering.

*Untouched:* geography's design rationale. Distance gates conflict, trade, alliance and later rumour. The variety-delta claim was volunteered, never required.

**Board geometry is not first-class.** `var(board)` negative against `var(seed)` across 207 boards — with the standing limitation that this shows sensitivity strongly and insensitivity only weakly, since every board came from one generator.

**No geography checker rule until the terrain pack exists.** A rule written now extracts 0 forever, `rule-inert` cannot fire because the construction is genuinely absent, and FLOOR baselines at 0 — manufacturing the silent-path signature on purpose.

**Stage 7 — the collision to resolve.** "Cached renders are canon" and "retroactive authoring back-propagates causes into the past" cannot both hold unconditionally, because back-propagation rewrites events that already have canon prose about them. Decide deliberately in design.

**Stage 8 — the sequencing trap.** Complexity is cheapest to iterate at 20 actors, but LOD changes how entities are *represented*, so every mechanism written before the contract exists gets rewritten after. Write the contract now, defer the population scale-up to Stage 10. Three tiers: statistical populations → named entities → simulated individuals. Crystallisation deterministic via `(world_seed, entity_id, query)`.

**Content packs, species and settings are a Stage 8 concern, not Stage 9.** Stage 8 exists because LOD changes how entities are represented; culture and setting vocabulary are the same trap on two more axes. **Revised exit criterion:** every Stage 9 mechanism must be expressible at all three LOD tiers, parameterisable per culture, and carry no hardcoded setting vocabulary.

**Stage 9 — naming first.** Names carry nearly all the felt sense of distinct cultures, and retrofitting a naming system after the log is full of names is miserable. Religion second — highest yield, because it gives succession disputes *reasons* rather than dice.

**Stage 15 — inference cost.** Local Ollama is free; hosted is not. Bring-your-own-key, hosted inference with quotas, or hosted simulation with client-side rendering. The render cache is the cost lever: cached renders are canon, which means they are also *paid for once*. The lazy-rendering architecture chosen for tractability turns out to be the business model.

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

### Counter shapes

`extracted` is not one quantity. It is three, and for two of the three shapes it is undefined rather than zero. Reporting all three as `extracted` is what makes the coverage invariants go vacuous exactly where they matter.

| shape | population | counters | `rule-inert` means |
|---|---|---|---|
| **Requirement** — engine supplies a list, rule checks the prose mentions it | items in the list | `required / satisfied / missing` | list non-empty and nothing checked |
| **Vocabulary scan** — marker from a constant list, support looked up in the body | marker hits in the prose | `scanned == fired + supported` | marker present, neither outcome produced |
| **Extraction** — pulls a candidate assertion out of the sentence | assertions extracted | `extracted / checked / unresolvable` | construction present, nothing extracted |

**The vocabulary-scan shape carries the failure history.** Every historical silent-path defect lived there — `"included"` absent from the partiality-marker list, `people`/`exiles`/`returns` absent from the countables lexicon. So `scanned == fired + supported` is the highest-value invariant in the scheme, and it is the one that does not yet exist. Until it does, a lexicon gap presents as nothing at all.

ACCOUNTING and FLOOR as originally stated apply to the extraction shape only. Requirement and vocabulary rules do not get a re-baseline when instrumented — they get a *first* baseline of a different quantity, and the distinction should stay visible in the record.

**A rule slot created by `Fired` alone is unfalsifiable.** `coverage` and `outcome` have no `Extracted` call sites, so `Inert()` flags them on every passage forever. A detector that alarms unconditionally is worse than no detector: it trains you to skip the output.

### Sidecar format

`{rule, scope, span, detail, blocking, fatal}` plus a per-scope `coverage` block with `extracted / checked / unresolvable / fired / accounted`. Exclusions appear as findings with `fatal: true`. `unresolved` entries carry their span.

### Disposal differs between chronicle and query

A chronicle has fifteen sections and can drop one with a note in its place. **A query answer has one answer and nowhere to put a warning.** A fatal finding on an answer returns the retrieved facts plainly, or an admission — never annotated prose carrying a known fabrication.

---

## 7. Test suite (Stage 4 — specced, not fully built)

Five layers, increasing cost:

1. **Dynamics invariants** — log metrics as assertions. Dangling refs = 0; repeat rate < 10%; single-actor chains = 0%; max causal depth ≥ 8; distinct two-step shapes ≥ 60; collapses per faction ≤ 1; coup success > 15%; covert coup success > 0; ECONOMY→non-ECONOMY ≥ 10% of edges; cross-domain ≥ 25%. Run across all five seeds.
2. **Checker rule unit tests** — synthetic passages, positive and negative per rule, plus lexicon-completeness tests (every marker in every list fires its rule on an identical sentence with only the marker swapped).
3. **Regression corpus** — 31 hand-verified fabrications from the render rounds, each mapped to the rule that should catch it. Cases fixed and then regressed are the highest-value entries.
4. **Chronicle verified against the log** — ruler lists, departure manner, tenure spans, raid counts (three outcomes), battle counts, killing counts split internal/external, marriage counts, every named year, every proper noun.
5. **Golden diff** — current output against a stored known-good render. Any figure that moves is a failure. **Diff the coverage block too** — extraction counts are far more stable than prose, and a rule going non-zero to zero is the signature of the silent-path family.

**Layer 3 is now permanently pinned to the sealed v1 record.** 20 of 28 scoped rows only fire against a world that no longer regenerates. "All v1 work can be dumped" is therefore no longer true for that one artefact — the sealed v1 record and its reference facts must survive every future ruleset change.

**Layer 4 deliberately duplicates the checker.** The checker decides what enters canon; the suite decides whether the checker works. A checker that silently stops firing is invisible without an independent verifier. If they ever share an implementation, that property is lost.

**Two standing properties added at the pre-verification phase, both asserted rather than described:**

- **Schema inclusion.** Every field name a consumer reads exists in the emitter's vocabulary. Run from both test assemblies — Layer 4 sweeps its own reads, because sweeping it from the checker's side would route the independent verifier through the implementation it exists to be independent of.
- **The engine still reproduces the sealed ruleset-4 baselines**, event for event, on all five seeds. Only the provenance header differs. Nothing asserted this before, and every measurement taken against those baselines silently rested on it. It fails on a genuine ruleset change, correctly — that is when the baselines are recut.

---

## 8. Sealed v1 record — seed 42 reference facts (historical)

> **This describes a world that no longer regenerates.** Under ruleset 4, positions are assigned at worldgen and four mechanics consume distance, so the stream is consumed differently and seed 42 is *a different history* — not a stale one. Everything below remains valid **only** as documentation of the sealed v1 record.
>
> It is not dead weight: **Layer 3's regression corpus depends on it permanently** (20 of 28 scoped rows). Keep the sealed record and these facts indefinitely.
>
> The live ruleset-4 reference set is a separate artefact, rebuilt by hand. See §9.

Verified by hand across many rounds. **694 events in the `.log` view; 1,035 in the record.**

**Powers:** Wurn League (f:1), Kebarrow Compact (f:2), Griwick Compact (f:3), Sworn Men of Meigate (f:4), Sworn Men of Laehiford (f:5), Hadale Commune (f:6), Vea Lode Covenant (f:7).

**Secessions:** Meigate Y19, Laehiford Y20, Hadale Y27 (all from Kebarrow); Vea Lode Y29 from Griwick.

**Collapses:** Wurn League Y20 (Kebarrow took Hadale, leaving it landless), Griwick Compact Y35, Sworn Men of Meigate Y50.

**Griwick plague:** Y26–28. 185 + 133 + 156 = **474 dead**; 296 + 208 = **504 fled**.

**Paernmel Has:** four *failed* attempts on him (Stonand Ker Y43, Keithfal Naell Y45, Throll Kell Y46, Drouldthas Stour Y49), one *successful* killing of him (Wuldweald Valdrith Y51), and two killings *he ordered* (Veillpea Dourn Y46, Thres Thrild Y47). Seven assassination records name him; role and outcome both decide the count.

**Vea Lode rulers:** Stald Gearngoll 29–45, Veillpea Dourn 45–46, Thres Thrild 46–47, Gatros Hearn 47–48, Keithfal Naell 48–50, Herpeim Raern 50–.

**Recurring false premises:** Stonand Ker never held a seat. Hehum Skul was a named heir whose claim was set aside; he never ruled. The Kebarrow Compact never took Griwick — Vea Lode did, in Y35.

**Secrets:** 77 `[secret]` events. `e:639` (Y35, Gatros Hearn's failed attempt on Sothkel Sald) is the canonical withheld-not-absent test case.

**Benchmark chronicle scopes:** Kebarrow Compact 2–21 and 22–41, Sworn Men of Meigate, the Wurn League, the Heth Fal reign.

---

## 9. Current status

**Ruleset 4.** 584 tests green, 2 skipped. No `SimConfig` threshold has moved since ruleset 3, and the engine now provably reproduces all five sealed ruleset-4 baselines event for event.

**Done:** v1 render and query. Stage 3 in reduced form (provenance stamp; versioning deferred). Stage 4 harness, five layers. Stage 6 geography half — board imported, stored, hashed, verified; positions at worldgen; four mechanics consuming distance. Four measurement phases, two aborted deliberately at step 1 when their step-1 findings invalidated the rest.

**Baselines:** five ruleset-4 machine baselines cut, all seals verifying, Layer 5 passing 0/0. `BaselineArchive` carries the board and checks it against the genesis fingerprint.

**The single blocking gap: no verified reference set at ruleset 4.** Machine baselines exist; hand-verified facts do not. Only Shay can close it. Everything is staged in `out/carry-forward/reference-set/` — candidate facts sheet, 16 candidate questions with supporting records, 5 ranked secret candidates, all marked unverified. This is a rebuild, not a re-verification. **The three machine items upstream of it have all returned and none invalidated a staged row**, so `docs/reference-set-verification.md` can start.

**Parked failures:** seed 7 `distinct deep-chain shapes` at 45 against 60 (was 42). Seed 99 went 74 → 69, recorded unexplained.

### Open items, in order

*Machine work — all three are done. Full report: `docs/phase-preverification-machine-report.md`.*

1. **Holdout distribution across the five ruleset-4 seeds, grouped by rule.** ✅ measured, **escalates**. 20 holdouts of 60 scopes across the panel, spread over eight rules with the heaviest (`action`) at 35% — nowhere near an over-firing verdict. The pre-committed "checker working" arm fails on its second half only: per-seed rate `range=[15, 46] width=31` against a stated 20 points. **Ruleset 3's own spread was `range=[8, 50] width=42`**, so the criterion was never met there either and failing it is not a regression. Scope *selection* is unchanged — the lists differ because the histories do. **The substantive finding is elsewhere**: eleven of the twenty holdouts were decided by a rule whose extraction counter never moved (see §6). That is what needs a human.
2. **Vea Lode contested-transfer check.** ✅ **no v1 entry is suspect.** Seven contested transfers in the sealed v1 record; six of them are crossed by a hand-verified ruler fact (five on Kebarrow, one on Hadale) and **all six agree with the record**. Vea Lode's own seat has none, so its §8 list was never at risk — a weaker reason than the derivation handling it, and worth stating as such. The ruleset-4 derivation *was* wrong: it collapsed by adjacency rather than by year, which deletes a genuine second tenure. Fixed in both copies. It had never fired, because every second tenure on every sealed record happens to be non-adjacent, so **no staged reference-set row changed** — asserted by re-deriving both rules and diffing.
3. **Schema assertion.** ✅ **zero dead reads**, now a standing test in both assemblies rather than a scan. 84 field names across 42 emitted kinds; 98 reads observed by recording at `Event.GetString` while the consumers run. One off-kind read (`ECONOMY.FAMINE.refuge`, a conditional field), reported and correct.

*Shay's own hands, blocking:*

4. **The 16-question query suite and the withheld-not-absent case.** Layer 3 needs nothing (pinned to the sealed v1 record); Layers 4 and 5 are machine-checkable. **The gate is open** — `docs/reference-set-verification.md` required this phase to name the invalid staged rows, and there are none. Its pre-committed branch did not trigger, so ruler lists may be verified from the derivation output.

### The decision waiting

**Stage 5 (workbench) versus Stage 6's economy half.** The workbench case has strengthened: every decisive moment across four phases was an inspection problem — a metric catching a miscalibration, a probe catching an ordering bug, a guard catching an underpowered comparison, a field-name mismatch findable only from outside the code.

**The counter-argument, which should be heard before deciding.** Since Stage 2, everything shipped has been machinery: harness, determinism, geography plumbing, controls, panel, the checker's own verifier. The world itself has not gained a single new *kind of thing*. Stage 6's economy half has now been deferred behind four consecutive phases, and the harness is more tractable than the world — it gives clean, gradeable answers, which is exactly what makes it comfortable to keep building.

Both choices are defensible. **The thing to avoid is a fifth instrumentation phase arriving without that having been a decision.**

**Sequencing constraint on either choice:** the ruleset-4 reference set closes *before* anything touches mechanics. The economy half is such a change; a workbench, if instrumentation invariance holds, is not. This asymmetry is the only thing that bears on the decision from outside the arguments themselves.
