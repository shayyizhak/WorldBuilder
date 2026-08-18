# Report — step two: relations become terminable

Run against `docs/brief-step-two-relation-termination.md`.

> ## HALTED, on §10's second and third conditions
>
> **The degeneracy guard fires and a Layer 1 invariant regresses.** Four of five worlds finish
> with zero live trade ties, and seed 99's `distinct deep-chain shapes` falls 69 → 45, from
> passing to failing. Ruleset 6 is implemented, measured and committed; **`baselines/ruleset-6/`
> was deliberately not cut**, because sealing a ruleset under a triggered halt spends five seeds
> of inference on a world state that is up for decision.
>
> Everything not downstream of the halt was completed: §1, §2, §3, §5, §6, §7 and §8 are done and
> reported below.

**Entry state:** ruleset 5, 599 green, 0 failing, 2 skipped.
**Exit state:** ruleset 6, **607 green, 18 failing**, 2 skipped — categorised in full at the end.
Sixteen of the eighteen are the ruleset bump's bookkeeping; two are real.

---

## The order of operations, because it is the whole safeguard

1. `docs/brief-step-two-design.md` committed — every §2/§3/§4 decision and the one constant, with
   its argument. Commit `e4f4bd3`.
2. Amended once, still before any run, replacing decay with a timeout. Commit `6c39625`, and the
   superseded argument is left standing in the file rather than rewritten.
3. Instruments built; **ruleset-5 "before" column measured**.
4. Ruleset bumped, mechanics landed, "after" column measured with the same code.

No figure below was available when the constant was chosen.

---

## §1 — `Event.Key` blast radius: **8.6 renders lost per render actually stale**

Measured on the ruleset-5 baselines by `wb keyshift`. One synthetic event inserted in the middle of
each seed's middle year; the key of every event in that year recomputed as it would then stand.

| seed | inserted at | events in that year | rekeyed | content actually changed |
|---|---|---|---|---|
| 7 | year 21 | 13 | 7 | 1 |
| 42 | year 28 | 21 | 11 | 1 |
| 99 | year 28 | 7 | 4 | 1 |
| 1234 | year 31 | 14 | 7 | 1 |
| 2025 | year 28 | 27 | 14 | 1 |
| **panel** | | **82** | **43** | **5** |

**43 cached renders lose their key for every 5 whose content changed.** A pack's cache key is a
hash over the `Event.Key` of every event in it, so a pack dies if *any* of its events was rekeyed,
while only a pack containing the inserted event is actually stale.

Not fixed here, per §1. It does not bite this transition — ruleset 6 changes worlds, so every
render regenerates anyway. It bites at Stage 7, where a world is kept and events are added to it.

The instrument checks its own model: events *before* the insertion point must still reproduce
their stored key, and it reports the count separately and voids the row if it is non-zero. It is
zero on all five seeds.

**Not filed against the Stage 7 card.** I have Trello tools in this session but no card id for
Stage 7, and I would rather hand you the number than guess which card it belongs on. Say the word
and I will attach it.

---

## §2 — the capability

`src/WorldBuilder.Core/Rules/RelationEnds.cs`. One helper, two shapes, one table mapping relation
kind to the event that names its ending. **A kind with no entry throws rather than deleting
quietly** — that is the guard, and it is tested.

### Termination is distinct from never-having-existed, and the distinction lives in the log

Decided deliberately against a tombstone in `RelationGraph`. The argument is in the design doc and
holds up: roughly forty `Has`/`ValueOf`/`From`/`To` call sites would each become ambiguous between
"is there a live edge" and "is there an edge", which is the absent-versus-unknown conflation
(`https://trello.com/c/QiADoVAB`) rebuilt under a new name in the subsystem that was meant to be
clean.

The property that makes the log a sufficient answer is asserted, not claimed:
`RelationTerminationTests.NoTieEndsWithoutAnEventSayingSo` walks every panel world and requires
that no tie vanishes without an event in that year saying a tie of that kind ended between those
two parties. Green on all five seeds.

**The cost, stated:** no rule can cheaply ask "did these two once trade" at decision time. Nothing
needs to today; a rule that does will need a log scan, which is what `Recent` already does.

### The compromise, recorded rather than hidden

The alliance deletion stays on the war declaration. Moving it onto `DIPLO.ALLIANCE_BROKEN` would
change the war's payload, and the war precedes the break it causes — so §5's check would fire
before the first termination on every seed with a war in it, spending the only mechanical guard
this step has to relocate a payload key between two adjacent events. So alliance breaks show in the
cause table as *not named by its event*, which is an accurate report of a real gap.

---

## §3 — a collapse emits **one event, and it is `POLITY.COLLAPSE` itself**

Not per-relation events: a house dying with twelve edges would emit twelve, three of them a trade
collapse between a dead house and somebody who decided nothing, turning a collapse year from one
readable line into thirteen and swamping the §4 cause distribution with the one cause that carries
no decision. Not a separate cleanup event either: the collapse already says the house is finished
and already disposes of its ground and its people.

It carries the count **and** the kinds, because a bare total is an unlabelled figure:

```
tiesEnded=7  tiesEndedKinds=Alliance:2,Trade:3,AtWar:2
```

plus one `relDel:` key per edge. `ACollapseSaysWhatItEnded` asserts the label agrees with the total,
so the breakdown cannot become decoration.

**It ends obligations, not memory.** Alliance, Trade, Vassal, AtWar go; Grievance, Kin, Marriage,
Fealty, Rivalry stay. An obligation needs two parties to hold it; a fact does not stop being a
fact, and a grudge against a house that is gone is exactly what a world with a memory should keep.

---

## §4 — trade termination

### The constants, with the arguments that predate the runs

| cause | in | argument |
|---|---|---|
| war declaration | yes | Definitional, no constant. "At war" and "trading" are not two states available at once. |
| partner collapse | yes | Falls out of §3. No constant. |
| distance | **no** | Trade formation does not consult geography — `BuyGrain` and `TradePact` ask `Geo` nothing. Terminating on a fact that was already true at formation is asymmetric in the direction that reads as a bug, and since places do not move it would cull every too-far tie once and never again. |
| disuse | yes | **Twenty years without a dealing.** Argued from the five-year pact cooldown (twenty is four consecutive missed opportunities, so an active tie never reaches it) and from the length of a reign. |

The disuse rule is a **timeout, not a decay**, and the reason is recorded in the design doc: decay
would move what `ProposeAlliance` scores against in every year of every world, and would have
diverged the log a decade before the first termination.

### The degeneracy guard fires

`wb ties`, same code on both sides.

| seed | r5 made | r5 ended | r5 peak | r5 final | r6 made | r6 ended | r6 peak | **r6 final** |
|---|---|---|---|---|---|---|---|---|
| 7 | 3 | 0 | 3 | 3 | 4 | 4 | 2 | **0** |
| 42 | 5 | 0 | 5 | 5 | 7 | 7 | 5 | **0** |
| 99 | 7 | 0 | 7 | 7 | 5 | 5 | 2 | **0** |
| 1234 | 5 | 0 | 5 | 5 | 8 | 8 | 4 | **0** |
| 2025 | 6 | 0 | 6 | 6 | 6 | 4 | 3 | 2 |

Peak equalled final on every ruleset-5 seed, exactly as §0 says. Under ruleset 6 four of five land
at zero. **The guard as the brief specifies it triggers, and the halt is called on it.**

### But the guard is confounded, and the confound is worth more than the verdict

The zeros are not the rule emptying the graph. They are the world running out of houses.

| seed | pairs of standing houses at the end |
|---|---|
| 7 | **0** |
| 42 | 1 |
| 99 | **0** |
| 1234 | **0** |
| 2025 | 6 |

A world with one house standing cannot hold a trade tie, and "final ties near zero" measures
hegemony there rather than the termination rule.

The same denominator run over ruleset 5 says something sharper. Adjudicated per `Invariants.cs`'s
standing rule — *a failing metric is adjudicated before it is satisfied*, category three, **it
measures the right thing badly** — the corrected instrument is live ties over pairs of houses that
both still hold ground:

| seed | r5 density | r5 years holding impossible ties | r6 density | r6 impossible |
|---|---|---|---|---|
| 7 | **129%** | 6 | 41% | 0 |
| 42 | **187%** | 13 | 70% | 0 |
| 99 | **182%** | 34 | 91% | 6 |
| 1234 | **165%** | 23 | 49% | 0 |
| 2025 | **104%** | 16 | 54% | 0 |

**Ruleset 5's trade graph was over 100% full.** It held more ties than there were pairs of living
houses to hold them, in up to 34 years of a 51-year history — ties to houses that had ceased to
exist. That is §0's defect as a number, and peak-versus-final could never have shown it.

Under ruleset 6 the density lands at 41–91% and the impossible years go to zero on four seeds.
**On the corrected measure the rule is not degenerate.** I am reporting both readings and not
choosing between them: the brief's guard is a pre-registered halt condition and overruling it with
a metric I defined after seeing it fail is exactly the move the pre-registration exists to prevent.
**The decision is yours.**

Seed 99's six remaining impossible years have a specific cause, below.

### Cause distribution, pooled over the panel

| cause | ties ended | carried by |
|---|---|---|
| `war` | 16 | `ECONOMY.TRADE_COLLAPSE` |
| `collapse` | 11 | `POLITY.COLLAPSE` |
| `disuse` | **1** | `ECONOMY.TRADE_COLLAPSE` |

**The twenty-year timeout fires once in five whole worlds.** War or collapse reaches almost every
tie first. The constant is not wrong so much as rarely operative, and if the rule survives the
halt that is worth knowing before anyone tunes it — the argument for twenty is untested by this
panel, not confirmed by it.

### The cause is recorded and not rendered

Per step one's standing rule — *a field with one reachable value across the panel does not get
rendered until it has two*. Two values reach `TRADE_COLLAPSE`, but one of them has **n = 1**.
Rendering the cause would put "war" in front of a reader as a universal on the strength of 16
against 1. `EventTemplates` is untouched; the sentence is still "trade between X and Y breaks
down", and `endCause` sits in the payload for anything that wants it.

### ECONOMY → non-ECONOMY: **holds**, ≥10% on every seed

| seed | before | after |
|---|---|---|
| 7 | 61/384 = 15.9% | 61/389 = 15.7% |
| 42 | 118/719 = 16.4% | 118/728 = 16.2% |
| 99 | 100/566 = 17.7% | 54/394 = **13.7%** |
| 1234 | 111/716 = 15.5% | 92/673 = **13.7%** |
| 2025 | 119/566 = 21.0% | 119/570 = 20.9% |

The mechanism is the one predicted before the run: `TRADE_COLLAPSE` is an ECONOMY event citing a
DIPLO one, so it adds to the denominator and to the cross-domain count while adding nothing to the
numerator. Seeds 99 and 1234 fall further than that alone explains, because both lost a large part
of their history — see §8.

---

## §5 — the replacement for additive-only: **holds on all five seeds**

`wb divergence`, and `RelationTerminationTests.NothingMovesBeforeTheFirstTermination`.

| seed | events r5 → r6 | first difference | first termination | verdict |
|---|---|---|---|---|
| 7 | 526 → 529 | index 84, year 5 | index 84, year 5 | holds |
| 42 | 873 → 878 | index 82, year 5 | index 82, year 5 | holds |
| 99 | 704 → 531 | index 143, year 8 | index 143, year 8 | holds |
| 1234 | 864 → 826 | index 149, year 9 | index 149, year 9 | holds |
| 2025 | 698 → 700 | index 313, year 25 | index 313, year 25 | holds |

**The first difference is the first termination, index for index, on every seed.** Nothing else
moved the world. The first termination is identified mechanically — the first event carrying
`endCause`, a key no ruleset-5 event writes — rather than by judgement about which severance was
interesting.

**The "divergent after" half is weak and is reported as weak.** One insertion renumbers every later
id, so everything after differs by renumbering alone. The instrument reports the first difference
in *content* separately; on all five seeds it is the same index, so the divergence is real and not
an artefact.

---

## §6 — the monotonic sweep

`wb standing`. 41 pieces of standing state over five worlds. **Report only; nothing found here was
repaired.**

Two columns, deliberately: the panel says what was *exercised*, and the classification is read off
the source. Counts cannot separate "no removal path" from "a path this panel never reached" — the
`quantity` rule read zero across sixty scopes while having had a live call site all along.

| standing state | exercised on the panel | read from the source |
|---|---|---|
| `Trade` edges | came down 24× in 5 of 5 | **repaired this step** |
| `Alliance` edges | came down 21× in 5 of 5 | removal path (war declaration) |
| `AtWar` edges | came down 22× in 5 of 5 | removal path (`ApplyPeace`) |
| `Grievance` **edges** | **only ever went up** | **no removal path.** `Adjust` only; nothing ever calls `Remove`. The value decays 11,148× and the edge never goes |
| `Fealty` edges | **only ever went up** | **no removal path** |
| `Fealty` value | came down 57× in 5 of 5 | a won contest writes −12 |
| `Kin`, `Marriage` edges and values | **only ever went up** | **no removal path** — arguably correct; these are facts about people |
| `Rivalry` edges and values | **never present** | **no writer anywhere.** `grep RelationKind.Rivalry` over `src` returns nothing but the enum |
| `Vassal` edges and values | **never present** | **no writer anywhere** — same |
| `ore in store` | **only ever went up** | **no removal path.** Grain spoils at 28%/yr and silver is spent; ore is produced every year by `EconomyPhase` and consumed by nothing. Raids move it between places |
| `actors/arcs/factions (ever)`, `places` | only ever went up | registries, correct by design |

### Three findings the sweep turned up that are not relation kinds

**1. `GoalBook` is not a fold of the log.** `WorldState`'s own summary says it is "the fold of the
event log" and that every mutation lives behind an internal surface only `EventReducer` calls.
Goals are the exception — created by the perception phase directly, and touched by the reducer at
exactly one point. A world replayed from its record has no goals in it at all. The sweep reports
this by name rather than scoring it, because a zero from an instrument that cannot see the thing is
the absent-versus-unknown conflation with a number attached.

**2. A landless house with nobody left never collapses.** `ConsequencePhase.DissolveLandless` skips
any faction where `members.Count == 0 && faction.Leader.IsNone`, so it never emits
`POLITY.COLLAPSE` and its ties are never cleaned up. Seed 99 has five factions ever, three
collapses, and one house standing at the end — one house is defunct without a collapse, which is
where its six remaining impossible-tie years come from.

**3. `ConsequencePhase.Collapse` is unreachable from one of its two callers.**
`RaiseLocalClaimant` calls it when `holdings.Count == 0`, and the first line of `Collapse` is
`if (holdings.Count == 0) return;`.

**The list is not too long to repair within a phase** — the substantive items are two dead relation
kinds, three no-removal-path kinds, ore, and two collapse-path gaps. Per §6 none of it was
repaired and none should be without a separate decision.

### The sweep found a defect in itself, twice, and both are worth recording

- `NeverPresent` was a category **no input could reach**: every row was emitted on every event, so
  "the sweep asked about it" was being read as "it was present". It reported `Rivalry` — which no
  rule anywhere reads or writes — as a relation kind that only ever went up. A numerator with no
  reachable path, reintroduced inside the instrument built to look for exactly that.
- The first version measured relation values as world totals only. `Fealty` writes +18 and −12 in
  one event, so the total rises by six and a real decrease leaves no trace. Values are now swept
  per edge as well, and a re-created edge is compared against nothing — carrying a dead edge's
  value forward scored a fresh 8-point tie made after a 25-point one was severed as a decrease, so
  the new mechanic's own terminations were reappearing as evidence that trade values decay, which
  they do not.

The same lesson caught `RelationTrajectory` before either: its first version read `rel:`/`relDel:`
payload keys itself and missed **the whole of war and peace**, because `AtWar` is applied by
`ApplyWarDeclared` in code and carried by no payload verb. All three folds now replay the record
through `EventReducer`.

---

## §7 — the negative control, answered honestly

**Nothing cites `TRADE_COLLAPSE`. 17 emitted across the panel, 0 cited.**

That was written down as the prediction in the design doc before the run, and no downstream
consumer was invented to improve it. `TRADE_COLLAPSE` cites the war and the pact that created the
tie; nothing cites it back, so it terminates causal chains rather than extending them, and the
control is the same weak one step one had.

There was a plausible-looking consumer available — a trade pact re-formed between two houses that
once traded could cite the collapse — and it was not written, because §7 forbids exactly that and
the resulting control would have been manufactured rather than found.

---

## §8 — the parked failures: one moved, and it is not the one that was parked

Reported unattributed, per §8.

| seed | deep chains | distinct shapes r5 → r6 | against the ≥60 bar |
|---|---|---|---|
| 7 | 56 → 56 | 45 → **45** | fails, unmoved — the parked failure |
| 42 | 132 → 132 | 99 → **99** | passes, unmoved |
| 99 | 87 → **47** | 69 → **45** | **passed, now fails** |
| 1234 | 156 → 147 | 97 → **91** | passes, moved down |
| 2025 | 84 → 84 | 66 → **66** | passes, unmoved |

**Seed 7's parked failure did not move. Seed 99's parked observation moved, and became a
failure.** Three of five seeds are bit-identical in chain structure; the movement is entirely in
99 and 1234.

Two more observations from the same two seeds, stated as observations:

- **Runaway hegemony arrives earlier.** `wb stats`' "no runaway faction before Y40" goes PASS → FAIL
  on both: seed 99 hits 70% concentration in **Y21** where it was Y46, seed 1234 in **Y36** where
  it was Y44. Seeds 7 and 42 were already failing; 2025 still never exceeds 70%.
- **Those worlds got shorter.** Seed 99 loses 173 events of 704; seed 1234 loses 38 of 864.

There is a plausible mechanism — `ProposeAlliance` scores an approach partly as `Trade / 2`, so
ending trade ties narrows the road to an alliance, and alliances are the brake on a runaway winner.
**That is a hypothesis and there is no evidence for it beyond the shapes matching.** It needs a
phase with a proper control, exactly as §8 says. Not attributed here.

---

## §9 — the baseline cut was **not** made, deliberately

`baselines/ruleset-6/` does not exist. Cutting it means the chronicle pipeline through ollama
`qwen3.6` for five seeds plus board, seal and sidecars — real inference cost — and sealing a
ruleset whose pre-registered halt condition has fired would be spending it on a state that is under
decision. The archive-contract split (`https://trello.com/c/Kl5i0hQN`) is still not done, so a
ruleset-6 set would still require both halves.

Consequence: `TheEngineStillReproducesTheSealedBaselines` fails on all five seeds with the accurate
message it was given in step one — the ruleset bumped and the set it owes has not been cut.

The staged reference set is discarded by this phase as intended; the candidate questions are
retained and only the answers and record ids die.

---

## §10 — halt conditions

| condition | state |
|---|---|
| Divergence before the first termination on any seed | **held** — first difference *is* the first termination on all five |
| Live trade ties near zero or near peak | **TRIGGERED** — four of five finish at zero. Confound diagnosed and a corrected measure given; the verdict is yours |
| ECONOMY→non-ECONOMY below 10%, or any Layer 1 invariant regressing | **TRIGGERED on the second half** — the share holds at 13.7–20.9%, but seed 99's `distinct deep-chain shapes` falls 69 → 45, pass to fail |
| A constant that cannot be argued from the mechanic | **held** — one constant, argued in a doc committed before the runs |
| The monotonic sweep too long to repair within the phase | **held** — the list is short; nothing repaired, per §6 |
| Instrumentation invariance failing, or a log-hash change from a probe | **held** — `NoCombinationOfSinksChangesTheWorld` green on all five |
| Suite not returning to green after the ruleset-6 cut | **not reached** — the cut was not made |

---

## The suite, in full: 607 green, 18 failing, 2 skipped

| failing | n | what it is |
|---|---|---|
| `InstrumentationInvarianceTests.TheEngineStillReproducesTheSealedBaselines` | 5 | `baselines/ruleset-6/` not cut. Accurate, and resolves the moment it is |
| `AdditiveRecordTests` (two theories) | 10 | **Structurally obsolete.** They assert that ruleset 5 is additive over the sealed ruleset-4 worlds. Ruleset 6 changes worlds, so the property is no longer one this build can have. The evidence for the 4 → 5 claim is in the step-one report and in `Provenance.cs`; it cannot be re-proved by an engine that no longer contains ruleset 5. Left red rather than retired, because retiring them presumes a decision about ruleset 6 that is not mine |
| `ProximityControlTests.TheFlatControlReproducesRulesetThreeExactly` (seeds 7, 42) | 2 | Same class — the flat control is asserted to reproduce ruleset 3 exactly, and ruleset 6 adds mechanics that a flat-distance run also runs |
| `PlotLedgerTests.BothOutcomesOfTheRollAreReached` (seed 99) | 1 | **Real.** Seed 99 no longer reaches both coup outcomes, because its history is 173 events shorter. A reachability regression, and the same seed as everything else in §8 |

Sixteen bookkeeping, two real — the coup reachability above and seed 99's chain shapes.

---

## Files

| file | change |
|---|---|
| `docs/brief-step-two-design.md` | new — the §2/§3/§4 decisions and the constant, committed before the runs |
| `src/WorldBuilder.Core/Rules/RelationEnds.cs` | new — the capability |
| `src/WorldBuilder.Core/Rules/ActionPhase.cs` | war ends trade between the belligerents |
| `src/WorldBuilder.Core/Rules/ConsequencePhase.cs` | both collapse paths end obligations; the disuse pass |
| `src/WorldBuilder.Core/Provenance.cs` | ruleset 5 → 6, with what changed and why the constant is a timeout |
| `src/WorldBuilder.Core/Analysis/RelationTrajectory.cs` | new — live ties per year, terminations, density, the degeneracy guard |
| `src/WorldBuilder.Core/Analysis/Divergence.cs` | new — §5 |
| `src/WorldBuilder.Core/Analysis/StandingState.cs` | new — §6 |
| `src/WorldBuilder.Core/Analysis/KeyBlastRadius.cs` | new — §1 |
| `src/WorldBuilder.Cli/CommandLine.cs` | `wb ties`, `wb divergence`, `wb standing`, `wb keyshift` |
| `tests/WorldBuilder.Tests/RelationTerminationTests.cs` | new — 26 tests |

No renderer, checker rule, threshold or `SimConfig` value was touched. `EventTemplates` is
unchanged: the terminating cause is recorded and not rendered, per the standing rule.

---

## Owed

- **The decision on the degeneracy guard.** Pre-registered halt fired; corrected measure says the
  rule is sound. Both readings are above and I did not choose.
- **Seed 99.** Chain shapes 69 → 45 and a coup branch no longer reached. Whether that is
  acceptable is the same decision.
- **`disuse` fires once in five worlds.** The twenty-year constant is untested by this panel.
- **`baselines/ruleset-6/`**, if ruleset 6 stands.
- **`AdditiveRecordTests` and the flat-control equality test** need retiring or rebasing, whichever
  the decision above implies.
- **The §6 list**, unrepaired by instruction: two dead relation kinds, three kinds with no removal
  path, ore, two collapse-path gaps, and `GoalBook` sitting outside the fold.
