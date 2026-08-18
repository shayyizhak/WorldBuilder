# Phase report — pre-verification machine work

Against `docs/phase-preverification-machine.md`. Entry state: ruleset 4, 541 tests green, working
tree clean. Exit state: ruleset 4, **584 tests green and 2 skipped**, no `SimConfig` threshold moved,
no checker rule added or changed.

**The phase halts.** Item 1 falls to the middle arm of its own pre-committed rule and escalates as
prose judgement. Items 2 and 3 completed and neither halts.

> **On completing 2 and 3 after item 1's halt.** The brief says to run until the queue is empty or a
> halt condition fires, and item 1's halt fired. It was read as a halt on *acting*, not on
> *measuring*: item 1's verdict escalates a question about what excluded passages say, which is
> independent of the ruler-list derivation and of the field-name vocabulary, and §6's deliverable —
> which staged rows are invalid — cannot be produced without items 2 and 3. Nothing was changed that
> a halt forbids: no rule, no threshold, no re-baselining, nothing marked verified, and the human
> protocol in `reference-set-verification.md` is not started.

---

## The deliverable, first

**No staged reference-set row is invalid. None needs re-staging.**

Established mechanically rather than argued: `wb reference` was re-run against
`baselines/ruleset-4/seed-42/world-42.jsonl` with the corrected derivation and the output diffed
against what is on disk in `out/carry-forward/reference-set/`.

```
candidate-facts.md: identical
candidate-query-suite.md: identical
withheld-not-absent-candidates.md: identical
```

The reason it is identical is in item 2 and is worth reading, because "the fix changed nothing" and
"the fix was unnecessary" are different statements and only the first is true here.

---

## 1. Holdout distribution across the five ruleset-4 seeds — **Escalate. HALT.**

Full table: `wb holdouts` (exit code 1 when the halt conditions are met). Written to
`docs/pre-verification-holdouts.md`. Everything below is read from the stored sidecars; nothing is
recomputed, because running today's rules over yesterday's prose gives the same figure on both
sides of the comparison.

### Denominators

| seed | scopes | held out | rate |
|---|---|---|---|
| 7 | 8 | 2 | 25% |
| 42 | 13 | 6 | 46% |
| 99 | 14 | 5 | 35% |
| 1234 | 13 | 2 | 15% |
| 2025 | 12 | 5 | 41% |
| **panel** | **60** | **20** | **33%** |

Per-seed holdout rate `range=[15, 46] width=31`, in percentage points.

**The degeneracy guard does not fire.** 20 holdouts is at or above its ten, so the grouping question
is live.

### Grouped by rule

| rule | held-out scopes | share | fired on survivors (r4) | fired on survivors (r3) |
|---|---|---|---|---|
| action | 7 | 35% | 0 | 0 |
| date | 5 | 25% | 0 | 0 |
| partition-sum | 3 | 15% | 0 | 0 |
| tenure | 3 | 15% | 0 | 0 |
| date-agreement | 2 | 10% | 0 | 0 |
| naming | 1 | 5% | 12 | 14 |
| quantity | 1 | 5% | 0 | 0 |
| succession | 1 | 5% | 0 | 0 |
| coverage | 0 | 0% | 5 | 2 |
| *(seven others)* | 0 | 0% | 0 | 0 |

### The pre-committed rules, applied

Quoted from the brief, with the figure that triggered each:

- **"Over-firing suspected — a single rule accounts for ≥ 60% of panel holdouts *and* that rule's
  non-holdout firing count rose from ruleset 3."** Not taken. The heaviest rule is `action` at 7 of
  20, **35%**, short of 60.
- **"Checker working — holdouts attribute to ≥ 4 distinct rules *and* per-seed holdout rate range
  ≤ 20 points."** Not taken. Eight distinct rules clears the first half comfortably; the per-seed
  rate `range=[15, 46] width=31` fails the second by **11 points**.
- **"Anything else — record the distribution, HALT, escalate as prose judgement."** **Taken.**
- **"A rule whose non-holdout firing count went to zero from a non-zero ruleset-3 figure."** No rule
  did. No immediate escalation on that ground.

`HoldoutTests.ThePanelAsItStandsFallsToTheMiddleArm` pins each of these, so the arm cannot drift
while nobody is looking, and the verdict is computed in code rather than read off the table by eye.

### Three things the reading needs, and one of them is about the instrument

**a) The range criterion was never met at ruleset 3 either.** Ruleset 3 held out **17 of 61** scopes
on the same five seeds — 28% against ruleset 4's 33% — at per-seed rates of 8%, 21%, 50%, 25%, 33%,
`range=[8, 50] width=42`. So the panel-wide rate rose by five points and ruleset 4's spread is
*narrower* than the set it is being compared against. (`wb holdouts --set ruleset-3 --against
ruleset-4` reproduces this.) Whatever the 20-point criterion was calibrated on, it was
not this panel at the previous ruleset, and failing it is not evidence of a regression.

**b) The over-firing arm's second condition is close to unreachable, structurally.** A blocking rule
fires almost only where it causes a holdout — that is what blocking means — so its count on
*surviving* scopes is 0 at both rulesets for every blocking rule in the table. `0 > 0` is false, so
the arm cannot be taken by a blocking rule however concentrated the holdouts are. This is the
"a comparative decision rule needs a degeneracy guard" lesson arriving in a new place: the rule's
discriminating half carries no information for the rules it is aimed at.

Extraction counts are reported beside the firing counts in `wb holdouts` and are **fenced out of the
verdict in writing and in code**, for the same reason the paired variance figure was fenced to
sizing N: swapping the statistic after seeing the table is not running a pre-registered test.

**c) Eleven of the twenty holdouts were decided by a rule whose extraction counter never moved.**
This is the substantive finding of item 1 and it is not what the brief expected to find.

### Findings raised by rules that extracted nothing — 22 rows, six rules, every one also `rule-inert`

Worked example, seed 42, "The Griwick Compact, 4–23", from the sidecar as stored:

```
invented-mind   span=exploiting          blocking=True  fatal=True
relative-time   span=the following year  blocking=True  fatal=True
vague-quantity  span=hundreds            blocking=False fatal=False
rule-inert      span=action              "action extracted nothing here"
rule-inert      span=date                "date extracted nothing here"
rule-inert      span=quantity            "quantity extracted nothing here"
```

`invented-mind` belongs to `action`; `relative-time` and `wrong-year` to `date`; `vague-quantity`
and `invented-particular` to `quantity`; `missing-ruler` to `tenure`; `hedged-outcome` to `outcome`;
`incomplete-enumeration` to `coverage`. The sidecar states, of the same rule in the same scope, both
that it read nothing here and that a finding it owns kept the section out of canon.

Across the panel: **22 such rows, in 6 rules, and every one of them sits beside a `rule-inert` row
for the same rule in the same scope.** The eleven finding kinds involved are `wrong-collapse`,
`invented-mind`, `unsupported-manner`, `no-such-event`, `relative-time`, `wrong-year`,
`vague-quantity`, `invented-particular`, `missing-ruler`, `hedged-outcome`,
`incomplete-enumeration`.

**Why this matters more than it reads.** `coverage-sound` has two invariants and the second is
`FLOOR: extracted >= previous_extracted`. A rule whose extraction is structurally zero has a floor
of zero, permanently, and can stop firing forever without the golden layer noticing. That is
precisely the configuration the project reference forbids on purpose for a geography rule written
before its terrain pack exists — *"a rule written now extracts 0 forever, `rule-inert` cannot fire
because the construction is genuinely absent, and FLOOR baselines at 0 — manufacturing the
silent-path signature on purpose."* Here it has been arrived at by accident, in six rules that are
firing right now and deciding canon.

Two consequences, neither of which is a defect in any rule's logic:

1. `rule-inert` is a false positive on those 22 scope-rule pairs. It says a rule read nothing where
   the rule found something.
2. The golden layer's floor protection does not cover those six rules' word-scanning paths at all.

**Recorded and not repaired.** Correcting an extraction counter raises a floor, and re-baselining a
floor is an explicit human action rather than something that happens by rerunning.
`HoldoutTests.SomeFindingsComeFromRulesWhoseExtractionCounterNeverMoved` pins the state.

### Scope selection

The full per-seed scope diff is in `docs/pre-verification-holdouts.md`. The short version: **the
denominator moved because the worlds moved, not because selection changed.** Selection is unchanged
— `Weightiest` ranks by count of Major-significance records naming a faction, `wb book` was run with
the same arguments for both sets — and the scope lists differ because the histories differ. Seed 42
at ruleset 4 has five powers where ruleset 3 had seven, no Sworn Men of Meigate or Laehiford or
Hadale at all, and a Meigate Covenant and Vea Lode Covenant that ruleset 3's seed 42 never
contained.

The standing caveat still applies and is unaddressed by this phase: **ranking by raw event count
under-represents things that ended.** Seed 7 dropped from 12 scopes to 8 and lost the Kraeford
Compact's three era sections; seed 2025 gained three.

---

## 2. Vea Lode contested-transfer check — **no HALT, both parts**

### Part (b), which ran first: the sealed v1 record

Every contested transfer in the sealed v1 record, from the record —
`V1ReferenceFactsTests` in the Layer 4 assembly, which derives them off the log and does not call
the engine's own seat derivation, because a check of a hand-verified fact that ran through the
derivation under suspicion would be checking the derivation against itself.

**Seven contested transfers in the v1 record:**

| seat | person | year | records |
|---|---|---|---|
| the Wurn League (f:1) | Trem Lolkoll | 17 | `e:236` challenge, `e:237` succession |
| the Kebarrow Compact (f:2) | Thulgea Bu | 16 | `e:219`, `e:220` |
| the Kebarrow Compact (f:2) | Weallhous Dreld | 20 | `e:295`, `e:296` |
| the Kebarrow Compact (f:2) | Gatros Hearn | 25 | `e:401`, `e:402` |
| the Kebarrow Compact (f:2) | Teillmol Lund | 27 | `e:444`, `e:445` |
| the Kebarrow Compact (f:2) | Paernmel Has | 39 | `e:729`, `e:730` |
| the Hadale Commune (f:6) | Sou Dra | 38 | `e:708`, `e:709` |

**Intersected against every hand-verified ruler fact in the v1 reference set** — §8 of the project
reference and the `Note` fields of `QuerySuite.ForSeed42`, which are where those facts are actually
carried:

| hand-verified fact | crosses | verdict |
|---|---|---|
| Vea Lode rulers: Stald Gearngoll 29, Veillpea Dourn 45, Thres Thrild 46, Gatros Hearn 47, Keithfal Naell 48, Herpeim Raern 50 | **none** — f:7 has no contested transfer | **agrees**, and never at risk |
| Hadale Commune: Durnrin Drar took the seat in 47, held it at 51 | **1** (Sou Dra, 38) | **agrees** |
| Kebarrow Compact: Stonand Ker never held a seat | **5** | **agrees** — he appears on no seat in the world |
| Kebarrow Compact: Hehum Skul was a named heir, never ruled | **5** | **agrees** |
| Sworn Men of Meigate: founded 19, so no ruler in year 5 | none | **agrees** |

**Six crossings found. Every one agrees with the record. No reference entry is marked suspect, and
no Layer 3 row needs enumerating.**

The one contested transfer not intersected by any hand-verified ruler fact is the Wurn League's, in
17. §8 carries a collapse fact about the Wurn League, not a ruler list.

The Vea Lode list is reproduced from the record exactly as §8 states it, holder for holder and year
for year, and the reason it was never at risk is that its seat has no contested transfer on it — a
weaker reason than "the derivation handled it", and worth saying so rather than reporting a pass.

### Part (a): the ruleset-4 derivation

`wb seats` over every seat in every sealed record, flagging every case where one person appears
twice on one seat — every pair, not only adjacent ones, so the check cannot agree with the rule by
construction.

| record | contested transfers | second tenures | fitting neither shape |
|---|---|---|---|
| ruleset-4 seed 7 | 5 | 0 | **0** |
| ruleset-4 seed 42 | 15 | 2 | **0** |
| ruleset-4 seed 99 | 14 | 3 | **0** |
| ruleset-4 seed 1234 | 12 | 0 | **0** |
| ruleset-4 seed 2025 | 7 | 1 | **0** |
| sealed v1 seed 42 | 7 | 0 | **0** |
| ruleset-3 seed 42 | 13 | 2 | **0** |

**Nothing fits neither shape.** No third rule was invented and none was needed.

**The derivation was wrong and its output was right.** The collapse rule read *adjacency* — two
neighbouring appearances by one person became one hold, whatever years they carried. That is correct
for every contested transfer and wrong for a second tenure with nobody recorded in between, which it
deleted from the list rather than collapsing.

It has never fired, on any record here, because **every second tenure on every sealed record is
non-adjacent** — somebody else always held the seat in between, so the pair never reached the
collapse. Seed 42's Thold Valmaer (Griwick, 23 and again in 24, with Bu Rumpirn between) is the
worked example, and it survives in the staged sheet as two holds because of that accident and not
because the rule was right.

Fixed in both places, which are deliberately two implementations:
`ReferenceSet.SeatHistory` (the engine's) and `RecordFacts.SeatHistory` (Layer 4's independent
copy). **Two records in one year are one transfer; two records in different years are two holds.**

**Why nothing caught it.** The only assertion on the list was that no two neighbouring spells share
a ruler — and that assertion passes under *both* errors. Collapsing correctly satisfies it;
deleting a tenure satisfies it more emphatically. It is the same shape as the raid split: an
assertion about the partition, never about the cells.

The test now enters at `ReferenceSet.SeatHistory` with both shapes present in one record and asserts
the collapse **and** the survival. Layer 4's assertion was rewritten from "no neighbouring spell
shares a ruler" to "no holder appears twice on one seat **in one year**", which is the claim a ruler
list actually makes.

### Queue item: ruler lists regenerated and diffed against the pre-fix lists

`SeatTransferTests.TheFixMovesNoRulerListOnAnySealedBaseline` re-derives the pre-fix rule alongside
the production one and diffs the two, on all five ruleset-4 seeds and the sealed v1 record.
**Zero lists changed.** It is not asserted as a permanent property: a record with an adjacent second
tenure would fail there, and correctly — that is the record in which the old rule would have deleted
a hold and the staged sheet would need re-staging.

---

## 3. Schema assertion — **no HALT. Zero dead reads.**

`wb schema --reads`, and `SchemaInclusionTests` in both test assemblies.

### Method

The vocabulary is read **off the records**, not off a declared table: a table beside the rules is a
second thing to keep in step, and five of the silent-path family came from exactly that shape. Five
whole ruleset-4 records plus the sealed v1 one. Structured delta keys reduce to their prefix
(`pop:p:3` → `pop`), because the tail is an entity id.

The reads are **observed, not declared**. Every payload read passes through `Event.GetString`, so a
recorder there sees what the code actually asks for, and cannot go stale the way a hand-maintained
list would. `SchemaSweep.Run` enters the consumers at the points the product calls — the reducer's
fold, the readable view, the chronicle pack builder, the checker over real prose about real packs
(Tier 1, 2 and 3), query retrieval across every shape, the reference-set derivations, and
`BaselineArchive.Check`. Layer 4 is swept **from its own assembly**, since sweeping it from the
checker's side would route the independent verifier through the implementation it exists to be
independent of.

### Result

**84 distinct field names across 42 event kinds emitted. 98 (kind, field) reads observed across 31
kinds. Zero dead reads.**

The mismatch table is empty. Classified against the brief's three buckets:

- **Dead read, assertion never fired** — **none.** The `loot` case was the eighth instance and was
  found and fixed by the carry-forward phase before this one; the previous phase reported it broke
  no existing assertion, so no previously-green verdict changed. The three names it read —
  `took`, `haul`, `plunder` — are now asserted absent from the emitter's whole vocabulary, from both
  sides, so reintroducing one shows up as a failure rather than as a silence.
- **Typo with a working fallback** — none.
- **Renamed field** — none.

### One off-kind read, reported and not failed

`ECONOMY.FAMINE.refuge`. `EventTemplates` asks a famine for its refuge; across six whole records the
engine has only ever written `refuge` on a plague. It is **not** a dead read: `EconomyPhase.Flight`
serves both kinds and sets the key when a refuge exists, the famines on this panel simply never had
one, and the template guards on `!GetEntity("refuge").IsNone` and renders "abandon the place"
instead. Correct as it stands.

It is worth one line because it is the honest limit of deriving a vocabulary empirically: a
conditional field never written on the panel is indistinguishable from one nothing writes. That is
why the assertion is at union level — a name absent from six whole histories is not a rare branch —
and why off-kind reads are reported rather than failed. Asserting on them would manufacture false
positives, which is how one attempt at a blanket coverage rule cost seven true chronicle sections.

### Two observations outside the brief's question

**Four event kinds are declared and never emitted by anything:** `ECONOMY.TRADE_COLLAPSE`,
`DIPLO.ALLIANCE_BROKEN`, `CONFLICT.SIEGE`, `INTRIGUE.GRIEVANCE_SETTLED`. Each has a name in
`EventKinds`, a sentence in `EventTemplates` and no emitter in any rule. This is the structural-zero
family — the same shape as the covert coup — and the `DispersionKind` comment already states the
principle it violates: *a label with no emitter is worse than a dead branch.* Recorded, not acted
on: adding or removing a mechanic is outside this phase's budget.

**The reverse direction is unmeasured and looks non-empty.** Several emitted fields appear in no
read at all — `CONFLICT.BATTLE.margin`, `POLITY.CHALLENGE.backing`, `DIPLO.TRIBUTE_DEMANDED.demand`,
`CONFLICT.RAID.target`, `POLITY.COLLAPSE.disown` among them. That is a field written for nobody,
which is a different and much cheaper failure than a read with no writer, and the brief did not ask
about it. Noted for whoever asks.

---

## Instrumentation invariance

The only instrument this phase attached to a production path is the read recorder, and it is
asserted rather than argued for:

- `SchemaInclusionTests.RecordingReadsLeavesTheLogIdentical` — log hash with and without the
  recorder, across all five seeds, plus an assertion that the recorder saw something during the
  simulation so the equality is not the equality of two unwatched runs.
- `InstrumentationInvarianceTests.TheEngineStillReproducesTheSealedRuleset4Baselines` — **new, and
  the stronger form.** The engine re-simulates each of the five panel seeds to a log byte-identical
  to the sealed ruleset-4 baseline's, event for event. Only the provenance header differs, and only
  in the engine commit and the artefact manifest. Nothing asserted this before, and every
  measurement taken against those baselines silently rested on it.

`wb holdouts` and `wb seats` read files and never construct a `Simulation`.

The one dispersion figure this phase emits is the per-seed holdout rate spread. It is a
`Dispersion.Range`, it prints as `range=[15, 46] width=31`, and
`HoldoutTests.TheRateSpreadIsAnIntervalAndPrintsAsOne` asserts it cannot be rendered without its
kind — the arm is stated in points of width and a bare 31 reads as a standard deviation to the next
person who meets it.

---

## Queue, and what it produced

| queue rule | fired | outcome |
|---|---|---|
| Item 3 finds a mismatch → re-run the layer and count changed verdicts | **no** | Zero mismatches. The prior `loot` instance changed zero verdicts and was reported by the carry-forward phase. |
| Item 2(a) fixes the derivation → regenerate the five ruler lists and diff | **yes** | Zero lists changed, asserted mechanically. No staged rows to re-stage. |
| Item 1 escalates on a rule going non-zero → zero → find where the input stops | **no** | No rule did, on the pre-committed statistic. |
| Any dispersion figure self-identifies at emission | **yes** | One figure, `range=[…] width=…`, asserted. |

---

## What is now on the record

Three additions to the ledger of lessons, all earned by measurement in this phase:

**A rule that fires without extracting has a floor of zero.** `coverage-sound`'s FLOOR invariant
protects nothing for a rule whose extraction counter never moves, and six rules deciding canon right
now are in that state. The silent-path signature, inside the mechanism built to detect it, arrived at
by accident rather than on purpose.

**"No duplicate in the list" is satisfied by two opposite errors.** Collapsing a contested transfer
correctly and deleting a genuine second tenure both pass it. Assert the collapse *and* the survival,
or the test is satisfied by a derivation that drops both — the same shape as the raid partition that
summed while one of its three cells was structurally zero.

**A pre-committed arm can carry no information and still look decisive.** The over-firing arm asks
whether the heaviest rule's firing count on surviving scopes rose; a blocking rule fires almost only
where it causes a holdout, so that count is zero on both sides for every rule the arm is aimed at.
Stated as a further instance of *a comparative decision rule needs a degeneracy guard* — the guard
that was written covered the panel being too small, not the statistic being structurally constant.

---

## For Shay — the escalation, in one place

**Item 1 needs a reading, and it is the only thing here that does.** The mechanical question is
answered: holdouts spread across eight rules with no rule near a majority, and the panel's per-seed
spread is narrower than ruleset 3's was. What cannot be settled by machine is whether the twenty
held-out passages *deserved* to be held out, and eleven of them were decided by a rule whose
coverage block says it read nothing — so for those eleven, the sidecar cannot tell you what the rule
was looking at. `baselines/ruleset-4/seed-*/chronicle-*.unverified.md` holds the passages.

**Two decisions follow from it, neither taken here:**

1. Whether to give the six word-scanning rules real extraction counters. It raises their floors and
   therefore requires an explicit re-baselining, which is why it was not done unattended.
2. Whether `rule-inert` should be suppressed for a rule that fired in the same scope. It is a false
   positive 22 times on the current panel.

**Everything else is green and the gate is open.** `reference-set-verification.md`'s gate condition
was that this phase name the invalid staged rows. There are none. Its pre-committed branch — *"if
item 2 part (b) found a crossing where a v1 hand-verified ruler list disagreed with the records"* —
**did not trigger**: six crossings were found and all six agree, so ruler lists may be verified from
the derivation output in that session, and no v1 entry is marked suspect.

---

## Commands

```
wb holdouts [--set ruleset-4] [--against ruleset-3] [--to <file>]   exit 1 when it halts
wb seats    --file <world.jsonl> [--lists]                          exit 1 on an unclassified repeat
wb schema   [--reads] [--verbose]                                   exit 1 on a dead read
wb reference --file baselines/ruleset-4/seed-42/world-42.jsonl --to <dir>
```
