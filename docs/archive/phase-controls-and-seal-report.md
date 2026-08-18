# Phase: controls, clamp, seal — ABORTED at step 1

Ruleset `4`, engine `1.2.0`, **501 tests green** (was 493). No threshold moved,
no ruleset change, no new mechanics, no new checker rules. All control worlds
marked in their headers and in their records.

**Two abort triggers fired.** The redraw control reproduced the gain and the
degeneracy guard fired on the same comparison; and the alliance clamp hypothesis
was falsified. Steps 2, 4 and 5 were not run. **No ruleset-4 baseline was cut
and nothing was sealed.**

Steps 0 and 3 are complete: step 0 because the brief asks for it first and
everything after benefits, step 3 because it is independent of step 1 and
because it corrects a figure I published wrong.

---

## Step 1 — the redraw control falsified the explanation

**Construction.** Every proximity the four mechanics read was replaced with a
value drawn per decision from that world's own realised distribution — same
distribution, same clamp exposure, **no stability and no spatial structure**.
Drawn from `RngPurpose.Control`, a stream no rule can reach.

### The attributability check came first, and it caught something

Before any control figure was read, the identity control — which routes every
proximity through the same substitution machinery and hands back what the board
said — was required to reproduce the real world exactly.

**It did not, and the diagnosis matters.** Of 898 events on seed 42, **897 were
byte-identical**; the single difference was the genesis event's `control` marker,
which a control run is required to carry. The machinery consumes nothing from
the streams the rules draw on. The assertion was then tightened rather than
loosened — every event after genesis byte-identical, and the genesis events
differing in nothing but the marker — which is a stronger statement than the one
that failed.

Without that check, every number below would be confounded with RNG
re-sequencing, and the confounding would be invisible.

### The measurement

Three arms, all against the sealed ruleset-3 baselines:

| seed | ruleset-3 | geography | Δ | redraw | Δ |
|---|---|---|---|---|---|
| 7 | 42 | 45 | **+3** | 62 | **+20** |
| 42 | 88 | 99 | **+11** | 116 | **+28** |
| 99 | 74 | 69 | **−5** | 58 | **−16** |
| 1234 | 64 | 97 | **+33** | 83 | **+19** |
| 2025 | 56 | 66 | **+10** | 52 | **−4** |

Redraw is deterministic across runs; verified by re-running the whole panel.

### The rule, applied

- **Sign matches on ≥4 of 5?** Yes — 4 of 5. Only seed 2025 diverges (+10 vs −4).
- **Panel median delta ≥ half geography's?** Geography's median is **+10**, so
  the bar is **+5**. Redraw's median is **+19**. Yes, and by nearly four times.

**On its face: redraw reproduces the gain.** A control with no spatial structure
and no stability at all — where two houses are neighbours this year and
strangers the next — moves causal variety further than geography did.

### The degeneracy guard fires, and it supersedes

The three arms' panel median deltas are 0, +10 and +19: a band of **19**. The
between-seed spread *within* an arm is **38** for geography (−5 to +33) and
**44** for redraw (−16 to +28).

The band is narrower than the within-arm spread. **The guard fires, so the rule
is void rather than passed.** At n=5, with per-seed deltas of that range, a
comparison of medians separated by 19 cannot discriminate between these arms.

**Both routes abort, so nothing turned on which applies.** But the distinction
is the honest one and it is stated in that order deliberately: I cannot claim
"redraw reproduces the gain" as a measured result, because the instrument that
would establish it is not sharp enough. What I can claim is weaker and still
damaging.

### What can be claimed

**A structureless control moved the metric at least as much as geography did,
and the panel is too small to tell the two apart.**

That is enough to establish the thing that matters: **the geography result was
never distinguishable from noise injection at this panel size.** The §6
explanation — distance makes which neighbour you fight a stable fact, and stable
facts let chains grow long — requires stable heterogeneity. Redraw has none, and
produced the same or a larger effect. The explanation is not supported, and it
was never tested by the evidence that was said to confirm it.

**The last two phases' headline should be read down accordingly.** The
pre-registered §6 prediction was falsified, the pre-registered alternative
matched the direction, and direction matching was never evidence between two
mechanisms that both predict the direction. That was stated in the brief before
this measurement existed, and the measurement bears it out.

**Step 2 was not run.** It is gated on step 1 clearing and step 1 did not clear.
Its construction is built and tested — `ProximityControlKind.Shuffle`, one draw
per unordered place-pair, fixed at worldgen, verified stable and symmetric — so
it can be run on a word. It would now be answering a narrower question than it
was written for: not "is geography the source" but "can anything be shown at
n=5".

---

## Step 3 — the alliance clamp hypothesis is falsified

Measured mechanically rather than statistically, as the brief requires. Per
alliance evaluation: could **any** distance value in the world's realised
proximity range have changed the post-clamp figure the rule actually uses? That
is a binary property and needs no sample.

**Absorbed 0 of 13. Pooled, and on every seed.**

| seed | absorbed / evaluations |
|---|---|
| 7 | 0/1 |
| 42 | 0/4 |
| 99 | 0/4 |
| 1234 | 0/2 |
| 2025 | 0/2 |
| **pooled** | **0/13 (0%)** |

The rule required absorption in at least 12 of 13 to hold. It is **0**. In every
single evaluation, varying distance across the range this world can present
*would* have changed the post-clamp value. **The clamp is not swallowing the
distance term. The hypothesis is dead.**

Per the brief: halt and escalate, and do not search for a replacement
explanation in the same run. None was sought.

### The arithmetic correction I owe

The previous report said seeing 0 of 13 by chance was "about one in eight". The
arithmetic was right for the rate it assumed and **the rate was never named**,
which is the defect. Corrected, with denominators stated:

| baseline flip rate | P(0 of 13) | |
|---|---|---|
| 15% — the unnamed assumption behind "one in eight" | 0.121 | ~1 in 8 |
| 6.0% — panel-wide, all four mechanics | **0.447** | **~1 in 2.2** |
| 6.3% — the non-alliance mechanics, the fairer comparator | **0.429** | **~1 in 2.3** |

**The right figure is about 45%, not one in eight.** Alliance moving zero times
in thirteen is close to a coin flip against the rate distance moves anything
else, and my report presented it as a nine-tenths-unlikely event. A wrong engine
figure is worse than a wrong model figure because nothing questions it, and this
one was mine.

The 6.3% denominator — the pooled flip rate over the *other* mechanics — is the
fairer comparator and is named as such. Alliance's own rate cannot serve, since
it is the quantity under test.

**So 0 of 13 needs no explanation at all.** It is what a 6% process looks like
half the time at n=13. What actually stands is: alliance's distance term is not
absorbed, is not decorative on inspection, and has simply not been observed to
flip anything — and thirteen evaluations cannot tell those apart.

### The family this belongs to

Recorded as the brief asks. This would have been the third appearance of *correct
rule, input never arrives* — after the checker's five and the covert coup's
structural zero. **It is not one.** The input arrives, the rule reads it, and the
term is live in every evaluation. What produced the appearance was a small
sample read against an unstated baseline.

The lesson generalises in the opposite direction to the one anticipated: the
silent-path family has a mirror image, where a healthy mechanism is diagnosed as
a silent path because n is small and nobody wrote the denominator down. Both are
failures to state what would count as normal.

---

## Step 0 — two findings promoted to standing rules

**Instrumentation invariance.** Attaching a measurement must not change the
world, asserted by hashing the full event log with and without it across the
whole seed panel. Now a standing test over the whole mechanic set rather than
probe scaffolding: every available sink is attached alone and in combination,
and all must leave the log identical — plus an assertion that the sinks actually
observe something, or the invariance is satisfied by instruments that never fire.

**RNG draw order is load-bearing**, recorded as a Stage 3 determinism constraint
where the guarantee is specified. Reproducibility is not a property of the rules
alone; it is a property of the rules *and the order in which they consume the
stream*. The worked example is kept because it is the only one anybody will
believe: the conquest site short-circuits, so the die is thrown before the holder
check, and hoisting that check into the guard — obviously equivalent, and what
anybody would write — silently stops drawing in those cases and re-sequences
everything after. Every test stayed green. The log hash is the only detector.

**Applied to my own work in this phase.** The alliance site was refactored to
name its scaling expression once so the absorption measurement could not drift
from the decision it measures. Under the new rule that is a behavioural change
until a hash says otherwise, so it was measured: all five seeds byte-identical.

---

## Steps 4 and 5 — blocked, and the block named

**Step 4 (board geometry sensitivity)** is gated on steps 1 and 2 clearing. Step
1 did not clear. The prereg amendment it asks for was therefore **not** made —
amending a pre-registration for a measurement that cannot run would put a
rationale in the record for a number nobody took. `docs/phase-explain-decide-seal-prereg.md`
stands unamended and unspent.

**Step 5 (`BaselineArchive` carries the board, then cut)** is gated on steps 1,
2 and 4. Nothing was cut. The v1 and ruleset-3 baselines are untouched and still
verify.

The gap stands as reported last phase: **from ruleset 4, a world is a log and its
board**, and `BaselineArchive` does not carry the board, so a ruleset-4 baseline
cut today would be sealed, hash-verified and unreadable. It fails loudly rather
than silently, because `Replay` refuses a world whose board fingerprint does not
match. Not fixed here — the brief puts the fix with the cut, and the cut is
blocked.

---

## Budget

| constraint | held |
|---|---|
| No `SimConfig` threshold moved | ✅ — including the one the clamp finding would have suggested, which is moot now the hypothesis is dead |
| Ruleset stays at 4 | ✅ |
| No new mechanics, no fifth distance consumer, no new checker rules | ✅ — asserted by a test that the probe sees exactly five sites across four mechanics |
| Control outputs quarantined | ✅ — marked in the header *and* in the genesis event, `wb baseline cut` refuses one, and a real world carries no `control` key at all rather than an empty one |

---

## What I need from you

1. **The geography result is not supported by the evidence that was said to
   support it.** A structureless control matched or exceeded it. What stands
   from the last two phases needs a decision, and the brief says that decision
   is yours.
2. **n=5 cannot discriminate these arms.** The degeneracy guard fired on its
   first use, which suggests it should have existed two phases ago. Every
   comparative claim made across this panel is subject to it — including
   "geography improved four seeds of five".
3. **Alliance 0 of 13 was never surprising**, and my "one in eight" was wrong by
   a factor of about four. Corrected above. Nothing about alliance needs fixing
   on this evidence.
4. **Step 2 is built and can run on a word.** It would now be testing whether
   anything can be shown at this panel size, rather than what it was written to
   test.
5. **The baselines remain uncut**, and the board-in-baseline gap remains open.
