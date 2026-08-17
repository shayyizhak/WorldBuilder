# Phase: variance, panel, and the decoupled cut — completed

Ruleset `4`, engine `1.2.0`, **515 tests green** (was 501). No threshold moved,
no ruleset change, no new mechanics, no new checker rules. All control and panel
worlds marked and quarantined. No hand verification of anything.

**This phase changed no rule and settled the question three phases had been
circling.** It also replaced the project's idea of how large a measurement is.

---

## The headline

At **N = 207**, four arms paired on the same seeds and the same boards:

| contrast | mean | 95% CI | p | Holm | clears 5-pt MDE |
|---|---|---|---|---|---|
| shuffle − redraw | −1.15 | [−3.16, +0.86] | 0.26 | no | no |
| geography − shuffle | +0.62 | [−1.46, +2.71] | 0.56 | no | no |
| **geography − redraw** | **−0.53** | **[−2.70, +1.65]** | **0.63** | no | no |

**These are precise nulls, not failures to detect.** The headline interval runs
from −2.7 to +1.65, so it excludes the pre-registered 5-point minimum effect in
both directions. The design had 80% power down to 3.2 points at the realised σ.

**Geography's effect on causal variety is not distinguishable from structureless
perturbation at N = 207.**

Arm means, reported as description: flat **64.43**, geography **63.06**, shuffle
**62.44**, redraw **63.59**. Geography sits *below* the no-distance arm.
**The geography − flat contrast was not pre-registered and is therefore not
tested here** — it is noted as a question for a later phase, not claimed.

**Realised paired σ: 15.87, against 16.48 estimated from the reference panel.**
Close, so the sizing held and the estimate was not lucky. N = 86 would have
sufficed; the margin taken for σ uncertainty cost 25 seconds of compute.

---

## What this does *not* touch

**Geography's design rationale is untouched.** Distance gates conflict, trade,
alliance and — at Stage 11 — rumour. That was never a claim about
causal-variety deltas, and it is not what was tested.

The following stand, and are worth separating from what fell:

- **The 680 / 555 / 34 census.** Hundreds of within-world decisions, not five
  worlds. Distance is consulted on 680 decisions, has room to move 555, and
  moves 34.
- **Wars are fought where they are declared.** A structural property of
  individual events — Threi Cut three years running — not a panel statistic.
- **The calibration catch.** Proximity was scaled against a distance no world
  contained. That was a correctness fix, not a comparative claim.
- **Alliance's distance term is live in 13 of 13 evaluations**, by inspection.
- **The engineering**: positions at worldgen, board import and hashing, the
  bundle writer, the controls, 515 tests.

---

## Step 0 — the paired variance, and a correction

Computed from existing data. **Used only to size N**, and that restriction was
written into `docs/panel-prereg.md` before it was computed — because the paired
variance was exactly the quantity that would have made the geography result look
better, and seen data does not get to decide the experiment it came from.

| seed | flat | geography | redraw | geo−red |
|---|---|---|---|---|
| 7 | 42 | 45 | 62 | −17 |
| 42 | 88 | 99 | 116 | −17 |
| 99 | 74 | 69 | 58 | +11 |
| 1234 | 64 | 97 | 83 | +14 |
| 2025 | 56 | 66 | 52 | +14 |

Paired mean **+1.00**, paired SD **16.48**. Geography arm SD **14.17**, redraw
arm SD **18.54**.

**Pairing bought nothing** — the paired SD is 1.16× and 0.89× the two arm SDs.
The rule's second branch applies: the seeds are not behaving as paired units,
the guard was reading the right variance, and the panel is genuinely the
constraint.

### A correction the brief inherited from me

The brief cites the within-arm figures as "38 and 44". **Those were ranges**,
which my report called *spread*, not standard deviations. The SDs are 14.17 and
18.54.

It matters, because the degeneracy guard compared a band of 19 against them:

- against the **ranges** (38, 44) — the guard **fires**;
- against the **SDs** (14.17, 18.54) — 19 exceeds both, and it **does not**.

Had it not fired, the rule would have applied and read *redraw reproduces the
gain*, which aborts as well. **Both readings abort, so the previous phase's
conclusion is robust to the error.** But it was reported under an ambiguity in
one of my own figures, and that is the third time one of my numbers has been
carried forward meaning something else. The other two: the checker rule count
that sat at 17, and "one in eight" against an unnamed baseline.

---

## Steps 1 and 2 — sizing, then building

**MDE = 5 points**, taken from the brief's default and accepted unchanged. Its
rationale is about the decision it feeds rather than about any data: geography's
own observed movement was ~10 points, and an advantage over structureless
perturbation smaller than half of that is not a foundation for a design
principle whether or not it is real. Fixed before the power calculation ran.

**N = 207.** The point estimate at σ = 16.48 gives 86. The registered figure is
taken at the 80% upper confidence bound on a σ estimated from five observations
(σ ≤ 25.66 → N = 207), for two reasons stated in advance: σ from n=5 is itself
very uncertain, and panel seeds each get their own board, carrying a variance
the reference seeds — all on one shared board — never had. Extending a panel
after results is an abort trigger, so the margin was taken beforehand.

**The panel: seeds 9000001–9000207**, fixed in `docs/panel-prereg.md` before any
arm ran. Contiguous, which is deterministic and exhaustively auditable and
harmless, because every consumer mixes the seed through splitmix64 before use.
The five reference seeds are outside the range and excluded by construction —
asserted in code, not assumed.

**207 seeds × 4 arms + a shared-board control arm ran in 21 seconds.** That
figure is the whole argument of the phase: the measurement that three phases
were sized around as though it were expensive costs less than half a minute.

### The flat arm

The no-distance arm is a control, not an archived measurement from the
ruleset-3 binary. Every consumer multiplies by a proximity and divides by a
hundred, so a hundred everywhere reproduces pre-geography behaviour — and that
is pinned as a test against the measured ruleset-3 figures on all five reference
seeds, exactly. **A contrast whose arms came from different binaries has a
second variable in it**; this one does not.

---

## Step 3 — board geometry, as a by-product

The question deferred from two phases ago, answered on these runs rather than in
a phase of its own. Each panel seed ran on its own board *and* on the shared
board, which separates the two variances: `var(own) = var(seed) + var(board)`,
`var(shared) = var(seed)`.

- own board: mean discriminating share **4.62%**, sd 2.26
- shared board: mean **4.91%**, sd 2.52
- **var(board) = 5.11 − 6.36 = −1.25** → the board adds nothing measurable.

**Geometry is not a first-class variable.** Stated with the limitation the brief
requires, in its terms: **this demonstrates sensitivity strongly and
insensitivity only weakly**, because every board sampled comes from one
generator and may share characteristics an Azgaar export does not. It is not
reported as "geometry does not matter".

**Discriminating share per mechanic, across 207 boards:**

| mechanic | mean | sd | n |
|---|---|---|---|
| marriage | 24.05% | 13.13 | 207 |
| alliance | 7.92% | 16.63 | 193 |
| conquest | 6.93% | 11.85 | 207 |
| raid targeting | 5.42% | 6.41 | 207 |
| war declaration | 1.50% | 1.96 | 207 |

**Alliance's 8% independently confirms the 0-of-13 correction.** At an 8% flip
rate, seeing none in thirteen happens about a third of the time. The finding
that was reported as "suggestive of a decorative branch" at "one in eight" was
an ordinary sample all along, and the panel says so from data rather than from
arithmetic.

---

## Step 4 — the archive fix, and the cut

Run independently of everything above, as the brief directs. The previous block
conflated a cheap regenerable half with an expensive human half; only the second
needed to wait.

**`BaselineArchive` carries the board, and the definition is written where the
archive format is specified.** From ruleset 4, **a world is a log and its
board** — a cell index means nothing without the board it indexes into, so an
archive holding the log alone does not hold the world, and it is incomplete *by
definition* rather than by oversight.

The board is required exactly when the log names one, so the ruleset-3
baselines — which predate boards — keep verifying untouched. And the archived
board is checked against the fingerprint on the world's own genesis event, not
merely required to be present: **archiving the wrong map would seal a world
nobody can read the distances of.** `Replay`'s refusal of a mismatched
fingerprint is preserved, which is what makes this a gap rather than a trap.

**Five machine baselines cut**, at `baselines/ruleset-4/seed-*`. All carry
`verification: stability-anchor-only`, all carry their board, every seal
verifies, and **Layer 5 passes 0/0 on every one** — the current side of each
diff rebuilt from the stored render cache by `--check-only`, which constructs no
client and so cannot repair a miss by generating.

Checker fingerprint `60f5b325` again, unchanged since v1. The v1 and ruleset-3
baselines are untouched and still verify; the ruleset-4 set sits beside them and
supersedes nothing.

**Hand verification stays blocked, and the block is named.** No re-verification
of seed 42 at ruleset 4 until the board question is settled. That is the only
non-regenerable cost in the project — the prose figures, the ruler lists, the
tenure spans, checked against the record by a person — and the board question
genuinely gates it, because a swapped board makes new worlds rather than changed
ones and the verification would be paid for twice.

**One environmental note.** Generation wedged the local model twice, both times
under concurrent load from this phase's own panel runs and test suite: Ollama
kept the model resident and stopped answering, and three seeds burned all their
retries against it. It recovered on its own once the machine was left alone, and
the three were regenerated to completion. The render cache made the retries
nearly free — a resumed run re-serves every passage already written — but the
lesson is smaller and duller than the rest of this phase: do not run a 207-seed
panel and a 22 GB model at the same time.

---

## Step 5 — the retired claims

Marked in place with what replaced them, never deleted. A claim that vanishes
without trace gets re-derived.

**Retired:**

- *"Causal variety rose on four seeds of five, by up to +33."* → geography −
  redraw is −0.53, CI [−2.70, +1.65], at N=207.
- *"`verbatim repeat rate` cleared as a geography result."* → same panel, same
  guard, never separately rescued. Panel arm means are 7.52% flat, 7.27%
  geography, 7.30% shuffle, 7.01% redraw — no MDE was set for it and no test is
  claimed, but nothing there supports a geography attribution either.
- *Any claim of the form "geography improved four seeds of five."*

**Stands:** the census, wars fought where declared, the calibration catch,
alliance live in 13 of 13, the engineering.

---

## The methodological record

**A pre-registered prediction shared by both competing mechanisms is not a test
of either.** The §6 prediction was pre-registered, falsified, and its
pre-registered alternative confirmed — and it discriminated nothing, because
both mechanisms predict a rise. **Pre-registration constrains the analyst; it
does not do the discriminating.** The control does.

**The reference panel is not the measurement panel.** Five exists because hand
verification is expensive. Statistical comparison needs none, and sizing it by
the wrong cost is what produced a claim the data could not support. The number
five was never a decision anybody made — it was inherited from a different
constraint and then treated as a sample size for three phases.

**Seen data sizes the next experiment; it does not decide the last one.** The
paired variance was available to rescue the geography result and was explicitly
not used that way.

**A properly powered null is a better artefact than the +33 ever was.** The +33
was a real number about five worlds that was never evidence about the engine.
The null is evidence about the engine, and it is the first thing in this
sequence that is.

---

## Budget

| constraint | held |
|---|---|
| No `SimConfig` threshold moved | ✅ |
| Ruleset stays at 4 | ✅ |
| No new mechanics, no fifth distance consumer, no new checker rules | ✅ |
| Control and panel worlds quarantined | ✅ — marked in header and record, `wb baseline cut` refuses one, nothing written to disk unless asked |
| No hand verification of anything | ✅ |
