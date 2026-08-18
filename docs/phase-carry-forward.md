# Phase: carry-forward

**Loop-prompt.** Run unattended until every halt condition holds or an abort triggers.

**Entry state:** ruleset 4, 515 tests green, working tree clean, no threshold moved. Five ruleset-4 machine baselines cut and verifying, Layer 5 passing 0/0. `BaselineArchive` carries the board and checks it against the genesis fingerprint. The geography headline is a properly powered null at N=207.

**This phase closes out. It builds nothing new.** Four carry-forward items, one of which prepares work only Shay can do.

---

## Hard budget

1. No threshold in `SimConfig` moves. Ruleset stays at 4.
2. No new mechanics, no new checker rules.
3. **No prose judgement.** Where a step needs a human to read and decide, this brief prepares the reading and halts. It does not perform it.

---

## Step 1 — The board block is spent; prepare the reference-set rebuild

**The block lifts.** `var(board)` came out negative against `var(seed)` across 207 boards. Its stated cost — paying for the reading twice if the board swaps — no longer holds, because a swap now changes only quantities measured as board-invariant, and if a future swap does move something, that is a finding requiring re-measurement regardless of whether verification happened first.

**But correct the framing before acting on it.** This is *not* a re-verification. Positions are assigned at worldgen and four mechanics consume distance, so ruleset-4 seed 42 consumes the stream differently and is **a different history**. Section 8's reference facts are not stale figures to be checked — they are facts about a world that no longer exists. There is nothing to diff.

So the job is not "re-verify seed 42." It is **establish the smallest set of hand-verified facts the test suite actually depends on**, and stage exactly those for reading. Human attention is the one non-regenerable cost in this project; spend it on the irreducible set and nothing more.

**Partition the existing reference material by what actually needs a human:**

- **Layer 3, the 31-case regression corpus** — stored passages with expected findings. **World-independent.** Needs nothing. Confirm this by running it against ruleset 4 and asserting the same 31 outcomes; if any case turns out to depend on world state, that case is misfiled and should be reported.
- **Layer 4, chronicle against log** — world-dependent but **machine-checkable end to end**. Ruler lists, departure manner, tenure spans, raid and battle counts, killings split internal/external, named years, proper nouns. Regenerate and run. No human.
- **Layer 5, golden diff** — already cut. No human.
- **The query suite, 16 questions with verified answers** — **needs a human.** The questions were authored against facts of the old history; both the questions and their answers must be re-established.
- **The canonical withheld-not-absent case** (`e:639` in the old world) — **needs a human.** An equivalent must be identified in the new history: a `[secret]` event whose subject is queryable and whose withholding is distinguishable from absence. This single case carries the v3 epistemic layer's premise, so it is worth choosing deliberately rather than taking the first match.

**Then prepare, and stop.** Produce for Shay:

- The regenerated ruleset-4 seed-42 chronicle and log.
- A machine-derived candidate facts sheet in the shape of section 8 — powers, secessions, collapses, notable figures with role-and-outcome counts, ruler tenures, secret count and candidates. **Marked throughout as machine-derived and unverified.** These are prompts for the reading, not a substitute for it.
- Candidate replacements for the 16 query questions, each with the retrieval set and machine answer, so the human step is checking rather than authoring.
- Three to five candidates for the withheld-not-absent case with the reasoning for each.

**Halt here for Shay.** Do not mark anything verified. Nothing produced by this step enters the suite as ground truth.

---

## Step 2 — Make dispersion figures self-identifying

Third instance of a verdict reported under an ambiguity in an engine figure: plague duration in two conventions, the unnamed 0-of-13 denominator, and now a range read as a spread. Three is a family.

**The project already holds this lesson from the other side** — *ambiguous engine labels are a fabrication vector independent of the model*, filed under rendering as something the engine does to the model. It generalises: **an ambiguous figure is a fabrication vector regardless of who reads it next.** Here the reader was Shay and the effect was identical — a plausible conclusion resting on a quantity whose meaning was not pinned.

**Fix mechanically, not by discipline.** Every dispersion or interval statistic the harness emits carries its kind at the point of emission — `sd=14.17`, `range=38`, `iqr=…`, `ci95=[…]`. A bare number that could be read as either is a defect. Same argument as the countables lexicon: the failure is silent, so the fix belongs in the emitter.

Add a test asserting no dispersion figure reaches a report unlabelled.

**Also record the good half:** all three ambiguities were caught, each by a different route, and every catch came from **re-deriving rather than re-reading**. That is worth naming as the working method it turned out to be.

---

## Step 3 — Log the concurrency wedge as a Stage 15 finding

Generation wedged the local model twice under concurrent load from this phase's own panel runs.

At N=207 headless with zero model calls this is an annoyance. It is also **a Stage 15 finding arriving early and cheaply**: Stage 15's economics rest entirely on the render cache, Stage 10 scales to 2,000+ actors, and at that point rendering is the bottleneck and concurrency is not optional. A generation path that wedges under concurrent load is a load-bearing defect discovered years before it would otherwise have surfaced.

- Record it on the Stage 15 card with the conditions that produced it.
- Apply the cheap mitigation now — bound concurrency on the generation path, and fail loudly rather than wedging.
- Do **not** investigate further this phase. It is recorded, not scheduled.

---

## Step 4 — Register `flat − geography`, do not run it

Arm means were flat 64.4, geography 63.1, shuffle 62.4, redraw 63.6. Geography sits below the no-distance arm. That contrast was not pre-registered and is correctly reported as description only.

Two readings, both live: distance genuinely constrains — removing options narrows what can happen — or the gap is within noise, which at these intervals it plainly is.

**Add `flat − geography` to `docs/panel-prereg.md` as a registered contrast for the next panel run, with its MDE, and run nothing now.** The panel exists and costs compute, so this is nearly free next time. Mining it from this run is not.

---

## Halt conditions

1. Layer 3 asserted world-independent against ruleset 4, or the exceptions reported.
2. Layer 4 regenerated and passing on ruleset-4 seed 42.
3. Reference-set materials staged for Shay, everything marked machine-derived and unverified, nothing entered as ground truth.
4. Dispersion figures self-identifying at emission, with a test asserting it.
5. Concurrency wedge recorded against Stage 15; concurrency bounded on the generation path; failure loud.
6. `flat − geography` registered, not run.
7. Budget intact.

**Abort** if: any Layer 3 case turns out world-dependent in a way that changes what the corpus means; the concurrency bound cannot be made to fail loudly; or any step needs a ruleset or threshold change.

---

## Escalate

- Anything requiring a judgement about what a fact *means* rather than what it is.
- Layer 3 cases found world-dependent.

---

## Record in the phase report

- **The best-executed phase in the record produced no positive result.** The null is precise — the headline interval excludes the MDE in both directions — and realised σ was 15.87 against 16.48 predicted, so the sizing held. Write it as the outcome it is, not as a disappointment.
- **Geography's design rationale is untouched.** Distance gates conflict, trade, alliance and later rumour. The variety-delta claim was volunteered, never required, and removing it improves the record.
- **What three phases of chasing a dead claim actually bought:** a probe that catches ordering bugs reading would not find; instrumentation invariance as a standing rule; RNG draw order as a Stage 3 determinism constraint; the measurement panel decoupled from the reference panel; `BaselineArchive` verifying the archived board against genesis rather than merely requiring one; and a discipline that killed its own headline twice.
- **Seed 42 at ruleset 4 is a different world, not a stale one.** The reference set is rebuilt, not refreshed. Any future ruleset change carries the same cost, and that cost is the argument for keeping the hand-verified set as small as the suite genuinely requires.

---

## Not in this phase

**Next is the workbench (Stage 5), and it needs its own scoping rather than a loop-prompt derived from this one.** The case for it is stronger than when it was deferred: every decisive moment across the last three phases was an inspection problem — a metric catching a miscalibration, a probe catching an ordering bug, a guard catching an underpowered comparison. Instrumentation for the builder is not polish. The economy half of Stage 6 waits behind it.
