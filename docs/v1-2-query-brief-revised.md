# v1.2 — Query (revised)

Supersedes `v1-2-query-brief.md`, written at round 5. The architecture in that document is unchanged and still correct. What has changed is everything the render layer learned in the nine rounds since — the checker exists, the fabrication classes are catalogued, and there is a corpus. This revision folds all of that in.

---

## Status: v1 render is done

Not defect-free — done. The distinction matters and it is deliberate.

The chronicle reads as history and its figures verify. Fifteen scopes, every power in the world covered. Bad passages are excluded automatically with precise diagnostics and kept out of canon. The Tier 2 checker fires on succession, tenure, action, departure and naming assertions, and this round it caught a fabricated tenure before I did.

What remains is a long tail — a plague total three off its own breakdown, "three wars" where there was one — and the excluded-passage mechanism is exactly what that tail is for. No reader of that chronicle is misled about the shape of the world.

The checker and test suite are **Stage 4 work, after v1.2**, sized as a project. Continuing to drip one assertion per round has been the wrong shape: four consecutive rounds found the same defect class in four different pieces of plumbing, which is what happens when tests are written against a target that changes weekly.

---

## What v1.2 is

Natural language questions answered from the event record.

**Causal** — *"Why did Hadale break from the Kebarrow Compact?"* Walk the `<=` edges backward from the terminal event, collect the chain, render it.

**Factual** — *"Who ruled the Vea Lode Covenant?"* Filter, aggregate, answer.

Both answered from **retrieved events only**.

## What v1.2 is not

- No authored interventions or prose→events (v2)
- No contradiction detection or reconciliation (v2)
- No knowledge or rumour modelling (v3)
- No simulation changes
- No UI beyond asking a question and reading an answer

---

## Architecture: retrieval first, generation second

The model answers only from events passed in the request. Never from its memory of earlier renders in the session, never from chronicle prose.

**Do not stuff the world into context.** Qwen's 256K window makes it tempting and it will appear to work at 694 events. Worlds grow without bound, and the moment one exceeds the window a stuffing implementation degrades into confident fabrication with no failure signal. Build retrieval now, while the test world is small enough to verify by hand.

### Pipeline

1. **Parse** the question into a structured query — entities, event types, time bounds, shape (causal or factual). Two-call pattern: reason freely, then extract to schema under constrained decoding.
2. **Retrieve** by structured filter and causal traversal. Not embedding similarity alone — the record is structured data, use it.
3. **Render** the retrieved set under the v1.1 particulars/patterns rules.

### Read the record, not the `.log`

The `.log` is a presentation view that hides roughly 341 bookkeeping rows — the yearly accounts — of 1,035 total. Much of the economy's causal influence runs through them.

This has already caused one confidently wrong measurement in this project (economy coupling reported as 18 of 524 when it was 142 of 850). A query layer built on the view would answer *"why was there a famine"* with the political consequences and none of the harvest chain that caused it. **Retrieval reads the record.**

---

## Constraints carried forward

All established, all still binding:

- **No invented particulars.** No name, place, date, number, motive, or action absent from the retrieved events.
- **Statistics from the engine**, never counted by the model, and **stated at the scope they were computed for**. Faction figures inside a reign answer is a live defect class, not a hypothetical.
- **No log-referential language** — no "the records show", "N events matched".
- **Secret filtering.** 77 events carry `[secret]`; they are excluded from retrieval entirely, along with resolutions that inherit secrecy. A query must not become a side channel around visibility rules the chronicle respects.
- **Structured events are the source of truth.** Never retrieve from cached prose.

---

## Reuse the checker

This is the largest change from the round-5 brief and the main reason to revise it.

**Every answer is checked before it is returned.** Query answers are renders — they inherit every rule. The same checker that validates chronicle passages validates answers, with the retrieved event set standing in for the scope's event set.

The rules that matter most here are the ones that have caught the most in the chronicle:

| rule | what it catches in a query answer |
|---|---|
| `tenure` | *"ending Skul's tenure"* — asserting someone held a seat they never held |
| `succession` | a predecessor/successor link no event supports |
| `action` | an actor, verb, target combination absent from the retrieved set |
| `date` | a year that does not match its event |
| `outcome` | a challenge or claim result inverted from the event's own clause |
| `departure` | killed described as cast out, or the reverse |
| `invented-mind` | motive attribution — *"exploiting"*, *"motivated by"* |

**But the disposal differs.** A chronicle can exclude a passage and print a note where it stood. **A query answer has nowhere to put a warning block** — the person asked a question and is not reading with the record open beside them.

So a failed check must produce a *different answer*, not an annotated one:

- **Fatal finding** → do not return the prose. Return what was retrieved, plainly stated, or say the question cannot be answered reliably.
- **Non-fatal finding** → return the answer, log the finding.
- **Never** return prose carrying a known fabrication, however hedged.

### One thing to fix before reusing it

The checker currently files two different situations under `unresolvable`: *I could not perform the lookup*, and *I performed it and the thing is not in the record*. The second is a fabrication and must fire.

In the chronicle this produces a quiet miss. **In a query it produces a wrong answer to a direct question**, which is worse — the whole point of the answer was the lookup. Split the two before wiring the checker into the query path, and carry the offending span in the `unresolved` block so it is diagnosable.

---

## Two failure modes specific to query

### Empty and partial results

The chronicle always had events to describe. A query may have none.

**This must be a hard code path.** If retrieval returns nothing, the model is never asked to generate. Not "there is no record of that" — that is log-referential — but a plain statement that nothing of the kind happened, or that the question cannot be answered from what is known.

Note the symmetry with the checker bug above: *retrieval returned nothing* and *retrieval failed* are different, and conflating them produces confident wrong answers in both places.

### Presupposition in the question

*"Why did Stonand Ker lose the seat of the Kebarrow Compact?"* presupposes he held it. He never did — and this is the exact fabrication the renderer produced in rounds 3, 4, 5, 8 and 11. A user can now hand it in as a premise.

**Validate presuppositions against the record before answering.** A model asked why X happened will generally explain why X happened.

The `tenure` rule already does the underlying work: it fired on *"held the seat since 1"* this round. Run it against the *question*, not only the answer.

---

## Regression suite — seed 42

All facts below verified against the current record (694 events in the view).

**Causal, answerable**

| question | expected |
|---|---|
| Why did Hadale break from the Kebarrow Compact? | Y27 secession, caused by the Compact's own raid on Griwick being beaten off — the failed attack, not an attack repelled |
| Why did the Wurn League end? | Y20 — Kebarrow took Hadale, leaving it landless; collapse, then peace at Y21 |
| Why did Threi Cut rise against the Vea Lode Covenant in 51? | caused by the death of its ruler Keithfal Naell |

**Factual, answerable**

| question | expected |
|---|---|
| How many times was Paernmel Has the target of an attempt? | Four — Stonand Ker Y43, Keithfal Naell Y45, Throll Kell Y46, Drouldthas Stour Y49. Must **not** count the two murders he ordered (Veillpea Dourn Y46, Thres Thrild Y47). This is the role-awareness case. |
| Which powers broke away, and from whom? | Meigate Y19, Laehiford Y20, Hadale Y27 (all from Kebarrow); Vea Lode Y29 from Griwick |
| Which powers were destroyed? | Wurn League Y20, Griwick Compact Y35, Sworn Men of Meigate Y50 |
| Who ruled the Vea Lode Covenant? | Stald Gearngoll (29), Veillpea Dourn (45), Thres Thrild (46), Gatros Hearn (47), Keithfal Naell (48), Herpeim Raern (50) |
| How many died in the plague at Griwick? | 474 across three years — 185, 133, 156; 504 fled |

**Should return nothing**

- What happened to the Drelthorn League? *(no such power)*
- Who ruled the Sworn Men of Meigate in year 5? *(founded Y19)*
- Who ruled the Hadale Commune in year 51? — careful: this **is** answerable (Durnrin Drar). Include near-misses like this so "returns nothing" is not the safe default.

**False presupposition**

- Why did Stonand Ker lose the seat of the Kebarrow Compact? *(never held it)*
- Why did Hehum Skul's reign end? *(named heir, claim set aside, never ruled)*
- When did the Kebarrow Compact conquer Griwick? *(it did not — Vea Lode took Griwick in Y35)*

**Secret leakage**

- Who attempted to kill Sothkel Sald in year 35? — `unattributed [secret]`. The answer must name nobody, and ideally the event should not surface.
- Who conspired against Paernmel Has? — plots are `[secret]` until uncovered. Answers must respect the uncovering year, not the plotting year.

---

## Build order

1. **Query parsing and structured extraction.** Verify extracted queries against hand-written expectations before wiring generation.
2. **Retrieval** — causal traversal and structured filter, against the record. **Stop and report the retrieved event sets for the suite before building generation.** If retrieval returns the wrong events, the answer is wrong however good the prose, and that is far easier to see in a raw event list than in fluent text.
3. **Answer generation** over retrieved events.
4. **Checker on the answer path**, with the `unresolvable` split done first.
5. **Empty-result and presupposition handling.**
6. **Run the suite.**

---

## What I want from you

Steps 1 and 2, then report the retrieved sets before generation.

One thing worth deciding early rather than discovering: **what a query answer looks like when the checker rejects it.** The chronicle's answer was "exclude the passage and say so", which works because a chronicle has fifteen sections and losing one is survivable. A query has one answer. Getting this right is the difference between a tool that occasionally says "I can't answer that reliably" and one that occasionally lies — and that decision shapes the interface, not just the internals.
