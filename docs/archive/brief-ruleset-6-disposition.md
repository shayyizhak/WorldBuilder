# Brief — ruleset 6 disposition

**One decision, one cut.** Nothing is retired, re-baselined or sealed until §1 returns. The five red baseline tests carry an accurate message; that is what it is for.

**Decided already, and not reopened here:**

- The collapse cleanup stands. Ends 3–7 ties per world, moves no outcome measure on any seed. It repairs the egregious half of §0 — ties held by houses that no longer exist — at zero cost to the histories.
- The disuse timeout stays in, **flagged untested**: it fired once across five worlds and reproduces the baseline exactly on two seeds. A rule that rare is neither validated nor refuted by this panel. Do not tune the constant on this evidence; revisit when a world runs long enough to exercise it.
- Ruleset 6 is not reverted wholesale. The question is the war rule alone.

---

## 1. Size the war rule

**The question.** Is the war rule systematically damaging histories, or is it high-variance and this panel caught the tail? Two of five seeds lost a quarter of their history and shifted hegemony by 8–25 years; three were untouched in chains, shapes and runaway year. n=5 cannot tell those apart.

Every metric here is log-only. **No inference cost.** This is cheap in a way the render baselines are not.

### Arms, paired on the same seeds and boards

| arm | rules active |
|---|---|
| null | none (reproduces ruleset 5) |
| collapse | collapse only |
| war | war + collapse |
| **random** | collapse, plus N trade ties removed at random, where N and the years match the war arm's per-world removals |

**The random arm is the discriminating one.** If random tie-removal does the same damage, the war rule is not the problem — the world is knife-edge sensitive to losing trade ties at all, which is a different finding with a different fix. Without it, any war-versus-null effect is confounded with "ties came down."

Match N and timing per world, not on average. A random arm removing a different number of ties measures a different treatment.

### Primary metric: runaway year, continuous

Year at which concentration first reaches 70%, **not** the Y40 pass/fail. The threshold arm is already degenerate — the null arm fails it on 2 of 5 seeds at ruleset 5, so a fail-rate comparison starts from a contaminated base. The continuous measure carries the information the threshold discards.

**Right-censoring must be pre-specified.** Seed 2025 never reaches 70%. Fix the rule before running — either a survival-style comparison treating it as `>51`, or exclusion with the exclusion count reported. **Do not decide this after seeing the data.**

Secondary: distinct deep-chain shapes; event count. Reported, not adjudicated.

### Pre-registration

- **Fix the contrast family before running and declare it closed:** `war − null`, `war − random`, `collapse − null`. Three. Holm. A family enlarged after verdicts are reported moves thresholds under published results.
- **State the MDE in years of runaway shift**, argued from what would matter to a reader, not from the observed 8–25.
- **Size N from the paired variance of the existing five seeds, and fence that use in writing to sizing only.** Seen data sizes the next experiment; it does not decide the last one.
- **Dry-run every arm of every decision rule against the ruleset-5 baselines before measuring.** Each arm must be shown reachable. This has caught two unreachable arms already — a criterion the population had never met, and a statistic that could not vary.
- **Degeneracy guard:** state the minimum panel spread in runaway year below which the comparison is void, and what the rule falls back to.

### Pre-committed disposition

| §1 returns | then |
|---|---|
| `war − null` real and large, `war − random` also real | war rule is the cause. **Ship collapse + disuse. Card the war rule** with the finding. |
| `war − null` real, `war ≈ random` | the world is knife-edge on trade ties. **Ship collapse + disuse.** The brake problem becomes the next work, and it is a world-design problem rather than a rule defect. |
| `war − null` not distinguishable | the panel caught a tail. **Ship all three.** |

Write which cell fired, with the figure that put it there.

---

## 2. Disposition, once §1 returns

### 2.1 Cut the surviving ruleset's baselines

Five seeds, both halves — the archive split (`https://trello.com/c/Kl5i0hQN`) is still owed, so a set still requires chronicle, findings and `renders.json` through ollama. Confirm the top line reports `N of M layers ran` and does not hide a skip.

Report the holdout rate beside ruleset 5's, as at the last cut.

### 2.2 Retire the twelve obsolete tests

**`AdditiveRecordTests` (10) — retire, and record the verdict elsewhere.** They assert ruleset 5 is additive over sealed ruleset-4 worlds. That property was true, was proven, and cannot be re-proven by an engine that no longer contains ruleset 5.

**Standing rule worth adding to §4:** *a one-time verdict about a ruleset transition is a record, not a test.* Tests assert standing properties. Transition properties belong in the provenance chain, where they survive the transition that made them unprovable. Retire these with an explicit pointer to the step-one report and `Provenance.cs`.

**`ProximityControlTests.TheFlatControlReproducesRulesetThreeExactly` (2) — rebase, do not retire.** Pinning it to ruleset 3 was incidental; what it actually asserts is that geography is cleanly separable — turn it off and the engine reproduces the same-ruleset flat world exactly. That is a standing property and worth keeping. Rebase to the surviving ruleset.

Note the shape: this is the same off-switch property as `TurningTheTerminationRulesOffGivesBackTheOldRuleset`. **Make it standard — every mechanic ships with an off-switch that reproduces the prior ruleset exactly.** It is a stronger guarantee than instrumentation invariance, because it proves a mechanics change touched nothing outside its own rules.

### 2.3 The two real failures — classify, do not re-baseline in this brief

Re-baselining is an explicit human act and gets its own decision with §1's figures in hand.

**Seed 99 `distinct deep-chain shapes` 69 → 45.** Before proposing any bar change, report shapes against history length across the panel at both rulesets. Seed 99 lost 25% of its events and 35% of its shapes, so the metric may scale with length — and seed 7 has sat at 45 while passing nothing since before this phase. **Do not move the bar in this brief.** Establishing whether it is a count or a rate is a separate measurement.

**Seed 99 `PlotLedgerTests.BothOutcomesOfTheRollAreReached`.** This invariant exists because a covert-coup branch was once structurally zero, so weakening it is the highest-risk change available here.

**Pre-committed:** do **not** relax it to a panel-level assertion unless the branch is shown to fire on at least one other seed at the surviving ruleset. If it fires nowhere, that is a structural zero again and a defect, not a bar to move. If it fires elsewhere, panel-level is honest and per-seed was always over-strict — say which, with the seeds.

---

## 3. Not decided here

- **`GoalBook` outside the fold** (`https://trello.com/c/46Yz9Gb7`) — ahead of the rest of the §6 list. A foundation defect, not a world-content one.
- The remaining §6 sweep items: two dead relation kinds, three no-removal-path kinds, ore, two collapse-path gaps.
- The archive contract split and the two verifiers that pass on nothing (`https://trello.com/c/Kl5i0hQN`).
- Absent-vs-unknown as a type (`https://trello.com/c/QiADoVAB`).
- The reference set, still deferred; candidate questions retained.

---

## 4. Halt conditions

- Any §1 decision-rule arm found unreachable in the dry run
- The runaway-year spread across the null panel falling below the stated degeneracy minimum
- The random arm unable to match the war arm's per-world removal count and timing
- `BothOutcomesOfTheRollAreReached` firing on no seed at the surviving ruleset
- Suite not green after 2.1 and 2.2, for any reason other than the two classified in 2.3

## 5. Report

Which disposition cell fired and the figure that fired it. The three contrasts with CIs and the pre-registered MDE quoted. The censoring rule, stated before the data. Shapes against history length, both rulesets. Whether the coup branch fires anywhere. Baseline cut confirmation with holdout rate beside ruleset 5's. Which tests were retired, which rebased, and where the retired verdict now lives.
