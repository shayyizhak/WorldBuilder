# Fix Round 12 — one bug, then the exit

## Context

Round 11 was applied. Tier 1 now fires, the coverage block ships, and three passages were correctly excluded from canon with precise diagnostics.

**The exclusions are the best evidence the architecture works.** `outside-the-window` caught *"Pouldrir Ho held the seat since 1"* against a record that opens in year 2 — a defect I flagged in rounds 9 and 11 and which came back both times. It was caught mechanically, on the first pass, and the passage never entered canon. `invented-mind` on "exploiting" and `unsupported-manner` on "seized" are both correct calls. The `name` rule even caught "Hedale", a typo in the prose.

**And coverage did its job immediately** — it exposed a second silent failure mode that nobody had predicted, which is item 1 and effectively the whole round.

Also confirmed fixed: *"killing 149 men"* is gone, Thosruld Lul is Y38, Hadale's marriages are three with the correct Y37 date, and the three-outcome raid classification reads well in the Wurn League section (*"one carried off plunder from Griwick in year 6, while three got through but took nothing, and one was beaten off"* — five, correctly split).

One long-standing question is closed: Vea Lode's raid count. The section enumerates eleven; the twelfth in my count was the Y29 Laehiford raid on Vea Lode, a same-year boundary case against the Y29 secession. Not a miscount. Worth writing the convention down somewhere.

---

## 1. PRIORITY: extracted assertions are being discarded before they are checked

Round 11's failure was rules never receiving input. This round they receive it and drop it.

| rule | extracted | checked | fired | unchecked |
|---|---|---|---|---|
| coined-term | 588 | 588 | 0 | 0% |
| count-enumeration | 11 | 8 | 0 | 27% |
| count-narration | 11 | 11 | 0 | 0% |
| summary-body | 62 | 40 | 0 | 35% |
| date-agreement | 71 | 27 | 0 | **62%** |
| partition-sum | 33 | 1 | 0 | **97%** |

`partition-sum` extracted thirty-three assertions across thirteen scopes and checked one. `date-agreement` discarded forty-four of seventy-one.

The worst scope is Kebarrow 2–21: twelve dates extracted and zero checked, twelve summary-body pairs extracted and zero checked, one partition extracted and zero checked. That section passed Tier 1 without a single assertion being tested.

**The likely cause, worth confirming rather than assuming:** a resolution step failing quietly — an unmatched subject, an unparseable figure, a scope lookup returning nothing — and returning early instead of recording. That is the same shape as the possessive bug from round 11: a gap that presents as a pass.

**The fix should make the gap impossible rather than smaller.** Every extracted assertion terminates in one of exactly three recorded states:

- `checked` — compared, and passed or fired
- `unresolvable` — recorded with the reason it could not be checked
- there is no third silent path

`unresolvable` counts belong in the coverage block alongside `extracted` and `checked`, so `extracted == checked + unresolvable` is an invariant the suite can assert.

### What this alone fixes

Five of the nine defects below are things the rules already know how to catch and simply did not finish checking. They are listed as items 2–6 so they can serve as acceptance cases, not because they need separate fixes.

---

## 2. A total that contradicts its own enumeration — `partition-sum`

Griwick Compact 24–36 opens with *"a plague that killed 477 people and caused 524 to flee"*, then gives the breakdown:

```
[Y0026]  plague breaks out at Griwick (p:4) and takes 185
[Y0027]  plague at Griwick (p:4), year 2: 133 dead and 296 flee
[Y0028]  plague at Griwick (p:4), year 3: 156 dead and 208 flee
```

185 + 133 + 156 = **474**. 296 + 208 = **504**. The prose states both the wrong totals and the correct components, in the same section.

`partition-sum` extracted five assertions in this scope and checked zero.

---

## 3. Four men, three outlaws, one pronoun — `partition-sum`

Kebarrow Compact 42–51: *"…drove four men out of the Compact and left three others declared outlaws."* Then: *"resulting in the cast-out of all four men for attempted murder. **Their** earlier conspiracies were uncovered in 46, leading to their declaration as outlaws."*

Three were declared outlaw — Stonand Ker, Keithfal Naell, Throll Kell. The fourth, Drouldthas Stour, was cast out in **49**, so his conspiracy cannot have been uncovered in 46.

The passage says "three others" and then attributes the outlawry to all four via a pronoun.

---

## 4. The same famine stated twice — `count-narration` or duplicate detection

Vea Lode Covenant 29–48, first paragraph:

> *"In 33, nine more died, and in 34, eight died while 35 fled."*

Five sentences later, same paragraph:

> *"Hunger continued, with 9 dead in 33, 8 dead and 35 fleeing in 34, and 2 dead and 22 fleeing in 35."*

Identical facts, once in words and once in digits. If duplicate detection is keyed on surface form it will miss this; it needs to compare extracted assertions, not spans.

---

## 5. A count that overstates its own list — `count-enumeration`

Hadale Commune: *"During this time, four exiles returned to serve the commune: Kou Peis in 32, Sou Dra in 34, Realsis Leirpu in 35, and Thosruld Lul in 38. **Three** of these returners were later cast out or declared outlaw."*

Two were:

```
Kou Peis        returned 32, cast out 39
Realsis Leirpu  returned 35, cast out 42, cast out again 45 (conspiracy)
Sou Dra         returned 34, took the seat 38, killed 47
Thosruld Lul    returned 38, killed by Heth Fal 43
```

---

## 6. Wars and battles conflated — `count-narration`

Kebarrow Compact 22–41: *"the Compact fought **three wars** against the Griwick Compact, winning all three battles at Kebarrow."*

One war — declared Y32, peace Y35. Three battles, at Y32, Y33 and Y34. The section's own third paragraph gets this right: *"the Compact fought the Griwick Compact in three battles at Kebarrow in 32, 33, and 34."*

---

## 7. An invented motive the checker has a rule for — lexicon

Kebarrow Compact 22–41: *"Wilwound Ska was killed in 31 by Stonand Ker, **who had been motivated by** the earlier raid on Threi Cut."*

`invented-mind` fired on "exploiting" in an excluded passage this same round and missed "motivated by" in a passage that entered canon.

This is round 11's `included` bug in a different lexicon. Two things follow:

- Add the motive-attribution vocabulary: *motivated by, seeking, hoping, fearing, determined to, in revenge for, out of, resentful, ambitious*.
- More usefully, **add a lexicon-completeness test** per the test-suite spec: every entry in every marker list must fire its rule on an identical sentence with only the marker swapped. That converts "we thought of that word" from a memory problem into a test.

---

## 8. Three that need Tier 2

These require event lookup and are already specified. They are not this round's work, but they should be in the corpus.

**Reign scope, twice in one section.** Sworn Men of Laehiford: *"During his tenure"* (Math Ham, 32–39) credited with raids in 23 and 31; *"Under his rule"* (Heth Fal, 39–51) credited with raids in 29 and 33. All four predate the tenure they are attached to.

**Meigate's version is unfixed, and the evidence has been removed.** *"Under Kreathbeas, the Sworn Men sent eight raids: two against the Kebarrow Compact carried off plunder."* Kreathbeas took the seat in 25; those two raids were Y20 and Y23, under Renbeir Surn. The years have been dropped from the sentence since round 11, which makes the claim harder to falsify without fixing it. This is corpus row 10 and it has now survived three rounds.

**A battle rendered as a conquest.** The Wurn League: *"The League lost Hadale to the Kebarrow Compact in year 8 (124 dead) and again in year 20 (106 dead)."* Y8 was a battle at Hadale; the conquest was Y20 only.

**External killings described as internal.** Kebarrow 42–51: *"The ruler's authority eroded through the murder of his own people."* Veillpea Dourn and Thres Thrild were both rulers of the Vea Lode Covenant.

---

## 9. Two notes on the instrument itself

**`coined-term` extracted 588 and fired zero** across thirteen scopes. A rule performing 588 comparisons that has never once objected deserves a sanity check in the opposite direction — confirm it fires on a deliberately coined term. `rule-inert` catches a rule that examines nothing; nothing yet catches a rule that examines everything and is incapable of objecting.

**`rule-inert` on `(front matter)` is noise.** The front matter is two lines of preamble. Exempt scopes below a minimum prose length, or the finding will be ignored wherever it appears.

---

## 10. Style

Vea Lode 29–48 has gone fully transliterated — one sentence per event, including a wall of eleven enumerated raids — and its third paragraph is chronologically scrambled: 45, 46, 45, 46, 47, 47, 46, 48.

This is the particulars/patterns balance oscillating rather than settling. Worth one prompt pass, but not worth another round of tuning; the checker is what holds the line now.

---

## Definition of done

Worth writing this now, while it is still decidable, because the render layer is close and "close" has been true in a misleading way before.

The render layer is **done** when all four hold on a single render of seed 42:

1. **`checked / extracted` ≥ 95% for every rule in every scope**, with `extracted == checked + unresolvable` as an asserted invariant.
2. **Tier 1 fires on every corpus case, and on none of the corrected versions.**
3. **Golden diff green** — no figure has moved against the previous accepted render.
4. **A hand review finds nothing the checker missed.**

Point 4 is the real exit, and it is deliberately not "zero defects". The chronicle will always contain some, and excluded passages are the designed outcome rather than a failure. The bar is that the machine finds what I find. When my review stops adding anything, the render layer is finished and v1.2 begins.

---

## What I want from you

Item 1, and only item 1, then the test suite layers 2, 5 and 3 from `test-suite-spec.md`.

**Six of this round's nine defects are things the checker already knows how to catch.** That is a different situation from rounds 8 through 11, where the defects were prompt failures that decayed after a round or two. These are plumbing failures in a mechanism that demonstrably works — the same mechanism that caught, on its first pass, a defect I had flagged three times without it staying fixed.

Layer 5's coverage diff now has a much sharper assertion available than prose comparison: `checked/extracted` per rule per scope. `partition-sum` at 1 of 33 is a failing build, and nobody needs to read a chronicle to know it.
