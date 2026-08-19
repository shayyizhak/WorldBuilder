# Brief — `GoalBook` into the fold

**Ruleset 6 → 7, intended as a record change with no simulation change.** Same shape as `ALLIANCE_BROKEN` at 4 → 5: emit what already happens, assert additive-only, re-cut baselines rather than invalidate them.

**Why it outranks the rest of the sweep list.** `WorldState`'s own summary says it is the fold of the event log and that every mutation lives behind a surface only `EventReducer` calls. Goals are the exception — created by the perception phase directly, touched by the reducer at one point. **A world replayed from its record has no goals in it and cannot decide anything.**

That is not a monotonic-state finding. It breaks *world state is a fold over the log*, which is the principle Stage 3's resolution rests on: the materialised log is the durable artefact **only if it contains the world**. Stage 7 replays to a point and applies deltas — onto a world with no goals. Stage 11 assumes facts are recoverable from the record.

---

## 1. Audit the goal lifecycle first

Before emitting anything, enumerate every transition a goal undergoes and where each happens:

- Created — by the perception phase, on what condition
- Mutated — priority, target, or score changes, and by what
- Resolved — completed, satisfied, or achieved
- Abandoned — dropped, superseded, expired, or invalidated by a target ceasing to exist
- The one point `EventReducer` already touches

**Every transition that changes state needs a record.** A goal that vanishes with nothing saying so is the same invisible-transition defect that `RelationEnds` fixed for relations — and the guard there is worth copying: a transition with no event to name it throws rather than passing silently.

**Also classify goals against the monotonic sweep.** They were not in §6's table. If goals only ever accumulate — created, never removed — that is the same family as `Grievance`, `Fealty`, `Kin` and ore, and it should be reported here rather than discovered later.

**HALT and report after the audit**, with the transition table and which of them are currently unrecorded. The emission design follows from it and should not be guessed at first.

---

## 2. Emission

Per transition surviving the audit: what the event records, what it cites, and whether anything downstream reads it.

**Constants by reasoning, never by fitting.** If the audit turns up a threshold — a goal expiring after N years, a score below which it is abandoned — argue it from the mechanic before any run.

**The renderer stays untouched.** Goals are engine bookkeeping; whether any of this is worth narrating is a separate question and belongs after the record exists. Per the standing rule, a field with one reachable value across the panel does not get rendered until it has two.

**No checker rule.** Same reasoning as geography and the terminated relations: emit first, observe across the panel, then decide whether anything joins Tier 3.

---

## 3. The two assertions

### 3.1 Additive-only, against the sealed ruleset-6 baselines

The form established at step one, including its correction:

> Every event in the ruleset-6 baseline appears in the new log with **the same world content**, in the same relative order, with its causes checked **through the alignment**, and the diff is insertions only.

Not literal key equality — `Event.Key` is FNV over (year, kind, participants, sequence), so any insertion rekeys every later event in that year. Comparing on world content with causes mapped through the alignment is the stricter assertion and the one that survives insertion.

- **Holds** → ruleset 7 is an additive record change with no simulation change. Record that in `Provenance.cs` as at 4 → 5. Re-cut baselines; **the reference set is not invalidated by this step.**
- **Fails** → emission is drawing from the RNG stream, which means the perception phase is not as separable as `WorldState` claims. **HALT and report where.** That is a Stage 3 determinism finding and larger than this brief.

### 3.2 The fix verification — replay must reproduce the world

Additive-only says the log is unchanged. It does not say the defect is fixed. The assertion that does:

> Fold a sealed baseline's log from empty and require the resulting state to equal the live state **including goals**, on every reference seed.

That is the property `WorldState` claims and nothing enforces. It is the deliverable; additive-only is the safety rail around it.

**Report what the same assertion says about every other component of `WorldState`.** If goals were the exception, that check passes everywhere else and is cheap standing protection. If something else also diverges, that is a bigger finding and worth having now.

### 3.3 Off-switch

Per the standing rule, in its strong form: switch goal emission off and all five sealed ruleset-6 logs come back event for event. For a record-only change this is the same assertion as 3.1 from the other side — build it as the explicit off-switch anyway, so the property is stated the way every other mechanic states it.

---

## 4. Baselines and the reference set

Ruleset 7 owes a baseline cut: five seeds, both halves, real inference. The archive split (`https://trello.com/c/Kl5i0hQN`) is still owed, so a set still requires chronicle, findings and `renders.json`.

**Cut it as part of this step**, not as a discovery afterwards. Layer 5 goes quiet between the bump and the cut — visible in the top line now rather than hidden, which is the point of having fixed it.

Report the holdout rate beside ruleset 6's 36%.

**If additive-only holds, the reference set survives** and the hand-verification work proceeds against ruleset 7 unchanged. That is the reason this step goes first.

---

## 5. Halt conditions

- After the §1 audit, always, with the transition table
- Additive-only failing on any seed (§3.1)
- The replay assertion failing on a component other than goals (§3.2) — report the full list rather than fixing them all here
- A constant that cannot be argued from what the mechanic represents
- A transition found with no event able to name it and no obvious record — that is a design question, not a plumbing one
- Suite not green after the ruleset-7 cut
- Instrumentation invariance failing, or a log-hash change from a probe

## 6. Report

The goal lifecycle table with which transitions were previously unrecorded. Additive-only verdict per seed. The replay-equals-live verdict, goals included, and what it said about every other component. Whether goals have a removal path, classified against the §6 sweep's three categories. Baseline cut confirmation with the holdout rate beside ruleset 6's. Constants with the arguments written before the runs that used them.
