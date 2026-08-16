# Loop-prompt — outcome distributions, and the raid mechanic

Follows `dynamics-metrics-loop.md` and its report. The verbatim-repeat metric was working and pointed at a mechanic nobody was looking at: `CONFLICT.RAID` failing in 25 of 31, with the same house raiding the same four places three times each.

Diagnose first, adjudicate second, fix third. Halt and report after each step.

---

## 0. Why this round is wider than raids

The coup defect and the raid defect are the same shape: **an outcome distribution so skewed that one branch is effectively decorative.** Coups were 100% exposed because no other branch existed. Raids are 80% beaten off, which is not structural but is close enough in effect that the log fills with near-identical lines.

This project already has that lesson written down, filed under rendering — *skewed outcome distributions are a latent fabrication risk*, because a model scores well guessing the majority case and gets the rare case confidently wrong. Two instances now say it is also a **simulation** lesson. So this round audits every event kind before touching the one that surfaced.

Find the third instance before the harness does.

---

## 1. Step 1 — audit every outcome distribution

**Across all five seeds: 7, 42, 99, 1234, 2025.**

For every event kind carrying an outcome, role or mode field, report the distribution — counts and shares, with `n`. Rank by skew. Include the kinds that look fine; a distribution that is healthy is worth recording as such, and the audit's value is the whole picture rather than the outliers.

Alongside each, report **reachability**: is every branch emitted at all on the panel? A branch with zero emissions on five seeds is either unreachable or so rare it cannot be distinguished from unreachable, and both need saying. This is the Step 2 lesson from the last round generalised — the `abandoned` case that had no emitter, and `LifecycleChainPct` where integer division let one occurrence round to zero.

**Do not set a skew threshold.** There is no principled bar yet, and inventing one to sort the output would be a threshold chosen by fitting. Rank and report; adjudication is Step 3's job.

**Halt when:** every outcome-bearing kind is reported with counts, shares and `n` across five seeds; branch reachability is stated per kind; the report ranks by skew without asserting a bar.

---

## 2. Step 2 — characterise raids specifically

Two distinct phenomena were observed and they may have different causes.

**The outcome skew.** Raids resolve three ways — beaten off, got through with a haul, got through empty. Report the three-way split per seed. Establish what decides the roll: what quantities feed it, what their observed ranges are, and whether the raider's own strength enters at all.

**The repetition.** The same house raided the same four places three times each. Establish how a target is chosen, and specifically **whether prior outcomes at that target feed the choice**. A raider who is beaten off and returns the next year to the same place, three times, is either modelling stubbornness deliberately or has no memory. Find out which.

Also report:

- The **zero-haul** case. "Got through empty" produced corpus row 16 — a render describing plunder when the haul was zero. How often does it occur, and is it distinguishable in the event's own fields without reading prose?
- Whether raid frequency is gated by anything, or whether a house raids whenever it can.
- Whether raids **cause** anything downstream. This matters for the chain-shape question below: 80% failure may mean most raids are causal dead ends.

**Halt when:** the three-way split, the decision inputs, the target-selection rule and the memory question are all reported per seed.

---

## 3. Step 2b — one cheap measurement, while the tooling is out

Seed 7 produces **exactly 611 events under both ruleset 1 and ruleset 2**, while its distinct deep-chain shapes fell 54 → 44.

**Determine whether it is the same 611.** If the event stream is byte-identical, the shape count is a pure measurement difference and the cause is in the counter, not the world. If it is a different 611, the world reorganised. That forks the investigation completely and costs one comparison.

Report the answer. Do not chase the cause in this round — it belongs to whichever round the answer points at.

---

## 4. Step 3 — adjudicate

Rationale written **before** measurement, per the standing discipline. Each finding falls into exactly one category:

- **A design choice nobody made deliberately** — the mechanic does what it was built to do, and what it was built to do is wrong. Change the mechanic.
- **A defect** — the mechanic does not do what it was built to do. Fix it.
- **Correct, and the metric is wrong** — as `plots terminated` turned out to be.

Adjudicate at minimum: the 80% failure rate, the repeat targeting, and anything Step 1 surfaced that ranks alongside them.

**The question to hold onto for the failure rate:** what is a raid *for*, in this world? If it is a low-stakes probe that usually fails, 80% may be right and the defect is only the repetition. If it is meant to be a way houses take resources from each other, 80% makes it decorative — a thing that happens constantly and changes nothing. The answer decides whether Step 4 touches the odds, the targeting, or both.

**Halt when:** each finding carries a written category, a rationale dated before its measurement, and a stated next step.

---

## 5. Step 4 — fix, with constants by reasoning

Whatever Step 3 decides:

- **Constants chosen by reasoning and stated before measuring**, as with the coup readiness terms. Not fitted to move the verbatim-repeat metric.
- **Measure each change separately** if more than one lands. Odds and targeting move the same population; a combined measurement cannot attribute the effect.
- **If targeting gains memory**, a raider avoiding a place that beat it off should have somewhere else to go. Check that target scarcity does not simply move the repetition to a different place, or stop raids happening at all.

**Halt when:** the changes are in, each measured separately, with the reason distribution and the three-way split reported per seed.

---

## 6. Step 5 — the downstream consequences, which are not optional

**The checker's raid rules were tuned on a corpus where raids nearly always fail.** Two extraction bugs already lived there — the phrase reader running four words past a name, and raids indexed by place so a sentence naming the raided *power* found nothing. Corpus rows 5, 16 and 21 are raid cases. A different outcome distribution is a different prose distribution, and rules that behaved well on 80%-failure prose may not on a mixed one.

- **Re-run Layer 3** in full. Every raid case must still fire its rule.
- **Re-run the checker's raid claims** against a ruleset-3 world and report the coverage block. A raid rule going quiet is the silent-path signature, and it now has somewhere to show.
- **Add raid outcome distribution as a Layer 1 metric** — with its `n`, its reachability assertion, and a positive control if it asserts any absence. It earned a place: it was invisible until another metric pointed at it sideways.

**Halt when:** Layer 3 is green; the raid coverage block is reported and no raid rule has gone inert; the new metric is in Layer 1 with its control.

---

## 7. Step 6 — ruleset and baseline

If simulation rules change, **`ruleset_version` becomes `"3"`.**

- The sealed baseline stays sealed. It is a ruleset-1 artefact.
- **Layer 5 continues to skip**, with its stated reason, now naming ruleset 3.
- A ruleset-3 baseline still requires a render round and is still out of scope. When it is cut, it is **`stability-anchor-only`**, not hand-verified — a golden diff needs its anchor stable, not correct, and that distinction is already written down for the other four seeds.

**Halt when:** the header carries the new ruleset version; Layer 5 skips with a reason naming it; the sealed baseline is unchanged and `.sealed` verifies.

---

## 8. Prohibitions

1. **No threshold value changes.** Redefinition of what is measured is in scope with justification; changing a bar to fit a number is not.
2. **No rationale written after its measurement.** If one was, say so and treat the adjudication as unsound.
3. **No coup constants change.** They were reasoned once and measured once; moving them to fix raids fits two things to each other with no independent check on either.
4. **Nothing leaves `KnownFailing` by hand.**
5. **The sealed baseline is read-only.**
6. **No skew threshold invented in Step 1.** Rank and report.

---

## 9. Exit criterion

**The outcome distribution of every event kind is known and recorded**, and every kind whose distribution is skewed enough to matter carries an adjudication naming its category.

The raid fix itself succeeds on the same terms as the coup fix: the distribution varies rather than being effectively constant, and the difference is visible in the numbers rather than argued. `verbatim repeat rate` holding again is evidence but not the criterion — the metric pointing here was a side effect of the real problem, and a fix that satisfies the metric without changing what raids mean has fixed the wrong thing.

## 10. Abort conditions

- A threshold value would need to change for a step to pass.
- A rationale cannot be written without reference to whether it passes.
- A raid rule goes inert after the change and the cause cannot be named.
- The sealed baseline is modified, or Layer 5 stops skipping.
