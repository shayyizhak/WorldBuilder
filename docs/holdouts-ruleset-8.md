# Holdout distribution — ruleset-8 against ruleset-7

Read from the stored sidecars. 26 held-out scope(s) of 58 across 5 seeds.

## Per seed

| seed | scopes | held out | rate | rules |
|---|---|---|---|---|
| 1 | 13 | 6 | 46% | count-narration, date, quantity, tenure |
| 7 | 8 | 2 | 25% | date |
| 42 | 13 | 7 | 53% | date, partition-sum, tenure |
| 1234 | 12 | 5 | 41% | date, naming, partition-sum, quantity, succession |
| 2025 | 12 | 6 | 50% | action, date, date-agreement, quantity, succession, tenure |

Per-seed holdout rate range=[25, 53] width=28, in percentage points.

## Every held-out scope

| seed | scope | rules | blocking | fatal |
|---|---|---|---|---|
| 1 | The Wougate Crown, 2–21 | date | 1 | 1 |
| 1 | House Wem, 9–28 | quantity | 1 | 1 |
| 1 | House Wem, 29–48 | count-narration | 1 | 1 |
| 1 | The Wougate Crown, 42–50 | date | 1 | 1 |
| 1 | House Wem, 49–51 | tenure | 1 | 1 |
| 1 | The rule of Kurnsa Bealldo over House Wem, 44–51 | date | 1 | 1 |
| 7 | The Goummeidale Compact, 42–50 | date | 1 | 1 |
| 7 | The rule of Maethhol Gondrur over the Goummeidale Compact, 40–51 | date | 1 | 1 |
| 42 | The Kebarrow Compact, 2–21 | partition-sum | 1 | 1 |
| 42 | The Wurn League, 2–21 | tenure | 1 | 1 |
| 42 | The Wurn League, 22–35 | date | 1 | 1 |
| 42 | The Griwick Compact, 24–43 | date | 1 | 1 |
| 42 | The Griwick Compact, 44–51 | partition-sum | 1 | 1 |
| 42 | The rule of Drarka Draernthun over the Meigate Covenant, 46–51 | date | 2 | 2 |
| 42 | The rule of Paernmel Has over the Griwick Compact, 50–51 | date | 1 | 1 |
| 1234 | The Galweall League, 2–21 | date, partition-sum | 2 | 2 |
| 1234 | The Trostead Compact, 2–21 | partition-sum | 2 | 2 |
| 1234 | The Galweall League, 22–41 | quantity | 1 | 1 |
| 1234 | The Trostead Compact, 22–30 | succession | 1 | 1 |
| 1234 | House Buldbei, 23–42 | naming | 1 | 1 |
| 2025 | The Waeslefell Compact, 2–21 | date-agreement, quantity | 2 | 2 |
| 2025 | The Baesveireach Compact, 4–30 | tenure | 1 | 1 |
| 2025 | Free Kremoor, 28–51 | succession | 1 | 1 |
| 2025 | Greater Baesveireach, 33–51 | action | 1 | 1 |
| 2025 | The Waeslefell Compact, 42–51 | action, date | 2 | 2 |
| 2025 | The Skomere Charter, 47–51 | action | 1 | 1 |

## Grouped by rule

Firing counts are the pre-committed statistic. Extraction is beside them for reading and takes no part in the verdict.

| rule | held-out scopes | share | fired on survivors (ruleset-8) | fired on survivors (ruleset-7) | extracted on survivors (ruleset-8) | extracted on survivors (ruleset-7) |
|---|---|---|---|---|---|---|
| action | 3 | 11% | 0 | 0 | 7 | 11 |
| coined-term | 0 | 0% | 0 | 0 | 1268 | 1548 |
| count-enumeration | 0 | 0% | 0 | 0 | 6 | 14 |
| count-narration | 1 | 3% | 0 | 0 | 20 | 19 |
| coverage | 0 | 0% | 2 | 3 | 0 | 0 |
| date | 11 | 42% | 0 | 0 | 5 | 7 |
| date-agreement | 1 | 3% | 0 | 0 | 43 | 63 |
| departure | 0 | 0% | 0 | 0 | 79 | 74 |
| naming | 1 | 3% | 9 | 8 | 1984 | 2374 |
| outcome | 0 | 0% | 0 | 0 | 0 | 0 |
| partition-sum | 4 | 15% | 0 | 0 | 3 | 5 |
| quantity | 3 | 11% | 0 | 0 | 0 | 1 |
| shape | 0 | 0% | 0 | 0 | 0 | 0 |
| succession | 2 | 7% | 0 | 0 | 4 | 10 |
| summary-body | 0 | 0% | 0 | 0 | 42 | 69 |
| tenure | 3 | 11% | 0 | 0 | 0 | 2 |

## Findings raised by rules that extracted nothing

A rule with an extraction counter stuck at zero has a floor of zero, so it can go silent forever without the golden layer noticing. Where the same scope also carries a `rule-inert` row, the sidecar states both that the rule read nothing here and that a finding it owns decided canon.

| seed | scope | rule | fired | kinds | also `rule-inert` |
|---|---|---|---|---|---|
| 1 | The Wougate Crown, 2–21 | coverage | 2 | incomplete-enumeration | yes |
| 1 | The Wougate Crown, 2–21 | date | 1 | wrong-year | yes |
| 1 | House Wem, 9–28 | quantity | 1 | invented-particular | yes |
| 1 | The Wougate Crown, 42–50 | date | 1 | relative-time | yes |
| 1 | House Wem, 49–51 | tenure | 1 | missing-ruler | yes |
| 1 | The rule of Kurnsa Bealldo over House Wem, 44–51 | date | 1 | out-of-order | yes |
| 7 | The Goummeidale Compact, 42–50 | date | 1 | relative-time | yes |
| 42 | The Wurn League, 2–21 | tenure | 1 | missing-ruler | yes |
| 42 | The Wurn League, 22–35 | date | 1 | wrong-year | yes |
| 42 | The Meigate Covenant, 29–51 | coverage | 1 | incomplete-enumeration | yes |
| 1234 | The Galweall League, 2–21 | date | 1 | relative-time | yes |
| 1234 | The Trostead Compact, 22–30 | succession | 1 | unshared-pair | yes |
| 2025 | The Waeslefell Compact, 2–21 | quantity | 1 | invented-particular | yes |
| 2025 | The Baesveireach Compact, 4–30 | tenure | 1 | missing-ruler | yes |
| 2025 | The Waeslefell Compact, 22–41 | coverage | 1 | incomplete-enumeration | yes |
| 2025 | Free Kremoor, 28–51 | succession | 1 | unshared-pair | yes |
| 2025 | Greater Baesveireach, 33–51 | action | 1 | no-such-event | yes |
| 2025 | The Waeslefell Compact, 42–51 | action | 1 | unsupported-manner | yes |
| 2025 | The Waeslefell Compact, 42–51 | coverage | 1 | incomplete-enumeration | yes |
| 2025 | The Waeslefell Compact, 42–51 | date | 1 | relative-time | yes |
| 2025 | The Skomere Charter, 47–51 | action | 1 | unsupported-manner | yes |

## Scope selection

The denominator moved, so the scope *list* moved too. Diffed against ruleset-7 on the same seeds — and these are different histories, not two renderings of one, so a scope present in one and absent from the other is usually a power that does not exist in the other world.

**Seed 1** — 13 scopes at ruleset-7, 13 at ruleset-8.


**Seed 7** — 8 scopes at ruleset-7, 8 at ruleset-8.


**Seed 42** — 13 scopes at ruleset-7, 13 at ruleset-8.


**Seed 1234** — 12 scopes at ruleset-7, 12 at ruleset-8.


**Seed 2025** — 12 scopes at ruleset-7, 12 at ruleset-8.


## Verdict

- Panel holdouts: 26 (at or above the guard's ten)
- Heaviest rule: date at 11 scope(s), 42% of the panel
- Distinct rules attributed: 9
- Per-seed rate range=[25, 53] width=28, width 28 points against the rule's 20
- Went non-zero to zero on survivors: none
- Findings from rules that extracted nothing: 21

**Escalate.** Halts.
