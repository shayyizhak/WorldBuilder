# Repairing the staged reference material — report

**Ruleset stays at 7. No mechanics change, no checker rule, no `SimConfig` edit, no baseline cut, and
nothing is marked `verified`.** Every item in the brief was a derivation fix with a determined answer,
and every one of them landed.

The three artefacts were regenerated against the same seal
`abd551f94a6fea58b5922303366d2deab5227c124d30b52b41ff16335415c33e`, with the live half of §1.1 re-run
so the material is internally consistent — `checks.md` names the questions the loop now emits, not the
ones it used to. `wb stage --seed 42` reports **no halt condition met**.

`docs/facts-sheet-second-reader.md` is not in the tree. The brief's §1 and §2 carry enough of it to
act on and every claim in them was re-derived from the record here rather than taken on trust, so the
missing source did not block anything — but it is not available to check the rest of the
second reading against.

---

## 1. The facts sheet

### 1.1 A tenure ends when the faction does — `ReferenceSet.SeatHistory`

The terminal hold now closes at the house's `POLITY.COLLAPSE` year where there is one, and at the last
year of the record otherwise. All three rows the brief named moved to the year it named.

| seat | collapse | holder | was | now |
|---|---|---|---|---|
| the Wurn League (f:1) | Y34 `e:638` | Stonand Ker | 33–51 (killed) | **33–34 (faction ended)** |
| the Kebarrow Compact (f:2) | Y39 `e:735` | Diweith Mound | 38–51 (cast out) | **38–39 (cast out)** |
| the Vea Lode Covenant (f:5) | Y39 `e:737` | Bu Rumpirn | 36–51 (died) | **36–39 (died)** |

Griwick's `Paernmel Has 50– (still holding)` and Meigate's `Drarka Draernthun 46– (still holding)`
are untouched, which is what makes this a branch rather than a change to the general rule.

### 1.2 Departure resolves against person **and** faction, inside the tenure window

`HowItEnded` now requires `e.Faction == faction` on all three kinds, not only on exile. That is what
stops `e:870` — Stonand Ker killed in Y47 as **Griwick's** leader — from closing a **Wurn** tenure
that ended in Y34. The window was already there and had nothing to bound, because the terminal `to`
was Y51 before §1.1.

The brief's check on the rule held exactly as written: two of the three repair themselves on their own
merits and keep the term they had.

| holder | record | year | faction | in window | term |
|---|---|---|---|---|---|
| Bu Rumpirn | `e:725` `LIFE.DEATH_NATURAL` | 39 | f:5 | yes | `died`, kept |
| Diweith Mound | `e:732` `POLITY.EXILE` | 39 | f:2 | yes | `cast out`, kept |
| Stonand Ker | nothing names him and f:1 in 33–34 | — | — | — | falls through |

### 1.3 `(faction ended)`

`ReferenceSet.FactionEnded`, reached only as a fall-through and only where the hold ends at a
collapse. **Only Stonand Ker reaches it**, as the brief predicted. A holder killed or cast out in the
collapse year still gets the term for what happened to him, because the fall-through is consulted last.

`ReferenceSet.StillHolding` is now a named constant and `SeatSpell.Open` replaces the three
open-coded string comparisons on it, so the vocabulary is closed rather than conventional.

### 1.4 Alliance spans are folded, not read off the closing event

`Termination` carries `Made`, tracked through the same `EventReducer` replay `RelationTrajectory`
already runs — the tie's most recent making, dropped again when the tie ends so a remade tie cannot
inherit a closed span's opening year.

**All 14 `?` openings resolved. None remains, on any panel seed.** The 5 rows that already had a year
from the `made` payload key are byte-identical, which is the cross-check that the fold and the one
source that existed agree.

> **Corrected 2026-08-19**, by §4 of `brief-phantom-mutations.md`. This said *11 resolved, 8 already
> carrying a year*. The pre-repair sheet holds **14** `?` and **5** with years; both readings sum to
> the 19 terminations, which is why the wrong split looked consistent. The regenerated sheet has
> **zero** `?`, counted directly, so the miscount was in this report's account of its starting state
> and never in the output — and `EverySpanOpensWhereTheRecordMakesTheTie` asserts the zero
> independently, panel-wide. Nothing else in this section changes.

| tie | was | now | making |
|---|---|---|---|
| Alliance f:1 ↔ f:3, ended `e:84` | `? – 5` | **`3 – 5 (2y)`** | `e:48`, Y3 — the brief's own worked example |
| Alliance f:2 ↔ f:3, ended `e:463` | `? – 27` | **`2 – 27 (25y)`** | `e:37`, Y2, a marriage |
| Alliance f:3 ↔ f:4, ended `e:872` | `? – 47` | **`35 – 47 (12y)`** | `e:644`, Y35, a marriage |
| AtWar f:1 ↔ f:3, ended `e:181` | `? – 10` | **`5 – 10 (5y)`** | `e:84`, Y5, the war declared |
| AtWar f:2 ↔ f:3, ended `e:735` | `? – 39` | **`38 – 39 (1y)`** | `e:718`, Y38, the war declared |
| Alliance f:2 ↔ f:3, ended `e:735` | `? – 39` | **`39 – 39 (0y)`** | `e:726`, Y39, a marriage weeks before the collapse |

The remaining eight are in the sheet. A `0y` span is not an artefact: the alliance was struck by
marriage in Y39 and the collapse ended it the same year, the same shape as the `5 – 5 (0y)` trade row
that was already there.

**A `?` now means the record holds no making.** Where one appears the row says so in words, and
`wb stage` halts on it — a `?` was the thing this removed, so one reappearing means something
different and should not print quietly.

---

## 2. The questions

### 2.1 The three inherited spans

Regenerated, not hand-edited. `Who has ruled the Wurn League?` now ends `Stonand Ker 33–34`, Kebarrow
`Diweith Mound 38–39`, Vea Lode `Bu Rumpirn 36–39`.

### 2.2 Every seat-year question named a transition year — all five

`ReferenceStaging.Interior` picks a year strictly inside one hold, latest hold first, and **emits no
question at all** where no hold on the seat has an interior year. Every seat still yields one, so
nothing was lost.

| seat | was | holders that year | now | holder |
|---|---|---|---|---|
| the Wurn League | 33 | Drarka Draernthun 32–33 **and** Stonand Ker 33– | **31** | Heillvar Maer 30–32 |
| the Kebarrow Compact | 38 | Beas Krouthea 35–38 **and** Diweith Mound 38– | **36** | Beas Krouthea 35–38 |
| the Griwick Compact | 50 | Raes Go 49–50 **and** Paernmel Has 50– | **48** | Thres Thrild 47–49 |
| the Meigate Covenant | 46 | Diweith Mound 44–46 **and** Drarka Draernthun 46– | **47** | Drarka Draernthun 46–51 |
| the Vea Lode Covenant | 36 | Sou Dra 33–36 **and** Bu Rumpirn 36– | **37** | Bu Rumpirn 36–39 |

The answer now carries the hold and says why the year is safe, so the question can be checked by
reading it rather than by re-deriving the seat.

**The boundary years were not re-added as a separate ambiguous probe.** The brief offers that
conditionally — "if they are wanted" — which is a decision about what the suite is for, and this pass
took no decisions. Five extra probes would also raise the candidate count without adding a question
that can fail. The report says so in `report.md` §4, so a session can see the years were removed
deliberately rather than lost.

### 2.3 Coverage after the change

Unchanged, all four requirements still met.

| requirement | need | before | after |
|---|---|---|---|
| candidates | 24 | 30 | 30 |
| negative premise | 3 | 5 | 5 |
| supplied figure restated | 1 | 2 | 2 |
| terminated relation | 1 | 2 | 2 |

18 suite-eligible and 12 `classification-sensitive`, both unchanged.

---

## 3. The secrets — recorded, and one correction to the premise

`secrets.md` states the limitation with its derivation, and candidate 1 (`e:655`) is adopted as
canonical and labelled as such in the file. Nothing was chased.

**The brief's §3 is substantively right and its arithmetic is not, so the generated text derives the
figures rather than transcribing them.** Seed 42 holds **42** secret records of **4** kinds, not 30 of
one:

| kind | records | in the withheld pool |
|---|---|---|
| `POLITY.COUP_PLOTTED` | 30 | 13 |
| `POLITY.PLOT_LAPSES` | 7 | 5 |
| `CONFLICT.ASSASSINATION` | 3 | 1 |
| `POLITY.PLOT_DIES_WITH_PLOTTER` | 2 | 1 |

The conclusion survives it. All three secret assassinations are **failed** attempts inside plot arcs —
`e:155` in "the Whispering against Searn Sisrill", `e:598` in "the Plot against Heillvar Maer" — so
every secret in this world really is a plot against a named person, the closing of one, or a secret
attempt on a life inside one. There is no other subject a secret is about here, the layer answers all
of them in one sentence, and the breadth worth having is not available.

**One thing the top-five ranking hides, now reported rather than adopted.** Counting kinds and
counting question templates disagree, so the file states both: the pool of 20 asks **3** question
shapes and the bench of 5 asks **2**, which means a third template sits below the ranking. It is still
a plot against a person and still answered in the same words, so the ranking is left alone —
expressibility is what a candidate is chosen for and these score identically on it. Reporting kinds
alone would have overstated the breadth; reporting the bench alone would have hidden the third shape.

The narrow vocabulary goes on the backlog as the skewed-distribution shape, not into this repair.

---

## 4. Re-stage and the diff

All seven artefacts regenerated against the same seal. `record-history.md` and
`record-bookkeeping.md` are **byte-identical** — the split rule did not move.

**Every row that moved, and nothing else:**

| file | rows moved | explained by |
|---|---|---|
| `facts-sheet.md` | 3 ruler-list claims | §1.1, §1.2, §1.3 |
| `facts-sheet.md` | 14 relation spans + the count line | §1.4 |
| `questions.md` | 3 ruler-list answers | §2.1 |
| `questions.md` | 5 seat-year questions and answers | §2.2 |
| `questions.md` | 1 terminated-relation answer (`? – 5` → `3 – 5`) | §1.4 |
| `checks.md` | 1 live probe question, re-run | §2.2 |
| `secrets.md`, `report.md` | new sections and a kind column | §3, §5 |

**No row moved that §1 or §2 does not explain.** Event counts, the plague and famine derivations, the
raid split, the role-and-outcome table, the false premises, the dispersion figures, the record split
and the holdout table are all unchanged.

---

## 5. The tests

Four properties in `tests/WorldBuilder.Tests/ReferenceMaterialTests.cs`, each one panel-wide —
seeds 1, 7, 42, 1234, 2025 — because a derivation defect is a property of the derivation and seed 42
is only where these were found. **20 of 20 pass; the whole suite is green at 676 passed, 2 skipped.**

| test | what it pins |
|---|---|
| `NoTenureOutlivesItsFaction` | no hold's closing year is past its house's collapse, and nobody is `still holding` a seat that ended |
| `EveryDepartureTermHasARecordNamingBothPersonAndFaction` | re-finds the record each term rests on, naming that person, that faction, inside those years |
| `NoSeatYearQuestionNamesATransitionYear` | reads the emitted question text and asserts exactly one holder that year |
| `EverySpanOpensWhereTheRecordMakesTheTie` | every span has a folded opening year, and agrees with `made` wherever the payload carries one |

Two are built to be non-vacuous rather than trusted to be: each asserts it exercised something (a
house did collapse, a hold did end in a departure, a seat was asked about, a closing event did carry
`made`), and the seat-year test asserts the *count* of questions equals the count of seats with an
interior year — so a fix that silently stopped emitting them would fail rather than pass.

`NoSeatYearQuestionNamesATransitionYear` reads the question the loop writes into `questions.md`, not
the year-picking helper. A test on the helper passes just as happily when the product throws the
helper's answer away.

---

## 6. Halt conditions

**None met.** Against the brief's §7:

- **A row moving that §1 or §2 does not explain** — none. §4 above lists all of them.
- **A hold with no interior year leaving a coverage requirement unmet** — no seat in this world has
  one, and `report.md` §4 now names any that do on a future seed. The coverage counts are what halt.
- **The `?`-elimination leaving spans unresolved** — none unresolved, on any panel seed. Any future
  one halts `wb stage` and says which tie and which record.
- **§5's tests failing on a panel seed other than 42** — none fail.

---

## 7. What was checked and not redone

The brief's §6 list was taken as given and nothing in it was re-derived, per its own instruction. Two
items in it are now also asserted mechanically rather than only agreed: the 15 contested transfers and
Thold Valmaer's genuine double tenure at Griwick are covered by the existing collapse-rule checks, and
the ruler lists' terminals are covered by `NoTenureOutlivesItsFaction`.

**None of this is verification.** The material is still machine-derived, every row still says
`verified: no`, no artefact here is a fixture, and the human session still happens — on better
material than it would have read.
