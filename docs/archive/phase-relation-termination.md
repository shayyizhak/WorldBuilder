# Phase — relation termination

**Supersedes §2 of `phase-four-dead-kinds.md`.** That §2 asked for three emitters and a three-arm measurement. The §1 audit changed what is on the table, so this is smaller and differently framed. §0, §1 and §4 of the original stand; §3 is deferred with a reason given in §5 below.

**Budget.** The standing hard budget (`phase-carry-forward.md:11-14`) is spent deliberately here: mechanics change, ruleset advances 4 → 5 → 6. This is an escalation granted, not an override — the justification is a correctness defect, not a roadmap item, and it is written down in §0 so a later reader can judge whether it was a good call.

**Dropped from the original four:** `CONFLICT.SIEGE` (no substrate, carded), `INTRIGUE.GRIEVANCE_SETTLED` (every route is a known trap — see §6).

---

## 0. Why this is a repair

Three findings from the audit are one defect:

- `RelationKind.Trade` is monotonic. Created by rules, removed by nothing. Peak equals final on all five seeds. Two factions who traded once in Y6 are trading partners in Y50 through two wars.
- `POLITY.COLLAPSE` removes no relations. Destroyed factions keep alliances and trade ties permanently.
- Grievances never reach 0. 260 cross 40; none clear.

**State that only ever goes up.** A world where nothing is released trends toward everything connected to everything, and a fully-connected relation graph carries no information. This is a correctness defect independent of whether anything renders it, which is why it earns the budget when a new feature would not.

`ECONOMY.TRADE_COLLAPSE` and `DIPLO.ALLIANCE_BROKEN` fall out of the repair. They are not added on top of it.

---

## 1. Step one — `ALLIANCE_BROKEN`, record-only, alone

The break already fires; only the event is missing. **Do this step by itself, before anything else changes**, because it is the only step whose key property can be tested against the existing sealed baselines — and any other change first destroys that opportunity.

**Additive-only assertion.** Against each of the five sealed ruleset-4 baselines: every event in the baseline appears in the new log, unchanged, in the same relative order, and the diff is insertions only.

- **Holds** → the world is the same world with a more complete record. Re-cut baselines as **ruleset 5**, and record in the header that 4 → 5 is an additive record change with no simulation change. That is a property worth being able to point at later.
- **Fails** → emission is drawing from the RNG stream. HALT and report where. This is worth knowing on its own and is a Stage 3 determinism finding, not a failure of this phase.

**`causes` design.** 42 of 47 alliance edges are dynastic — cross-faction marriage, not `FormAlliance`. Point a break at **the marriage that created the tie**, not at `ALLIANCE_FORMED`; pointing at formation would leave most breaks citing nothing. For the 5 diplomatic edges, cite the formation goal. Where the origin cannot be resolved, emit the event with **no** cause rather than a plausible one — a missing input renders as omission, never as connective text, and that rule holds for the engine as much as the model.

**Branches.** Record-only means the branch structure already exists. Report the observed distribution of break circumstances across the five seeds; if it is heavily skewed, note it as a latent fabrication risk for the renderer rather than fixing it here.

**No checker rule yet.** Same reasoning as geography: emit first, observe across seeds, then decide whether it joins Tier 3's mandatory classes.

**HALT and report after step one.**

---

## 2. Step two — relations become terminable

Ruleset 6. This changes worlds; baselines are re-cut and the additive-only property does not apply.

1. **Relation termination is a general capability**, not a trade special case. Whatever mechanism ends a trade tie should be the mechanism that ends any relation kind.
2. **`POLITY.COLLAPSE` cleans up.** A destroyed faction's relations end. Decide explicitly whether that emits per-relation events or one cleanup event — and say which, because a collapse that silently drops twelve edges is the same class of invisible-transition defect this phase exists to fix.
3. **Trade termination rule.** What ends a trade tie: war declaration, distance, partner collapse, disuse. **Constants by reasoning, never by fitting.** Write the argument before any run. A threshold chosen to make peak-versus-final come out is a finding against this phase.
4. **`ECONOMY.TRADE_COLLAPSE`** emits at termination, with the terminating cause attached.

**Success is not "trade ties fall."** It is that `peak != final` on trade edges for reasons each traceable to a stated rule. Report the peak/final gap per seed and the cause distribution behind it.

**Note the interaction:** 21 of 24 war declarations occur between factions with a live trade tie. If war ends trade, that coupling changes, which is a substantive shift in how the economy touches conflict. Report ECONOMY→non-ECONOMY edge share before and after against the ≥10% invariant.

---

## 3. Step three — the monotonic sweep

The generalisable deliverable, and probably the most valuable thing here.

Enumerate every piece of standing state — all relation kinds, every accumulating scalar, every collection an entity carries — and classify each as: has a removal path exercised on the panel / has a removal path never exercised / has no removal path.

Middle category is the covert-coup shape. Third is the defect this phase repairs.

Report the table. **Repair nothing found here without a separate decision** — the point is to know the extent, and fixing an unbounded list is how a bounded phase stops being bounded.

---

## 4. The two parked failures

Seed 7 `distinct deep-chain shapes` 45 against 60; seed 99 74 → 69 unexplained. A relation graph trending toward fully connected is *plausibly* related to chain-shape variety, and I have no evidence for that beyond the shapes matching.

**Report whether either moves. Do not attribute.** If one moves, that is a hypothesis for a later phase with a proper control, not a result.

---

## 5. Why §3's measurement is deferred

Record-only `ALLIANCE_BROKEN` adds zero reachable branches — the branch already existed. `TRADE_COLLAPSE` adds one. One branch is not a test of the reachable-branches hypothesis, and running the three-arm design on it would produce an underpowered null that is easy to misread as a result. Defer to a phase that adds enough branches to test.

**One free reading, if step one holds.** With additive-only confirmed, the simulation is unchanged, so causal variety *must not move*. If it does, the metric is counting record density rather than causal structure. That is a negative control obtained for nothing, and given how much of this roadmap rests on that metric, it is worth reading carefully. Report the figure with the additive-only verdict.

---

## 6. Carried, not built

- **`INTRIGUE.GRIEVANCE_SETTLED`** — decay-to-zero is structurally unreachable (260 cross 40, none clear); peace already cancels a third and already renders, so emitting there double-names an existing event; marriage-settlement (33 of 42 qualify) is a new mechanic. Card it with the grievance-ratchet finding attached — the ratchet is the part worth keeping.
- **`CONFLICT.SIEGE`** — no substrate. Already carded.

---

## 7. Halt conditions

- After step one, always, with the additive-only verdict per seed
- Additive-only failing on any seed
- A constant in step two that cannot be argued from what the mechanic represents
- Layer 1 dynamics invariants regressing, or the ECONOMY→non-ECONOMY share dropping below 10%
- The monotonic sweep returning a list long enough that repairing it would exceed this phase — report and stop
- Instrumentation invariance failing, or a log-hash change from a probe

## 8. Report

Additive-only verdict per seed and the causal-variety reading beside it. Break-circumstance distribution. Trade peak/final gap per seed with cause distribution. ECONOMY→non-ECONOMY before and after. The monotonic table. Whether the parked failures moved, unattributed. Constants with their arguments, written before the runs that used them.

The staged reference set is discarded by this phase, as intended. Retain the candidate *questions*; only answers and record IDs die.
