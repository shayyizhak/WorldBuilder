# Brief — `GoalBook` phase 2: emission

**Supersedes §2, §3 and §4 of `brief-goalbook-into-the-fold.md`.** §1's audit changed three things: the reducer reaches six endings rather than one, goals have removal paths and are not the monotonic family, and 24 of 27 `WorldState` components already fold exactly. §5 and §6 of that brief stand except where restated here.

**Ruleset 6 → 7.** Intended as a record change with no simulation change, but **not** additive-only in the 4 → 5 sense — see §3.

---

## 0. The two decisions, taken

**`Into` everywhere there is a host event; one new event kind for the orphans.**

Every transition with a causing event rides it as a payload key. Creation already carries the id of the event that produced it. Advances are caused by named events. Arc attachment happens on the war or the plot. The 235 inferable endings sit beside a conquest, alliance, coup, betrayal, homecoming or death.

The reason is causality, not volume. **Correcting the audit's arithmetic:** 235 separate ending events would be ~6.5% of the panel's 3,628, not a doubling — the ~50% figure belongs to the whole set (505 creations + 765 advances + 133 attachments + 424 unfolded endings ≈ 1,827). Volume is a real consideration for the full set and not the deciding one for endings. The deciding one is that a separate event would record the same fact twice with an arrow between them.

**Finding 1 confirmed — all or nothing.** Not because the book would fill and never empty; the reducer reaches 378 clears, so it would not. Because **an empty goal book fails loudly and a half-right one fails quietly.** Replay currently yields an obviously broken world. Partial emission yields a plausible book that diverges, and a replayed-then-continued world makes different decisions with nothing announcing it.

This is already enforced mechanically, which is better than policy: the §4 theory asserts *exactly* `goals.arc`, `goals.identity` and `goals.progress` differ. A partial fix leaves them differing and stays red.

---

## 1. What gets emitted

### 1.1 Riding the host event — `Into`

| transition | host | n (panel) |
|---|---|---|
| Created | the event the goal already cites | 505 |
| Advanced | the named step event (`SupportCourted`, `RaidReturned`, …) | 765 |
| Attached | the war or plot the goal spawned | 133 |
| Ended, inferable | the adjacent conquest / alliance / coup / betrayal / homecoming / death | 235 |

Keys must **name the goal** — owner, kind, target — because the whole defect is that these transitions are currently inferable but unnamed. An adjacency that a reader has to reconstruct is not a record.

The six reducer-owned endings already fold. Leave them; do not double-record.

### 1.2 One new kind for the orphans

The 189 silent endings are the retirement sweep: `Expired` 80, `Completed` 74, `AlreadySatisfied` 18, `TargetDefunct` 13, `TargetDead` 4.

**One event per sweep occurrence, carrying the count and the reasons** — the `POLITY.COLLAPSE` precedent exactly:

```
goalsRetired=4  goalsRetiredReasons=Expired:2,Completed:1,TargetDefunct:1
```

plus one key per goal identifying it. A bare total is an unlabelled figure. **Assert the label agrees with the total**, as `ACollapseSaysWhatItEnded` does, so the breakdown cannot become decoration.

### 1.3 Refusals

441 `BookFull` refusals against 505 admissions. **Do not emit them in this step.** A refusal is not a state transition — nothing changed — and the record's job here is to make the fold reproduce state. Record the count in the census; revisit if §5 finds them load-bearing.

---

## 2. The guard

`RelationEnds`' rule, in its goal form: **a transition with no way to name it throws rather than passing silently.** The audit's labelling work already provides the compile-time half — every site carries a required label. Extend it so a label with no emission route is a build failure, not a silent omission.

Three labels are never reached on this panel — `OwnerLeftFaction`, `TargetInvalid`, `OwnerDeadAtRetirement`. **Reported as unexercised, not unreachable.** They still need emission routes; a route that this panel never takes is fine, a label with no route is not.

---

## 3. The correctness assertions

### 3.1 Key-level record extension — the replacement for additive-only

`Into` adds payload keys to existing events, so content changes and additive-only as written cannot hold. You hit this wall in step two, when moving alliance deletion onto `ALLIANCE_BROKEN` would have changed the war's payload.

State it as its own property, and do not call it additive-only:

> Against each sealed ruleset-6 baseline: every event appears in the same relative order; **every payload key present in the baseline is present and equal**; new keys may be added; causes unchanged through the alignment; and no new events except the retirement events of §1.2.

Weaker than byte-identity, stronger than nothing, and it supports the claim that matters — the simulation did not change, the record got richer.

**Failure branch:** a baseline key changed value, or an event appeared that is not a retirement event. **HALT and report which.** §1 established that streams derive per `(seed, year, entity, purpose)` with no shared sequential generator, so emission consuming a draw is avoidable by construction — provided emission takes no draw of its own. If the property fails, that construction is wrong somewhere and it is a Stage 3 finding.

### 3.2 The deliverable — replay reproduces goals

> Fold each sealed baseline's log from empty; the resulting state equals live state on all 27 components **including the three goal components**, on every reference seed.

The §4 theory asserting exactly three components differ **must go red when this lands.** Update it in the same change, so the repair announces itself rather than needing to be remembered.

### 3.3 Off-switch

Strong form, per the standing rule: switch goal emission off and all five sealed ruleset-6 logs come back event for event, keys included.

---

## 4. Baselines

Ruleset 7 owes a cut: five seeds, both halves, real inference. The archive split (`https://trello.com/c/Kl5i0hQN`) is still owed, so a set still requires chronicle, findings and `renders.json`.

**Cut it as part of this step.** Layer 5 goes quiet between bump and cut — visible in the top line now, which is the point of having fixed it. Report the holdout rate beside ruleset 6's 36%.

**The renderer stays untouched.** Whether any goal transition is worth narrating is a separate question and belongs after the record exists. Per the standing rule, a field with one reachable value across the panel does not get rendered until it has two.

**No checker rule.** Emit first, observe across the panel, then decide whether anything joins Tier 3.

---

## 5. One measurement, because it feeds the brake decision

441 refusals against 505 admissions at `MaxPerOwner = 2`, with `SeizeLeadership` at 157 — the largest kind by a wide margin, against `FormAlliance` at 13. Internal ambition may be crowding out external goals.

**Stated as a hypothesis with a cheap check, not a mechanism.** I proposed a mechanism from a variable that did not move once already this phase.

> At the year a runaway forms, how many factions held a full book, and what was in it?

If balancing goals are being refused for space while a leader runs away, a candidate brake sits inside existing machinery. If books are full of unrelated goals, it is nothing. Computable from the panel, no new worlds, one command.

**Report the figure. Do not act on it here.** It feeds a decision that has not been made.

---

## 6. Halt conditions

- Key-level extension failing on any seed (§3.1) — a changed baseline key, or a non-retirement event appearing
- The replay assertion failing on a component other than the three goal components
- A transition found with no emission route and no obvious host — a design question, not plumbing
- A constant that cannot be argued from what the mechanic represents
- Suite not green after the ruleset-7 cut
- Instrumentation invariance failing, or a log-hash change from a probe

## 7. Report

Key-level extension verdict per seed. The replay verdict on all 27 components, and confirmation the exactly-three theory went red and was updated. Per-transition emission counts against the audit's census — **any count differing from §2 of the audit is a finding**, since the audit measured the same worlds. The retirement event's count-versus-label assertion. Baseline cut with holdout rate beside ruleset 6's. The §5 figure, unactioned.

---

## 8. Carded, not built

- **`GoalKind.TakePlace` is created by nothing.** Seventh declared-and-unreachable construction, with the four dead event kinds, `Rivalry` and `Vassal`. Enough to justify the standing assertion: **every declared kind has a writer, or is explicitly marked reserved.**
- The `RetireGoals` shared-label finding — two branches behind one condition, one dead, invisible while they shared a label. Same shape as `seized`/`exposed`.
