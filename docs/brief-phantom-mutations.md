# Brief — phantom relation mutations, and the last two sheet repairs

Three items. §1 is an engine defect in the sealed log and is the substantial one; §3 and §4 are derivation fixes with determined answers.

**Ruleset 7 → 8** if §1 lands. The log changes; the world should not.

---

## 1. `RelationGraph` notifies a mutation whether or not it changes anything

The Alliance between the Kebarrow Compact (f:2) and the Griwick Compact (f:3), every event touching it:

| record | year | kind | what it does |
|---|---|---|---|
| `e:37` | 2 | `LIFE.MARRIAGE` | makes it |
| `e:92` | 6 | `LIFE.MARRIAGE` | makes it **again**, while live |
| `e:463` | 27 | `DIPLO.WAR_DECLARED` | deletes it |
| **`e:718`** | **38** | `DIPLO.WAR_DECLARED` | **deletes it again — nothing made it between Y27 and Y38** |
| `e:726` | 39 | `LIFE.MARRIAGE` | makes it |
| `e:735` | 39 | `POLITY.COLLAPSE` | deletes it |

`e:718` carries `relDel:f:2:f:3:Alliance=1` and `relDel:f:3:f:2:Alliance=1` for a tie that ended eleven years earlier.

**This is `GoalBook.Remove` in a second subsystem.** That one notified its watcher regardless of whether the book held the goal and produced 15 phantom endings in 477, invisible because `created − ended = live` holds by construction. Same identity, same blindness, same defect.

### 1.1 Audit before fixing

Enumerate **every site that notifies a state mutation**, not just relation removal:

- relation add / remove / value change
- goal transitions (already presence-checked — confirm, and use it as the reference shape)
- anything else emitting a payload key that asserts a change

For each, report whether it checks that the change is real before notifying. **This is the third instance of the family, so the deliverable is the audit, not the one fix.** `e:92` shows the making side has it too.

**HALT and report the audit** before changing emission. If the list is long enough that repairing it exceeds this brief, that is the finding — report and stop.

### 1.2 The guard

`RelationEnds` already has the shape: a kind with no event to name its ending throws rather than deleting quietly. The analogue here is that **a no-op mutation emits nothing**, and a mutation that claims to change something the graph does not hold is a defect rather than a silent key.

Prefer refusing to emit over emitting-and-ignoring. A key in the log is a claim about the world.

### 1.3 The correctness property

The log loses keys; the world must not move. State it at the key level, in the form that worked at 6 → 7:

> Against each sealed ruleset-7 baseline: every event appears, in the same relative order, with causes unchanged through the alignment; **no event added, none removed**; and the only key differences are the removal of mutation keys the graph did not hold.
>
> And: folding each baseline log produces state identical to before, on all 27 components.

The second half is the one that matters. If a phantom key was load-bearing anywhere, this fails and says where.

**HALT if any event appears or disappears**, or if any state component moves.

---

## 2. What this changes downstream — recount, do not re-litigate

Step two's four-arm panel counted ties ended: null 0, collapse 364, war 516, random 542. Those come from deletions, so they are inflated by an unknown amount.

**Re-run the 90-seed panel with the corrected emission and report the four figures again.** It is log-only, no inference, and was 14 seconds.

**Pre-committed:** the reported verdict `war − null = +0.27 y [−0.27, +0.81]` was measured on **runaway year**, not on tie counts, so it should not move. If it does, say so loudly — that would mean the phantom keys were affecting the simulation and not just the record, which contradicts §1.3.

The degeneracy guard read live ties rather than deletions, so it should also hold. Report both rather than assuming.

Correct the step-two report in place with a note, rather than rewriting it. The verdict stands or falls on its own figures.

---

## 3. The role-and-outcome table splits its third column

The numbers are right: with *subject of any assassination* as the definition, the three columns partition exactly, 0 of 28 rows failing `records = failed + killed + ordered`.

The label is wrong. The prose calls the third case *"the sponsor of a killing done to somebody else"*, and for **9 of 28 people the only sponsorship was a failed attempt**: Reweld Wul (a:1), Thulgea Bu (a:9), Pouldrir Ho (a:13), Saern Meastouth (a:28), Heillvar Maer (a:29), Thosruld Lul (a:34), Leimmil Theall (a:38), Diweith Mound (a:42), Thres Thrild (a:57).

So *"how many killings did Reweld Wul order?"* reads 1 from this table when the answer is 0.

**Split the column into `killings they ordered` and `attempts that failed`.** Four columns then partition against the record count, and the table answers the question a reader would actually ask. Match the prose to the columns.

This is the section's own cited lesson half-applied: role and outcome both decide the count, and the first two columns split on outcome while the third did not.

---

## 4. Reconcile the `?` count

The repair report states 11 `?` openings resolved and 8 rows already carrying a year. The pre-repair sheet has **14 `?` and 5 with years**. Both sum to 19.

Count `?` in the regenerated file:

- **zero** → the report miscounted its starting state; the output is fine, correct the report
- **three** → the fold missed three rows and the halt on a remaining `?` did not fire, which is the more serious reading

Either way say which, because a count that does not reconcile is how a dropped row hides.

---

## 5. Ruleset 8 and the cut

If §1 lands, bump and cut: five seeds, both halves, real inference. The archive split (`https://trello.com/c/Kl5i0hQN`) is still owed, so a set still requires chronicle, findings and `renders.json`.

Report the holdout rate beside ruleset 7's 34.5%.

Layer 5 goes quiet between bump and cut — visible in the top line, which is the point of having fixed it.

---

## 6. Re-stage

Regenerate all seven artefacts against the new seal. Everything stays `verified: no`.

Diff against the current set and report every row that moved. **A row moving that §1, §3 or §4 does not explain is a finding.**

Expect the terminated-relations table to change: `e:718` should stop being a deletion at all, so the count moves from 19 and the f:2↔f:3 alliance's history becomes two terminations rather than an ambiguous three.

---

## 7. Tests worth pinning

- **No event emits a mutation key for a change the state does not hold.** Panel-wide, replayed through `EventReducer`. This is the one that would have caught both `e:718` and `e:92`.
- **The role-and-outcome table's four columns sum to the record count**, per person, panel-wide.
- **No relation span opens after it closes**, and no tie is deleted twice without an intervening making.

The first must be non-vacuous: assert it examined a non-zero number of mutation keys, or a change that stopped emitting them entirely would pass.

---

## 8. Halt conditions

- The §1.1 audit returning a list too long to repair within this brief
- Any event added or removed by §1.3, or any state component moving
- `war − null` moving in §2
- Any `?` remaining after §4 that §1 does not explain
- A row moving in §6's diff that no item here explains
- Suite not green after the ruleset-8 cut

## 9. Report

The mutation-notify audit table, site by site, with presence-checking marked. The key-level and fold verdicts per seed. The four re-counted arm figures and whether `war − null` moved. The four-column role table. The `?` reconciliation with which of the two readings holds. Baseline cut with the holdout rate beside ruleset 7's. The re-stage diff.

**Nothing is marked `verified`.** The human session happens after this brief returns clean — that is the point of running it first.
