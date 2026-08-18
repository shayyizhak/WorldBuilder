# Report — ruleset 6 disposition

Run against `docs/brief-ruleset-6-disposition.md`, pre-registered in
`docs/brief-ruleset-6-disposition-prereg.md` (commit `0b153f5`, before any panel seed existed).

> ## §1 HALTED on §4's third condition — the random arm could not be matched
>
> **15 of 310 scheduled removals could not be delivered, on 15 of 90 worlds.** The pre-registered
> consequence is that `war − random` is void, the registered family of three cannot be evaluated as
> written, and **no disposition cell fires**. Nothing was retired, re-baselined or sealed: §2.1 and
> §2.2 are gated on §1 returning and it did not.
>
> **§2.3's measurements are done** — they are classification only, and they are what the eventual
> decision needs.
>
> The primary contrast is unaffected by the halt and is reported below, because throwing away a
> sound n=90 figure to make a halt look tidier helps nobody.

**Entry state:** 613 green, 18 failing, 2 skipped.
**Exit state:** 613 green, 18 failing, 2 skipped — the same 18. No mechanic changed.

---

## §1 — sizing the war rule

### What ran

90 seeds (9100001–9100090), each on its own board, four arms, paired. 14 seconds, zero inference.
Output in `docs/ruleset-6/war-panel.txt`; the pre-run dry run in `docs/ruleset-6/dry-run.txt`.

### The treatment was delivered — checked before anything was concluded from it

| arm | trade ties ended, mean | total over 90 worlds |
|---|---|---|
| null | 0.00 | 0 |
| collapse | 4.04 | 364 |
| war | 5.73 | 516 |
| random | 6.02 | 542 |

This row exists because an arm that changed nothing *because it did nothing* and an arm that changed
nothing *despite doing something* are the same line in every table below, and only this one tells
them apart. It matters immediately: see `collapse − null`.

### The halt

**310 removals scheduled, 15 undeliverable, across 15 worlds — always exactly one per world.**

The cause is structural rather than a bug. The random arm's world diverges from the war arm's world
the moment their first removals differ, so a year that had a live trade tie in the war arm need not
have one in the random arm. Matching count *and* timing exactly is not generally achievable once
the arms diverge, and the brief pre-committed that failing to is a halt.

I have not amended the matching rule. The two available amendments — sliding a missed removal to the
next year with a live tie, or accepting a documented tolerance — both change the registered
treatment, and choosing either after seeing the data is the move the pre-registration exists to
prevent.

### The primary contrast, which the halt does not touch

`war − null` involves neither the random arm nor its schedule.

| contrast | n | mean | 95% CI | p | clears MDE 5y |
|---|---|---|---|---|---|
| **`war − null`** | 90 | **+0.27 y** | **[−0.27, +0.81]** | 0.329 | **no** |
| `war − random` | 90 | −0.07 y | [−0.63, +0.49] | 0.814 | no — **and VOID** |
| `collapse − null` | 90 | +0.00 y | [+0.00, +0.00] | 1.000 | no |

Degeneracy guard: **passed** — null arm `sd=16.93` against the 3-year minimum, 32% censored against
the 50% ceiling.

**`war − null` is a precise null, not an inconclusive one.** The whole interval sits inside ±1 year;
the experiment excludes any systematic shift approaching the 5-year MDE, and the point estimate is
*positive* — hegemony arriving marginally later, not earlier. The war rule does not systematically
shorten contested history.

**The verdict on it is robust to the halt.** p = 0.329 fails at α = 0.05 **uncorrected**, so no
correction scheme over any family size could rescue it. Whether the family is three, two or one, the
answer is the same word.

### `collapse − null` is exactly zero, and now means something

Flagged in the prereg §8 as the contrast to watch: zero variance on all five dry-run seeds. It has
zero variance on all ninety panel seeds too — the collapse arm's runaway year is identical to the
null arm's on every world, and so is its shape count (63.63 against 63.63, same sd).

The prereg committed that if this repeated, **the contrast reports nothing and would be said to
report nothing.** It repeated. What the treatment-delivered row adds is that this is no longer
ambiguous: the collapse arm **ended 364 trade ties across 90 worlds** and moved neither metric on
any of them. That is not "the arm did nothing"; it is "the arm did a great deal and nothing
downstream reads it".

So the collapse cleanup is inert with respect to hegemony and causal variety, on 95 worlds
(90 + the 5 reference seeds). It repairs the §0 defect at no cost. That was already decided in the
brief and is not reopened; it is now measured rather than assumed.

### What the panel says about the reference-panel result

The five reference seeds gave `war − null` a paired mean of **−6.6 years** (sd 10.85). Ninety fresh
seeds give **+0.27** (sd 2.58). The reference figure sits far outside the panel's interval, and the
panel's sd is a quarter of the one it was sized from.

Read plainly: **the two hurt reference seeds were a tail.** The disposition cell the brief specifies
for `war − null` not distinguishable is *ship all three* — and that cell does not read `war − random`
at all. **I am not firing it**, because the registered family is incomplete and firing a cell on a
broken family is exactly what the pre-registration forbids. The observation is handed over instead.

---

## §2 — disposition

### 2.1 Baselines: **not cut.** 2.2 Tests: **not retired, not rebased.**

Gated by the brief's own opening line — *nothing is retired, re-baselined or sealed until §1
returns* — and §1 halted. Both remain owed, and both are one decision away.

### 2.3 The two real failures, classified

#### Seed 99 `distinct deep-chain shapes` 69 → 45 — **the metric is substantially a count of history length**

Measured on 90 worlds rather than on the two that raised the question:

> **shapes against event count, null arm: r = 0.871 over 90 worlds.**

And on the reference panel, at both rulesets, as shapes per 1000 events:

| seed | r5 events / shapes | rate | r6 events / shapes | rate | rate change |
|---|---|---|---|---|---|
| 7 | 526 / 45 | 85.6 | 529 / 45 | 85.1 | −0.6% |
| 42 | 873 / 99 | 113.4 | 878 / 99 | 112.8 | −0.5% |
| **99** | 704 / 69 | **98.0** | 531 / 45 | **84.7** | **−13.6%** |
| 1234 | 864 / 97 | 112.3 | 826 / 91 | 110.2 | −1.9% |
| 2025 | 698 / 66 | 94.6 | 700 / 66 | 94.3 | −0.3% |

Seed 99's **count** fell 34.8%; its **rate** fell 13.6%. So roughly three-fifths of the drop is
simply that there was less history to draw shapes from, and two-fifths is a real thinning.

Two things worth having beside that:

- **Seed 7, the long-standing failure, has the lowest rate on the panel** (85.6) and always has.
  Its problem is not length.
- **Seed 99 at ruleset 6 lands at 84.7 — the same rate as seed 7.** The two failures are now the
  same shape, which they were not before.

**The bar is not moved here**, per §2.3. But the measurement it asked for says plainly that a `≥ 60`
count bar on a world whose length varies by a factor of two is partly a length bar, and establishing
what it should be is the separate measurement the brief reserves.

#### Seed 99 `BothOutcomesOfTheRollAreReached` — **the missing branch is `exposed`, not `seized`**

This matters, because the invariant exists for the case where the covert-coup *success* path was
structurally zero. That is not what happened.

| seed | `exposed` | `seized` |
|---|---|---|
| 7 | 11 | 2 |
| 42 | 12 | 8 |
| **99** | **0** | **8** |
| 1234 | 12 | 7 |
| 2025 | 10 | 2 |

Seed 99 wins eight coups at ruleset 6. What it never does is have one *uncovered*.

The halt condition — the branch firing on no seed at the surviving ruleset — **did not fire**:
`exposed` occurs 11, 12, 12 and 10 times on the other four. Per the brief's pre-commitment, the
condition for panel-level being honest is met, and per-seed was over-strict **for this branch**.
Stated with the seeds, and **not acted on**: re-baselining is an explicit human act.

Attributed to an arm, since the machinery exists:

| seed 99 arm | `exposed` | `seized` |
|---|---|---|
| none (= ruleset 5) | 5 | 10 |
| collapse | 5 | 10 |
| **war** | **0** | **8** |
| disuse | 5 | 10 |
| all (= ruleset 6) | 0 | 8 |

The war arm removes it; the other two rules leave it untouched. Consistent with everything else in
this and the previous report.

---

## §4 — halt conditions

| condition | state |
|---|---|
| Any §1 decision-rule arm unreachable in the dry run | **held** — 3 of 3 cells producible, both degeneracy arms reachable, censoring exercised, random arm matched 5 of 5 |
| Null-panel runaway spread below the stated minimum | **held** — `sd=16.93` against 3 |
| Random arm unable to match count and timing | **TRIGGERED** — 15 of 310 removals, 15 of 90 worlds |
| `BothOutcomesOfTheRollAreReached` firing on no seed | **held** — `exposed` fires on 4 of 5 seeds |
| Suite not green after 2.1 and 2.2 | **not reached** — neither was done |

---

## Files

| file | change |
|---|---|
| `docs/brief-ruleset-6-disposition-prereg.md` | new — MDE, censoring, family, N, guard, reachability, committed before the run |
| `src/WorldBuilder.Core/Analysis/WarRulePanel.cs` | new — the four arms, the censoring rule, the degeneracy guard |
| `src/WorldBuilder.Core/Rules/TerminationArm.cs` | `RandomTrade`, and `RandomTieSchedule` |
| `src/WorldBuilder.Core/Rules/RelationEnds.cs` | `RemoveScheduledAtRandom`, on `RngPurpose.Control` |
| `src/WorldBuilder.Core/Analysis/WorldView.cs` | carries its `Board` — see below |
| `src/WorldBuilder.Core/Analysis/WorldStats.cs`, `RelationTrajectory.cs`, `PackCauses.cs`, `PackDigest.cs` | re-fold through the view's own board |
| `src/WorldBuilder.Cli/CommandLine.cs` | `wb warpanel [--dry-run]`; `Runaway` now reads `WorldStats` |
| `docs/ruleset-6/` | generated: dry run, panel |

### A latent defect the panel found

Five separate places re-fold a `WorldView`'s own log to get state at a point in time —
`WorldStats`, `PackCauses`, `PackDigest`, `RelationTrajectory`, and the runaway helper — and every
one of them looked up **the repository's stored board** instead of the world's own. On the reference
seeds those are the same board and nothing ever showed. On a measurement panel, where each seed gets
its own board and none is stored, all five refuse the world outright.

Fixed at the source rather than at the call sites: `WorldView.Board` reads the board off its own
folded state, so it cannot disagree with what the view was built from. This would have bitten the
next measurement panel of any kind, silently on the reference seeds and loudly everywhere else.

### One rule of the project's own caught a name

`WarRulePanel.MinimumSpread` failed `NoPublicMemberExposesADispersionAsABareNumber` — the lexicon
that retired the word "spread" from emitted figures. It was a threshold *in years* that a standard
deviation is compared against, not a dispersion figure, so it is renamed
`MinimumYearsOfVariation` rather than wrapped. The test was right to stop it.

---

## Owed

- **The decision on the halt.** Either amend the matching rule and re-register, or accept
  `war − null` on its own. My reading: `war − null` at +0.27 [−0.27, +0.81] over 90 worlds answers
  the question the brief asked, and the cell it points to does not read the void contrast — but that
  is a call for you, not for me.
- **§2.1 and §2.2**, unblocked by that decision.
- **The `≥ 60` shape bar** — measured to be substantially a length bar; establishing what it should
  be is its own measurement.
- **`BothOutcomesOfTheRollAreReached`** — panel-level is defensible for the `exposed` branch, with
  the seeds recorded above. Re-baselining stays an explicit human act.
