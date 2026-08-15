# Test suite specification

## Why

Ten rounds of review have been a human reading one seed by hand. That has worked, but it does not scale and it has failed in two specific ways:

**It misses regressions.** Round 11 contained four defects that had already been fixed — *"since year 1"*, *"149 men"*, faction raids inside a reign scope, and the Hadale courtings-away figure. All four were catchable by comparing the render against a previous one. None was caught by the checker. They were caught because I happened to remember.

**The reviewer makes measurement errors.** Three so far, all the same shape — a filter silently dropping rows: dangling causal edges excluded by an `if cause in events` guard; the `.log` view read as the world when ~290 bookkeeping rows were hidden; the Wurn League's Y20 marriage missed in a scan. Each produced a confidently wrong number in a brief.

A test suite fixes both. It also decouples the project from a review conversation, which matters before v1.2 and matters much more at Stage 10 when worlds get too large to read.

This is Stage 4 of the roadmap, pulled forward.

---

## Structure

Five layers, increasing in cost and decreasing in how often they should run.

```
tests/
  dynamics/        layer 1 — log invariants, no chronicle needed
  checker/         layer 2 — rule unit tests on synthetic passages
  corpus/          layer 3 — the regression corpus, one case per known fabrication
  chronicle/       layer 4 — rendered chronicle verified against the log
  golden/          layer 5 — cross-round diff against stored known-good renders
```

Layers 1–3 need no model call and should run on every build. Layer 4 needs a render. Layer 5 needs stored artefacts.

---

## Layer 1 — Dynamics invariants

**Input:** a world record (the `.jsonl`, not the `.log` view). **No chronicle, no model.**

This is the v0 acceptance criteria made permanent. Every metric below was computed by hand during v0 review; they are now assertions.

| Metric | Assertion | Source |
|---|---|---|
| Dangling causal references | `== 0` | v1 round 1 |
| Verbatim repeat rate (digit-normalised) | `< 10%` | v0 run 1 was 11–22%, run 2 was 8.9% |
| Single-actor causal chains | `== 0%` | v0 run 1 failure |
| Maximum causal depth | `>= 8` | currently 15 |
| Distinct two-step chain shapes | `>= 60` | v0 run 2 was 328 pre-fix-count; currently 91 |
| Faction collapses per faction | `<= 1` | v0 zombie-faction bug |
| Coup success rate | `> 15%` | v0 run 1 was 13% and read as fizzle |
| Covert coup path success | `> 0` | v0 run 3 regression: 127 plotted, 0 won |
| `ECONOMY` → non-`ECONOMY` edges | `>= 10%` of total edges | currently 142/850 ≈ 17% |
| Cross-domain edges | `>= 25%` of total | currently ~30% |

**Two hard rules for this layer, both learned from reviewer error:**

1. **Read the record, never the `.log`.** Any metric computed over a presentation view will drift from the thing it claims to measure. The `.log` header now says so; the test suite must obey it.
2. **A filter that drops rows must fail loudly, not silently.** If an edge's target is missing, that is a dangling-reference failure, not a row to skip. Assert the count of rows read equals the count of rows in the file.

Run across the full seed panel: **7, 42, 99, 1234, 2025.** A metric that holds on 42 alone is an anecdote.

---

## Layer 2 — Checker rule unit tests

**Input:** short synthetic passages written by hand. **No world, no model.**

Each Tier 1 rule gets a positive case (must fire) and a negative case (must not fire). These are the tests that would have caught Tier 1 returning empty.

### Rule 1.1 — count versus enumeration

```
FIRE   "Fourteen people returned. These returns included A in 22, B in 24, … [14 names]."
         → exhaustive list marked partial
FIRE   "Two marriages bound the commune: X in 38, Y in 48. A second … in 49."
         → 3 items against a count of 2
FIRE   "Three battles, which it won. In 7 … In 8 …"
         → 2 items against a count of 3
FIRE   "Its rule passed through three holders. [narrates two]"
FIRE   "Four people were murdered, including A, B, C and D."
         → exhaustive list marked partial (round 7)
PASS   "It sent six raids, three of which carried off plunder from A in 4 and 17
        and B in 12, while three were beaten off at C in 7 and 22 and D in 13."
         → 3 and 3 against 6, both exact
PASS   "Fourteen returned, among them A, B and C."
         → 3 items, count 14, partial marker correct
```

Implementation notes: the count parser must handle **spelled-out numerals** ("fourteen") as well as digits, because sections are not internally consistent about which they use. The partiality-marker list must include at minimum: *including, included, among them, such as, notably, chief among*.

### Lexicon and normalisation tests

Four of the five causes of round 11's silent Tier 1 were input never reaching a correct rule — a missing marker, a missing countable noun, an unhandled word order, and an unstripped possessive. Each of those deserves its own test, because none of them is visible in rule logic:

```
MARKERS      every entry in PartialMarkers fires 1.1 on the same sentence
             with only the marker swapped — "including", "included",
             "among them", "such as" all behave identically
COUNTABLES   "Fourteen people", "fourteen exiles", "fourteen returns",
             "fourteen raids", "fourteen marriages" all yield a count of 14
WORD ORDER   1.3 extracts a date from both "X was killed in 46"
             and "the murder of X in 47"
POSSESSIVE   normalisation of "Realsis Leirpu's" yields the subject
             "realsis leirpu", not "leirpu's"
NON-EMPTY    every rule extracts at least one assertion from a fixture
             passage written to contain one — assert extracted > 0, not
             merely that no finding fired
```

That last one is the general form of the lesson: **a rule test that only asserts "no false positive" passes when the rule is inert.** Every positive case must assert extraction occurred, not just that the expected finding appeared.

### Rule 1.2 — partition sums

```
FIRE   "Eleven rulers: five killed, five cast out."           → sums to 10
FIRE   "Five held the seat: two died, one was replaced, one cast out." → sums to 4
PASS   "Twelve cast out: six for attempted murder, four for a lost claim, two for a lost challenge."
```

### Rule 1.3 — internal date agreement

```
FIRE   "X was killed in 46. … The murder of X in 47 …"
PASS   same year in both mentions
```

### Rule 1.4 — summary versus body

```
FIRE   "…ended with A holding the seat after killing B."  +  body: "C killed B, and A took the seat."
FIRE   "…with A taking the seat."                          +  body: "A took service with the power."
PASS   summary claim entailed by a body claim
```

---

## Layer 3 — Regression corpus

**Input:** the passage as originally rendered, plus the seed-42 record. **Assert: the named rule fires.**

This is the accumulated output of eleven rounds of hand review. Each row is a real fabrication that reached canon, with the correct answer known. **Every one must fire its rule, and none must fire on the corrected version of the same passage.**

| # | Passage (abbreviated) | Truth | Rule | Round |
|---|---|---|---|---|
| 1 | "Ska was killed by Stonand Ker, who was succeeded by Le Vild" | Ker never held the seat; Le Vild set aside Kou Peis's claim | succession | 3–5 |
| 2 | "Dreld's rule ended when he was beaten in a challenge by Saern Meastouth" | Meastouth was beaten; Dreld died Y25 to Gatros Hearn | outcome | 6 |
| 3 | "The rule of Heth Fal of the Sworn Men of Laehiford" + Kebarrow events | two reigns: Kebarrow 33–35, Laehiford 39–51 | tenure | 7 |
| 4 | "Le Vild cast out in 33, Heth Fal in 35, Nael War in 37, Paernrom Sir in 38" | Nael War and Paernrom Sir were killed | departure | 7 |
| 5 | "one by the Griwick Compact on Kebarrow in 32" | the raid was on Hadale, in 22 | action | 7 |
| 6 | "Four murdered from within, including [exactly four]" | exhaustive list marked partial | 1.1 | 7 |
| 7 | "ended with Turaer Danpa holding the seat after killing Befu Seirn" | Danpa killed Heillvar Maer; Bu Rumpirn killed Seirn | 1.4 | 8 |
| 8 | "Thurnean Kourn took the seat after contesting Veillpea Dourn's claim" | Dourn contested; Kourn's claim was upheld | outcome | 8 |
| 9 | "peace in 21 as the Compact's standing collapsed" | the Wurn League collapsed | action | 8 |
| 10 | "Under Kreathbeas, the Sworn Men sent eight raids" | faction-lifetime figure, not reign | quantity | 8, 11 |
| 11 | "Veillpea Dourn and Thres Thrild [both] in 46" | Thrild was 47 | date | 8 |
| 12 | "Voudreirn Wer won Baedros Mam away in 49" | 48 | date | 8 |
| 13 | "Two marriages … [three named]" | three; and Y37 not Y38 | 1.1 | 9 |
| 14 | "three battles which it won. In 7 … In 8 …" | third battle Y20 unnarrated | 1.1 | 9 |
| 15 | "uprisings at Vea Lode in 15 and Threi Cut in 15" | Threi Cut was 13 | date | 9 |
| 16 | "Hadale in 19 … carried off plunder" | carried off 0 ore | quantity | 9 |
| 17 | "Tor Nathgoull, who took the seat in 48 when his house ended" | house ended 50 | date | 9 |
| 18 | Kebarrow 2–21 with no Y20 war, battle, conquest or collapse | all four in window | coverage | 9 |
| 19 | "Kou Peis contested but lost to Veillpea Dourn" | Dourn contested; Kou Peis was heir | outcome | 10 |
| 20 | "three holders" + Reweld Wul absent | three narrated | 1.1 | 10 |
| 21 | "three places taken from the Wurn League" (Kebarrow scope) | Kebarrow took two | quantity | 10 |
| 22 | "murdered by Nael War in 18 and killed by Nael War at Meigate" | one killing | duplicate | 10 |
| 23 | "Fourteen … These returns included [14 names]" | exhaustive marked partial | 1.1 | 11 |
| 24 | "with Realsis Leirpu taking the seat" + "He took service with the power" | took the seat | 1.4 | 11 |
| 25 | "Pouldrir Ho, who held the seat since year 1" | no event; log starts Y2 | tenure | 9, 11 |
| 26 | "killing 149 men" | 149 dead | particular | 9, 11 |
| 27 | "Four people were courted away" (Hadale) | nine | quantity | 11 |
| 28 | "ending Skul's tenure" | Hehum Skul never held the seat | succession | 11 |
| 29 | "Thosruld Lul in 39" | 38 | date | 11 |
| 30 | "peace in 51, two years after the collapse" | collapse Y50; the 2 years is the war | date | 11 |
| 31 | Wuldweald reign: election narrated before the murder that caused it | causal order | ordering | 11 |

Rows 10, 25 and 26 appear twice in the round column because they were fixed and regressed. Those three are the highest-value tests in the suite.

**Corpus format.** One file per case:

```json
{
  "id": "r11-skul-tenure",
  "seed": 42,
  "scope": "The rule of Wuldweald Valdrith over the Kebarrow Compact, 51-51",
  "passage": "Valdrith took the seat of the Kebarrow Compact, ending Skul's tenure.",
  "expect_rule": "succession",
  "expect_span": "ending Skul's tenure",
  "corrected": "Valdrith took the seat of the Kebarrow Compact. Hehum Skul, the named heir, was cast out.",
  "note": "Skul never held the seat; a set-aside claim rendered as an ended tenure"
}
```

`passage` must fire `expect_rule`. `corrected` must not fire anything. Both assertions matter — a checker that flags everything is as useless as one that flags nothing.

---

## Layer 4 — Chronicle verified against the log

**Input:** a rendered chronicle and its record. **The layer that replaces the hand review.**

For every section, extract and verify:

- **Ruler lists** — every seat-holder in the window, from `POLITY.SUCCESSION`, `POLITY.CHALLENGE` where the outcome is a win, and `POLITY.SECESSION` where the event names a founding seat-holder. That third source was missed until round 8 and is how founding rulers were invisible.
- **Departure manner** per ruler — killed, cast out, died naturally, still holding. Assert the partition is exhaustive.
- **Tenure spans** — clamped to the render window at both ends. One-sided clamping was the round-8 bug.
- **Raid counts** — sent and suffered, split three ways: beaten off, got through with a haul, got through empty. Raids suffered must resolve place ownership at the time of the event.
- **Battle counts** — won and lost, every battle in the window involving the subject.
- **Killing counts** — internal versus external, classified by whether perpetrator and target shared a faction at the time.
- **Marriage counts** — state and apply the convention (first-named party). It is applied consistently now and should be asserted.
- **Every named year** against its event's year, for every event type without exception. Three of the last four rounds had a date error, in three different event types.
- **Every proper noun** against the record.

**Statistics carry a scope.** Assert that a figure quoted inside a reign passage was computed for that reign, not for the faction. This is the check that catches corpus row 10, which has now failed twice.

---

## Layer 5 — Golden diff

**Input:** the current chronicle and a stored known-good chronicle for the same seed.

This is the layer that catches regressions, and it is the cheapest useful thing in the suite.

- Store each accepted render as `golden/chronicle-{seed}-r{n}.md`.
- On a new render, diff every section against the most recent golden.
- **Any assertion that changes is reported for review** — not failed, since renders legitimately vary, but surfaced.
- **Any figure that changes is failed.** The log did not change; a count that moves is a regression in one direction or the other.

All four of round 11's regressions were figure or phrase changes against round 10's render. This layer catches them mechanically.

**Diff the coverage block, not just the prose.** Per `checker-spec.md` 2.4, each run emits per-scope extraction counts for every rule. Those counts are far more stable across renders than sentences are — the log has not changed, so the number of countable claims, dated acts and partitions in a section should barely move even when the wording does.

- **A rule whose extraction count drops sharply between rounds is failed.** Six counts checked last round and two this round means a lexicon or normalisation gap, not a shorter section.
- **A rule that goes from non-zero to zero in any scope is failed outright.** That is the exact signature of round 11's five causes, and it is detectable in one integer comparison.

This is the assertion that would have caught Tier 1 going inert, in a run that took no model call and read no prose.

A useful side effect: the golden set is also the LoRA corpus. Accepted renders are being logged for that purpose anyway; storing them as test fixtures costs nothing extra.

---

## Running it

```
wb test dynamics --seeds 7,42,99,1234,2025     layer 1, no model
wb test checker                                 layer 2, no model
wb test corpus                                  layer 3, no model
wb test chronicle --seed 42                     layer 4, needs a render
wb test golden --seed 42                        layer 5, needs stored artefacts
wb test all
```

Layers 1–3 should be fast enough to run on every commit. Layers 4–5 run per render.

---

## Build order

1. **Layer 2 first.** It is a handful of synthetic strings and it immediately answers the round 11 question — does rule 1.1 fire on the case it was written for.
2. **Layer 5 second.** Cheap, and it stops the regressions that are currently the largest single category of defect.
3. **Layer 3.** Encode the corpus. Rows 10, 25 and 26 first, since those are the known repeat offenders.
4. **Layer 1.** Port the dynamics metrics. Straightforward but needs the record reader.
5. **Layer 4.** The largest build, and it subsumes most of what the hand review does.

Layers 2, 5 and 3 together are the ones worth having before v1.2. Layer 4 can follow.

---

## One thing to decide

Layer 4 duplicates the checker. Both verify prose against events, and if the checker were complete, layer 4 would be redundant.

They are not the same thing, and keeping both is deliberate: **the checker decides what enters canon; the test suite decides whether the checker works.** A checker that silently stops firing — which is what happened this round — is invisible without an independent verifier. If the two ever share an implementation, that property is lost.

Worth stating explicitly in the code so it does not get refactored away by a future you.
