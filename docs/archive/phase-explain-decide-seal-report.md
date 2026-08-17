# Phase: explain, decide, seal — ABORTED at step 1

Ruleset `4`, engine `1.2.0`, 493 tests green. No threshold moved, no ruleset
change, no new mechanics, no new checker rules.

**Step 1's hypothesis failed. Per the brief's abort list, steps 2 and 4 were not
run and no ruleset-4 baseline was cut.** Step 3 was completed, because it is a
repair with no dependency on step 1's outcome.

---

## The decision, first

**Hypothesis:** on seed 99 the places are sited closely enough relative to each
other that realised proximity is near-uniform, so distance rarely discriminates
and the four mechanics fall back to near pre-geography behaviour.

**Verdict: failed.** Seed 99's separation spread and its discriminating share
are both comparable to the rest of the panel, which is the brief's stated
failure condition. Its discriminating share is not merely comparable — it is
**above** the panel median. The seed where geography supposedly did least is the
seed where it discriminated more than the median.

Per the brief: *"The regression is unexplained and something else is going on.
Halt and escalate. Do not look for a second explanation in the same run."* No
second explanation was sought.

### The figures the verdict rests on

Separation between the places each world actually has, in the board's own cost
units. Spread is the coefficient of variation — standard deviation as a
percentage of the mean — fixed in the pre-registration before measuring, and
chosen over an IQR because this figure gets compared across boards.

| seed | pairs | min | median | max | spread |
|---|---|---|---|---|---|
| 7 | 21 | 23 | 43 | 78 | 37% |
| 42 | 21 | 18 | 36 | 71 | 36% |
| 99 | 21 | 20 | 33 | 70 | **35%** |
| 1234 | 21 | 22 | 40 | 77 | 36% |
| 2025 | 21 | 24 | 46 | 75 | 34% |

The proximities those places actually presented to a rule:

| seed | lowest | median | highest | width |
|---|---|---|---|---|
| 7 | 71 | 100 | 130 | 59 |
| 42 | 67 | 100 | 133 | 66 |
| 99 | **64** | **100** | **124** | **60** |
| 1234 | 68 | 100 | 129 | 61 |
| 2025 | 76 | 100 | 131 | 55 |

Discriminating share — of the decisions distance had room to move, the share
where holding proximity flat at 100 changes the outcome, with the random draw
held fixed:

| seed | consulted | open | moved | share |
|---|---|---|---|---|
| 7 | 70 | 58 | 2 | 3% |
| 42 | 147 | 108 | 6 | 5% |
| 99 | 172 | 149 | 9 | **6%** |
| 1234 | 154 | 123 | 11 | 8% |
| 2025 | 137 | 117 | 6 | 5% |

Panel median share 5%; half of it is 2%.

### Applying the rule, clause by clause

- **Lowest or second-lowest on separation spread?** Yes — 2025 (34%) < 99 (35%)
  < 42 = 1234 (36%) < 7 (37%). Seed 99 is second-lowest.
- **Lowest or second-lowest on discriminating share?** No. 7 (3%) < 42 (5%) =
  2025 (5%) < **99 (6%)** < 1234 (8%). Seed 99 is fourth.
- **Share below half the panel median (2%)?** No. It is 6%, three times it.

**Two of three clauses fail, and they are the two that carry the hypothesis.**

A strict rank reading would call this "mixed" — one criterion met, two not — and
mixed also halts. It is reported as *failed* rather than *mixed* because the one
criterion that passed is an artefact of ranking five numbers inside a
three-point band: every seed's separation spread lies between 34% and 37%. Being
second-lowest of that is noise, not signal, and treating it as half an
explanation would be generous to a hypothesis that the decisive figure
contradicts outright. **Which of the two readings is right is a question about
what the measurement means rather than what it is, and the brief sends those to
you.** Both readings stop the phase, so nothing turned on it.

---

## The finding that matters more than seed 99

**Across the whole panel, distance changes the outcome of about one decision in
twenty.** 680 decisions consulted a proximity, 555 had room to be moved by one,
and **34 were.** A pooled discriminating share of 6%.

Pooled by mechanic:

| mechanic | moved / open | share | kind |
|---|---|---|---|
| marriage | 10 / 61 | 16% | ranking |
| conquest | 4 / 28 | 14% | roll |
| raid targeting | 5 / 74 | 7% | ranking |
| war declaration | 15 / 379 | 4% | gate + ranking |
| **alliance** | **0 / 13** | **0%** | roll |

That reframes the previous phase's headline. Causal variety rose by up to +33
and `verbatim repeat rate` cleared everywhere, and the mechanical intervention
that produced it changed **thirty-four decisions across five fifty-year
histories**. Geography is not re-weighting the world broadly; a very small
number of decisions are carrying the whole effect.

This is consistent with the alternative pre-registered last phase — *distance
makes which neighbour you fight a stable fact, and stable facts let chains grow
long* — and it sharpens it considerably. It is not that many decisions shifted
slightly. It is that a handful shifted, early, and the histories diverged from
there. **That is a stronger and more interesting claim than the one the last
report made, and it is also more fragile**, because an effect carried by
thirty-four decisions is an effect a different board could plausibly not
produce. Which is precisely what step 2 exists to test, and step 2 did not run.

### Alliance never once moved

**0 of 13.** Distance has not changed a single alliance outcome anywhere on the
panel. That is the profile of a decorative branch — the exact class the engine
dynamics phase existed to remove, arriving through a new door.

n = 13 is too small to call it unreachable: at a true rate of 15% the chance of
seeing none in thirteen is about one in eight. So this is **suggestive, not
conclusive**, and it is stated that way deliberately.

The likely mechanism, offered as a hypothesis and **not confirmed**: alliance
appeal is clamped to 5–90 before the roll, and if appeal usually lands outside
that band the distance multiplier is absorbed by the clamp before it can reach
the dice. Confirming that would mean instrumenting the clamp, which is a second
investigation and outside this phase.

### A caveat on war declaration's denominator

Its 379 "open" decisions are *evaluations*, not declarations — the gate is
tested every time a revenge goal is acted on, most of which never become a war,
and the `PursuePlace` weighting is counted alongside. So 4% understates
distance's effect on wars that actually happen. The figure is comparable across
seeds and boards, which is what it is for; it is not a statement about
declarations.

---

## Step 2 and step 4 — not run

Both were skipped, and the brief requires the block to be named.

**Step 4 (cut ruleset-4 baselines)** is explicitly *"gated on steps 1 and 2 both
clearing. Do not start this if either halted."* Step 1 halted. Nothing was cut,
nothing was sealed. The five ruleset-3 baselines stand untouched and the v1
baseline is unmodified.

**Step 2 (is board geometry first-class?)** was not run, on the brief's abort
instruction. It is also the natural next thing to run, and its pre-registered
decision rule is already written down and unspent — see
`out/stage-6b/pre-registration.md`, which fixes "materially" as *between-board
variation exceeding within-board variation, for any one mechanic* and names the
five boards to test. That was fixed before any figure was seen and can be
executed as-is whenever you decide to.

**One thing found while preparing step 4 that will matter when it runs.**
`BaselineArchive` does not archive the board. A ruleset-4 world is a log *and*
its board — that is the whole point of the bundle — so a ruleset-4 baseline cut
today would be sealed, hash-verified, internally consistent and **unreadable**,
because nothing in it says which map its cell indices refer to. It would fail
loudly rather than silently, since `Replay` refuses a world whose board
fingerprint does not match, so this is a gap rather than a trap. It is not fixed
here: the fix belongs with the cut, and the cut is blocked.

---

## Step 3 — `wb test corpus`

Complete. Both halves.

**One implementation, not two copies.** The previous phase had already shared
the *path resolver*; that was the smaller half and it left both callers still
able to disagree about what to do with the path. The whole policy — seed 42 from
the sealed record, everything else simulated — now lives in `Corpus.WorldFor`,
and `Corpus.RunAll` is the single entry point. `wb test corpus` calls it and
`BaselineWorld.ForSeed` delegates to it. Layer 4's duplication of the *checker*
is untouched and stays: duplicated verification is the property being bought
there, and duplicated implementation is not the same thing.

**And it is in the standing halt list.** The substantive failure was never the
bug — it was that a tool broke across two rulesets without failing anything,
because the only thing asserting about the corpus was a test class with its own
way of getting a world. `CorpusTests.TheCommandsOwnEntryPointPassesEveryRow`
now asserts on `Corpus.RunAll`, which is what the command runs. 34 rows, 34
pass, through both paths.

---

## The counterfactual probe

Step 1's central measure needed instrumentation that did not exist, and the
brief's phrasing — *"re-ranking each decision with proximity held flat and
counting where the winner changes"* — is a counterfactual rather than a survey.
Counting how often a rule reads a proximity says nothing; every raid reads one.

`GeographyProbe` records, at each of the six sites across the four mechanics,
whether the outcome would have differed with proximity held at 100.

**The property that had to be true before any of its numbers were worth
reading: attaching it cannot change the world.** Every site was restructured to
take its single random draw into a variable and compare it against both lines,
and `Rng.WouldPick` re-picks from other weights at the same relative position
without drawing at all. This is asserted rather than reviewed —
`AttachingTheProbeChangesNothingAboutTheWorld` hashes the full event log with
and without the probe on all five seeds — because a counterfactual that takes
its own draw shifts every later stream in the year and the run still looks
perfectly plausible.

One ordering bug was caught by that discipline and not by reading. The conquest
site's original condition short-circuits as `attackerWon && margin > … &&
rng.Chance(…) && field.Controller == defender.Id`, so the die is thrown *before*
the holder is checked. The obvious restructuring hoists the holder check into
the guard and silently stops drawing in cases that used to draw. The world hash
caught it immediately.

The measured ruleset-4 figures are byte-identical to the sealed ones from the
previous phase, so the restructuring changed nothing.

---

## Budget

| constraint | held |
|---|---|
| No `SimConfig` threshold moved | ✅ none touched |
| Ruleset stays at 4 | ✅ |
| No new mechanics, no fifth distance consumer | ✅ — asserted by a test that the probe sees exactly five sites across four mechanics |
| No new checker rules | ✅ none added |

Nothing outside the budget was spent. The board-in-baseline gap above is
surfaced, not fixed.

---

## What I need from you

1. **Seed 99 is unexplained.** The hypothesis is dead; its regression has no
   cause on file. The brief forbids searching for a second explanation in this
   run, correctly.
2. **Failed or mixed?** Two of three clauses fail and the third passes on a
   three-point band. I read that as failed. It is a question of what the
   measurement means, which is yours.
3. **Alliance at 0 of 13.** Distance may be decorative in one of the four
   mechanics. The clamp hypothesis is unconfirmed and confirming it is its own
   piece of work.
4. **Step 2 is ready to run with its rule already fixed**, and it now carries
   more weight than when it was written: an effect carried by thirty-four
   decisions is one a different board could plausibly fail to produce.
5. **The board must join the baseline contents before any ruleset-4 cut.**
