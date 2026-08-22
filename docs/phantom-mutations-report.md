# Phantom relation mutations — report

**Ruleset 7 → 8.** A declaration of war no longer claims it broke an alliance that was not there.
**The log loses keys and the world does not move**, asserted at the key level and at the level of all
27 state components against every sealed ruleset-7 baseline.

All three items landed. §1's audit found **two** sites with the defect: the one the brief named, which
is repaired, and a second — `leg:-`, a legitimacy delta against no faction — which is reported,
bounded by a test, and left for its own change because repairing it costs three earlier rulesets their
byte-level reproduction. §3, §4, §5 and §6 are done. §2 is answered but its 90-seed form is blocked by
a pre-existing crash that is not this brief's.

Generated figures come from `wb mutations`, `wb warpanel`, `wb stage` and `wb baseline cut`
(2026-08-19). Re-run them rather than trusting this transcript.

---

## 1. The mutation-notify audit — §1.1

Full table in `docs/mutation-notify-audit.md`. **Every payload key the reducer applies is probed
against the state on both sides of its own event**, so the verdict is a difference and not a reading
of the call sites — §4 of the project reference is about what happens when a measurable property of
the code is written out by hand.

**25,435 keys examined across the reference panel.** Idle splits three ways, and only the first is the
`GoalBook.Remove` family.

| family | keys | real | no referent | no entity | already there | absorbed | guarded before this brief |
|---|---|---|---|---|---|---|---|
| `arcEnd` | 129 | 129 | 0 | 0 | 0 | 0 | yes — every site walks `OpenArcs()` |
| `ctrl` | 7 | 7 | 0 | 0 | 0 | 0 | yes — derived from `HoldingsOf` / the taken places |
| `disown` | 59 | 59 | 0 | 0 | 0 | 0 | yes — iterates the faction's own members |
| `goalArc` | 133 | 133 | 0 | 0 | 0 | 0 | yes — the reducer **throws** on an absent goal |
| `goalEnd` | 419 | 419 | 0 | 0 | 0 | 0 | yes — the reducer throws |
| `goalStep` | 765 | 765 | 0 | 0 | 0 | 0 | yes — the reducer throws |
| `join` | 66 | 66 | 0 | 0 | 0 | 0 | yes — skips an actor who already has a faction |
| `leg` | 2047 | 1754 | 0 | **2** | 0 | **291** | **no. The second site — §1.5.** |
| `pop` | 540 | 540 | 0 | 0 | 0 | 0 | n/a |
| `rel` | 15484 | 15438 | 0 | 0 | **46** | 0 | n/a — see §1.6 |
| `relDel` | **132 → 118** | **118** | **14 → 0** | 0 | 0 | 0 | **no. The site the brief named.** |
| `stock` | 4782 | 4684 | 0 | 0 | 0 | **98** | n/a — a real delta a floor absorbs |
| `treas` | 886 | 885 | 0 | 0 | 0 | **1** | n/a |

`goalAdd` is not probed and says so: it creates rather than mutates, so there is no prior state to
compare, and `GoalBook.Restore` already refuses a duplicate rather than placing one quietly.

### 1.1 The list is two sites long, and one of them is repaired

**`DIPLO.WAR_DECLARED` / `relDel` — 14 keys, the site the brief named.** Repaired, §1.2.

**`LIFE.DEATH_VIOLENT` / `leg:-` — 2 keys, found by the audit.** Reported and not repaired; the
reasoning is §1.5.

Everything else was already guarded, and two of the guards are the reference shapes §1.2 pointed at:
`RelationEnds.OnItsOwnEvent` returns null and emits nothing where the tie is not live, and the goal
keys refuse rather than shrug — the reducer throws when the book does not hold the goal it names.

So the brief's halt condition — *the audit returning a list too long to repair within this brief* —
fired **partially**, and the brief's own instruction is what was followed: repair what fits, report
what does not.

**A note on how the second site was nearly missed.** The first version of the classifier listed the
families that could have an absent referent, and a key naming the null id fell through that list into
`absorbed` — a legitimacy delta the floor ate, which is not a defect. It surfaced only when the
classifier was changed to treat an absent probe as absent whatever the family. That is the
hand-written-list trap in miniature, inside the instrument built to avoid it, and it is why the audit
now derives the verdict from the reading rather than from a list of families.

### 1.2 The guard

`DeclareWar` had carried two unconditional `RelDel` keys since alliances existed. The guard now lives
in `RelationEnds.Into(draft, state, a, b, kind, guarded)`, beside the other two severance shapes
rather than at the call site, because the call site is what forgot it. **Each direction is checked on
its own** — an alliance is written both ways and should be live both ways, and "should be" is exactly
what the unguarded version assumed.

Emitted in the payload position the two unconditional keys occupied. `EventDraft.Set` appends, so a
declaration that does sever a live pact has to write these keys where it always did, or the payload
has been reordered rather than shortened — and §1.3's property is a subsequence walk, which
reordering fails and shortening does not.

### 1.3 The correctness property — the log loses keys, the world does not move

`PhantomMutationTests`, against every sealed ruleset-7 baseline.

| seed | events, r7 → r8 | alliance severance keys, r7 → r8 | dropped | events added / removed | state components differing |
|---|---|---|---|---|---|
| 1 | 760 → 760 | 10 → 6 | 4 | 0 / 0 | none of 27 |
| 7 | 594 → 594 | 8 → 6 | 2 | 0 / 0 | none of 27 |
| 42 | 954 → 954 | 16 → 14 | 2 | 0 / 0 | none of 27 |
| 1234 | 897 → 897 | 16 → 12 | 4 | 0 / 0 | none of 27 |
| 2025 | 772 → 772 | 10 → 8 | 2 | 0 / 0 | none of 27 |
| **panel** | **3977 → 3977** | **60 → 46** | **14** | **0 / 0** | **none** |

Every dropped key is a `relDel:…:Alliance`; the test asserts the *kind* of each omission rather than
only counting them, because a record that dropped a real severance would satisfy "is a subsequence"
perfectly. It also asserts something was dropped, so the theory cannot be satisfied by a change that
did not happen.

**Neither halt fired.** No event appeared or disappeared and no state component moved.

### 1.4 An off-switch, because four standing claims depended on the old emission

Not in the brief, and the alternative was retiring four proven properties. Removing keys
unconditionally broke every byte-level claim that reaches back past ruleset 8:
`GoalRecordTests.TurningGoalRecordingOffGivesBackRuleset6`,
`GoalRecordTests.Ruleset7ExtendsTheRuleset6RecordAndChangesNoKeyInIt`,
`RelationTerminationTests.TurningTheTerminationRulesOffGivesBackTheOldRuleset`, and the ruleset-5
comparison behind it — 15 failures across the panel.

`Simulation(guardSeverances: false)` restores ruleset 7's emission, so those comparisons reach back
as far as the switches they turn off. That is the design `RelationTerminationTests` already documents
in its own words — *each later ruleset adds a switch, and a comparison reaches back as far as the
switches it turns off* — and ruleset 7's note already calls its off-switch the stronger claim. A world
run with it off is a diagnostic artefact on the same footing as the other three: `sever-unguarded` in
the header and the genesis event, refused by `wb baseline cut`, and `wb run --no-sever-guard` says so
on the way past.

### 1.5 The second site — `leg:-`, reported and deliberately not repaired

`ActionPhase` emits `.Leg(victim.Faction, -8)` on a violent death. Where the victim belonged to no
house that writes **`leg:-`** — a legitimacy penalty against nobody. The site checks
`victim.Faction.IsNone` two lines earlier, for the grievance edge and for whether he was a leader, and
not for this. Twice across the panel: `e:581` Y39 on seed 1, and one on seed 1234.

**The reducer drops it without a word, and by a different mechanism than the severance.** `leg:-`
splits into *two* tokens where `case "leg" when parts.Length == 3` wants three, so it matches nothing
at all — it is not an absent referent the reducer looks up and shrugs at, it is a key the reducer never
recognises. Same emitting-and-ignoring shape, one step earlier.

**Why it is not fixed with the severances.** The natural guard is in `EventDraft.Delta`, where every
delta passes and no rule has to remember — and that is exactly what puts it out of reach of the
severance off-switch, which reaches the one rule that writes severances rather than the eight that
write deltas. I implemented it, and it cost rulesets 5, 6 and 7 their byte-level reproduction: seven
assertions went red because `guardSeverances: false` no longer gives ruleset 7's log back. Repairing
it properly means putting a switch where every draft passes, which is a wider change than the one-site
repair it was found during — so §1.1's instruction applies: report and stop.

**Bounded rather than left open.** `TheOnlyKeysNamingNobodyAreTheTwoAlreadyKnown` asserts the count is
**exactly two**, panel-wide, and that both are `leg:-` on a violent death. It cannot spread while it
waits, and a repair has to bring the number down deliberately rather than slipping past unnoticed.

### 1.6 Two adjacent findings the audit turned up, reported and not fixed

**616 payload keys are written twice into their own event**, across 278 events — `ECONOMY.YIELD`
(`rel` and `stock`), `ECONOMY.FAMINE` and `ECONOMY.PLAGUE` (`pop`). This is known and deliberate:
`GoalRecordTests` documents it (*an `ECONOMY.YIELD` moves grain into and out of the same store*) and
its subsequence walk exists because of it. Every occurrence is a real delta applied in order, and both
survive a round trip because `Event.Data` is an ordered list and `JsonDocument` preserves duplicate
property names. Quantified here for the first time. It is also the whole of the `rel` row's 46 idle
keys: `DecayAndDrift` writes one edge's grievance decay and its balance-of-power increase into the
same event, and where those cancel the net effect on that edge is nothing.

**390 keys are absorbed by a floor** (`leg` 291, `stock` 98, `treas` 1) — a legitimacy penalty levied
on a house already at zero, a population loss at an empty place. **Deliberately not repaired.** The
rules did apply the penalty and the world's rule is that legitimacy stops at zero; suppressing the key
would delete the record of a penalty that was levied. Counted, reported, and excluded from what `wb
mutations` exits non-zero on.

---

## 2. What this changes downstream — §2

**Nothing, and it could not have.** The panel's ties-ended figure counts `Trade` terminations by state
diff through `RelationTrajectory`, not by counting `relDel` keys. The phantom keys were all
*alliances* and they deleted nothing, so they produced no termination to inflate. The brief's premise
— *those come from deletions, so they are inflated by an unknown amount* — reads the figure as a key
count, and it is not one.

**Measured rather than argued.** The panel was re-run under both emissions and diffed:

| arm | trade ties ended, 84 seeds, guard off | guard on |
|---|---|---|
| null | 0 | 0 |
| collapse | 328 | 328 |
| war | 474 | 474 |
| random | 498 | 498 |

**Identical per seed, on all four arms, on every figure the panel prints** — ties ended, runaway year,
distinct shapes, event counts and the three pre-registered contrasts. The two full outputs diff clean.

**`war − null` did not move**, which is what the brief pre-committed to. Nor did the degeneracy guard.

### 2.1 The 90-seed form is blocked, by something that is not this brief

**Six of the 90 panel seeds crash before they finish** — 9100001, 9100004, 9100035, 9100067, 9100071,
9100088 — so the paired 90-seed experiment cannot be re-run and the figures above are over the 84 that
can. The sealed ruleset-6 figures are over 90 (null 0, collapse 364, war 516, random 542), so they are
not comparable to the table above; what is comparable is the A/B, and it is exact.

**The crash predates this change**, verified by re-running with the unguarded emission and getting the
identical exception:

```
System.InvalidOperationException: e:597 names goal 75, which the book does not hold.
The fold has diverged from the run that wrote this log.
  at EventReducer.GoalOn → ApplyDeltas → Apply → Chronicle.Emit
  at ActionPhase.CourtSupport → SeizeLeadership → ActAsPerson → Run
```

**The diagnosis.** `ActionPhase.Run` iterates a snapshot of the goal book. By the time
`SeizeLeadership` reaches a goal, something earlier in the same tick may have removed it — a `disown`
key clears everything its owner held — and `ActAsPerson` checks that the owner is alive and has a
faction but not that the book still holds the goal. `GoalRecord.Advance` then writes `goalStep:75`,
and the reducer refuses, correctly: that guard is the one this brief's family is about, and here it is
working.

**Not fixed here, deliberately.** It is a ruleset-7 defect the four-arm harness exposes because the
arms change tie termination, hence the world, hence goal lifetimes — and the panel has not been run
since goals entered the fold. Whether the right behaviour is to skip the action or to lapse the goal
is a design decision, and the brief contains none. Any fix is provably world-preserving for every log
that exists, since the situation currently ends the run rather than producing a world — which makes it
a clean, small, separate piece of work rather than something to decide at the end of this one.

### 2.2 The step-two report

No correction is owed. Its figures are unchanged, and `docs/ruleset-6/war-panel.txt` remains the
record of the sealed run it reports.

---

## 3. The role-and-outcome table splits its third column — §3

`AttemptTally` now carries four columns and the sheet prints four:

| column | what it counts |
|---|---|
| failed attempts on them | object, outcome failed |
| killed | object, outcome succeeded |
| killings they ordered | subject, outcome succeeded |
| attempts they ordered that failed | subject, outcome failed |

**The brief's finding reproduces exactly.** Nine of seed 42's twenty-eight people sponsored an attempt
and never a killing, and they are the nine it names: Reweld Wul (a:1), Thulgea Bu (a:9), Pouldrir Ho
(a:13), Saern Meastouth (a:28), Heillvar Maer (a:29), Thosruld Lul (a:34), Leimmil Theall (a:38),
Diweith Mound (a:42), Thres Thrild (a:57) — one failed sponsorship each, no successful one. So *how
many killings did Reweld Wul order?* answered 1 from the old table and answers 0 from this one.

The sheet now names those nine in prose, so the case the old label got wrong is stated rather than
left for a reader to spot. The prose is matched to the columns, and the wrong-answer field on the
staged question calls out both traps: the record count, and the pooled sponsor figure.

**The partition is the weaker half and it held before the repair too** — which is exactly why the
mislabelling survived. `TheRoleAndOutcomeColumnsPartitionTheRecordCount` therefore does both: it
asserts the four columns sum to the record count, panel-wide, and re-derives both sponsor columns from
the record so a table whose third column became a total again would pass the sum and fail the test.

---

## 4. The `?` reconciliation — §4

**Reading one holds: the report miscounted its starting state; the output is fine.**

| file | `?` spans | spans with a year |
|---|---|---|
| pre-repair sheet | **14** | 5 |
| regenerated sheet | **0** | 19 |

Counted directly in both files. `docs/repair-reference-material-report.md` said *11 resolved, 8
already carrying a year*; both splits sum to the 19 terminations, which is why the wrong one looked
consistent. **Zero `?` remain**, so the halt on a remaining `?` did not fire because there was nothing
to fire on — and `EverySpanOpensWhereTheRecordMakesTheTie` asserts that zero independently and
panel-wide, which is the check that makes the reading safe rather than merely plausible.

That report is corrected in place with a dated note, per the brief. The second reading — three rows
missed with the halt silently not firing — is ruled out by the direct count and by that test.

---

## 5. Ruleset 8 and the cut — §5

**Five seeds cut, both halves, real inference, all five seals verifying.** Each set carries the seven
artefacts the archive split still owes: chronicle, findings sidecar, unverified passages, `renders.json`,
world log, readable log and board.

| seed | records | seal |
|---|---|---|
| 1 | 760 | `7cb60c2b…` |
| 7 | 594 | `965ece0c…` |
| 42 | 954 | `d5925c04…` |
| 1234 | 897 | `81ebe108…` |
| 2025 | 772 | `3fd3d847…` |

`wb baseline check` passes on all five. The checker fingerprint is `60f5b325…` — **identical to
ruleset 7's**, which is correct: no checker file changed, so the rules that judged these chronicles are
the same rules.

**Cut twice.** The first cut stamped `b0a6c4b`, which contains ruleset 6, because the ruleset was
still uncommitted — see §10. After committing as `68a7198` the five sets were cut again, and these are
those seals. **The second cut needed no inference at all:** only the world header carries the commit,
so the events were byte-identical and `wb book --check-only` hit every cached render, reproducing all
five chronicles and findings sidecars byte for byte. What would have been five seeds of model time was
a rebuild, five re-runs and a re-check.

**Layer 5 is back at 4 of 4 layers.** It went quiet between the bump and the cut, exactly as the brief
said it would, and `wb test --baseline baselines/ruleset-8/seed-42` now runs it: 0 failed, 15 noted.
The two `distinct deep-chain shapes: 58, expected >= 60` failures in layers 1 and 3 are **pre-existing**
— the same two fire against the sealed ruleset-7 world — and are not this change's.

### 5.1 The holdout rate rose, and it is not the ruleset

**26 of 58 scopes held out (44.8%), against ruleset 7's 20 of 58 (34.5%).** Same 58 scopes.

| seed | scopes | r7 held out | r8 held out |
|---|---|---|---|
| 1 | 13 | 7 (53%) | 6 (46%) |
| 7 | 8 | 4 (50%) | 2 (25%) |
| 42 | 13 | 4 (30%) | 7 (53%) |
| 1234 | 12 | 1 (8%) | 5 (41%) |
| 2025 | 12 | 4 (33%) | 6 (50%) |
| **panel** | **58** | **20 (34.5%)** | **26 (44.8%)** |

**Established as generation variance rather than a consequence of the change**, by the check the
project already has for exactly this: restore ruleset 7's `renders.json` beside the **ruleset-8** world
and re-run `wb book --check-only --factions all`. Ruleset 7's prose against the ruleset-8 world produces
a **byte-identical chronicle and byte-identical findings** — the same 7 fatal findings it had against
the ruleset-7 world. So the world the checker sees has not moved, and nothing in the removed keys
reaches a prompt.

What moved is the prose: the ruleset-8 renders were generated into a fresh directory with no cache to
inherit, so every section was written again by a non-deterministic model. The rate went up on three
seeds and down on two, and `date` accounts for 11 of the 26. **Not adjudicated here** — it is a
rendering figure, not a ruleset one, and this brief changed no checker rule.

---

## 6. Re-stage — §6

All seven artefacts regenerated against the new seal `d5925c04…` into `out/reference-set-r8/`.
`wb stage --seed 42` reports **no halt condition met**, and everything still says `verified: no`.

**Staged twice, once per cut.** The second re-stage, against the re-cut seal, moved **one line per
artefact and nothing else** — the seal itself, in the five artefacts that quote it. `checks.md`
does not quote it and is byte-identical, live retrieval probes included, which also says the planner
gave the same answers on a second run. The table below is the substantive diff, ruleset 7 → 8.

The staging output directory is now named for the ruleset rather than hardcoded to `r7`, so a bump
cannot quietly overwrite the previous set — which is the "before" side of every diff like this one.

**Every row that moved, and nothing else:**

| file | what moved | explained by |
|---|---|---|
| `record-history.md` | **`e:718` loses its two `relDel:…:Alliance` keys** | §1 |
| `record-history.md`, `record-bookkeeping.md` | the ruleset and seal line | §5 |
| `facts-sheet.md` | the role-and-outcome table gains a fourth column; the third changes for **exactly the nine** people | §3 |
| `facts-sheet.md` | the held-out scopes list | §5.1 |
| `questions.md` | the assassination answer splits the sponsor figure | §3 |
| `questions.md` | held-out-scope flags, 5 → 18 | §5.1 |
| `report.md` | holdout figures, 4 of 13 → 7 of 13 | §5.1 |
| `secrets.md`, `checks.md` | ruleset and seal only | §5 |

**No row moved that §1, §3, §4 or §5 does not explain.** `e:718` is the record the brief named, and the
loss of its two phantom keys is the only content change in the entire 954-record history file.

### 6.1 One expectation in the brief does not materialise, and the reason is the finding

§6 expected the terminated-relations count to move from 19 and the f:2↔f:3 alliance's history to
become "two terminations rather than an ambiguous three".

**It stayed at 19, and the alliance already had exactly two terminations.**

| tie | span | ended by | record |
|---|---|---|---|
| Alliance f:2 ↔ f:3 | 2 – 27 (25y) | `DIPLO.WAR_DECLARED` | `e:463` |
| Alliance f:2 ↔ f:3 | 39 – 39 (0y) | `POLITY.COLLAPSE` | `e:735` |

**Because a key that deletes nothing produces no termination.** `RelationTrajectory` diffs the graph
the reducer produces, so `e:718`'s phantom severance never appeared in that table at all — the "three"
the brief counts is three *claims in the log*, of which the derivation only ever saw two. Which is
precisely why the defect was invisible for as long as it was, and why the audit had to look at keys
rather than at derived figures.

Coverage after the re-stage: 30 candidates, 5 negative premise, 2 supplied figure, 2 terminated
relation, 18 suite-eligible — every requirement still met and every count unchanged from ruleset 7.

---

## 7. Tests — §7

| test | what it pins | seeds |
|---|---|---|
| `NoEventClaimsAChangeTheStateDoesNotHold` | no mutation key names something the state does not hold | 5 |
| `TheOnlyKeysNamingNobodyAreTheTwoAlreadyKnown` | the unrepaired `leg:-` site stays at exactly two keys | 5 |
| `ACausalTraceNeverWalksBackIntoTheWorldsGenesis` | no causal trace returns a genesis row except as the tip | 5 |
| `NoStagedCausalAnswerIsABareRecordId` | every causal answer is words, or declines; none cites a genesis row | 5 |
| `TheStagedCausesAndTheTraceAgree` | the two rules for "what is a cause" give the same answer | 1 |
| `EverySealsCommitContainsTheRulesetItSeals` | every manifest's commit contains the ruleset it claims | all sets |
| `TheUnrepairableSetsAreTheTwoRulesetsThatWereNeverCommitted` | the exception list is exactly the two that cannot be repaired | — |
| `TheAuditProbesEveryKeyFamilyTheReducerApplies` | the audit's coverage list matches the reducer's own `switch` | 1 |
| `Ruleset8DropsOnlyPhantomSeverancesFromTheRuleset7Record` | same events, same order, only phantom severances dropped | 5 |
| `Ruleset8LeavesTheWorldWhereRuleset7LeftIt` | all 27 state components equal the sealed ruleset-7 fold | 5 |
| `NoSpanOpensAfterItClosesAndNoTieEndsTwiceWithoutAMaking` | every ending carries a making, and it postdates the previous ending | 5 |
| `TheRoleAndOutcomeColumnsPartitionTheRecordCount` | four columns sum to the record count, both sponsor columns re-derived | 5 |

**The first is non-vacuous by assertion.** It requires more than a thousand keys examined and requires
`relDel`, `rel` and `goalEnd` to be among the families probed — so a site that stopped emitting its
keys entirely would fail rather than pass with a clean table. The second test closes the way that
coverage could be narrowed silently: a key family added to the reducer and not to the audit fails
there rather than being skipped.

**One caveat worth stating.** The span test could not have caught `e:718` on its own. A key that
deletes nothing produces no termination, so there is nothing for it to check — which is why the audit
exists and why it comes first. The span test is for the inverse case.

---

## 8. Halt conditions — §8

| condition | fired |
|---|---|
| the §1.1 audit returning a list too long to repair | **partially** — two sites, one repaired, the other reported and bounded (§1.5) |
| any event added or removed, or any state component moving | no |
| `war − null` moving in §2 | no — every panel figure identical per seed |
| any `?` remaining after §4 | no — zero, counted directly |
| a row moving in §6's diff that no item explains | no — every row traces to §1, §3 or §5 |
| suite not green after the ruleset-8 cut | no — `dotnet test` is **703 passed, 2 skipped** |

**Two things outside the list.**

Six of the 90 four-arm seeds cannot complete, so §2's 90-seed form is unavailable. §2.1 has the
diagnosis; the contrast it was there to protect is answered by the A/B instead.

`wb test` reports 2 failures in layers 1 and 3 — `distinct deep-chain shapes: 58, expected >= 60`. The
same two fire against the sealed **ruleset-7** world, so they predate this change and are not its. Layer
5 itself is clean at 0 failed, 15 noted.

---

## 9a. Three findings from the second reader's part 3, acted on

`docs/facts-sheet-second-reader-part-3.md`. Both substantive findings held; one supporting claim in it
did not, and is noted at the end.

### A genesis row is not a cause

Two of the four staged causal questions answered *why was war declared over Threi Cut* with `e:9` —
the record of Threi Cut coming into existence. A stopping condition presented as a finding.

**Fixed in the reading, not in the record.** The edge is true: the war was fought in pursuit of a goal
formed because that place exists, and `PerceptionPhase` writes it deliberately — *"the honest answer
to why does this faction want that place is because that place is there and it has ore in it."* That
is a fair answer about a **goal** and not about a **war**, and the war inherits the goal's cause
wholesale through `.Because(goal.Cause)`. Deleting the edge would destroy a true provenance fact and
cost a ruleset bump; the defect is offering it as an answer.

So `ContextPackBuilder.Trace` now ends a branch at a genesis row rather than including it — the same
shape as the rule already beside it for secrets. **This was a product defect, not only a staging
one:** `QueryEngine` retrieves through `Trace` for every question the planner calls causal, so the
genesis row was reaching the model as the last link in the chain. 51 cause edges across the panel
point at a genesis row; 18 events would be left with no cause at all if the edges were removed from
the log, which is the measurement that argued against doing so.

### A causal answer states its cause in words

All four read `the recorded causes, walked back: e:506`, which nothing can be held against: any
response mentioning that id satisfies it, including one naming the wrong person. Every other category
in the file answers in words, and the *what would a wrong answer look like* field cannot be written at
all for a pointer.

They now read `because Sou Dra (a:22) is cast out of the Kebarrow Compact (f:2) — the losing side of
an open challenge (e:506)`, and the wrong-answer field says naming the record without the reason is
also wrong. The two that bottomed out on genesis now decline: *the record names no cause for it beyond
the world's own genesis, which is where the walk stops rather than what it found.*

**One question left the suite because of it, and that is the finding underneath the finding.**
Suite-eligible went 18 → 17: *Why did the Wurn League take Threi Cut in year 2?* was eligible only
because the **genesis row** came back under causal retrieval. With `e:9` gone from its supporting
records, the conquest record itself is not reached by that path — which was always true and was being
masked by the row that should never have been cited.

### The holdout rate is retired as a halt condition

Doctrine in §4 of the project reference, a void-note at the head of
`docs/archive/brief-closing-ruleset-6-report.md` where the gate was cleared, and `wb holdouts` now
prints the caveat above its own scope table, since the rate is the first number anyone reaches for.

The evidence is §5.1: 34.5% → 44.8% on worlds byte-identical apart from fourteen payload keys nothing
reads, with the warm re-cut as the control at zero movement. What survives untouched is everything
`wb holdouts` says about code structure — call sites, floors, which rules extract nothing — because
none of that was ever a rate comparison.

### One claim in part 3 that does not hold

It says `e:9` is in `record-bookkeeping.md`, and concludes the split rule and the causal trace disagree
about what belongs to the history. **`e:9` is in `record-history.md`** — zero occurrences in the
bookkeeping file, one in history. The split rule is `Significance == Bookkeeping` **and names nobody**,
and `e:9` names `subject=Threi Cut (p:8)`. It is one of the 164 quiet rows §2 already documents as
staying in the history file for exactly that reason, so there is no disagreement. The finding stands
without it.

Two smaller slips: the role/outcome split has **four** staged questions, not three — Stonand Ker
(1 failed on him / 1 killed him / **5 ordered** / 0 failed orders) is missing from the list, and he is
the largest case. And the cuts are 33 hours apart, not "three weeks".

---

## 10. A seal's commit now has to contain the ruleset it seals

The first ruleset-8 cut stamped `engine_commit b0a6c4b`, and **that commit contains ruleset 6** — the
code was still uncommitted, so the build's source metadata carried HEAD. A seal names a commit so that
a reader can go there, build, and regenerate the world; that one named a build which would produce a
different history. The seal verified, every artefact hash matched, and the single property the seal
exists to carry did not hold.

**It had happened twice before, and nothing checked.** Auditing every manifest in the archive:

| set | manifest claims | commit contains | |
|---|---|---|---|
| `ruleset-3` | 3 | 3 | ✓ |
| `ruleset-4` | 4 | 4 | ✓ |
| `ruleset-5` | 5 | **4** | ✗ |
| `ruleset-6` | 6 | 6 | ✓ |
| `ruleset-7` | 7 | **6** | ✗ |
| `ruleset-8` | 8 | **6** → **8** | ✗ → ✓ |

Always the same way: bump the ruleset in the working tree, cut, commit afterwards.

**Rulesets 5 and 7 exist in no commit of this repository at all**, so those two sets can never be
re-cut correctly — the code that produced them was never stored. Walking the branch confirms it: no
commit in the history carries either version. That is unrepairable rather than merely outstanding, and
it is recorded as such.

**Three things now hold.** `BaselineArchive.Cut` refuses to seal a baseline whose commit does not
contain its ruleset, naming the fix in the message — so a fourth instance cannot be created.
`SealProvenanceTests.EverySealsCommitContainsTheRulesetItSeals` walks every manifest under
`baselines/` (discovered, not listed) and asserts the claim, with the two unrepairable sets named as
explicit exceptions and a floor on how many must be confirmed, so a run where everything fell into the
exception list fails rather than reads clean. And
`TheUnrepairableSetsAreTheTwoRulesetsThatWereNeverCommitted` asserts the exception list is exactly
those two and re-derives the reason — that no commit carries either ruleset — so the list cannot be
widened to fit a new failure.

The ruleset-8 set was re-cut against `68a7198` and is the first set since ruleset 6 whose commit
contains its own ruleset. The checker fingerprint is unaffected either way: it hashes five checker
files as stored in git, none of which this brief touched, which is why it is identical to ruleset 7's.
