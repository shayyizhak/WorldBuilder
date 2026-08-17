# Loop-prompt — dynamics metrics under ruleset 2

Follows `coup-resolution-fix-loop.md` and its report. Three Layer 1 metrics are failing and the coup threshold is being asked a question its samples cannot answer. This round adjudicates all four.

Halt and report after each step.

---

## 0. The discipline for this round

This round redefines metrics. **Redefining a metric to make it pass is indistinguishable from lowering a threshold**, unless something separates the two. That something is order of operations:

**State the redefinition and its rationale in writing, then measure.** Never measure first and justify after. A rationale written after seeing the number is a rationalisation, and this project has already learned that constants chosen by fitting are worthless.

A metric may be redefined **only** if it can be shown to encode a model the engine no longer implements. "The number moved and the new number is defensible" is not sufficient. The question is always: *what was this metric for, and does the current definition still measure that thing?*

Every redefinition is recorded with its predecessor, the date, the ruleset version, and the reason. A metric with no history of why it says what it says is the same defect as a findings sidecar with no record of what produced it — fifth venue this project has met it, and the sixth is not far off.

---

## 1. Step 1 — the coup threshold is a sample-size problem, not a bar problem

Panel sizes are 14 to 36 plots. On seed 7, `n = 14`: two seizures is 14.3%, three is 21.4%. **There is no achievable value between them.** A `> 15%` bar on that seed is a "≥ 3 of 14" bar wearing a percentage. Seed 2025 landing on exactly 15.0% is the same artefact — 3 of 20, granularity 5 points per plot.

Pooled across the panel: **29 of 124 = 23%**, comfortably clear.

**Decision: keep the threshold, change where it is asserted.**

- `CoupSuccessPctOfPlotted > 15%` asserted **on the pooled panel**, not per seed.
- **Per-seed floor: `> 0`** — the covert-path invariant already in place, which now holds everywhere.

This preserves the original intent — covert seizure is a real route to power in every world — without pretending fourteen samples support a percentage.

**Report `n` alongside every rate metric, per seed.** A rate without its denominator hides exactly this. Where `n` is small enough that the achievable values are coarse, say so in the output rather than leaving it to be rediscovered.

**Halt when:** the pooled assertion and the per-seed floor both run; every rate metric reports its `n`; no threshold value has changed.

---

## 2. Step 2 — positive controls for metrics that assert zero

Step 4 of the last round found that `single-actor causal chains == 0` passes identically whether the engine no longer produces them or could not produce one if it tried. A survey cannot distinguish those. A **positive control** can.

Layer 2 already solved this one layer up: synthetic fixtures written to contain the construction, asserting the rule fires. Do the same here — a hand-built log containing a known single-actor chain, asserting the detector catches it.

Apply to every Layer 1 metric asserting zero or asserting absence. Each needs a constructed counter-example that the detector must catch.

**Halt when:** every zero-asserting metric has a positive control; deliberately breaking a detector fails its control; the controls are in the standard suite.

---

## 3. Step 3 — adjudicate the three failing metrics, one at a time

For each: write the adjudication **first**, then measure. Each falls into exactly one of three categories.

- **Encodes a superseded model** — redefine, with rationale and provenance.
- **Measures something real that got worse** — fix the engine, not the metric.
- **Measures the right thing badly** — fix the measurement.

### 3a. `plots terminated ≥ 85%` (seed 42, now 80%)

Written when a plot was voided by its target's death. Under seat-attachment a plot pending at run end is an **unfinished conspiracy**, which is a horizon effect rather than a defect — the run stops at Y51 and plots opened near the end have not had time to conclude.

The likely shape of a principled redefinition: exclude plots opened within the observed plot lifespan of the horizon, so the metric measures plots that *had time* to terminate. Reason it through and state it; do not adopt it because it is suggested here.

Note this is the newest and least battle-tested metric on the panel — it was added during the coup work.

### 3b. `verbatim repeat rate < 10%` (seed 1234, now 12%)

Its purpose was the fizzle signature — a world producing the same event line over and over. More conspiracies reaching conclusions means more events of similar shape, which may be legitimate richness rather than monotony.

**Check the normalisation before concluding anything.** The metric is digit-normalised. *"uncovered after 4 years"* and *"uncovered after 2 years"* normalise to the same string, so genuinely distinct events may be counted as repeats. If so this is category three — the right thing measured badly — and the fix is the normaliser, not the threshold.

### 3c. `distinct deep-chain shapes ≥ 60` (seed 7: 44, seed 99: 52)

Treat this as category two until proven otherwise: a real loss of causal variety.

But rule out the mechanical explanation first. **Seed 42 went 1035 events to 940.** Fewer events means fewer chains means fewer distinct shapes, which would make this partly an artefact of a smaller log rather than a poorer one. Report shapes per thousand events alongside the raw count. If the rate holds and the total falls, the finding is about log volume; if the rate falls too, variety genuinely dropped and the cause is worth finding — plausibly that covert wins now funnel through one common path (`SettleCoup`) where several outcomes previously existed.

**Halt when:** each of the three has a written adjudication naming its category, dated, with the rationale recorded before its measurement; any redefinition carries its predecessor and reason; any category-two finding has a named cause or a stated next step.

---

## 4. Step 4 — the standing trap, recorded

Write this into the Layer 1 documentation, because it is the general form of this round:

> **A metric that encodes a superseded model will pull the world back toward the model it encoded.** Every stage from here changes rules. A dynamics bar written against the old rules is not neutral — it is a constraint on the new ones, and satisfying it may mean undoing a deliberate improvement.
>
> Therefore: on every ruleset change, each failing metric is adjudicated before it is satisfied. The question is not "how do we make this pass" but "does this still measure what it was for".

**Halt when:** the note is in the Layer 1 docs and referenced from `KnownFailing`.

---

## 5. Prohibitions

1. **No threshold value changes.** Redefinition of *what is measured* is in scope and must be justified; changing a bar to fit a number is not.
2. **No rationale written after its measurement.** If a rationale was formed after seeing a number, say so in the report and treat the adjudication as unsound.
3. **Nothing leaves `KnownFailing` by hand.** Only by holding.
4. **The sealed baseline is read-only.** It remains a ruleset-1 artefact; Layer 5 continues to skip with its stated reason.
5. **No coup constants change.** They were chosen by reasoning last round and measured once. Changing them now to move a dynamics metric would fit two things to each other with no independent check on either.

---

## 6. Exit criterion

**Every Layer 1 metric either holds, or carries a written adjudication naming its category, its rationale, and the round that owns it.**

`KnownFailing` may be non-empty at the end of this round. What it may not contain is an entry with no explanation — a quarantine with no diagnosis is a metric quietly switched off.

## 7. Abort conditions

- Any threshold value would need to change for a step to pass.
- A redefinition cannot be justified without reference to whether it passes.
- A positive control cannot be constructed for a zero-asserting metric — report it as a stated limit rather than leaving the metric looking verified.
- The sealed baseline is modified, or Layer 5 stops skipping.
