# Phase: variance, panel, and the decoupled cut

**Loop-prompt.** Run unattended until every halt condition holds or an abort triggers. Rules are pre-committed below, before the measurements exist.

**Entry state:** ruleset 4, 501 tests green, no threshold moved. Redraw control aborted the previous phase; the degeneracy guard fired on the same comparison. Nothing cut, nothing sealed. `BaselineArchive` does not carry the board.

**Standing correction already applied:** the 0-of-13 alliance figure is ~45% against the 6% panel rate, not one in eight. The clamp hypothesis is falsified — absorbed 0 of 13, the distance term is live in every evaluation.

---

## What this phase is for

Two questions, in order, and the first is cheap enough to answer before lunch:

1. **Was the degeneracy guard reading the wrong variance?** The guard compared arm medians against *within-arm between-seed* spread. If the arms ran the same five seeds, most of that spread is common to both arms and cancels. The quantity that governs discrimination is the spread of the **paired per-seed difference**, not the spread within either arm.
2. **If the panel really is the constraint, how big does it need to be?** Answered by calculation, then built — because a measurement panel costs compute, not human attention, and has been sized as though it cost both.

**The central distinction this phase installs:** the **reference panel** (7, 42, 99, 1234, 2025) exists because hand verification is expensive. The **measurement panel** needs no hand verification at all — headless simulation, zero model calls, metrics computed in C#. Sizing the second by the cost of the first is how five seeds became a statistical claim. They are separate objects from here on.

---

## Hard budget

1. **No threshold in `SimConfig` moves.**
2. **Ruleset stays at 4.** Controls are experimental configuration, not rules.
3. **No new mechanics, no fifth consumer of distance, no new checker rules.**
4. **Control and panel worlds are quarantined** — marked in the world-file header, never sealed, never archived as baselines, never entering a render cache.
5. **No hand verification of anything.** If a step wants a human to read prose, it is out of scope.

---

## Step 0 — Paired variance check

Twenty minutes, existing data, no new runs. This decides whether the rest of the phase is five seeds or two hundred.

**Compute.** For each of the five seeds, the paired difference geography − redraw in causal variety. Report the five differences, their mean, and their standard deviation. Alongside: the within-arm between-seed SD already reported (38 and 44).

**Then state the ratio.** Required panel size scales with (σ/Δ)². At σ=40 and Δ=19 the arms need roughly 70 seeds each; at Δ=10, roughly 250. At σ=15 and Δ=19, five seeds is close to sufficient. Same observations, different variance, two orders of magnitude apart.

**Hard constraint on what this may be used for.** The paired variance is used **only to size the panel**. It may not resurrect any claim about geography. These data have been seen, and re-analysing seen data with a newly chosen variance is how a dead result comes back to life wearing better statistics. **The claim itself is decided on fresh data from the sized panel, in step 3.** Report the paired analysis regardless of which way it comes out, and say in the report that it was not used to decide anything except N.

**Decision rule.**

- **Paired SD materially below within-arm SD** (below half, say — the comparison is coarse and the rule only routes) → pairing was the right design and the panel may be smaller than feared. Proceed to step 1 with the paired σ.
- **Paired SD comparable to within-arm SD** → the seeds are not behaving as paired units, the guard was reading the right variance, and the panel is genuinely the constraint. Proceed to step 1 with that σ.

Either branch continues. This step routes; it does not gate.

---

## Step 1 — Size the panel, before building it

**Pre-register the minimum effect worth detecting, in writing, before running the calculation.**

Default, with its rationale: **5 points of causal variety** on the paired geography − redraw contrast. Rationale: the contrast decides whether *stability of the spatial fact* is treated as a design principle worth building further mechanisms on. Geography's own observed movement was ~10 points; an advantage over structureless perturbation smaller than half of that is not a foundation for a design principle, whether or not it is real. This number is chosen by reasoning about the decision it feeds, not fitted to the data.

If you disagree with the default, halt and escalate — do not adjust it after seeing a power curve.

**Compute** required N per arm at 80% power, α=0.05, paired design, using the σ from step 0. Write N and the calculation into `docs/panel-prereg.md`.

**Cap.** If required N exceeds 2,000 seeds per arm, **halt**. An effect that needs 2,000 fifty-year histories to distinguish from noise has answered the design question in the negative, and that is a result rather than a blocker.

---

## Step 2 — Build the measurement panel

**Requirements:**

- N seeds drawn deterministically and **recorded as an explicit list** in `docs/panel-prereg.md` before any arm runs. The panel is fixed; it is not extended after seeing results.
- The five reference seeds are **excluded** from the measurement panel. Keeping them separate is the point.
- Headless. Zero model calls. All metrics computed in C#.
- Panel worlds marked in the header and excluded from archive, baseline and render-cache paths. Assert this rather than assuming it.
- Each panel seed gets its own board via `wb map make`.

**Note what this gives for free.** N boards means board geometry varies across the panel by construction. The step-4 geometry question from the previous brief — discriminating share per mechanic, between-board against within-board variation — becomes properly powered as a by-product, on the same runs. Compute it; do not run a separate geometry phase.

---

## Step 3 — Four arms, three pre-registered contrasts

**Arms**, all on the same panel seeds, paired:

1. **ruleset-3** — no distance.
2. **geography** — ruleset 4 as it stands.
3. **shuffle** — one synthetic value per unordered place-pair, fixed at worldgen, stable for the run. Stable heterogeneity, no spatial structure.
4. **redraw** — fresh synthetic value per decision. No stability, no structure.

Synthetic values drawn from each world's own empirical proximity distribution, from an **independent RNG stream** crystallised from `(world_seed, arm, site, decision_id)`. Main-stream draw count and order must be byte-identical to the geography arm — assert it, using the tightened identity check from the previous phase (897-of-898 was a real catch; keep the assertion tight and let the control marker be the only permitted difference).

**Contrasts, and what each isolates** — pre-register all three before running, with a Holm correction across them:

- **shuffle − redraw** → does *stability* add anything beyond perturbation?
- **geography − shuffle** → does *spatial structure* add anything beyond stability?
- **geography − redraw** → the headline, and the one the MDE was set for.

**Decision rules.**

- **geography − redraw clears the MDE and survives correction** → geography's effect is distinguishable from structureless perturbation. Then read the two component contrasts to say *why*, and state the mechanism as narrowly as they support.
- **It does not clear** → record plainly: geography's effect on causal variety is not distinguishable from structureless perturbation at N=[panel size], with the confidence interval quoted. This is a real result and it is not a failure of the phase. **Geography's design rationale is untouched** — distance gates conflict, trade, alliance and later rumour, and that was never a claim about causal-variety deltas.
- **Any contrast clears while the headline does not** → report both, change nothing, escalate.

**No degeneracy guard is needed here** because the design is paired and powered by construction — but report the realised paired σ against step 0's estimate. A large miss is itself a finding about the panel.

---

## Step 4 — Archive fix and machine cut (not gated on anything above)

Run this independently. The previous block conflated a cheap regenerable half with an expensive human half; only the second needed to wait.

**Fix `BaselineArchive` to carry the board.** The definitional statement matters as much as the code: **from ruleset 4, a world is a log and its board.** Anything claiming to archive a world without both is incomplete by definition, not by oversight. Write it where the archive format is specified. Preserve `Replay`'s refusal of a mismatched board fingerprint — that refusal is what makes this a gap rather than a trap.

**Cut machine baselines across the five reference seeds.** They are regenerable for free and they buy Layer 5 regression detection immediately, which is worth most during exactly this kind of measurement churn. If the board is later swapped, re-cutting costs compute and nothing else.

**Hand verification stays blocked.** No re-verification of seed 42 at ruleset 4 until the board question is settled. That is the only non-regenerable cost in the project and the board question genuinely gates it.

---

## Step 5 — Retire the dead claims from the record

Do not leave these to be rediscovered as true.

**Retired:** the +33 causal-variety attribution to geography. `verbatim repeat rate` clearing as a geography result — same panel, same guard, never separately rescued. Any claim of the form "geography improved four seeds of five."

**Stands:** the 680 / 555 / 34 census — hundreds of within-world decisions, not five worlds. Wars fought where declared, a structural property of individual events. The calibration catch, which was a correctness fix rather than a comparative claim. Alliance's distance term live in 13 of 13. The engineering: positions at worldgen, board import and hashing, the bundle writer, 501 tests.

Mark each retired claim in place with what replaced it, rather than deleting. A claim that vanishes without trace gets re-derived.

---

## Halt conditions

Report and stop when **all** hold:

1. Paired σ computed and reported, with an explicit statement that it was used only to size N.
2. MDE pre-registered with rationale before the power calculation; N and the calculation written to `docs/panel-prereg.md`.
3. Panel seed list fixed and recorded before any arm ran; reference seeds excluded.
4. Four arms run, three contrasts reported under Holm correction, realised paired σ compared against the estimate.
5. Discriminating share per mechanic computed across the panel's boards, between-board against within-board.
6. `BaselineArchive` carries the board; machine baselines cut on five reference seeds; hand verification still blocked with the block named.
7. Retired and standing claims marked in the record.
8. Budget intact.

**Abort immediately** if: required N exceeds 2,000 per arm; the identity assertion cannot be made to hold on any arm; the panel would need extending after results were seen; or any step needs a ruleset or threshold change.

---

## Escalate, do not resolve

- Disagreement with the 5-point MDE, **before** the power calculation runs.
- Any contrast clearing while the headline does not.
- A realised paired σ far from step 0's estimate.

---

## Record in the phase report

- **A pre-registered prediction shared by both competing mechanisms is not a test of either.** The §6 prediction was pre-registered and confirmed and still discriminated nothing. Pre-registration constrains the analyst; it does not do the discriminating.
- **The reference panel is not the measurement panel.** Five exists because hand verification is expensive. Statistical comparison needs no hand verification, and sizing it by the wrong cost is what produced a claim the data could not support.
- **Seen data sizes the next experiment; it does not decide the last one.** Note explicitly that the paired variance was available to rescue the geography result and was not used that way.
- Whatever step 3 returns, including a null. A properly powered null on the headline contrast is a better artefact than the +33 ever was.
