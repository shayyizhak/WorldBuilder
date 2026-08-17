# Phase: explain, decide, seal

**Loop-prompt.** Run unattended until every halt condition holds or an abort triggers. Decision rules are pre-committed below — do not round-trip for them.

**Entry state:** ruleset 4, 483 tests green, geography half of Stage 6 complete, four commits on `main`. Five sealed ruleset-3 baselines. No ruleset-4 baselines cut.

**This phase adds no mechanics and no simulation behaviour.** It explains two outstanding results, resolves one dependency, repairs one tool, and seals. If you find yourself changing what the simulation does, you are out of scope — abort and report.

---

## Hard budget

Pre-committed, because these are the ways this phase would quietly become a different phase:

1. **No threshold in `SimConfig` moves.** Not one, for any reason, including a measurement that appears to justify it. Constants are chosen by rationale before measurement; a threshold moved after seeing seed 99's numbers is fitting. If the measurement genuinely argues for a change, surface it and halt — do not spend it.
2. **Ruleset stays at 4.** A ruleset change invalidates the ruleset-3 baselines wholesale and makes step 4 pointless.
3. **No new mechanics, no fifth consumer of distance.**
4. **No new checker rules.** In particular, nothing for geography-in-prose — that rule ships with the terrain pack, not before it, and a rule written now extracts 0 forever and baselines FLOOR at 0.

Anything outside this budget is surfaced in the report, not silently spent.

---

## Step 1 — Explain seed 99, and seed 7 alongside it

Seed 99's `distinct deep-chain shapes` went 74 → 69 while four seeds rose by up to +33. Seed 7 sits at 45 against a threshold of 60, up from 42.

**Hypothesis under test:** on seed 99, the places are sited closely enough relative to each other that realised proximity is near-uniform, so distance discriminates between candidate targets rarely or never, and the four mechanics fall back to something close to pre-geography behaviour.

**Measure, per seed, across all five, from the stored boards — regenerate nothing:**

- Inter-place separation for the places the world actually has: median, min, max, and spread (IQR, or coefficient of variation — pick one and use it consistently).
- The distribution of realised proximity percentages across the run, not the board's theoretical range.
- **Discriminating share:** of the decisions where a mechanic ranked candidate targets, the fraction where the proximity spread across candidates was wide enough to change which candidate won. Compute this by re-ranking each decision with proximity held flat and counting where the winner changes. This is the measure that matters — the other two are context for it.

**Pre-committed decision rule.** The rules are comparative, deliberately, so that no absolute constant has to be invented here:

- **Hypothesis holds** if seed 99 ranks lowest or second-lowest of the five on *both* separation spread and discriminating share, **and** its discriminating share is below half the panel median. → Record the seed 99 regression as explained by board geometry. Change nothing. Note in the report that this makes board geometry a first-class variable, which is the input to step 2.
- **Hypothesis fails** if seed 99's spread and discriminating share are comparable to the rest of the panel. → The regression is unexplained and something else is going on. **Halt and escalate.** Do not look for a second explanation in the same run; a second hypothesis found by searching the data after the first failed is not pre-registered.
- **Mixed** — one criterion met, not the other. → Report both figures, mark partially explained, halt.

**Seed 7** gets the same measurement and appears in the same table, but no separate decision rule. It stays a `KnownFailing` entry with its category and rationale, and it leaves only by holding. Do not drop it and do not re-baseline it.

---

## Step 2 — Is board geometry a first-class variable?

This decides whether the ruleset-4 baselines can be sealed against a `wb map make` board or must wait for a real Azgaar export.

**Test.** Generate several `wb map make` boards at different generator seeds, confirm they differ meaningfully in separation distribution (if they do not, say so — that is itself the answer), and re-measure the four distance attributions on each.

**Pre-committed decision rule:**

- **Attributions move materially across boards** → geometry is a first-class variable. **Halt.** Do not seal. Sealing against one board's geometry would calibrate against a distribution that is not the one being shipped, and the hand verification would be paid for twice.
- **Attributions hold across boards** → proceed to step 4, and record the residual risk explicitly: this test can demonstrate sensitivity strongly but insensitivity only weakly, because every board in the sample comes from one generator and may share characteristics Azgaar's do not. State that limitation in the report in those terms. Do not report it as "geometry does not matter."

Either way, the importer stays as it is. It is built, tested, and format-identical; nothing here is about import work.

---

## Step 3 — `wb test corpus`

Broken since ruleset 2. The pristine ruleset-3 binary fails identically, so it is not geography's doing. The fix exists in the test suite and was never applied to the CLI's copy of the same idea.

- **Fix by removing the duplication, not by copying the fix across.** The CLI and the suite should not hold two implementations of one idea. Layer 4 duplicating the *checker* is deliberate — duplicated verification is the property being bought. Two copies of one *implementation* is a defect, and this is what that defect costs.
- **Then wire it into the standing halt list.** The substantive failure is not the bug; it is that a tool broke across two rulesets without failing anything. A fix that leaves it outside the halt conditions has fixed the smaller half.

---

## Step 4 — Cut ruleset-4 baselines

**Gated on steps 1 and 2 both clearing.** Do not start this if either halted.

- Same procedure as the ruleset-3 cut. `wb baseline cut` reads the producing engine from the world file's own header, not the build running it — keep that property.
- Cut across all five seeds.
- v1 stays untouched. The ruleset-3 baselines stay sealed; they are not superseded by these and are not to be deleted.
- Layer 5 unskipped and passing on every new baseline before the cut is considered done.

**Before sealing, read this.** Worlds are disposable and freely regenerable — that is settled. The *hand verification* of a baseline is not: the mapped fabrications, the query answers, the reference facts. That is the one cost in this project that is paid in human attention and cannot be regenerated. Seal deliberately, not as a formality.

---

## Halt conditions

Report and stop when **all** of these hold:

1. Seed 99 is explained under the step 1 rule, or halted as unexplained/partial.
2. Seed 7 measured and recorded, `KnownFailing` intact with category and rationale.
3. Step 2 resolved either way, with the residual-risk limitation stated if attributions held.
4. `wb test corpus` passing, single implementation, present in the standing halt list.
5. Ruleset-4 baselines cut across five seeds — or explicitly blocked by step 2, with the block named.
6. No `SimConfig` threshold moved, ruleset still 4, no new mechanics, no new checker rules.

**Abort immediately** if: a fix requires moving a threshold; step 1's hypothesis fails; the corpus fix cannot be done without duplicating an implementation; or any step needs a ruleset change.

---

## Escalate to Shay, do not resolve

- Any question of what a measurement *means* rather than what it is.
- Step 1 failing or landing mixed.
- Step 2 finding geometry material — the follow-on question is whether to source a real export before sealing, and that is a scheduling call, not a technical one.

---

## Record in the phase report

Beyond the results themselves:

- **The calibration catch.** Proximity was first scaled against the board's median cell separation — arithmetically fine, useless in practice, because places are sited deliberately far apart and no world contained two at that distance. Every mechanic was discounted everywhere while the comments claimed it was centred. The metric caught it; the comments did not.
- **An amendment to the constants principle.** "Rationale before measurement" forbids tuning a value to obtain a wanted result. It does not forbid checking that the reference population is the right one — that check is what saved this phase. Write next to the constants that **no threshold moved, but the meaning of their input did**. Without that line, "no threshold moved" reads in six months as "nothing changed."
- **The falsified §6 prediction.** Causal variety was predicted to fall on the reasoning that three of four changes add inputs rather than branches. It rose on four seeds of five, by up to +33, matching the pre-registered alternative: distance makes which neighbour you fight a stable fact, and stable facts let chains grow long. `verbatim repeat rate`, parked as unattributed across two rounds, cleared everywhere without being aimed at.
- **The checker fingerprint cross-check.** `60f5b325`, byte-for-byte the figure computed by hand for v1. Worth more than the tool that produced it.

---

## Not in this phase

Named here so it is not picked up by accident:

- Geography in prose. Contract card filed; the rule ships with the terrain pack.
- Stage 6's economy half.
- Anything touching content packs, species, or settings.

**The phase-level call after this one is Shay's**, and the recommendation is the workbench before economy. A world with geography now exists, which was the stated precondition. More to the point, this phase's largest win was a metric catching a calibration error, and both of its loose ends are inspection problems. Economy roughly doubles the attributions needing near/far-style measurement. Build the instrument before the harder phase, not after it.
