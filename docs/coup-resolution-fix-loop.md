# Loop-prompt — coup resolution: the fix

Follows `coup-resolution-diagnostic-loop.md` and its report. The diagnosis stands; this round acts on it.

Halt and report after **each step**. Steps 2 and 3 are separately measurable and must be measured separately, because each moves the same population and a combined measurement cannot attribute the change.

---

## 0. What the diagnosis established

- **Nothing is unexamined.** 130 plots across five seeds, every one reached by the resolver. The missing-state-transition hypothesis was wrong.
- **The target-death gate consumes the population** — 82 of 109 lapses. A plot whose target is killed by someone else is discarded.
- **Only 2 plots in 130 ever reached the leak roll and lost it.** The dice the resolver is built around are almost never thrown.
- **No win path exists.** The sole emitter of `POLITY.COUP_RESOLVED` hard-codes `mode: exposed` and `Outcome.Failed`. `won` and `lost` are unreachable, so `CoupDecidedPct` is structurally zero for every world.
- The renderer carries a template for a covert win, and the audit carries a counter for it. Both were built expecting a branch that was never written.

**The defect underneath all of it:** in `min(70, 8 + age×6 + (100 − guile)/4)`, age feeds exposure and nothing else. For a plotter, time is pure downside — every year raises the chance of being caught and never raises the chance of striking. The plot's only reachable fates are exposure and lapse. It is not a race the conspiracy usually loses; it is a race with a finish line on one side only.

---

## 1. Step 1 — settle the denominator, before any code

`coup success > 15%` is ambiguous and the ambiguity is load-bearing. Over *decided* plots the panel has 21; over *plotted* it has 130. A fix could reach 15% of decided while coups remained irrelevant to the world.

**Decision: success is measured over plotted.** The v0 intent was a world in which power can be seized covertly. A plot that lapses is a plot that did not seize power, and excluding it from the denominator measures the resolver's internal bookkeeping rather than the world's dynamics.

Rename accordingly so the two can never be confused again — `CoupSuccessPctOfPlotted` as the invariant, with `CoupDecidedPct` retained as a diagnostic if it is useful, clearly labelled as a sub-population.

**Do not adjust the 15% threshold in this step.** If it proves to be the wrong bar over the new denominator, that is a finding to report and escalate, not to quietly lower. The standing rule holds: thresholds move by explicit decision, never to make a build pass.

**Halt when:** the invariant names its denominator, both figures are computed and reported per seed, and nothing else has changed.

---

## 2. Step 2 — a plot bids for a seat, not against a person

**Decision: the plot attaches to the seat, not to the incumbent.**

The question underneath the target-death gate is whether a conspiracy is a personal vendetta or a bid for power. It is currently modelled as the former, which is why an unrelated murder voids it. Modelled as the latter, the incumbent's death stops being fatal to the plot and becomes the plotter's opening — better history, better story, and it fits the architecture: thin entities, properties not identities. The plot targets `f:2`'s seat, not `a:50`.

Consequences to implement:

- **Target death no longer lapses the plot.** The plot continues against whoever now holds the seat.
- **Seat change no longer lapses it either**, for the same reason — that gate is second in the distribution and has the same cause.
- **The plotter taking the seat by other means** (election, open challenge, inheritance) *does* end the plot. It succeeded by other means; record it as such rather than as a covert win, and give it its own reason so it is countable.
- **The plotter's own death still ends it.** That gate is correct.
- **Lifespan and thread-lost still apply.** A conspiracy is not immortal.

Retain the reason taxonomy — every terminated plot still ends with a named reason, and the accounting identity from the diagnostic round must still balance.

**Measure before continuing.** Report the reason distribution, the leak-roll reach count, and both denominators across all five seeds. This step alone will move most of the population and its effect must be legible on its own.

**Halt when:** the identity balances on all five seeds; the reason distribution is reported; the number of plots reaching the leak roll is reported per seed, before and after.

---

## 3. Step 3 — the leak roll becomes three-way

**Decision: expose, strike, or defer** — with maturation rising alongside exposure rather than exposure rising alone.

This restores the missing symmetry directly. Age currently feeds only the exposure term; give readiness its own term so that a plot which survives becomes both more likely to be caught *and* more likely to act. The plotter's guile should reduce exposure and support readiness; the target's own standing or vigilance is the natural counterweight, if the engine has such a quantity to hand.

Constraints:

- **Both new branches must be reachable and must be reached.** The renderer's covert-win template at `EventTemplates.cs:154` and the audit's `won`/`lost` counters exist and are currently dead. After this step they must be exercised on the panel — assert it.
- **A covert win must produce a seat change**, with the same downstream consequences as any other succession. A win that does not move the seat is a cosmetic event.
- **The audit's `abandoned` branch is also unreachable.** Either give it an emitter or delete it — an unreachable case in a switch is a claim about the world that nothing can make true.
- **Pick starting constants by reasoning, not by fitting.** State the reasoning in the report. Then measure.

**Halt when:** all three branches fire on the panel; a covert win moves a seat; both invariants are computed over the stated denominator and reported per seed; no threshold has been altered.

---

## 4. Step 4 — the general guard: an invariant that cannot vary is not an invariant

`CoupDecidedPct` reported a plausible number for months while being structurally incapable of any other value. The threshold was tuned against it and the invariant reported green against it. That is a defect class, not an incident.

**Add a reachability assertion to Layer 1.** For every dynamics metric that is a ratio or a rate, assert that at least one code path can move it — minimally, that every branch feeding its numerator is reachable and is exercised somewhere on the panel.

A metric whose numerator has no reachable emitter must **fail loudly at definition time**, not report zero. Zero and impossible are different, which is the same absent-versus-withheld distinction this project has now met in the checker, the query layer, the plot ledger and here.

**Halt when:** the reachability check runs over every Layer 1 ratio metric; deliberately making a numerator branch unreachable fails the check; the check is in the standard suite.

---

## 5. Step 5 — ruleset version and the baseline

This round changes simulation rules, so **`ruleset_version` becomes `"2"`.** This is the first time that counter does work, and the reason it was separated from `engine_version` last round.

Consequences that must be handled explicitly rather than discovered:

- **Seed 42 now renders a different world.** That is correct and expected.
- **The sealed baseline stays sealed and untouched.** It is the v1 baseline under ruleset 1. Create-only; nothing here moves it aside.
- **Layer 5's anchor is ruleset-scoped.** The golden diff must read the baseline's `ruleset_version` and, when the current ruleset differs, report **skipped — baseline is ruleset 1** with the reason stated. It must not fail silently, must not pass by comparing incomparable worlds, and must not update the anchor.

A new baseline under ruleset 2 requires a render round, which is generation and out of scope here. Record it as the next artefact-cutting round.

**Halt when:** the header carries `ruleset_version: "2"`; Layer 5 skips with a stated reason rather than failing or passing; the sealed baseline is unchanged and `.sealed` verifies.

---

## 6. Prohibitions

1. **No threshold changes.** Not 15%, not any dynamics bar. If a bar proves wrong over the new denominator, report it.
2. **Nothing leaves `KnownFailing` by hand.** The existing test forces the list to shrink when a threshold starts holding on its own; that is the only mechanism.
3. **No tuning before measuring.** Steps 2 and 3 each report before the next begins. Constants chosen by reasoning, stated in the report, then measured — not fitted to hit 15%.
4. **The sealed baseline is read-only.**
5. **The plot ledger stays out of the event log** and stays provably harmless — `AttachingTheLedgerChangesNothingAboutTheWorld` must still pass with bit-identical reruns.

---

## 7. Exit criterion

Per the standing rule, a harness number rather than a feeling:

**Both coup invariants hold on all five seeds, over the stated denominator, with no threshold having moved** — and both leave `KnownFailing` by holding rather than by edit.

If they do not hold, the round still succeeds provided every plot is accounted for and the reason distribution explains why. A world where covert seizure is rare but possible is a legitimate answer; a world where it is impossible is not. The difference must be visible in the numbers, not argued.

## 8. Abort conditions

- Any step would require changing a threshold to pass.
- The accounting identity fails to balance and the shortfall cannot be named.
- A covert win is emitted that does not move a seat.
- Layer 5 either fails or passes against a ruleset-1 baseline instead of skipping.
- The ledger changes the world.
- The sealed baseline is modified in any way.
