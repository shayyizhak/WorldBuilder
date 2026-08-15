# Fix Round 11 — debug Tier 1, then stop fixing prose

## Context

Round 10 was applied and seed 42 re-rendered. Both sidecars are now wired: `chronicle-42_findings.json` carries five records with `rule`, `scope`, `span`, `detail`, `blocking` and `fatal`. That half of round 10 item 1 is done and the format is right.

**Round 10's other items landed:**

- The Wurn League section now narrates all three seat-holders, Reweld Wul included.
- *"Three places taken from the Wurn League"* is gone from the Kebarrow section.
- Thra Bround is one death rather than two.
- Kou Peis and Veillpea Dourn are the right way round at Y45.
- The Heth Fal reign renders rather than being excluded.
- Griwick 4–23's raid enumeration is exact: six sent, three with plunder and three beaten off, each named.

**This brief is deliberately not a prose-fix round.** The defect count is not falling — eight at round 8, six at round 9, six at round 10, twelve now — and four of this round's twelve were defects that had already been fixed. Prompt-level fixes decay after one or two rounds. Another round of tightening the prompt buys two more rounds.

Three things instead: make Tier 1 work, make regressions impossible, then one confirming render and move to v1.2.

---

## 1. PRIORITY: Tier 1 returns empty on a case it is specified to catch

`chronicle-42_tier1.json` is `[]`.

The Sworn Men of Laehiford section contains:

> *"**Fourteen** people returned from exile and took service with the power between 22 and 51. These returns **included** Trem Lolkoll in 22, Math Ham in 24, Sou Dra in 24, Teillmol Lund in 31, Le Vild in 34, Drarka Draernthun in 35, Heth Fal in 37, Herpeim Raern in 39, Draes Wild in 43, Stonand Ker in 46, Voudreirn Wer in 46, Kou Peis in 47, Thurnean Kourn in 48, and Drouldthas Stour in 51."*

Fourteen stated. Fourteen named. Marked with a partiality marker.

This is rule 1.1 verbatim — *"items equal to the count with a partiality marker is itself a failure"* — and it is the same construction flagged in round 7. It requires no event access, no world model, and no model call.

**Do not fix the prose. Find out why the rule did not fire.** Three candidate causes, all cheap to distinguish:

1. **Rule 1.1 is not implemented.** Tier 1 may currently only contain the partition-sum check (1.2), which has nothing to fire on here.
2. **The partiality-marker list is incomplete.** If it matches "including" but not "included", round 7's case would pass and this one would not.
3. **Count extraction misses spelled-out numerals.** "Fourteen" is a word; the years in the enumeration are digits. If the count parser only reads digits, no count is found and the rule has nothing to compare against.

Report which. If it is (3), note that the document mixes conventions — the Laehiford section spells figures as words throughout (*"thirty-one grain"*, *"sixty-eight people"*) while every other section uses digits, so a digits-only parser would be blind to exactly one section.

**A second Tier 1 case is present in the same section**, for rule 1.4:

- Opening: *"The period began in 20 when Laehiford broke from the Kebarrow Compact, with Realsis Leirpu taking the seat."*
- Second paragraph: *"He took service with the power in 20."*

```
[Y0020]  POLITY.SECESSION  Laehiford (p:5) breaks from the Kebarrow Compact (f:2) as the Sworn Men of Laehiford, with Realsis Leirpu (a:61) taking its seat
```

Taking the seat and taking service are different things, and the passage asserts both about the same person in the same year. The body contradicts its own opening — a Tier 1.4 catch needing only the text.

**Acceptance for this item:** Tier 1 flags both cases on the *current* render, before any prose is changed.

---

## 2. Four fixes came back

| Defect | Fixed in | Broken again |
|---|---|---|
| *"held the seat since year 1"* (Pouldrir Ho) | round 9 | now |
| *"killing 149 men"* | round 9 | now |
| Faction-lifetime raids attributed to a reign | round 8 | now |
| Hadale courtings-away figure | round 8 | now |

**Pouldrir Ho.** Griwick 4–23: *"Pouldrir Ho, who held the seat since year 1, was killed in year 20."* The log's earliest event is Y2 and no event records him taking the seat. Round 9 asked whether the engine carries pre-Y2 state; that question is still unanswered and is now load-bearing, because if the engine does hold a founding year the checker cannot validate against the log, and if it does not, this is invented.

**"Killing 149 men."** Vea Lode 49–51. The log says 149 dead. Fixed at round 9, back now.

**Reign scope.** Sworn Men of Meigate: *"Renbeir died in 25, and Kreathbeas Waeth took the seat. **Under Kreathbeas**, the Sworn Men sent eight raids: two against the Kebarrow Compact **in 20 and 23** carried off plunder…"* Kreathbeas took the seat in 25. This is round 8 item 4 verbatim.

**Hadale courtings-away.** *"Four people were courted away from its ruler during the period."* The log has nine `POLITY.COURTS_SUPPORT` events against f:6. Round 8's render said nine and verified.

These four are the argument for item 3. None of them needed to be caught by a human reading the prose; all four are assertions that were once correct and are now not.

---

## 3. New defects

### 3.1 A fabricated tenure

The rule of Wuldweald Valdrith: *"Valdrith took the seat of the Kebarrow Compact, **ending Skul's tenure**."*

```
[Y0051]  POLITY.SUCCESSION_DISPUTED  Wuldweald Valdrith (a:91) contests the claim of Hehum Skul (a:72), the named heir, to the Kebarrow Compact (f:2)
[Y0051]  POLITY.SUCCESSION           Wuldweald Valdrith (a:91) takes the seat of the Kebarrow Compact (f:2), setting aside the claim of the named heir Hehum Skul (a:72)
[Y0051]  POLITY.EXILE                Hehum Skul (a:72) is cast out of the Kebarrow Compact (f:2) — the losing claim
```

Hehum Skul never held the seat. A set-aside claim has become a tenure that was ended.

**This is the project's most persistent fabrication class** — the same shape as Stonand Ker, and as Turaer Danpa killing Befu Seirn. It has now appeared in rounds 3, 4, 5, 8 and 11. Here it occurs in a two-event scope with no ambiguity to blame.

It is a Tier 2 succession check: *ending X's tenure* asserts X held the seat, which requires an event showing X holding it.

### 3.2 Causality reversed in the same section

The Wuldweald reign narrates the election in paragraph one and the murder that triggered it in paragraph two, joined by *"During this same year."* The murder caused the succession; the causal edge is in the log.

### 3.3 A wrong year

*"…and Thosruld Lul in 39."* The log has Y38.

### 3.4 A duration attached to the wrong anchor

Vea Lode 49–51: *"The Covenant and the Sworn Men made peace in year 51, **two years after the collapse** of the Sworn Men."*

```
[Y0050]  POLITY.COLLAPSE     the Sworn Men of Meigate (f:4) is finished — landless, its last 12 followers scatter
[Y0051]  DIPLO.PEACE_SIGNED  the Vea Lode Covenant (f:7) and the Sworn Men of Meigate (f:4) make peace after 2 years, after the collapse
```

The "2 years" is the war's duration (declared Y49, peace Y51). The collapse was Y50 — one year before the peace. The duration has been re-anchored to the nearest preceding event. Round 4's class, and the established rule already covers it: use the event's own duration field for the interval it describes.

---

## 4. Two style regressions, in opposite directions

**Kebarrow 22–41 has eleven rulers and names one.** Earlier renders named Weallhous Dreld, Gatros Hearn, Teillmol Lund, Theald Va, Wilwound Ska, Le Vild, Heth Fal, Nael War, Paernrom Sir and Kondruth Tru, and that section was the strongest in the book. It is now entirely aggregate — accurate, verifiable, and empty of people.

**Kebarrow 42–51 has gone the other way:** *"In 43… In 45… In 46… In 47… In 48… In 49… In 50… In 51…"* One sentence per year in log order. That is the round-2 transliteration failure.

Both were correct two rounds ago. This is the particulars/patterns balance oscillating rather than settling, and it is the clearest sign that prompt-level tuning has stopped converging.

Minor: *"failed Counter-raids"* is capitalised mid-sentence, and "counter-raid" is not a concept the log carries.

---

## 5. Still open

Vea Lode 29–48 states eleven raids suffered; I count twelve on places it held at the time. Unchanged for three rounds. What is wanted is the counting convention, not a recount.

---

## Build order for this round

1. **Debug Tier 1** until it flags both cases in item 1 on the current render. Report the cause.
2. **Build the test suite** in `test-suite-spec.md`, including the regression corpus. This is the larger piece of work and it is what stops item 2 recurring.
3. **Then** fix the prose defects in items 2, 3 and 4 — with a test asserting each stays fixed.
4. One confirming render of seed 42.
5. Move to v1.2.

---

## What I want from you

Item 1 first, and the answer matters more than the fix. Tier 1 was specified precisely so that it could be built and trusted without a model in the loop; if the rule that motivated the whole tiering does not fire on the case it was written for, that needs understanding before anything is layered on top.

After that, the honest position on the render layer: it is close to done, and done does not mean defect-free. The chronicle reads as history and its figures verify — the Wurn League's arc, Griwick's plague-driven collapse, Laehiford's exile churn are all genuinely good. What remains is a long tail that prompt work will not close, because prompt fixes decay. The end state you designed for is decent prose plus a checker that catches what slips, with failed passages excluded rather than corrected. That architecture is right. It needs the checker to work, and then it needs to stop being iterated on.
