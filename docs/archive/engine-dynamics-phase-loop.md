# Phase loop — engine dynamics

Supersedes `raid-mechanic-loop.md`. If that round has already run, its report is input to this one and its findings enter the queue in §7 as already-characterised.

**This is a phase, not a round.** Run unattended until §9's exit conditions hold or §8 forces an escalation. Work the queue, extend the queue with what you find, and stop when the phase is done rather than when the first finding is resolved.

Write a checkpoint report to `out/` after each queue item. Do not halt for them.

---

## 1. Why this exists

The last four rounds each had their own brief, and each brief re-stated the same method in different words. That cadence cost a round-trip per finding, and most of what came back between rounds was derivable from the record and the reports rather than genuinely undecidable.

So: the method is written down once, the decision rules are pre-committed, and the loop only stops for the things that actually need a human.

---

## 2. Scope

**In scope:** the correctness and richness of the simulation's own dynamics — event outcome distributions, mechanic behaviour, causal structure, and the Layer 1 metrics that measure them.

**Out of scope, and do not drift into them:**

- Rendering, the checker, and the query layer, except where §6 requires re-verification after a rules change.
- Cutting a new baseline. That needs generation and is a separate round.
- Anything on the roadmap from Stage 5 onward.
- Splitting retrieval sets, emitting the question set as data — both change artefacts the sealed baseline holds hashes for.

**The phase ends and the next work is Stage 5 or 6.** This is the last engine-dynamics phase before the roadmap resumes. That framing matters: the goal is an engine good enough to build on, not an engine with no known defects.

---

## 3. The standing method

Apply to every queue item, recursively, without re-deriving it:

1. **Characterise** across all five seeds — 7, 42, 99, 1234, 2025. Counts, shares, `n`. Never one seed; a metric holding on 42 alone is an anecdote.
2. **Adjudicate** into exactly one category, with the rationale written **before** the measurement that tests it:
   - **A design choice nobody made deliberately** — the mechanic does what it was built to do, and that is wrong. Change the mechanic.
   - **A defect** — it does not do what it was built to do. Fix it.
   - **Correct, and the metric is wrong** — redefine the metric, with predecessor and reason recorded.
3. **Decide fix-or-park** against §5's bar. Write the decision before starting either.
4. **Fix**, if fixing: constants chosen by reasoning and stated before measuring, never fitted to move a metric. Measure each change separately when two changes move the same population.
5. **Check downstream** per §6.
6. **Record** and take the next item.

A rationale formed after seeing a number is a rationalisation. If that happens, say so in the report and mark the adjudication unsound.

---

## 4. Pre-committed decision rules

These are settled. Apply them; do not escalate them.

**Rates and samples.** Every rate reports its `n` and, where `n` is small enough that achievable values are coarse, its granularity. Assert a percentage bar on the pooled panel, not per seed, wherever per-seed `n` cannot support one. Per-seed assertions fall back to a floor of `> 0` — the mechanic works at all in every world.

**Absence and zero.** A metric asserting zero asserts a **count**, never a percentage — integer division let one lifecycle chain in 156 round to 0% and pass. Every absence-asserting metric needs a positive control: a constructed case the detector must catch. A metric added without one fails the meta-test.

**Reachability.** Every branch of every outcome must be emitted somewhere on the panel. A branch with no emitter is a claim about the world nothing can make true: **give it an emitter or delete it.** This is not subject to the fix budget — it is always in scope and always cheap.

**Derived artefacts name their inputs.** Anything computed from something else records what produced it. Where the computation is deterministic, reproduce rather than store. Fixtures read the sealed baseline's `world-42.jsonl`; they never re-simulate to obtain a world they assert about.

**Instrumentation.** Stays out of the event log — events are what happened in the world, instrumentation is what happened in the engine. Proved harmless by bit-identical reruns with and without it, never asserted to be.

**Accounting.** Any population being tracked balances: examined + skipped-with-reason + resolved equals the total. An unexplained shortfall is an abort, not a row to skip. Same rule as a dangling causal edge.

**Reading.** The record, never the `.log` view. A filter that drops rows fails loudly. Ranking by raw event count under-represents things that ended — use a rate, or a floor for anything destroyed.

**Thresholds.** No threshold *value* changes without escalation. Redefining *what is measured* is in scope with justification. A metric encoding a superseded model is adjudicated before it is satisfied — it will otherwise pull the world back toward the model it encoded.

**`KnownFailing`.** Entries leave only by holding. Every entry carries its category, rationale, and the round that owns it. A quarantine with no diagnosis is a metric quietly switched off.

**The sealed baseline is read-only.** Layer 5 skips on ruleset mismatch with its reason stated — never fails, never passes.

**Ruleset version** increments once per phase, not once per fix. Bump it when the phase's simulation changes are complete.

---

## 5. The fix bar — what stops this going forever

Every finding gets **characterised and adjudicated**. Not every finding gets **fixed**.

The bar is the project's actual goal, not simulation correctness:

> **Does this defect make the world less interesting to read, or less able to support a campaign?**

Fix if yes. If no, park it: diagnosed, categorised, in `KnownFailing` with its reasoning, watched by the harness. Parking is not deferral-by-neglect; it is a recorded decision that the defect does not clear the bar.

Three cases that are **always** fixed regardless of the bar, because they are cheap and unambiguous:

- An unreachable branch — emitter or deletion.
- A metric that cannot vary, or that can report zero without meaning none.
- An accounting identity that does not balance.

**Fix budget: four mechanic changes in this phase.** Not four findings — four changes to how the simulation behaves. Reachability fixes, metric fixes and accounting fixes do not count against it.

When the budget is spent, everything remaining is characterised and parked, and the phase moves to §9. If something above the bar is left unfixed when the budget runs out, that is an escalation under §8 — report it and stop, rather than quietly spending a fifth.

---

## 6. Downstream checks after any rules change

Not optional, and not deferrable to a later round.

- **Re-run Layer 3 in full.** Every corpus case must still fire its rule and none fire on its corrected form.
- **Report the checker's coverage block** for any rule family touching the changed mechanic. A rule going inert is the silent-path signature and it now has somewhere to show.
- **Any mechanic whose distribution changed gains a Layer 1 metric**, with its `n`, its reachability assertion, and a positive control if it asserts absence.
- **Fixtures still read the sealed baseline**, and the baseline still hashes as recorded.

The specific live case: the checker's raid rules were tuned on prose where raids nearly always fail, and two extraction bugs already lived there. A different outcome distribution is a different prose distribution.

---

## 7. The queue

Work in this order. Add to it as findings appear; new items enter at the position their dependencies allow.

**7.1 — Audit every outcome distribution.** Every event kind carrying an outcome, role or mode field, across five seeds: counts, shares, `n`, and branch reachability. Rank by skew. **Invent no skew threshold** — ranking and reporting only; adjudication is per-item.

This is first because two instances of the same shape — coups 100% exposed, raids 80% beaten off — say it is a class rather than two incidents. Expect a third.

**7.2 — Raids.** Two phenomena, possibly separate causes:

- *Outcome skew.* Three-way split — beaten off, through with a haul, through empty. What decides the roll, what feeds it, whether the raider's own strength enters.
- *Repetition.* The same house raided the same four places three times each. Does prior outcome at a target feed target selection? A raider beaten off returning three times either models stubbornness deliberately or has no memory.

Also: how often the zero-haul case occurs and whether it is distinguishable from the event's own fields; whether raid frequency is gated; whether raids cause anything downstream.

**Pre-committed design intent, to save an escalation:** a raid is how houses take resources from each other and how grievance accumulates — not a low-stakes probe that usually fails. Under that reading, 80% failure makes the mechanic decorative: constant, and changing nothing. Repetition without memory is a defect. **If the evidence contradicts this reading, escalate under §8 rather than proceeding on it.**

**7.3 — Seed 7's chain shapes.** 611 events under both rulesets, distinct deep-chain shapes 54 → 44. **First: determine whether it is the same 611.** Byte-identical stream means the cause is in the counter, not the world, and the item is a measurement bug. A different 611 means the world reorganised. One comparison, and it forks everything after it.

**7.4 — Whatever 7.1 surfaces**, adjudicated in rank order against §5's bar.

---

## 8. Escalate — halt and report — only for these

1. **A question about what the world is for** that the docs, the roadmap and §7's pre-commitments do not answer. Semantic intent is the one thing that cannot be derived from the record.
2. **A threshold value would have to change.**
3. **Anything that would modify the sealed baseline**, or a judgement that a new baseline is needed now.
4. **The fix budget is exhausted** and something above the bar is unfixed.
5. **An accounting identity does not balance** and the shortfall cannot be named.
6. **A downstream check fails** and the cause cannot be named.

Everything else: decide it, record the reasoning, continue. In particular, do not halt to confirm an adjudication, to ask whether a constant is reasonable, or to check whether a finding is worth fixing — §3, §4 and §5 answer all three.

---

## 9. Phase exit

The phase is complete when **every item in the queue, and every item added to it, is either fixed-and-verified or diagnosed-and-parked**, and:

- Every outcome distribution on the panel is known and recorded.
- Every unreachable branch has an emitter or is deleted.
- Every Layer 1 metric holds, or carries its category, rationale and owning round in `KnownFailing`.
- Every absence-asserting metric has a positive control.
- Layer 3 is green; no checker rule has gone inert.
- The ruleset version is bumped once, the header carries it, Layer 5 skips with a reason naming it, and the sealed baseline is untouched with `.sealed` verifying.
- The full suite is green apart from parked entries.

**Then write the phase report** covering: what was found, what was fixed and why, what was parked and why, every adjudication with its category, the before/after distributions, and — separately — **a stated judgement on whether the engine is now good enough to build Stage 5 and 6 on**, with the evidence for it.

That last item is the real deliverable. Everything above it is how you earn the right to write it.

---

## 10. Prohibitions

1. No threshold value changes.
2. No rationale after its measurement.
3. Nothing leaves `KnownFailing` by hand.
4. The sealed baseline is read-only.
5. No coup constants change — they were reasoned once and measured once; moving them to fix something else fits two things to each other with no independent check on either.
6. No skew threshold invented in 7.1.
7. No drift into rendering, query, or roadmap stages beyond §6's required re-verification.
8. No fifth mechanic change. Escalate instead.
