# Brief — closing ruleset 6

**The decision, recorded.** `war − null` is accepted on the surviving pair and **ruleset 6 stands with all three rules.**

Why that is not a pre-registration violation, stated once so it can be judged later:

- The random arm existed to stop a war effect being attributed to war when it was really *ties came down*. **There is no effect to attribute.** `war − null` is +0.27 y, the whole interval inside ±1 y against a 5-year MDE, point estimate positive. A confound-remover for a positive result is moot against a precise null.
- Dropping a contrast from a registered family **loosens** multiplicity correction. That only helps a significance claim. The claim here is a null at p = 0.329 **uncorrected**, so the drop can only make it harder to sustain. Evaluating the surviving pair is conservative in the direction that matters.
- Refusing to amend the matching rule after seeing the data was correct and cost nothing.

Record this reasoning in the report, not just the verdict. A future reader needs to see the argument to judge whether the call was good.

---

## 1. Cut `baselines/ruleset-6/`

Five reference seeds, both halves — the archive split (`https://trello.com/c/Kl5i0hQN`) is still owed, so a set still requires chronicle, findings and `renders.json` through ollama.

Confirm the top line reports `N of M layers ran` and does not hide a skip. Report the holdout rate beside ruleset 5's, as at the last cut.

**Do §4 first if you intend to change the reference seeds.** Cutting a set and then swapping a seed pays the inference twice.

---

## 2. Dispose of the twelve obsolete tests

**`AdditiveRecordTests` (10) — retire, with the verdict recorded elsewhere.** They assert ruleset 5 is additive over sealed ruleset-4 worlds. The property was true, was proven, and cannot be re-proven by an engine that no longer contains ruleset 5. Retire with an explicit pointer to the step-one report and `Provenance.cs`.

**Standing rule for §4 of the project reference:** *a one-time verdict about a ruleset transition is a record, not a test.* Tests assert standing properties; transition properties belong in the provenance chain, where they survive the transition that made them unprovable.

**`ProximityControlTests.TheFlatControlReproducesRulesetThreeExactly` (2) — rebase to ruleset 6, do not retire.** Pinning it to ruleset 3 was incidental. What it asserts is that geography is cleanly separable — turn it off, and the engine reproduces the same-ruleset flat world exactly. That is a standing property.

**Make the off-switch property standard.** `TurningTheTerminationRulesOffGivesBackTheOldRuleset` and the rebased flat control are the same construction, and it is stronger than instrumentation invariance: it proves a mechanics change touched nothing outside its own rules. **Every mechanic ships with an off-switch that reproduces the prior ruleset exactly.** Add to §4.

---

## 3. The two invariant changes — explicit acts, each with its reasoning

Both are re-baselinings. Record what changed, why, and against which seal.

### 3.1 `distinct deep-chain shapes` — per-seed floor, not a panel-wide count

Measured: r = 0.871 against event count over 90 worlds. A `≥ 60` count bar on worlds varying two-fold in length is partly a length bar.

**Do not convert it to a rate.** That requires picking a rate, and there is no principled argument for what rate makes a world interesting — it would be a constant chosen by fitting.

**Use a per-seed floor.** The invariant's job is regression detection, not absolute quality, and per-seed floors are the construction the checker already uses. Floors are re-baselined only by explicit human action, never by rerunning.

Seed 7 has the lowest rate on the panel (85.6) and always has; its failure must survive this change. If a per-seed floor makes seed 7 pass, the floor was cut from the wrong run — say so rather than accepting it.

### 3.2 `BothOutcomesOfTheRollAreReached` — split, do not relax

The invariant exists because the covert-coup **success** path was once structurally zero. Relaxing both branches together drops the protection that discovery bought.

- **`seized` stays per-seed.** Fires on all five (2, 8, 8, 7, 2). That is the branch the invariant is for.
- **`exposed` goes panel-level**, with the seeds recorded: 11, 12, 0, 12, 10. Per-seed was over-strict for this branch; panel-level is honest because it fires on four of five.

**Record that seed 99's `exposed` is removed by the war arm alone** (5 → 0, collapse and disuse leave it untouched). That is a real narrowing of one world, accepted with the null in hand, not an artefact being papered over.

---

## 4. Reconsider the reference seeds before hand verification

Do this **before** §1 if you intend to act on it.

The five reference seeds gave `war − null` = −6.6 y, `sd = 10.85`. Ninety fresh seeds gave +0.27, `sd = 2.58` — a quarter the dispersion. The reference five were selected and kept because they made good reference worlds, and selecting for variety inflates variance. They also share a board, which is how five re-fold sites reading the repository's stored board instead of the world's own stayed invisible.

**Seed 99 at ruleset 6 is a tail world:** hegemony at Y21, 531 events, one house standing, no coup ever uncovered.

You are about to spend hand-verification hours. **Swapping a seed is free now and costs a full re-verification later.**

State the selection criteria *before* looking at candidate worlds, and keep them plain — the panel exists to avoid tails, not to pick pretty histories. Suggested and open to argument: no runaway before Y40; at least two houses standing at the end; both coup branches present; length within some stated band of the panel median. **Whatever the criteria, write them down first**, because choosing a reference world after reading its history is how a panel gets selected for interestingness again.

If seeds change, the sealed v1 record is untouched — Layer 3 depends on it permanently and it is not a reference seed in this sense.

---

## 5. Not in this brief

- **`GoalBook` outside the fold** (`https://trello.com/c/46Yz9Gb7`) — a foundation defect, ahead of the rest of the sweep list
- The remaining §6 sweep items: two dead relation kinds, three no-removal-path kinds, ore, two collapse-path gaps
- The archive contract split and the two verifiers that pass on nothing (`https://trello.com/c/Kl5i0hQN`)
- Absent-vs-unknown as a type (`https://trello.com/c/QiADoVAB`)
- The `disuse` constant, flagged untested — fires once in five worlds. Do not tune it on this evidence.
- **The brake problem.** Runaway hegemony before Y40 fails on 2 of 5 reference seeds *at ruleset 5* — it predates this phase and is not caused by it. It is a world-design problem and a candidate for the first genuinely new mechanism since Stage 2.

---

## 6. Halt conditions

- A per-seed shape floor that makes seed 7 pass
- `seized` failing to fire on any reference seed after a seed swap
- The ruleset-6 holdout rate unlike ruleset 5's
- Suite not green after §1 and §2, for any reason other than the two changes in §3
- Reference-seed criteria not written before candidate worlds were examined

## 7. Report

The accepted-verdict reasoning as written above. Baseline cut confirmation per seed with the holdout rate beside ruleset 5's. Which tests were retired, which rebased, and where the retired verdict now lives. Both invariant changes with their before/after and the seal they were cut against. If seeds changed: the criteria, written first, and what each new seed was chosen against.

---

## A note on the seventh instance

Five re-fold sites reading the repository's stored board rather than the world's own — invisible on the reference seeds because they share a board, fatal on any panel where they do not.

That is now seven of the same shape this phase: Layer 4 over field names the engine never wrote; `all layers passed` over a skipped layer; FLOOR unfalsifiable for five rules; `rule-inert` firing by construction; `quantity` at 0/60 read as a dead path; `no runaway before Y40` passing on ties to dead houses; and this.

**A green measuring something other than what it claimed, exposed only when the population stopped being degenerate in the relevant dimension.** The fix at `WorldView.Board` is the right shape — five call sites can drift apart, one source cannot. The general form is worth adding to §4: *when a property holds on the panel by coincidence of the panel's construction, no test on that panel can detect it.* That is the argument for measurement panels being built differently from reference panels, stated one level more generally than the existing rule.
