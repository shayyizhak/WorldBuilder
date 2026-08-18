# Brief — step two: relations become terminable

**Supersedes §2 of `phase-relation-termination.md`.** That §2 was written before step one ran. Four things learned since change it: the `Event.Key` insertion finding, the all-dynastic break skew, the negative control's weakness, and the now-visible cost of a ruleset bump on Layer 5 and the baselines.

**Ruleset 5 → 6.** Mechanics change, deliberate, budget spent. Worlds change; additive-only does not apply and is replaced by §5.

---

## 0. Why, unchanged

`RelationKind.Trade` is created by rules and removed by nothing — peak equals final on all five seeds. `POLITY.COLLAPSE` removes no relations, so destroyed factions keep allies and trading partners permanently. A relation graph that only ever densifies carries less information every year.

This is a correctness repair. `ECONOMY.TRADE_COLLAPSE` falls out of it; it is not the point of it.

---

## 1. `Event.Key` — measure now, fix at Stage 7, do not fix here

Step one established that inserting an event rekeys every later event in that year, and that the render cache is keyed on `Event.Key`. Its doc comment claims the key exists so a cached render survives an id shift — true for a retcon, false for an insertion.

**It does not bite this transition.** Ruleset 6 changes worlds, so every render regenerates regardless. It bites when you want to *keep* a world and add events to it — which is Stage 7 retroactive authoring, and which is already carrying the "cached renders are canon versus back-propagation rewrites the past" collision. This is a second, independent mechanism by which the cache invalidates for reasons unrelated to content.

**Do here:** measure the blast radius while it is cheap. On the ruleset-5 baselines, insert a synthetic event mid-year and report how many cached renders lose their key against how many have genuinely changed content. One number, recorded against the Stage 7 card.

**Do not** redesign the cache key in this step.

---

## 2. Relation termination as a general capability

Whatever ends a trade tie is the mechanism that ends any relation kind. Not a trade special case — the sweep in §6 will almost certainly find others, and a per-kind mechanism means writing this again.

State the capability's shape before writing it: what ends a relation, what the termination records, and whether termination is a distinct state from never-having-existed. **That last question is the absent-versus-unknown distinction again** (`https://trello.com/c/QiADoVAB`) — a former ally and a never-ally are different facts, and a query layer that cannot tell them apart has the same defect three subsystems have already had. Decide it deliberately.

---

## 3. `POLITY.COLLAPSE` cleans up

A destroyed faction's relations end.

**Decide explicitly whether that emits per-relation events or one cleanup event, and say which.** A collapse that silently drops twelve edges is precisely the invisible-transition defect this phase exists to repair. If one cleanup event, it must carry the count and the kinds; a bare "relations cleared" is an unlabelled figure.

---

## 4. Trade termination and `ECONOMY.TRADE_COLLAPSE`

**Constants by reasoning, never by fitting.** Write the argument before any run. A threshold chosen because peak-versus-final came out nicely is a finding against this phase.

Candidate causes to consider and argue for or against: war declaration between partners, partner collapse (falls out of §3), distance (geography is live), disuse decay.

**The war coupling is the sharp edge.** 21 of 24 war declarations occur between factions with a live trade tie. If war ends trade, that coupling inverts and a large share of ties die at once.

**Degeneracy guard, both directions.** A fully-connected trade graph carries no information; so does an empty one. Report the live-tie count per year across all five seeds. If final ties land near zero or near peak, HALT — the rule is degenerate at one end and the fix is the rule, not the threshold.

**Report ECONOMY→non-ECONOMY edge share before and after** against the ≥10% Layer 1 invariant. This is where step two is most likely to fail loudly.

**Success is `peak != final` for reasons each traceable to a stated rule**, not "trade ties fall." Those come apart, and only the first is a result.

**On the terminating cause as a rendered field:** step one found all 15 alliance breaks dynastic, making `tie` a payload field with one reachable value. Standing rule to apply here — **a field with one reachable value across the panel does not get rendered until it has two.** If the trade termination cause is effectively single-valued, record it and leave it out of the render.

---

## 5. What replaces additive-only

Termination checks consume RNG draws, so divergence from the first termination onward is expected and is not a defect. The checkable form:

> Each seed's ruleset-6 log is **identical to its ruleset-5 log up to the first termination event**, and divergent after.

Divergence *before* the first termination means something other than this change moved the world. **HALT and report where.**

Report the first-divergence year per seed alongside the first-termination year.

---

## 6. The monotonic sweep

Enumerate every piece of standing state — all relation kinds, every accumulating scalar, every collection an entity carries — and classify each:

- has a removal path, exercised on the panel
- has a removal path, never exercised on the panel
- has no removal path

The middle category is the covert-coup shape. The third is what this phase repairs.

**Report. Repair nothing found here without a separate decision.** Fixing an unbounded list is how a bounded phase stops being bounded. If the list is long enough that repairing it would exceed this phase, that itself is the finding — report and stop.

Note the §3-of-the-earlier-brief caution: counts alone cannot tell "no removal path" from "removal path not reached on this panel." Read the source for the classification; the panel only tells you what was exercised.

---

## 7. The negative control is weak again, and say so rather than fixing it

Step one's control was clean but limited: inserted events cited by nothing terminate chains rather than extending them.

`TRADE_COLLAPSE` will *cite* a war. Whether anything cites `TRADE_COLLAPSE` is a design question. **If the honest answer is that nothing does, write that down** — do not invent a downstream consumer to make the control stronger. A weak control honestly labelled is worth more than a strong one manufactured.

---

## 8. The parked failures

Seed 7 `distinct deep-chain shapes` 45 against 60; seed 99 74 → 69 unexplained. Both unmoved by step one.

**Report whether either moves. Do not attribute.** A relation graph that stops densifying is *plausibly* related to chain-shape variety and there is no evidence for it beyond the shapes matching. If one moves, that is a hypothesis for a phase with a proper control.

---

## 9. Ruleset 6 costs a baseline cut — budget it

The archive contract split is carded and not done (`https://trello.com/c/Kl5i0hQN`), so a ruleset-6 set still requires both halves: log, board, seal, plus chronicle, findings and `renders.json` through ollama for five seeds.

**Layer 5 goes quiet between the bump and the cut.** That is now visible in the top line rather than hidden behind `all layers passed`, which is the point of having fixed it — but plan the cut as part of this step rather than discovering it as five red tests.

Reference set: still deferred, and discarded by this phase as intended. Retain the candidate *questions*; only answers and record IDs die.

---

## 10. Halt conditions

- Divergence before the first termination on any seed (§5)
- Live trade ties landing near zero or near peak (§4 degeneracy guard)
- ECONOMY→non-ECONOMY share dropping below 10%, or any Layer 1 invariant regressing
- A constant that cannot be argued from what the mechanic represents
- The monotonic sweep returning a list too long to repair within this phase
- Instrumentation invariance failing, or a log-hash change from a probe
- Suite not returning to green after the ruleset-6 cut

## 11. Report

First-divergence and first-termination year per seed. Live-tie trajectory per seed with peak and final. Termination cause distribution. ECONOMY→non-ECONOMY before and after. The `Event.Key` blast-radius number. The monotonic table. Whether the parked failures moved, unattributed. Constants with the arguments that were written before the runs that used them. Whether anything cites `TRADE_COLLAPSE`, answered honestly.
