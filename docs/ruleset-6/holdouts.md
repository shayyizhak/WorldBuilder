# Holdout distribution — ruleset-6 against ruleset-6

Read from the stored sidecars. 21 held-out scope(s) of 58 across 5 seeds.

## Per seed

| seed | scopes | held out | rate | rules |
|---|---|---|---|---|
| 1 | 13 | 4 | 30% | count-narration, date, partition-sum, tenure |
| 7 | 8 | 2 | 25% | date, succession |
| 42 | 13 | 6 | 46% | action, count-narration, date, succession, tenure |
| 1234 | 12 | 6 | 50% | count-narration, date, naming, quantity, succession |
| 2025 | 12 | 3 | 25% | action, date, date-agreement |

Per-seed holdout rate range=[25, 50] width=25, in percentage points.

## Every held-out scope

| seed | scope | rules | blocking | fatal |
|---|---|---|---|---|
| 1 | House Wem, 29–48 | count-narration | 1 | 1 |
| 1 | The Wougate Crown, 42–50 | partition-sum | 1 | 1 |
| 1 | House Wem, 49–51 | tenure | 1 | 1 |
| 1 | The rule of Kurnsa Bealldo over House Wem, 44–51 | date | 1 | 1 |
| 7 | The Goummeidale Compact, 2–21 | date | 1 | 1 |
| 7 | The rule of Maethhol Gondrur over the Goummeidale Compact, 40–51 | succession | 1 | 1 |
| 42 | The Kebarrow Compact, 2–21 | date | 1 | 1 |
| 42 | The Wurn League, 2–21 | tenure | 1 | 1 |
| 42 | The Kebarrow Compact, 22–40 | action | 1 | 1 |
| 42 | The Griwick Compact, 24–43 | count-narration | 1 | 1 |
| 42 | The Meigate Covenant, 29–51 | succession | 1 | 1 |
| 42 | The rule of Drarka Draernthun over the Meigate Covenant, 46–51 | date | 1 | 1 |
| 1234 | The Galweall League, 2–21 | quantity | 1 | 1 |
| 1234 | The Trostead Compact, 2–21 | quantity | 1 | 1 |
| 1234 | House Buldbei, 3–22 | date | 1 | 1 |
| 1234 | The Galweall League, 22–41 | count-narration | 1 | 1 |
| 1234 | The Trostead Compact, 22–30 | succession | 1 | 1 |
| 1234 | House Buldbei, 23–42 | naming | 1 | 1 |
| 2025 | The Waeslefell Compact, 2–21 | date-agreement | 1 | 1 |
| 2025 | The Waeslefell Compact, 22–41 | action, date | 2 | 2 |
| 2025 | Greater Baesveireach, 33–51 | date | 1 | 1 |

## Grouped by rule

Firing counts are the pre-committed statistic. Extraction is beside them for reading and takes no part in the verdict.

| rule | held-out scopes | share | fired on survivors (ruleset-6) | fired on survivors (ruleset-6) | extracted on survivors (ruleset-6) | extracted on survivors (ruleset-6) |
|---|---|---|---|---|---|---|
| action | 2 | 9% | 0 | 0 | 9 | 9 |
| coined-term | 0 | 0% | 0 | 0 | 1385 | 1385 |
| count-enumeration | 0 | 0% | 0 | 0 | 12 | 12 |
| count-narration | 3 | 14% | 0 | 0 | 15 | 15 |
| coverage | 0 | 0% | 0 | 0 | 0 | 0 |
| date | 7 | 33% | 0 | 0 | 8 | 8 |
| date-agreement | 1 | 4% | 0 | 0 | 48 | 48 |
| departure | 0 | 0% | 0 | 0 | 82 | 82 |
| naming | 1 | 4% | 9 | 9 | 2146 | 2146 |
| outcome | 0 | 0% | 1 | 1 | 0 | 0 |
| partition-sum | 1 | 4% | 0 | 0 | 1 | 1 |
| quantity | 2 | 9% | 0 | 0 | 2 | 2 |
| shape | 0 | 0% | 0 | 0 | 0 | 0 |
| succession | 3 | 14% | 0 | 0 | 8 | 8 |
| summary-body | 0 | 0% | 0 | 0 | 49 | 49 |
| tenure | 2 | 9% | 0 | 0 | 1 | 1 |

## Findings raised by rules that extracted nothing

A rule with an extraction counter stuck at zero has a floor of zero, so it can go silent forever without the golden layer noticing. Where the same scope also carries a `rule-inert` row, the sidecar states both that the rule read nothing here and that a finding it owns decided canon.

| seed | scope | rule | fired | kinds | also `rule-inert` |
|---|---|---|---|---|---|
| 1 | House Wem, 49–51 | tenure | 1 | missing-ruler | yes |
| 1 | The rule of Kurnsa Bealldo over House Wem, 44–51 | date | 1 | out-of-order | yes |
| 7 | The Goummeidale Compact, 2–21 | coverage | 1 | incomplete-enumeration | yes |
| 7 | The Goummeidale Compact, 2–21 | date | 1 | out-of-order | yes |
| 7 | The rule of Maethhol Gondrur over the Goummeidale Compact, 40–51 | succession | 1 | wrong-killer | yes |
| 42 | The Kebarrow Compact, 2–21 | date | 1 | out-of-order | yes |
| 42 | The Wurn League, 2–21 | tenure | 1 | missing-ruler | yes |
| 1234 | The Galweall League, 2–21 | quantity | 1 | invented-particular | yes |
| 1234 | The Trostead Compact, 2–21 | quantity | 1 | invented-particular | yes |
| 1234 | House Buldbei, 3–22 | date | 1 | wrong-year | yes |
| 1234 | The Trostead Compact, 22–30 | succession | 1 | unshared-pair | yes |
| 2025 | The Baesveireach Compact, 4–30 | outcome | 1 | hedged-outcome | yes |
| 2025 | The Waeslefell Compact, 22–41 | action | 1 | wrong-collapse | yes |
| 2025 | The Waeslefell Compact, 22–41 | coverage | 1 | incomplete-enumeration | yes |
| 2025 | The Waeslefell Compact, 22–41 | date | 1 | out-of-order | yes |
| 2025 | Greater Baesveireach, 33–51 | date | 1 | relative-time | yes |

## Scope selection

The denominator moved, so the scope *list* moved too. Diffed against ruleset-6 on the same seeds — and these are different histories, not two renderings of one, so a scope present in one and absent from the other is usually a power that does not exist in the other world.

**Seed 1** — 13 scopes at ruleset-6, 13 at ruleset-6.


**Seed 7** — 8 scopes at ruleset-6, 8 at ruleset-6.


**Seed 42** — 13 scopes at ruleset-6, 13 at ruleset-6.


**Seed 1234** — 12 scopes at ruleset-6, 12 at ruleset-6.


**Seed 2025** — 12 scopes at ruleset-6, 12 at ruleset-6.


## Verdict

- Panel holdouts: 21 (at or above the guard's ten)
- Heaviest rule: date at 7 scope(s), 33% of the panel
- Distinct rules attributed: 9
- Per-seed rate range=[25, 50] width=25, width 25 points against the rule's 20
- Went non-zero to zero on survivors: none
- Findings from rules that extracted nothing: 16

**Escalate.** Halts.
