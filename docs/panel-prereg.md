# Pre-registration — the measurement panel

Written before the panel was built and before any arm was run. The seed list at
the bottom was fixed here first; it is not extended after results, and extending
it is an abort trigger.

---

## 1. The distinction this installs

The **reference panel** — seeds 7, 42, 99, 1234, 2025 — exists because hand
verification is expensive. Five worlds is about as much prose as a person will
read against a record.

The **measurement panel** needs no hand verification at all: headless
simulation, zero model calls, every metric computed in C#. It costs compute and
nothing else.

Sizing the second by the cost of the first is how five seeds became a
statistical claim. **They are separate objects from here on**, and the reference
seeds are excluded from the measurement panel so they cannot be confused again.

---

## 2. Step 0 — the paired variance, and what it may be used for

**It was used only to size N.** It was not used, and will not be used, to
revisit any claim about geography. These data have been seen; re-analysing seen
data with a newly chosen variance is how a dead result comes back to life
wearing better statistics. The headline claim is decided on fresh data from the
sized panel.

Causal variety on the reference panel, and the paired contrast:

| seed | flat (r3) | geography | redraw | geo−red |
|---|---|---|---|---|
| 7 | 42 | 45 | 62 | −17 |
| 42 | 88 | 99 | 116 | −17 |
| 99 | 74 | 69 | 58 | +11 |
| 1234 | 64 | 97 | 83 | +14 |
| 2025 | 56 | 66 | 52 | +14 |

- **Paired mean: +1.00. Paired SD: 16.48.**
- Geography arm, deltas vs flat: mean +10.40, **SD 14.17**.
- Redraw arm, deltas vs flat: mean +9.40, **SD 18.54**.

**Pairing bought nothing.** The paired SD is 1.16× the geography arm's SD and
0.89× the redraw arm's. The decision rule's second branch applies: the seeds are
not behaving as paired units, the guard was reading the right variance, and the
panel is genuinely the constraint. **Proceed with σ = 16.48.**

### A correction the brief inherited from me

The brief cites "the within-arm between-seed SD already reported (38 and 44)".
**Those were ranges, not standard deviations.** My report wrote them as
"spread", meaning max − min. The standard deviations are **14.17 and 18.54**.

This matters, and is recorded rather than quietly fixed. The degeneracy guard
compared a band of 19 against those figures:

- against the ranges (38, 44), as applied — **the guard fires**;
- against the SDs (14.17, 18.54) — 19 exceeds both, and **the guard does not fire**.

Had the guard not fired, the rule would have applied and read *redraw reproduces
the gain*, which aborts as well. **Both readings abort, so the previous phase's
conclusion is robust to the error.** But the verdict it was reported under was
sensitive to an ambiguity in one of my own figures, and that is the third time a
number of mine has been carried forward meaning something else.

---

## 3. Step 1 — the minimum effect worth detecting

**MDE: 5 points of causal variety on the paired geography − redraw contrast.**

Taken from the brief's default and accepted without change. Its rationale, which
I agree with: the contrast decides whether *stability of the spatial fact* is
treated as a design principle worth building further mechanisms on. Geography's
own observed movement was about 10 points; an advantage over structureless
perturbation smaller than half of that is not a foundation for a design
principle, whether or not it is real.

**This number was chosen by reasoning about the decision it feeds and is not
fitted to any data.** It was fixed before the power calculation was run and is
not adjusted afterwards.

---

## 4. The power calculation

Paired design, two-sided α = 0.05, power 80%.

```
N = (z(0.975) + z(0.80))² · σ² / Δ²
  = (1.959964 + 0.841621)² · σ² / 25
  = 7.848878 · σ² / 25
```

**At the point estimate σ = 16.48:** N = 7.848878 × 271.5 / 25 = **85.2 → 86**.

**That is not the number registered.** Two reasons, both stated before running:

1. **σ is estimated from five observations and is itself very uncertain.** The
   one-sided 80% upper confidence bound on σ² with 4 degrees of freedom is
   `4 s² / χ²(0.20, 4)` = 1086 / 1.6488 = 658.7, giving **σ ≤ 25.66**. At that
   σ, N = 7.848878 × 658.7 / 25 = **206.8 → 207**.
2. **The panel adds a variance the reference seeds do not have.** Each panel seed
   gets its own board, where all five reference seeds shared the stored one. Board
   variance is in the panel contrast and absent from the estimate σ came from, so
   the true panel σ is more likely above 16.48 than below it.

Extending a panel after seeing results is an abort trigger, so the margin is
taken in advance rather than regretted afterwards. Compute is the only cost.

> **N = 207 seeds per arm.** Four arms, paired, on the same 207 seeds.

Under the brief's cap of 2,000 per arm, so no halt.

At N = 207 the design has 80% power down to Δ = 5 for any σ ≤ 25.7, and 80%
power at σ = 16.48 down to Δ = 3.2.

---

## 5. The panel

**207 seeds: 9000001 through 9000207 inclusive.**

Contiguous, which is deterministic, exhaustively auditable, and harmless: every
consumer mixes the seed through splitmix64 before use — `Rng.For`, `NameForge`
and `BoardMaker` all do — so adjacent seeds produce uncorrelated worlds and
uncorrelated boards.

**The reference seeds 7, 42, 99, 1234 and 2025 are not in this range and are
excluded by construction.** Keeping them separate is the point of the exercise.

**Each panel seed gets its own board**, from `wb map make` at that seed, 20×14.
Board geometry therefore varies across the panel by construction, which makes
the board-sensitivity question a by-product of these runs rather than a separate
phase.

---

## 6. The arms and the contrasts

Four arms, all on the same 207 seeds and the same board per seed:

| arm | distance input |
|---|---|
| **flat** | every proximity 100 — reproduces ruleset 3 exactly, verified on the reference panel |
| **geography** | the board |
| **shuffle** | one synthetic value per unordered place-pair, fixed at worldgen — stable, no spatial structure |
| **redraw** | fresh synthetic value per decision — no stability, no structure |

Synthetic values are drawn from each world's own empirical proximity
distribution, on a stream no rule can reach.

**Three contrasts, all registered here, with Holm correction across them:**

| contrast | isolates |
|---|---|
| shuffle − redraw | does *stability* add anything beyond perturbation? |
| geography − shuffle | does *spatial structure* add anything beyond stability? |
| **geography − redraw** | **the headline. The MDE was set for this one.** |

**Decision rules:**

- **geography − redraw clears the MDE and survives Holm** → geography's effect is
  distinguishable from structureless perturbation; read the two component
  contrasts to say why, and state the mechanism as narrowly as they support.
- **It does not clear** → record plainly that geography's effect on causal
  variety is not distinguishable from structureless perturbation at N = 207,
  with the interval quoted. This is a result, not a failure. **Geography's design
  rationale is untouched** — distance gates conflict, trade, alliance and later
  rumour, and that was never a claim about causal-variety deltas.
- **Any contrast clears while the headline does not** → report both, change
  nothing, escalate.

The realised paired σ is reported against the 16.48 estimate. A large miss is
itself a finding about the panel.
