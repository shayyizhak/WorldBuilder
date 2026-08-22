# Report — closing ruleset 6

> **One halt condition below is void, retired 2026-08-22.** *"The ruleset-6 holdout rate unlike
> ruleset 5's — held, 36% against 33%"* compared two draws from a distribution nobody had
> characterised. The ruleset 7 → 8 cut settled it: the panel rate moved 34.5% → 44.8% on worlds that
> are byte-identical apart from fourteen payload keys nothing reads, because a cold cut has no render
> cache and a non-deterministic model rewrites every section. The gate happened to clear and would
> have cleared or failed at roughly random. **Nothing else in this report depends on it** — the
> ruleset-6 disposition rests on the runaway-year contrast, not on the holdout rate — and the
> counter-shape findings stand, because they were about code structure rather than rate comparisons.
> Doctrine in §4 of the project reference; evidence in `docs/facts-sheet-second-reader-part-3.md` and
> `docs/phantom-mutations-report.md` §5.1.

Run against `docs/brief-closing-ruleset-6.md`. **All six items complete. No halt condition fired.**

**Entry state:** 613 green, 18 failing, 2 skipped, ruleset 6 uncut.
**Exit state:** **617 green, 0 failing**, 2 skipped. `baselines/ruleset-6/` cut and sealed for five
seeds. Layer 5 runs again.

Order followed as the brief directs: **§4 before §1**, because the reference panel changed and
cutting a set then swapping a seed pays the inference twice.

---

## The accepted verdict, and the argument for it

`war − null` is accepted on the surviving pair and **ruleset 6 stands with all three rules.**

Recorded here as reasoning rather than as a verdict, so a later reader can judge whether the call was
good:

- **The random arm existed to stop a war effect being attributed to war when it was really "ties came
  down". There is no effect to attribute.** `war − null` is +0.27 y over 90 paired worlds, the whole
  interval inside ±1 y against a 5-year MDE, and the point estimate is *positive*. A confound-remover
  for a positive result is moot against a precise null.
- **Dropping a contrast from a registered family loosens multiplicity correction**, which can only
  help a significance claim. The claim here is a null at p = 0.329 **uncorrected**, so the drop makes
  it harder to sustain rather than easier. Evaluating the surviving pair is conservative in the
  direction that matters.
- **Refusing to amend the matching rule after seeing the data was correct and cost nothing.** The
  amendment would have changed the registered treatment; the answer did not depend on it.

What that argument does *not* cover, stated so it is not read as covering it: the null is about
**systematic** effect. Seed 99 really did lose a quarter of its history to the war rule, and the
panel says that is a tail rather than the rule's ordinary behaviour. Seed 99's narrowing is accepted
with the null in hand, not explained away by it — and seed 99 has left the panel for a different
reason (§4).

---

## §4 — the reference panel: **seed 99 → seed 1**

### The criteria were written and committed first

`docs/reference-seed-criteria.md`, commit `f422181`, **before any candidate world was examined**. The
§6 halt on this is clear: the only worlds looked at beforehand were the current five, already
examined at length in two previous reports, and the 90-seed measurement panel.

**Two of the brief's suggested criteria are rejected, with the reason.** "No runaway before Y40" and
"at least two houses standing" are both the **brake problem**, which §5 places out of scope. Measured
on 90 ordinary worlds the runaway year has median Y35 and lower quartile **Y18** — the Y40 bar fails
on more than half of them — and three of five current seeds finish ruleset 6 with one house standing.
A panel selected on either would be a panel selected *not to exhibit an unfixed engine defect*, in
exactly the five worlds anyone reads. That is the seventh-instance shape, manufactured deliberately.
**The brake problem stays visible in the reference panel.**

The criteria adopted are about the record, not the world's politics: both coup branches present; the
world fills the book's default scopes; length within ±35% of the panel median (461–957 events).

### The screening, emitted by `wb refseed`

| seed | R1 coup branches | R2 scopes | R3 length | verdict |
|---|---|---|---|---|
| 7 | ok (exposed 11, seized 2) | ok (3 wars, 22 reigns, 3 powers) | ok (529) | suitable |
| 42 | ok (12, 8) | ok (5, 42, 5) | ok (878) | suitable |
| **99** | **NO (0, 8)** | ok (5, 32, 5) | ok (531) | **rejected** |
| 1234 | ok (12, 7) | ok (6, 34, 4) | ok (826) | suitable |
| 2025 | ok (10, 2) | ok (3, 27, 6) | ok (700) | suitable |

**Seed 99 is the only failure, and it fails on the one criterion that is not a proxy for anything:**
at ruleset 6 it never has a conspiracy uncovered, so it cannot support the per-seed assertion it was
in the panel to support. The length criterion does *not* do the work of rejecting it — 531 is inside
the band.

### The replacement is seed 1, by the rule fixed in advance

The search rule was "the lowest seed ≥ 1 satisfying every criterion, skipping the current five and
both panel ranges". It returned **seed 1** on the first candidate: 695 events (panel median 709),
6 exposed / 1 seized, 5 wars, 5 powers.

**Seed 1 has 58 distinct chain shapes — below the ≥ 60 bar.** It is taken anyway, because the
criteria were committed first and deliberately exclude quality proxies, and because 58 is *ordinary*:
the panel median is 63 and its lower quartile 51. Accepting what the rule returned is the discipline;
its `seized` count of 1 is thin and is recorded as such.

Halt check — **`seized` fires on every seed after the swap**: 1, 2, 8, 7, 2. Held.

### The panel now has one source, which is why the swap was affordable

The five seeds were written out by hand in **twenty places** — every test file, six CLI defaults,
`Holdouts`. That is the same shape as the five re-fold sites the last report fixed. `ReferencePanel`
now holds two lists, because they are two things:

- `Current` — what gets cut, verified and asserted against from now on.
- `Sealed` — the seeds a ruleset-3, -4 or -5 directory actually holds, which stays correct however
  often the live panel is reconsidered.

**And a third answer turned out to be the right one for verbs that read a set by name.** Neither
list is correct there: `wb holdouts --set ruleset-6` went looking for seed 99 on the strength of a
default. Which worlds a seal holds is a property of the seal, so `SeedsIn` enumerates the directory.

### Not changed, and recorded as owed

**The reference panel keeps the stored board.** Per-seed boards would invalidate the board hash in
every sealed set and in the v1 record, which Layer 3 depends on permanently. The brief is right that
a shared board is how the five re-fold sites stayed invisible; the fix went to `WorldView.Board`
instead, which is the correct level.

---

## §1 — `baselines/ruleset-6/` is cut

Five seeds, both halves, real inference through ollama `qwen3.6:latest` (`07d35212591f…`). All five
seals verify. One build produced every world: engine `1.2.0` @ `f422181`.

| seed | events | passages | held out | rate | ruleset-5 rate |
|---|---|---|---|---|---|
| **1** | 695 | 13 | 4 | 30% | — (new to the panel) |
| 7 | 529 | 8 | 2 | 25% | 25% |
| 42 | 878 | 13 | 6 | 46% | 38% |
| 1234 | 826 | 12 | 6 | 50% | 15% |
| 2025 | 700 | 12 | 3 | 25% | 41% |
| **panel** | | **58** | **21** | **36%** | **33%** |

**The panel rate is 36% against ruleset 5's 33%** — the halt condition on an unlike rate is clear.
Per-seed `range=[25, 50] width=25` against ruleset 5's `[15, 42] width=27`, so the spread is
comparable too.

Individual seeds move a great deal inside a stable panel figure — 1234 goes 15% → 50% and 2025 goes
41% → 25%. Worth recording rather than smoothing: the panel rate is the stable statistic and a
per-seed holdout rate on 12 passages is not.

Checker fingerprint `60f5b325…`, **unchanged from rulesets 4 and 5** — none of the five fingerprinted
files was touched. Board hash `8eb0a9af…`, the same board. Prose was not re-verified; these are
machine baselines and each manifest says `stability-anchor-only`.

**The top line reports coverage and does not hide a skip:**

```
3 of 4 layers ran, 2 failures
  layer 5 SKIPPED — no stored render to compare against
```

and against the sealed set, layer 5 runs: `0 failed, 15 noted` → `1 of 1 layers ran, none failed`.
The two layer-1 failures are the two known-failing shape floors below.

---

## §2 — the twelve obsolete tests

**`AdditiveRecordTests` (10) — retired.** It asserted that ruleset 5 was additive over the sealed
ruleset-4 worlds: true, proven, and unprovable by any engine that no longer contains ruleset 5. The
verdict now lives where it survives the transition that made it unprovable —
`docs/phase-relation-termination-report.md` for the evidence and per-seed table, and `Provenance.cs`
where the ruleset-5 entry records that 4 → 5 was an additive record change with no simulation change.

**`ProximityControlTests.TheFlatControlReproducesRulesetThreeExactly` (2) — rebased and renamed to
`TurningGeographyOffGivesTheSameFlatWorldEveryTime`**, pinned at ruleset 6 on the new panel: 71, 47,
89, 64, 56. Pinning it to ruleset 3 was incidental to when it was written; what it asserts is that
distance is separable.

**The off-switch property is now standard, and the two forms are distinguished.**
`TurningTheTerminationRulesOffGivesBackTheOldRuleset` is the **strong** form — switch the three
termination rules off and all five sealed ruleset-5 logs come back event for event. The flat control
is the **weak** form and now says so in its own summary: geography's previous ruleset is three bumps
back and mechanics have changed since, so it pins a characterisation figure instead. Both are
recorded in §4 of the project reference as a standing rule.

---

## §3 — the two invariant changes

### 3.1 `distinct deep-chain shapes` — a per-seed floor beside the target

Not converted to a rate: that needs a shapes-per-event constant that makes a world interesting, and
there is no argument for one. It would be a constant chosen by fitting.

**The rule for setting a floor: a seed's floor is its own last accepted value. A seed that has never
reached the target keeps the target and keeps failing.** So a floor is never an excuse, and it can
only be raised by hand after a run somebody judged good — rerunning never lowers one.

| seed | measured | floor | before | after |
|---|---|---|---|---|
| 1 | 58 | **60** (never reached it) | — | **fails**, known |
| 7 | 45 | **60** (never reached it, since ruleset 1) | fails vs 60 | **fails**, known |
| 42 | 99 | **99** | passes vs 60 | passes, and a fall to 70 now fails |
| 1234 | 91 | **91** | passes vs 60 | passes, tighter |
| 2025 | 66 | **66** | passes vs 60 | passes, tighter |

**Halt check: seed 7 still fails.** Its failure survives the change, which the brief required. The
floor was not "cut from the wrong run" — seed 7 has no good run to cut from, having been below the
target since ruleset 1, so the target *is* its floor.

The change is a real gain in both directions: three seeds gain guards tighter than 60 — a fall from
99 to 70 used to pass — and two seeds keep failing a bar they have never met.

Cut against seal: the ruleset-6 set above, engine `f422181`.

### 3.2 `BothOutcomesOfTheRollAreReached` — split, not relaxed

Asserting both branches together made the weaker assertion the price of the stronger one.

- **`AConspiracyCanSucceedOnEverySeed`** — `seized` stays per-seed. That is the branch the invariant
  exists for: it was structurally zero under ruleset 1 and nothing noticed. Fires 1, 2, 8, 7, 2.
- **`AConspiracyCanBeUncoveredSomewhereOnThePanel`** — `exposed` moves to panel level, with the
  figures: **6, 11, 12, 12, 10** on seeds 1, 7, 42, 1234, 2025. Per-seed was over-strict for this
  branch; reachability is a property of the engine and a world where no plot happens to be uncovered
  in fifty years is not evidence the path is dead. The assertion still fails loudly if it fires
  nowhere, which would be a structural zero and a defect.

**Recorded: seed 99's `exposed` was removed by the war arm alone** — 5 under the null, collapse and
disuse arms, 0 with war switched on. A real narrowing of one world, accepted with the null in hand,
and the reason seed 99 left the panel.

---

## §6 — halt conditions

| condition | state |
|---|---|
| A per-seed shape floor that makes seed 7 pass | **held** — seed 7 fails at 45 against a floor of 60 |
| `seized` failing to fire on any reference seed after a seed swap | **held** — 1, 2, 8, 7, 2 |
| The ruleset-6 holdout rate unlike ruleset 5's | ~~**held** — 36% against 33%, spread 25 against 27~~ **void**, see the note at the head of this report |
| Suite not green after §1 and §2, other than §3's two changes | **held** — 617 green, 0 failing; the only Layer 1 failures are the two known shape floors |
| Reference-seed criteria not written before candidate worlds examined | **held** — committed at `f422181`, candidates screened afterwards |

---

## Files

| file | change |
|---|---|
| `docs/reference-seed-criteria.md` | new — criteria and search rule, committed before screening |
| `src/WorldBuilder.Core/ReferencePanel.cs` | new — `Current` and `Sealed`, one source for twenty sites |
| `src/WorldBuilder.Core/Analysis/ReferenceSuitability.cs` | new — the criteria, measured |
| `src/WorldBuilder.Core/Analysis/Invariants.cs` | `ChainShapeFloors`, per seed, with the rule for setting one |
| `tests/WorldBuilder.Tests/AdditiveRecordTests.cs` | **deleted** — verdict moved to the provenance chain |
| `tests/WorldBuilder.Tests/ProximityControlTests.cs` | flat control rebased to ruleset 6 and renamed |
| `tests/WorldBuilder.Tests/PlotLedgerTests.cs` | coup invariant split into per-seed and panel halves |
| `src/WorldBuilder.Cli/CommandLine.cs` | `wb refseed`; `SeedsIn` enumerates a set's own seeds; quantiles |
| `docs/WORLDBUILDER-PROJECT.md` | five standing rules added to §4 |
| `baselines/ruleset-6/seed-{1,7,42,1234,2025}` | new — sealed, verified |
| `docs/ruleset-6/holdouts.md`, `docs/floor-coverage.md`, `docs/step-two/*` | regenerated at ruleset 6 |

---

## Owed, unchanged from the brief's §5

- **`GoalBook` outside the fold** — ahead of the rest of the sweep list.
- The remaining §6 sweep items: two dead relation kinds, three no-removal-path kinds, ore, two
  collapse-path gaps.
- The archive contract split and the two verifiers that pass on nothing.
- Absent-vs-unknown as a type.
- The `disuse` constant, still flagged untested.
- **The brake problem.** Now measured on ninety ordinary worlds: runaway median Y35, lower quartile
  Y18. It predates this phase and is not caused by it, and the reference panel was deliberately not
  selected to hide it.

Newly owed from this work:

- **The reference panel shares one board.** Not changed here because it would invalidate every
  sealed set's board hash and the v1 record. It remains the dimension in which the reference panel
  is degenerate by construction.
- **Seed 1's `seized` count is 1.** It satisfies the criterion and is thin. If it reaches zero on a
  later ruleset, the panel needs re-screening rather than the assertion relaxing.
