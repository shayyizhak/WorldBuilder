# Phase — the four dead event kinds

**Loop-prompt.** Halt conditions in §5. The queue extends itself under §6.

**Contingent on a decision not yet taken.** This assumes Stage 6's economy half and Stage 5's workbench both stay parked and the next work is world content. If that decision goes the other way, this brief is shelved, not adapted.

## 0. Why

`ECONOMY.TRADE_COLLAPSE`, `DIPLO.ALLIANCE_BROKEN`, `CONFLICT.SIEGE` and `INTRIGUE.GRIEVANCE_SETTLED` are declared, named and rendered, and emitted by nothing. That is the covert-coup structural zero four more times: vocabulary and renderer already paid for, emission missing.

Since Stage 2, nothing shipped has been a new *kind of thing* in the world. This is the smallest available piece of work that is, and it is bounded by construction — the kinds already exist, so the scope is emission, not design of a new domain.

**Justification does not rest on the measurement in §3.** Four event kinds that exist in the vocabulary and never occur are a defect on their own terms: they are dead weight in the renderer and a lie in the schema. Ship them because of that. §3 tests a separate belief and must not gate §2.

---

## 1. Substrate audit — do this first, and let it shrink the phase

For each of the four kinds, establish from the code what state it would need and whether that state exists:

- Does the engine track the *thing* the kind is an event about? (alliances, trade routes, siege state, grievances)
- Is there a point in the tick where its trigger condition could be evaluated without new state?
- Is there an existing kind it would need to interact with, and does that interaction already have a shape?

**Pre-committed drop rule.** A kind requiring new persistent state is **dropped from this phase** and recorded as a backlog card naming the state it needs. It is not designed here. Dropping three of four and shipping one is a success; expanding scope to keep all four is the failure this rule exists to prevent.

Expected shape, to be confirmed or refuted rather than assumed: `ALLIANCE_BROKEN` has its substrate; `TRADE_COLLAPSE` almost certainly does not and is the economy half in disguise; `SIEGE` and `GRIEVANCE_SETTLED` unknown.

**HALT and report after §1** with the audit table and the surviving set, before writing any emitter.

---

## 2. Emission

For each surviving kind:

1. **Trigger condition**, stated as a rule over existing state. Written before it is coded.
2. **Branches.** A kind that resolves one way is a latent fabrication risk — the model scores well guessing the majority case and gets the rare case confidently wrong. Every kind lands with at least two genuinely reachable outcomes, and reachability is demonstrated by observing both across the five seeds, not argued.
3. **`causes` edges.** What antecedents attach, and what the kind can itself cause. A kind that causes nothing is a leaf and adds no chain depth; say so explicitly if that is the intent.
4. **Renderer check.** The renderers exist. Confirm they are not stubs and that they render a missing input as omission rather than connective text.

**Constants by reasoning, never by fitting.** Any rate or threshold introduced here is argued from what the mechanic represents, before any run. A constant chosen to make a number come out is a finding against this phase.

**Mechanic-change budget applies.** Four emitters is a mechanic change. Escalate rather than override if the budget binds.

**Instrumentation invariance and the with/without log hash** apply to anything added for measurement. RNG draw order is load-bearing: adding an emitter changes the stream, which is expected — but adding a *probe* must not.

**No checker rule for the new kinds yet.** Same reasoning as geography: a rule written before the construction can occur extracts 0 forever, `rule-inert` cannot fire, and the floor baselines at 0. Once a kind is observed emitting across seeds, revisit whether it joins Tier 3's mandatory classes.

---

## 3. The measurement, and its control

The standing belief is that causal variety tracks how many mechanics have genuinely reachable branches. This phase adds branches, so it is the natural test. It is also the exact situation where the geography phase went wrong, so:

**Three arms, paired on the same seeds:**

| arm | new kinds emit | `causes` edges |
|---|---|---|
| baseline | no | — |
| causal | yes | the mechanic's real antecedents |
| scrambled | yes, same emission points | drawn at random from eligible antecedents |

**Scrambled is the discriminating arm.** A detached arm with no edges would show zero gain trivially and prove nothing — an event with no causes cannot enter a chain. Scrambled holds node count and edge count fixed and destroys only the structure, which is the structureless-perturbation analogue of the geography shuffle.

**Pre-registered:** if `causal ≈ scrambled`, the variety gain is node-count inflation and the reachable-branches belief is not supported by this test. State the MDE before running. State the prediction each competing explanation makes, and confirm they differ — **a prediction shared by both mechanisms is not a test of either.**

**Dry-run every arm of every decision rule against ruleset-4 baseline data before measuring.** Both defects in the last phase's rule were reachability defects that existing data would have exposed for free.

**The contrast family closes when its verdicts are reported.** Fix the family before running.

Dispersion figures self-identify at emission (`sd=`, `range=[a, b] width=`, `cv=`, `ci95=`, `var=`).

---

## 4. Consequence for the reference set

This changes the stream, so ruleset 5 seed 42 is another different history and the staged reference set is discarded. **That is intended.** Building the hand-verified set before an intentional mechanics change buys protection across exactly the transition where the golden diff is expected to fail wholesale.

The set is built once, after this phase, against a world intended to stay put. Retain the staged candidate *questions* — the question design survives; only the answers and record IDs die.

---

## 5. Halt conditions

- After §1, always, with the audit table and surviving set
- A kind whose branches cannot both be observed across five seeds
- A constant that cannot be argued from the mechanic
- Mechanic-change budget binding
- Instrumentation invariance failing, or a log-hash change from a probe
- Any decision-rule arm found unreachable in the §3 dry run
- Layer 1 dynamics invariants regressing on any seed

Note the two parked failures for context, not as gates: seed 7 `distinct deep-chain shapes` 45 against 60, seed 99 74 → 69 unexplained. If either moves, say so and say which arm.

---

## 6. Queue

- Any kind dropped in §1 → append a backlog card naming the state it needs and which stage owns it
- Both branches of a kind not observed → append: determine whether the rare branch is structurally unreachable, which is the covert-coup shape again
- §3 verdict `causal ≈ scrambled` → append nothing automatic; that is a prose judgement and escalates
- Any new field a consumer reads → the schema assertion covers it; confirm it runs green

---

## 7. Report

Audit table and drop decisions; per-kind trigger, branches with observed counts per seed, causes edges; the three-arm table with CIs and the pre-registered MDE quoted; constants with their arguments; which parked failures moved.

State plainly which of the four now emit and which were deferred. That is the deliverable — the measurement is secondary and may return a null without this phase having failed.
