# `GoalBook` phase 2 — report

**Ruleset 6 → 7. `GoalBook` is in the fold.** Every goal transition now travels in the record, a world
folded from its own log reproduces all 27 components of `WorldState` including its goals, and the
off-switch gives back all five sealed ruleset-6 logs byte for byte.

Generated figures come from `wb goals` and `wb bookpressure` (5 seeds, 50 years, 2026-08-19). The
per-seed tables are emitted by the code; re-run rather than trusting this transcript.

---

## 1. The deliverable — §3.2

> Fold each sealed baseline's log from empty; the resulting state equals live state on all 27
> components **including the three goal components**, on every reference seed.

**Holds on all five seeds.**

| seed | components checked | components that differ |
|---|---|---|
| 1 | 27 | none |
| 7 | 27 | none |
| 42 | 27 | none |
| 1234 | 27 | none |
| 2025 | 27 | none |

`ReplayTests.AFoldReproducesEveryComponentOfTheWorld` asserts it as a standing property, against an
exclusion list that is now **empty**. The exactly-three theory did go red when this landed, and was
updated in the same change, as §3.2 required — the list is kept as an empty array rather than deleted,
because the mechanism (whatever falls out of the fold next fails here) is worth more than the current
answer. The theory also asserts the replayed world has goals in it, so it cannot be satisfied by a
panel that formed none.

**A second assertion, because state equality is weaker than it looks.**
`GoalRecordTests.TheFoldMakesTheSameTransitionsTheRulesDid` compares the full transition census, not
just the end state: same creations by kind, same advances by step, same endings by reason, same arc
attachments. A record could arrive at the right final book by a wrong route — an ending under the
wrong label, an advance folded twice and another dropped — and end-state equality would not see it.

---

## 2. Key-level record extension — §3.1

> Every event appears in the same relative order; every payload key present in the baseline is present
> and equal; new keys may be added; and no new events except the retirement events.

**Holds on all five seeds.** `GoalRecordTests.Ruleset7ExtendsTheRuleset6RecordAndChangesNoKeyInIt`.

| seed | ruleset 6 | ruleset 7 | of which goal rows | baseline events surviving |
|---|---|---|---|---|
| 1 | 695 | 760 | 65 | 695 |
| 7 | 529 | 594 | 65 | 529 |
| 42 | 878 | 954 | 76 | 878 |
| 1234 | 826 | 897 | 71 | 826 |
| 2025 | 700 | 772 | 72 | 700 |
| **panel** | **3,628** | **3,977** | **349** | **3,628** |

Not one baseline event was lost, and not one baseline key changed value. **+9.6% log growth**, well
under the brief's arithmetic: §0 estimated ~1,827 transitions and worried about volume for the full
set. Batching is why — 1,865 transitions travel in 349 new events plus keys on events that already
existed.

**Stated as an ordered subsequence, not a lookup per key.** `EventDraft.Set` appends rather than
replaces, so one event legitimately carries the same key more than once — an `ECONOMY.YIELD` moves
grain into and out of the same store. The first version of this check looked each key up, resolved
every occurrence to the first, and reported five seeds' worth of changed yield figures on a record
that had not changed at all. A subsequence walk cannot make that mistake because it never looks a key
up. Recorded because it is the third instrument defect this phase (see §7).

---

## 3. The off-switch — §3.3

> Switch goal emission off and all five sealed ruleset-6 logs come back event for event, keys included.

**Holds on all five seeds, byte for byte**, arm marker aside.
`GoalRecordTests.TurningGoalRecordingOffGivesBackRuleset6`.

This is the assertion that carries the whole "no simulation change" claim, and it is doing real work
here rather than restating §2. The change touched **46 call sites** across five files, moved goal
formation to a batched proposal that applies the book's cap itself, and moved one transition earlier
within its tick. Any of that could have re-sequenced a stream; nothing else in the suite would have
noticed.

A world run with recording off is marked `arm=record-no-goals` in its header and its genesis event,
`IsDiagnostic` is true, and `wb baseline cut` refuses it —
`AWorldThatDidNotRecordItsGoalsIsMarkedDiagnostic` asserts both halves, including that a real world is
*not* marked. Reachable from the CLI as `wb run --no-goal-record`.

**The two off-switches compose, and that turned out to matter.** `RelationTerminationTests` anchors on
the *ruleset-5* seals, so its null arm now has to turn off the termination rules **and** goal
recording to reach back that far. Ten of its assertions failed on the bump and were correct to: with
recording left on, they compared a ruleset-7 log against a ruleset-5 one and found bookkeeping rows.
That is the argument for per-mechanic switches over a single "previous ruleset" flag — each ruleset
adds one, and a comparison reaches back as far as the switches it turns off.

---

## 4. What gets emitted, and where §1 needed correcting

| transition | n | how it reaches the record |
|---|---|---|
| Created | 505 | `GOALS.FORMED`, one row per year — **not the cited event** |
| Advanced | 765 | a key on the step's own event |
| Attached | 133 | a key on the war or plot it opened |
| Ended, hosted | 230 | a key on the conquest / alliance / coup / homecoming / killing |
| Ended, orphan | 189 | `GOALS.ENDED`, one row per occurrence |
| Ended, folded | 43 | already the reducer's, on the death / exile / defection |

### Two places the brief's §1 could not be implemented as written

**1. Creation has no host event.** §1.1 routes creation onto "the event the goal already cites". That
event is already in the log — often years earlier — and `Event` is an immutable record in an
append-only log, so it cannot be amended. The perception phase emits nothing else, so creation is an
orphan in exactly §1.2's sense and takes §1.2's shape: `GOALS.FORMED`, one row per year, carrying the
count, the breakdown by kind and one key per goal. The cause is not lost; it is inside the `goalAdd`
payload, which is where the fold reads it.

**2. 35 of the 189 silent endings are not the retirement sweep.** §1.2 attributes all 189 to it. The
sweep accounts for `Expired` 80 and `Completed` 74; `AlreadySatisfied` 18, `TargetDefunct` 13 and
`TargetDead` 4 are action-phase guards that drop a goal whose target has become unreachable. They have
no host either, and a design covering only the sweep would have left them out. They emit one
`GOALS.ENDED` row per occurrence — batching them to the end of the phase was rejected because the
resolution phase looks goals up by owner and kind later in the same tick, so the goal has to leave the
book when the guard fires.

### Three counts differ from the §1 audit — §7 asked, and here they are

None is a simulation change; the base event counts in §2 are identical.

| ending | §1 audit | ruleset 7 | why |
|---|---|---|---|
| all endings | 477 | **462** | 15 were removals of a goal already gone, counted twice |
| `Spent` | 77 | **72** | −15 phantoms, +10 relabelled from the two below |
| `OwnerDead` | 27 | **21** | 6 losing challengers now end on the challenge |
| `OwnerExiled` | 21 | **17** | 4 losing challengers, same |
| `OwnerDefected` | 5 | 5 | unchanged; 5 phantoms beside it are gone |

**The phantoms.** `GoalBook.Remove` notified its watcher whether or not the book still held the goal.
A challenger who lost an open challenge is exiled or killed by `SettleCoup`, the reducer clears his
book, and the rules then removed the same goal again as `Spent` — fifteen times across the panel, and
the audit counted all fifteen as endings. `created − ended = live` cannot detect this: that identity
holds by construction whatever the labels say. Now `Remove` reports a vanished removal separately, and
`TheFoldMakesTheSameTransitionsTheRulesDid` asserts the count is **zero**.

**The relabelling.** `ChallengeOpenly` used to remove the goal after `SettleCoup`. It now puts the key
on the challenge draft, which is earlier in the tick — the one ordering change in this phase. State is
identical either way and nothing between the two points reads the book, which is what the off-switch
proves. What changes is the label the ten carry, and `Spent` on the challenge is the truthful one: the
man stopped wanting the seat because he lost the contest for it. The exile is what followed.

`Defect` no longer records its ending at all. `ApplyDefection` clears a defector's whole book when it
folds `INTRIGUE.BETRAYAL`, and the structural cases run before the payload deltas — so a `goalEnd` key
there names a goal the same event has already taken out. **The reducer refused the log and said so**,
which is how this was found rather than shipped; see §7.

---

## 5. The guard — §2

**Compile-time.** `GoalRecord.Route` is a switch expression over `GoalEnd` with no discard arm, and
warnings are errors in this build, so a new `GoalEnd` with no route **fails the build**. CS8524 (the
unnamed-value arm) is suppressed by pragma and only that one: a `_ =>` arm would have silenced CS8509
with it, and CS8509 is the entire mechanism.

The route is three-valued — `Folded`, `Host`, `OwnEvent` — rather than "an event kind or null". Null
meant either "the reducer folds this already" or "a rule is emitting the causing event right now", two
entirely different arrangements sharing one value, which is the ambiguous-label defect this project
keeps finding in its own record.

**Run-time.** A goal key naming a goal the book does not hold throws rather than skipping, and so does
a creation the caps would have refused. `AFoldRefusesAGoalKeyItCannotResolve` asserts it. This is not
decorative: it is what caught the defection double-record.

**Both directions are refused.** `GoalRecord.End` throws if given an ending that is not routed `Host`;
`EndWithoutAHost` throws if given one that is. Double-recording is as wrong as not recording.

**The three unexercised labels keep their routes.** `OwnerLeftFaction`, `TargetInvalid` and
`OwnerDeadAtRetirement` fire zero times on this panel, and `EveryRouteIsReachedAndEveryEndingHasOne`
asserts that every label has a route and every route has labels — reported as unexercised, not as
unreachable.

---

## 6. Refusals — §1.3

Not emitted, as directed. Recorded in the census: **441 `BookFull`, 0 `AlreadyHeld`**, against 505
admissions.

The perception phase now applies the cap itself, because the decision has to include it — a phase that
proposed the 441 anyway would be a different simulation. It tests against the book *plus* what it has
already proposed this year, and in the same order the old `Add` used (full before duplicate), so the
mix of refusal reasons is unchanged.

---

## 7. Instrument defects found before believing it

Four, this phase. The standing expectation is that every new instrument mis-fires on first contact,
and it did.

1. **A key-extension check that resolved duplicate keys to the first occurrence** and reported five
   seeds' worth of changed yield figures on an unchanged record. Fixed by asserting an ordered
   subsequence, which never looks a key up. §2.
2. **A `cited` column that stopped meaning anything** once every ending was recorded. Replaced with the
   route read off `GoalRecord.Route`, so the report cannot drift from the table the build enforces.
3. **The `ReducerReach` probe from the §1 audit, now deleted.** It existed to make one column able to
   vary when creation was outside the fold; with creation in the fold a plain fold exercises
   everything, so the scaffolding is gone. Worth noting that the *reason* it existed was itself a
   defect it caught: the obvious version of that column could only ever read zero.
4. **Causes on the bookkeeping rows.** Citing each goal's own cause on `GOALS.FORMED` is the obvious
   thing to write, and it moved the pinned chain-shape figures on all five seeds — because the
   causal-variety metrics Layer 1 asserts are counts over exactly those edges. It is the defect
   `PerceptionPhase.LatestCauseFor` already documents from the other side: a cause that is technically
   true and manufactures "the long lifecycle-shaped chains that made the depth look real". The
   bookkeeping rows now carry no causes, no participants and no arc.

The arc omission is worth its own line. `CloseFinishedArcs` is the one log read in the rules that does
not filter by event kind — it walks every event of the year and collects `e.Arc`. A bookkeeping row
carrying an arc would have kept a famine alive.

---

## 8. §5 — the book-pressure figure, unactioned

> At the year a runaway forms, how many factions held a full book, and what was in it?

| seed | runaway year | standing factions | books full | what they held |
|---|---|---|---|---|
| 1 | none | — | — | — |
| 7 | 22 | 2 | 2 | Goummeidale Compact\*: RestoreLegitimacy+Avenge; House Thream: SecureGrain+RestoreLegitimacy |
| 42 | 39 | 2 | 2 | Griwick Compact\*: RestoreLegitimacy+Avenge; Meigate Covenant: FormAlliance+SecureGrain |
| 1234 | 36 | 2 | 2 | Galweall League\*: RestoreLegitimacy+Avenge; House Buldbei: SecureGrain+RestoreLegitimacy |
| 2025 | none | — | — | — |

Pooled: `RestoreLegitimacy` 5, `Avenge` 3, `SecureGrain` 3, `FormAlliance` 1. Three of five worlds
produced a runaway; across those, **6 of 6 standing factions held a full book**.

**That figure reads as support for the hypothesis and is not.** The denominator is the finding: at
most **two** factions were standing in any world at its runaway year, so 6-of-6 is two factions per
seed and not a population. And with two powers left, the only available ally *is* the hegemon, which
`FindAllyCandidate` excludes by design — so `FormAlliance` is absent here because there is nobody to
form one with. That is structural, not a book-space effect.

**Measured at the runaway year, the question cannot be answered either way.** The field is too small
by then for the mechanism to be visible, and any figure taken here is consistent with both
explanations. Whoever takes the brake question up should measure over the decade *before* the
threshold, while there are still powers to ally with. The caveat is emitted by `wb bookpressure`
beside the number rather than left in this document.

**Not acted on**, per §5.

---

## 9. Baselines — §4

**`baselines/ruleset-7/` is cut. Five seeds, both halves, real inference. All five seals verify.**

| seed | events (r6 → r7) | scopes | held out | rate | seal |
|---|---|---|---|---|---|
| 1 | 695 → 760 | 13 | 7 | 54% | `eb1601f18a4661bc…` |
| 7 | 529 → 594 | 8 | 4 | 50% | `5a74737ada5f75e1…` |
| 42 | 878 → 954 | 13 | 4 | 31% | `abd551f94a6fea58…` |
| 1234 | 826 → 897 | 12 | 1 | 8% | `81238cbf7d23e577…` |
| 2025 | 700 → 772 | 12 | 4 | 33% | `b8e7a980e36a5ee0…` |
| **panel** | **3,628 → 3,977** | **58** | **20** | **34.5%** | |

**Holdout rate: 34.5% (20 of 58), against ruleset 6's 36% (21 of 58).** Same panel, same scope lists,
same checker fingerprint (`60f5b325bf6a8a97…` — the checker was not touched). `wb holdouts --set
ruleset-7 --against ruleset-6` still reports **Escalate**, as it did at ruleset 6, on the same two
grounds: 20 holdouts is above the guard's ten, and the per-seed spread is `range=[8, 53] width=45`
against a stated 20 points. Neither is a regression — ruleset 6's own spread was `width=25` and
ruleset 3's was `width=42`, so that criterion has never been met and failing it is not news.

Each world was generated with real inference on `qwen3.6:latest`, digest `07d35212591f…`, matching the
ruleset-6 set. `TheEngineStillReproducesTheSealedBaselines` passes on all five, so Layer 5 is back
after going quiet across the bump — which is what §4 said to expect, and it was visible in the top line
the whole time rather than hidden.

**Two things reported rather than acted on.**

`Went non-zero to zero on survivors: outcome.` This looks like a signal and is not attributable: the
ruleset-7 chronicles are a *fresh generation*, and generation is not reproducible run to run at
temperature 0. A rule firing differently across two renders of different prose says nothing about the
ruleset. `outcome` is also one of the three rules with no `Extracted` call site at all, so its counters
are the unfalsifiable slot §6 of the project reference already names. Two consecutive identical runs is
the evidence standard here, and this is one run.

**The invocation is not in the manifest, and that nearly invalidated this comparison.** The chronicle a
baseline seals depends on `wb book`'s section arguments, and nothing in the manifest records them. Cut
with the defaults, ruleset 7 produced a **42**-scope panel against ruleset 6's 58 — a holdout rate of
36% on a different denominator, which would have read as "unchanged" while comparing different things.
The original invocation (`--factions all`) was recovered by diffing the two scope lists by hand, and the
ruleset-7 cut now records it in its notes. A field of its own is carded: a free-text note is a
convention, and this project's standing position is that a property of an artefact which nothing checks
is a property that will drift.

---

## 10. Carded, not built — §8

- **`GoalKind.TakePlace` is created by nothing.** Consumed at `ActionPhase.cs:46`, added nowhere.
  Seventh declared-and-unreachable construction, with the four dead event kinds, `Rivalry` and
  `Vassal`. The brief's suggested standing assertion — every declared kind has a writer, or is
  explicitly marked reserved — is not built here.
- **`RetireGoals`' dead-owner branch.** Split out of the shared condition in §1 and it reads zero: the
  reducer clears an actor's goals on the death event, earlier in the same year, so this branch cannot
  be the one that catches a dead owner. Kept with its own label so the zero stays visible.
- **`Event.GetString` returns the first of duplicate keys** while `ApplyDeltas` applies all of them.
  Pre-existing and not touched here, but it is the mechanism behind instrument defect 1 and it is a
  latent hazard for any consumer that reads a key an emitter writes more than once.
- **The baseline manifest does not record the invocation that produced its chronicle.** §9 has what
  this cost. It is the same family as everything else on this list: a property of an artefact that
  nothing checks. The fix is a manifest field beside `checker_rules` and `inference`, and it wants its
  own decision because it changes the seal format for every future cut.
- **`wb holdouts` compares two sets without checking their denominators match.** It prints "13 scopes
  at ruleset-6, 13 at ruleset-7" and then reports a panel rate regardless of whether those agree. It
  would have published a rate over 42 scopes against one over 58 without comment. A guard there is
  cheap and is the machine-checkable half of the item above.

---

## 11. Machine state

- `Ruleset.Version` = **7**. `Provenance.cs` carries the record-change verdict, the key-level property,
  the off-switch result and the three moved counts.
- `SimConfig` unchanged — no threshold moved.
- The renderer is untouched. No checker rule was added. Both goal kinds are
  `Significance.Bookkeeping`, so the readable log and the chronicle do not see them.
- Instrumentation invariance holds with the goal census attached, alone and beside the plot ledger and
  the geography probe, on all five seeds.
- New CLI: `wb goals`, `wb bookpressure`, `wb run --no-goal-record`.
