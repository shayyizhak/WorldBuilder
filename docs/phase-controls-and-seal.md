# Phase: controls, clamp, seal

**Loop-prompt.** Run unattended until every halt condition holds or an abort triggers. Decision rules are pre-committed below. Where a rule needs a comparator the previous brief did not define, it is stated here **before** the measurement exists — do not amend it after seeing numbers.

**Entry state:** ruleset 4, 493 tests green, no threshold moved. Step 3 of the previous phase complete. Steps 1, 2 and 4 outstanding. No ruleset-4 baselines cut, nothing sealed.

**Escalation resolved:** seed 99 is recorded as **failed**, not mixed. The rank arm of that rule was void rather than met — every seed's separation spread lay in a 34–37% band, and a rank criterion over a degenerate population carries no information. The quantitative arm failed outright and independently: seed 99's discriminating share was above the panel median where the rule required below half of it. No second explanation is to be sought for seed 99 in this phase.

---

## What this phase is actually for

The previous phase produced a result that is more interesting than the one it went looking for: **680 decisions consulted a proximity, 555 had room to be moved by one, and 34 were.** Causal variety rose by up to +33 and the repeat rate cleared everywhere, on the back of thirty-four flips across five fifty-year histories.

That is consistent with two mechanisms with very different consequences:

- **§6 as stated** — distance makes which neighbour you fight a *stable fact*, and stable facts let chains grow long. Note what this requires: **stable heterogeneity**. Nothing in it requires a board.
- **Perturbation** — 34 flips are early divergences that cascade, and any perturbation of similar size would do the same.

Both predict a rise in causal variety. Both predict the repeat rate clearing. **Direction matching does not separate them**, so last phase's confirmation is not evidence between them. Steps 1 and 2 below separate them. Run the falsifier first.

---

## Hard budget

1. **No threshold in `SimConfig` moves.** Including one the alliance clamp finding appears to justify. Surface, do not spend.
2. **Ruleset stays at 4.** The controls are experimental configuration, not rules.
3. **No new mechanics, no fifth consumer of distance, no new checker rules.**
4. **Control outputs are quarantined.** Worlds produced under a control config are diagnostic artefacts. They are never sealed, never archived as baselines, and never enter a render cache. If a control world can be mistaken for a real one on disk, that is a defect in this phase — mark them in the world-file header.

---

## Step 0 — Promote two findings into standing rules

Both came out of the probe and both are larger than the phase that produced them. Do this first; it is small and everything after benefits.

**Instrumentation invariance.** Attaching a measurement must not change the world, asserted by hashing the full event log with and without the instrumentation across all five seeds. This is now a standing property, not probe scaffolding — every future probe adopts it.

**RNG draw order is load-bearing.** The conquest site short-circuits, so the die is thrown before the holder check, and the obvious restructuring silently stops drawing where it used to. Every test stayed green. Consequences:

- Make the with/without log-hash check a standing test across the whole mechanic set, not just instrumented sites. It is the only thing that detects this class.
- Record it as a **Stage 3 determinism constraint**: a pure refactor at a short-circuiting site can change worlds. Reproducibility is not a property of the rules alone; it is a property of the rules *and the order in which they consume the stream*.

---

## Step 1 — Redraw control (the falsifier)

**Run this first.** It is the only step that can invalidate the phase's headline claim, and everything downstream is cheaper if it fires.

**Construction.** Replace the proximity input at all four sites with a synthetic value drawn per decision from the **empirical distribution of realised proximity** for that world — same distribution, same clamp exposure, no stability, no spatial structure.

**Critical:** draw the synthetic value from an **independent RNG stream**, crystallised from `(world_seed, "control-redraw", site, decision_id)`. The main stream's consumption sequence must be byte-identical to the geography run. Otherwise the measured difference is confounded with RNG re-sequencing, which step 0 has just established changes worlds on its own. Assert this: main-stream draw count and order identical to the ruleset-4 run.

**Measure.** Per-seed causal variety and verbatim repeat rate, against the sealed ruleset-3 baselines, exactly as the geography run was measured. Three arms now exist: ruleset-3 (no distance), ruleset-4 (geography), redraw.

**Pre-committed rule.**

- **Redraw reproduces the gain** — delta sign matches geography on ≥4 of 5 seeds **and** panel median delta is ≥ half geography's. → The §6 explanation is **wrong despite having matched direction**. The gain is noise injection. **Abort the phase.** Do not proceed to step 2, do not cut. This is a finding about the last two phases, not this one.
- **Redraw does not reproduce** — fails either arm. → Stability matters. Proceed to step 2.

**Degeneracy guard** (the amendment owed from last phase): if the three arms' panel median deltas all fall within a band narrower than the between-seed spread *within* any single arm, the comparison cannot discriminate. The rule is void, not passed. **Halt and escalate.**

Report deltas and sign patterns. Do not run significance tests on n=5; per the standing rules, rates are pooled where per-seed n is too small and counts are reported rather than percentages where the denominator is small.

---

## Step 2 — Stable shuffle control

Only if step 1 cleared.

**Construction.** As step 1, but one draw per unordered place-pair, fixed at worldgen and stable for the run. Same distribution, same clamp exposure, **stable heterogeneity with no spatial structure**. Independent stream, same invariance assertion.

**Pre-committed rule.** Same two arms as step 1.

- **Shuffle reproduces the gain** → §6 is confirmed **as stated**, and geography is *one implementation* of stable heterogeneity rather than the source of the gain. Record it in exactly those terms — the mechanism is real and the attribution to geometry is not. This lowers the expected sensitivity in step 4 but does not decide it; run step 4 regardless.
- **Shuffle does not reproduce** → the gain is specific to spatial structure, which is a **stronger** result than §6 as stated. Record the strengthening explicitly; it is easy to file a passed control as "as expected" and lose the fact that the claim got bigger.

Same degeneracy guard.

---

## Step 3 — Alliance clamp, by inspection not sampling

Alliance moved 0 of 13. **Do not attempt to resolve this statistically** — it will not resolve at any seed count worth spending. The hypothesis is mechanical, so measure the mechanism.

**Measure.** Per alliance evaluation, determine whether varying the distance term across its full realised range could change the post-clamp value at all. That is a binary property per evaluation. Report the absorbed fraction.

**Pre-committed rule.** To explain 0 of 13, essentially every evaluation must be absorbed — so:

- **Absorbed in at least 12 of 13** → clamp hypothesis holds. Alliance's distance term is decorative under current calibration. **Record; do not fix.** Whether alliance recalibration is in scope is Shay's call, not this phase's, and the budget forbids the threshold move anyway.
- **Absorbed in fewer than 12** → hypothesis falsified. 0 of 13 is unexplained. **Halt and escalate.** Do not search for a replacement explanation in the same run.

**Also resolve the arithmetic.** The report gives one-in-eight for P(0 of 13) by chance; at a 6% flip rate P(0 of 13) is about 0.45, and one in eight implies roughly 15%. State which population the baseline was drawn from. The panel-wide 6% is probably the wrong denominator — the right one is alliance's own movable-decision rate, which may differ substantially. Correct the figure in the record either way. Engine figures are worse than model figures when wrong, because nothing questions them.

**Note the family.** This is the third appearance of *correct rule, input never arrives* — after the checker's five and covert coup's structural zero — and the first on the mechanics side reported green by an invariant. Worth a line in the standing lessons: the silent-path family is not a checker phenomenon, it is a plumbing phenomenon, and mechanics have plumbing too.

---

## Step 4 — Board geometry sensitivity (revised measure)

Only if steps 1 and 2 cleared.

**Measure swap, and the reason, recorded before measuring.** The pre-registered rule in `docs/phase-explain-decide-seal-prereg.md` stands unchanged: *between-board variation exceeding within-board variation, for any one mechanic.* What changes is the quantity it applies to.

- **Was:** the four distance attributions. These rest on 34 moved decisions panel-wide, and alliance's arm on 13. At that n, "moved materially" and "moved" are indistinguishable.
- **Now:** **discriminating share per mechanic** — the fraction of that mechanic's evaluations where proximity spread across candidates was wide enough to change the winner. It rests on 555 movable decisions, and it is the quantity that directly encodes whether board geometry gives distance room to act.

Amend the prereg document with this swap and its rationale **before** running. The rule itself is not amended.

**Pre-committed rule.**

- **Between-board exceeds within-board for any mechanic** → geometry is a first-class variable. **Halt, do not cut.** Sealing against one board's geometry would calibrate against a distribution that is not the one shipped.
- **It does not** → proceed, and state the limitation in these words: this demonstrates sensitivity strongly but insensitivity only weakly, because every board sampled comes from one generator and may share characteristics Azgaar's do not. Do not report it as "geometry does not matter."

If step 2 found the gain reproduces under shuffle, note the prior it sets but do not let it substitute for the measurement.

---

## Step 5 — `BaselineArchive`, then cut

Gated on steps 1, 2 and 4 all clearing.

**Fix `BaselineArchive` to carry the board.** The definitional half matters more than the code: **from ruleset 4, a world is a log and its board.** Anything claiming to archive a world without both is incomplete by definition, not by oversight. Write that where the archive format is specified. A gap rather than a trap only because `Replay` refuses a mismatched board fingerprint — preserve that refusal.

**Then cut, as before.** Five seeds. `wb baseline cut` continues to read the producing engine from the world file's own header, not the build running it. Layer 5 unskipped and passing on every new baseline. v1 untouched; the ruleset-3 baselines stay sealed and are not superseded.

**Before sealing.** Worlds are disposable; the *hand verification* of one is not. It is the only cost here paid in human attention and not regenerable. Seal deliberately.

---

## Halt conditions

Report and stop when **all** hold:

1. Step 0's two standing rules written down and the log-hash test extended across the mechanic set.
2. Redraw control run, rule applied, degeneracy guard evaluated.
3. Shuffle control run, rule applied — or step 1 aborted.
4. Alliance absorbed-fraction measured; the P(0 of 13) figure corrected with its denominator named.
5. Step 4 resolved either way, with the limitation stated in the required terms if it cleared.
6. `BaselineArchive` carrying the board, and baselines cut across five seeds — or explicitly blocked, with the block named.
7. Budget intact: no threshold moved, ruleset 4, no new mechanics or checker rules, all control worlds quarantined and marked.

**Abort immediately** if: the redraw control reproduces the gain; any degeneracy guard fires; the alliance clamp hypothesis is falsified; a control cannot be built without altering the main RNG stream; or any step needs a ruleset or threshold change.

---

## Escalate, do not resolve

- Any question of what a measurement *means* rather than what it is.
- Redraw reproducing — that is a finding about the last two phases and needs a decision about what stands.
- Alliance clamp falsified.
- Whether alliance recalibration enters a later phase.

---

## Record in the phase report

- The **34-of-555** figure as the phase's headline, with whichever mechanism the controls left standing, stated as narrowly as the evidence allows.
- The **degeneracy guard** amendment: a comparative rule needs a stated minimum panel range below which its rank arm is void and it falls to its absolute arm. Without it, a tight panel silently converts a rank criterion into a coin flip. This is why seed 99 read as failed rather than mixed.
- **Direction matching is not evidence between mechanisms that both predict the direction.** The §6 prediction was pre-registered and confirmed, and that was still not enough. This is the sharpest methodological lesson available from these two phases.
- **RNG draw order is load-bearing**, and the with/without log hash is the only detector.
- Whatever the controls returned, including a passed control that made the claim *larger* — those are the easiest results to file as unremarkable.
