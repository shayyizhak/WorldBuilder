# v1.2 — Query

## Context

v1.0 (single-event render) and v1.1 (aggregation) are built and, after five fix rounds, working. Faction and reign chronicles now read as history and their figures verify against the log.

v1.2 is the last piece of v1: **natural language questions answered from the event log.**

Do not start this until the round 5 fixes are clear. The remaining fabrication in the render layer will reappear here in a worse form, because query answers assert relationships directly rather than incidentally.

---

## What v1.2 is

A question in natural language, an answer grounded in retrieved events.

Two broad shapes:

**Causal** — *"Why did the Sworn Men of Laehiford partition?"* Walk the causal edges backward from the terminal event, collect the chain, render it.

**Factual** — *"Who has ruled the Kebarrow Compact?"* Filter events, aggregate, answer.

Both are answered from **retrieved events only**.

## What v1.2 is not

- No authored interventions or prose→events (v2)
- No contradiction detection or reconciliation (v2)
- No knowledge or rumour modelling (v3)
- No changes to the simulation
- No UI beyond what is needed to ask a question and read an answer

---

## Architecture: retrieval first, generation second

**The model must answer only from events passed to it in the request.** It must never answer from its own memory of what it rendered earlier in the session, and it must never answer from the chronicle prose.

Qwen 3.6's 256K context window makes it tempting to stuff the whole world in and skip retrieval. **Do not.** Worlds grow without bound — seed 42 is 691 events over 51 years, and the tiering design targets far larger. The moment a world exceeds the window, a context-stuffing implementation degrades into confident fabrication with no failure signal. Build retrieval properly now, while the test world is small enough to verify against by hand.

### Pipeline

1. **Parse** the question into a structured query — entities referenced, event types of interest, time bounds, query shape (causal or factual).
2. **Retrieve** the relevant events from the log by structured filter, not by embedding similarity alone. The log is structured data; use it. Causal questions traverse the `<=` edges; factual questions filter on type, actor, faction, place, and year.
3. **Render** the retrieved set into an answer, under the same particulars/patterns rules as v1.1.

Step 1 is a good fit for the two-call pattern: let the model reason freely about what the question is asking, then extract into a constrained query structure. Use structured output for the extraction call.

---

## Constraints carried forward from v1.1

All of these are already established and must hold here:

- **No invented particulars.** No name, place, date, number, motive, or action not present in the retrieved events.
- **Pattern statements only about supplied figures.** Any count, average, or duration must come from engine-computed statistics, not from the model counting retrieved events. Query results need the same statistics pipeline the chronicle scopes use.
- **No log-referential language.** Answers describe the world, not the record — no "the log shows", "N events matched", "according to the records".
- **Secret filtering.** `[secret]`-flagged events are excluded from retrieval entirely, along with resolution events that inherit secrecy. A query must not become a side channel around the visibility rules the chronicle respects.
- **Structured events remain the source of truth.** Never retrieve from cached prose.

---

## Two failure modes specific to query

### Empty and partial results

The chronicle always had events to describe. A query may have none, or too few to answer.

**An answer with no supporting events must say so.** Not "there is no record of that" — that is log-referential — but a plain statement that nothing of the kind happened, or that the question cannot be answered from what is known. This must be a hard path in the code, not a hope about model behaviour: if retrieval returns nothing, the model should not be asked to generate an answer at all.

Test it explicitly with questions about actors, factions, and places that do not exist, and with questions about real entities in years where nothing happened.

### Presupposition in the question

*"Why did Stonand Ker lose the seat?"* presupposes he held it. He never did — this is exactly the fabrication the render layer produced twice, and a question can carry the same false premise in from the user.

**Validate the presuppositions of a question against the log before answering.** If the premise is unsupported, say so rather than constructing an explanation for something that did not happen. A model asked why X happened will generally explain why X happened.

---

## Evaluation

Build a fixed question set against seed 42 with known correct answers, and treat it as a regression suite. Suggested coverage:

**Causal, answerable:**
- Why did Meigate break from the Kebarrow Compact? (Expect: legitimacy loss following the Y18 succession, revolt at Y18, secession at Y19.)
- Why did the Kebarrow Compact and the Wurn League make peace in year 9? (Expect: exhaustion, after two years.)

**Factual, answerable:**
- Who ruled the Kebarrow Compact between years 22 and 41? (Expect eleven seat-holders, counting both successions and open challenges.)
- How many times was Paernmel Has the target of an assassination attempt? (Expect five — four failed, one fatal. This is the role-awareness case from round 4; it must not count the murders he ordered.)
- Which factions broke away from the Kebarrow Compact, and when? (Expect: Sworn Men of Meigate Y19, Sworn Men of Laehiford Y20, Hadale Commune Y27.)

**Should return nothing:**
- What happened to the Drelthorn League? (No such faction.)
- Who ruled the Sworn Men of Meigate in year 5? (Faction did not exist until Y19.)

**False presupposition:**
- Why did Stonand Ker lose the seat of the Kebarrow Compact? (He never held it.)
- When did the Kebarrow Compact conquer Griwick? (It did not.)

**Secret leakage:**
- Who attempted to kill Stald Gearngoll in year 36? (`e:669` is `[secret]` and unattributed — the answer must not name anyone, and ideally the event should not surface at all.)

Report: accuracy on answerable questions, false-answer rate on the empty and presupposition sets, and zero secret leakage.

---

## Build order

1. Query parsing and structured extraction; verify the extracted query against hand-written expectations before wiring up generation.
2. Retrieval — causal traversal and structured filter. Verify retrieved sets by hand against the log for the question suite.
3. Answer generation over retrieved events.
4. Empty-result and presupposition handling.
5. Run the regression suite.

Stop and report after step 2. If retrieval returns the wrong events, the answer will be wrong no matter how good the generation is, and that is much easier to see in a raw event set than in fluent prose.

---

## What I want from you

Build steps 1 and 2, then report the retrieved event sets for the question suite before building generation.

Push back if any of this is wrong. In particular: if structured query extraction turns out to be too brittle for open-ended questions and you think a hybrid — structured filter plus semantic search over event descriptions — is needed, say so with reasoning. That is a legitimate design change rather than a shortcut, but it must not become context-stuffing by another name; retrieval still has to return a bounded, inspectable set of events.
