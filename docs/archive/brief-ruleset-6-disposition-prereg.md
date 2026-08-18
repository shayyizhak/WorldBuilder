# Pre-registration — sizing the war rule

Written against `docs/brief-ruleset-6-disposition.md` §1, **before any panel seed was run**. The
dry run below uses the five reference seeds, which exist already; the panel it sizes does not exist
yet. Every figure here is log-only and cost no inference.

Committed on its own so the claim that it predates the measurement is checkable rather than
asserted — the same discipline `docs/brief-step-two-design.md` was written under, and for the same
reason.

---

## 1. The question, and why n=5 cannot answer it

`wb discriminate` narrowed ruleset 6's whole regression to the war rule. But it did so on two seeds
of five, with three untouched in chains, shapes and runaway year. Two readings survive that:

- the war rule **systematically** shortens contested history; or
- the war rule is **high-variance**, most worlds shrug it off, and this panel caught the tail.

They have different dispositions — card the rule, versus ship it — and five paired observations
cannot separate them.

---

## 2. Arms

Four, paired on the same seeds and the same boards.

| arm | rules active |
|---|---|
| `null` | none — reproduces ruleset 5 |
| `collapse` | collapse cleanup only |
| `war` | war + collapse |
| `random` | collapse, plus N trade ties removed at random, N and the years matched per world to the war arm's own removals |

**The random arm is the discriminating one.** Without it, any `war − null` effect is confounded
with "trade ties came down": a world knife-edge sensitive to losing ties at all produces the same
contrast, and the fix for that is world design rather than a rule defect.

**Matching is per world, never on average.** The schedule is read off the war arm's own run on that
seed and that board — one entry per war-caused `ECONOMY.TRADE_COLLAPSE`, carrying its year — and
replayed in the random arm, which removes a live tie chosen uniformly at random on
`RngPurpose.Control`, a stream no rule may read. **A scheduled removal that finds no live tie is a
halt**, not a rounding error: `RandomTieSchedule.Matched` goes false, `wb warpanel` exits 1 and the
`war − random` contrast is declared void.

Matching held on all five dry-run seeds.

---

## 3. Primary metric, and the censoring rule

**Runaway year: the first year one house holds 70% of the settled population.** Continuous, not the
Y40 pass/fail. `WorldStats.RunawayYear`, which is the figure `wb stats` already reports — not a
second definition written beside it.

The threshold form is unusable here and the brief is right about why: the null arm fails the Y40 bar
on 2 of 5 reference seeds *at ruleset 5*, so a fail-rate comparison starts from a contaminated base.

> ### The censoring rule, fixed here before any panel data exists
>
> A world that never reaches 70% is recorded as `> 51` and enters the paired contrast at **52**
> (`years + 1`). The pair is **retained**, not excluded.
>
> **Why this and not exclusion.** Retaining is conservative in the direction that matters. Where
> the null arm is censored and the treated arm is not, the true shift is at least as large as the
> measured one — 52 is a floor on the null arm's real runaway year — so the rule can only
> *understate* the war rule's effect, never inflate it. Exclusion was the available alternative and
> is rejected because it would drop exactly the worlds that stayed contested longest, which are the
> worlds the question is about.
>
> A pair censored in both arms contributes a delta of 0 and is retained.

Censoring is exercised on the dry-run seeds: 1 of 5 (seed 2025) never reaches 70%.

**Secondary, reported and not adjudicated:** distinct deep-chain shapes; event count.

---

## 4. The minimum effect worth detecting

> **MDE = 5 years of runaway shift.**

Argued from what a reader loses, and **not** from the observed 8–25.

A history's interest lives in its contested period; once one house holds 70% the remaining years
are absorption rather than history. The engine's own acceptance bar puts the contested period at
the first 40 years of 51 — that is what "no runaway faction before Y40" is asserting. Five years is
one-eighth of that contested period, and it is the smallest shift that reliably removes a complete
storyline from it: wars in the panel run 1–5 years and `MinWarYears` is 2, so five years is the
span in which a whole war can open, run and close. Below that, no arc that could have played out
fails to.

The observed 8–25 played no part in choosing it, and the number is deliberately well under that
range: this experiment is sized to detect effects considerably smaller than the ones that raised
the question.

---

## 5. Sizing

From the paired variance of the five reference seeds. **This use is fenced to sizing.** Seen data
sizes the next experiment; it does not decide the one it came from, and no verdict below is read
off these five.

```
paired sd on war − null                     10.85 years
N at that sd, MDE 5, 80% power, α = 0.05    37
one-sided 80% upper bound on sd             16.91
N at that bound                             90
```

> **N = 90 seeds, four arms, paired.**

The margin is taken in advance rather than regretted afterwards, exactly as `docs/panel-prereg.md`
§4 did. Two reasons, both stated before running: σ estimated from five observations is itself very
uncertain, and each panel seed gets **its own board** where the five reference seeds shared the
stored one, so board variance is in the panel contrast and absent from the estimate σ came from.

**Seeds 9100001–9100090.** Contiguous, disjoint from the reference seeds and from the causal-variety
panel's 9000001–9000207, and asserted to be so in code rather than by inspection. Every consumer
mixes the seed through splitmix64 before use, so adjacent seeds give uncorrelated worlds and boards.

---

## 6. The contrast family — fixed at three, and closed

| contrast | isolates |
|---|---|
| `war − null` | does the war rule move the runaway year at all? |
| `war − random` | is it *this* rule, or any loss of trade ties? |
| `collapse − null` | does the collapse cleanup move anything? |

**Holm across the three. The family is closed here and is not extended.** Enlarging it after
verdicts are reported moves thresholds under published results, which is the same move as
re-analysing seen data under a newly chosen variance.

---

## 7. The degeneracy guard

> The null arm's runaway year must have **sd ≥ 3 years** across the panel, and **at most 50%** of
> the null panel may be censored.

Below the spread minimum, the measure cannot express a 5-year effect and the comparison is a
contrast on granularity. Above the censoring ceiling, the panel is mostly worlds where the metric
never fires and the contrast is measuring censoring rather than hegemony.

**Fallback if either fires:** report the per-world deltas and the count of worlds in which the war
arm moved the runaway year at all, as description, with no contrast and no disposition. The
disposition table is not evaluated on a void comparison.

Dry run: `sd = 11.39`, 20% censored — the guard passes on the reference seeds.

---

## 8. Reachability, dry-run against the reference seeds

Every arm of every decision rule shown producible, in code, before the panel runs. Output in
`docs/ruleset-6/dry-run.txt`.

| check | result |
|---|---|
| all three disposition cells producible | **3 of 3** |
| degeneracy guard, spread arm | reachable — a flat panel returns VOID |
| degeneracy guard, censoring arm | reachable — a 60%-censored panel returns VOID |
| primary statistic varies | yes, `sd = 11.39` |
| censoring rule exercised | yes, 1 world of 5 |
| random arm matches its schedule | yes, 5 of 5 |

Two notes recorded before the run rather than discovered after it:

- **The four input combinations produce three cells**, because `war − random` is not read when
  `war − null` is null. That is the brief's table, not a defect — but it means the panel can only
  distinguish two of the three cells if `war − null` is real.
- **`collapse − null` has exactly zero variance on all five dry-run seeds.** It is not structurally
  zero — the collapse cleanup can move a world — but a contrast that cannot vary is precisely the
  failure this project has shipped twice. **If the panel also returns zero variance on it, that
  contrast reports nothing and will be said to report nothing**, rather than being read as evidence
  that the collapse cleanup is harmless.

---

## 9. Pre-committed disposition

Evaluated in code (`Disposition`), not read off a table by a person.

| `war − null` | `war − random` | disposition |
|---|---|---|
| real and clears MDE | real and clears MDE | the war rule is the cause. **Ship collapse + disuse. Card the war rule.** |
| real and clears MDE | not distinguishable | the world is knife-edge on trade ties. **Ship collapse + disuse.** The brake problem is world design, not a rule defect |
| not distinguishable | — | the five-seed panel caught a tail. **Ship all three.** |

"Real" means: survives Holm at α = 0.05 **and** the whole 95% interval clears the 5-year MDE.

---

## 10. Fixed before the run, and not reopened by it

- The collapse cleanup stands regardless of §1 — decided in the brief, not here.
- The disuse timeout stays in, **flagged untested**: one firing across five worlds. Not tuned on
  this evidence.
- Ruleset 6 is not reverted wholesale. The question is the war rule alone.
