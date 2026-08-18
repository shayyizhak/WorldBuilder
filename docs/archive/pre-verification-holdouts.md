# Holdout distribution — ruleset-4 against ruleset-3

Read from the stored sidecars. 20 held-out scope(s) of 60 across 5 seeds.

## Per seed

| seed | scopes | held out | rate | rules |
|---|---|---|---|---|
| 7 | 8 | 2 | 25% | action, succession |
| 42 | 13 | 6 | 46% | action, date, date-agreement, naming, partition-sum |
| 99 | 14 | 5 | 35% | action, date, partition-sum, tenure |
| 1234 | 13 | 2 | 15% | date, partition-sum |
| 2025 | 12 | 5 | 41% | action, date-agreement, quantity, tenure |

Per-seed holdout rate range=[15, 46] width=31, in percentage points.

## Every held-out scope

| seed | scope | rules | blocking | fatal |
|---|---|---|---|---|
| 7 | House Thream, 2–29 | action | 1 | 1 |
| 7 | The Goummeidale Compact, 22–41 | succession | 1 | 1 |
| 42 | The Wurn League, 2–21 | naming, partition-sum | 2 | 2 |
| 42 | The Griwick Compact, 4–23 | action, date | 2 | 2 |
| 42 | The Griwick Compact, 24–43 | date | 1 | 1 |
| 42 | The Vea Lode Covenant, 29–39 | date | 1 | 1 |
| 42 | The Griwick Compact, 44–51 | date-agreement | 1 | 1 |
| 42 | The rule of Drarka Draernthun over the Meigate Covenant, 46–51 | action | 1 | 1 |
| 99 | The Peirnhewick Crown, 2–21 | partition-sum | 1 | 1 |
| 99 | The Peirnhewick Crown, 22–41 | action | 1 | 1 |
| 99 | Free Pistead, 37–47 | date | 1 | 1 |
| 99 | Free Kraefell, 48–51 | tenure | 1 | 1 |
| 99 | The rule of Vi Thrinli over the Sworn Men of Pistead, 50–51 | tenure | 1 | 1 |
| 1234 | The Galweall League, 2–21 | partition-sum | 1 | 1 |
| 1234 | The Trostead Compact, 22–30 | date | 1 | 1 |
| 2025 | The Deafil League, 2–35 | quantity | 1 | 1 |
| 2025 | The Waeslefell Compact, 2–21 | date-agreement | 1 | 1 |
| 2025 | The Baesveireach Compact, 4–30 | action, tenure | 2 | 2 |
| 2025 | Greater Baesveireach, 33–51 | action | 2 | 2 |
| 2025 | The Skomere Charter, 47–51 | action | 1 | 1 |

## Grouped by rule

Firing counts are the pre-committed statistic. Extraction is beside them for reading and takes no part in the verdict.

| rule | held-out scopes | share | fired on survivors (ruleset-4) | fired on survivors (ruleset-3) | extracted on survivors (ruleset-4) | extracted on survivors (ruleset-3) |
|---|---|---|---|---|---|---|
| action | 7 | 35% | 0 | 0 | 7 | 6 |
| coined-term | 0 | 0% | 0 | 0 | 1633 | 1668 |
| count-enumeration | 0 | 0% | 0 | 0 | 10 | 18 |
| count-narration | 0 | 0% | 0 | 0 | 31 | 28 |
| coverage | 0 | 0% | 5 | 2 | 0 | 0 |
| date | 5 | 25% | 0 | 0 | 9 | 8 |
| date-agreement | 2 | 10% | 0 | 0 | 42 | 59 |
| departure | 0 | 0% | 0 | 0 | 78 | 107 |
| naming | 1 | 5% | 12 | 14 | 2450 | 2634 |
| outcome | 0 | 0% | 0 | 0 | 0 | 0 |
| partition-sum | 3 | 15% | 0 | 0 | 5 | 5 |
| quantity | 1 | 5% | 0 | 0 | 0 | 2 |
| shape | 0 | 0% | 0 | 0 | 0 | 0 |
| succession | 1 | 5% | 0 | 0 | 8 | 12 |
| summary-body | 0 | 0% | 0 | 0 | 43 | 59 |
| tenure | 3 | 15% | 0 | 0 | 2 | 2 |

## Findings raised by rules that extracted nothing

A rule with an extraction counter stuck at zero has a floor of zero, so it can go silent forever without the golden layer noticing. Where the same scope also carries a `rule-inert` row, the sidecar states both that the rule read nothing here and that a finding it owns decided canon.

| seed | scope | rule | fired | kinds | also `rule-inert` |
|---|---|---|---|---|---|
| 7 | House Thream, 2–29 | action | 1 | wrong-collapse | yes |
| 42 | The Griwick Compact, 4–23 | action | 1 | invented-mind | yes |
| 42 | The Griwick Compact, 4–23 | date | 1 | relative-time | yes |
| 42 | The Griwick Compact, 4–23 | quantity | 1 | vague-quantity | yes |
| 42 | The Kebarrow Compact, 22–40 | coverage | 1 | incomplete-enumeration | yes |
| 42 | The Griwick Compact, 24–43 | date | 1 | wrong-year | yes |
| 42 | The Vea Lode Covenant, 29–39 | date | 1 | relative-time | yes |
| 42 | The rule of Drarka Draernthun over the Meigate Covenant, 46–51 | coverage | 1 | incomplete-enumeration | yes |
| 99 | The Peirnhewick Crown, 2–21 | outcome | 1 | hedged-outcome | yes |
| 99 | The Peirnhewick Crown, 2–21 | quantity | 1 | vague-quantity | yes |
| 99 | The Peirnhewick Crown, 22–41 | coverage | 1 | incomplete-enumeration | yes |
| 99 | Free Pistead, 37–47 | date | 1 | relative-time | yes |
| 99 | The Peirnhewick Crown, 42–51 | coverage | 2 | incomplete-enumeration | yes |
| 99 | Free Kraefell, 48–51 | tenure | 1 | missing-ruler | yes |
| 99 | The rule of Vi Thrinli over the Sworn Men of Pistead, 50–51 | tenure | 1 | missing-ruler | yes |
| 1234 | The Galweall League, 22–41 | coverage | 1 | incomplete-enumeration | yes |
| 2025 | The Deafil League, 2–35 | quantity | 1 | invented-particular | yes |
| 2025 | The Baesveireach Compact, 4–30 | action | 1 | unsupported-manner | yes |
| 2025 | The Baesveireach Compact, 4–30 | tenure | 1 | missing-ruler | yes |
| 2025 | The Waeslefell Compact, 22–41 | coverage | 1 | incomplete-enumeration | yes |
| 2025 | Greater Baesveireach, 33–51 | action | 2 | no-such-event, unsupported-manner | yes |
| 2025 | The Skomere Charter, 47–51 | action | 1 | unsupported-manner | yes |

## Scope selection

The denominator moved, so the scope *list* moved too. Diffed against ruleset-3 on the same seeds — and these are different histories, not two renderings of one, so a scope present in one and absent from the other is usually a power that does not exist in the other world.

**Seed 7** — 12 scopes at ruleset-3, 8 at ruleset-4.

- gone: The Sti Seam War: the Goummeidale Compact against House Thream, 3–7
- gone: The War of the Goummeidale Compact's Grudge: the Goummeidale Compact against the Kreagemoor Covenant, 36–38
- gone: House Thream, 2–51
- gone: The Kraeford Compact, 2–21
- gone: The Kreagemoor Covenant, 8–38
- gone: The Kraeford Compact, 22–41
- gone: The Goummeidale Compact, 42–51
- gone: The Kraeford Compact, 42–51
- gone: The rule of Grigun Thrundeith over the Kraeford Compact, 50–51
- gone: The rule of Theirnpae Gro over the Goummeidale Compact, 51–51
- new: The War of the Goummeidale Compact's Grudge: the Goummeidale Compact against House Thream, 5–7
- new: The Long Quarrel over Kraeford: the Goummeidale Compact against the Kraeford Compact, 21–23
- new: House Thream, 2–29
- new: The Kraeford Compact, 2–23
- new: The Goummeidale Compact, 42–50
- new: The rule of Maethhol Gondrur over the Goummeidale Compact, 40–51

**Seed 42** — 14 scopes at ruleset-3, 13 at ruleset-4.

- gone: The War for Threi Cut: the Griwick Compact against the Wurn League, 5–8
- gone: The War for Threi Cut of 7: the Kebarrow Compact against the Wurn League, 7–10
- gone: The Wurn League, 2–10
- gone: The Sworn Men of Laehiford, 19–35
- gone: The Sworn Men of Hadale, 20–39
- gone: The Kebarrow Compact, 22–41
- gone: The Sworn Men of Meigate, 26–51
- gone: The Kebarrow Compact, 42–44
- gone: The rule of Stonand Ker over the Sworn Men of Meigate, 41–51
- gone: The rule of Sothkel Sald over the Griwick Compact, 50–51
- new: The War for Threi Cut: the Griwick Compact against the Wurn League, 5–10
- new: The War for Griwick: the Kebarrow Compact against the Griwick Compact, 27–29
- new: The Wurn League, 2–21
- new: The Kebarrow Compact, 22–40
- new: The Wurn League, 22–35
- new: The Meigate Covenant, 29–51
- new: The Vea Lode Covenant, 29–39
- new: The rule of Drarka Draernthun over the Meigate Covenant, 46–51
- new: The rule of Paernmel Has over the Griwick Compact, 50–51

**Seed 99** — 14 scopes at ruleset-3, 14 at ruleset-4.

- gone: The the Peirnhewick Crown Aggression: the Peirnhewick Crown against the Staernwefell Crown, 3–5
- gone: The the Peirnhewick Crown Aggression of 20: the Peirnhewick Crown against the Pistead Compact, 20–21
- gone: The Pistead Compact, 2–21
- gone: The Staernwefell Crown, 3–24
- gone: The Sworn Men of Kraefell, 19–31
- gone: The Sworn Men of Pistead, 26–50
- gone: The Staernwefell Charter, 35–51
- gone: Free Puholt, 36–49
- gone: The rule of Beind Grorn over the Peirnhewick Crown, 48–51
- gone: The rule of Veall Lea over the Sworn Men of Pistead, 50–51
- new: The War of the Peirnhewick Crown's Grudge: the Peirnhewick Crown against the Staernwefell Crown, 7–9
- new: The the Peirnhewick Crown Aggression: the Peirnhewick Crown against the Pistead Compact, 11–13
- new: The Staernwefell Crown, 4–9
- new: The Pistead Compact, 6–22
- new: The Burghers of Staernwefell, 13–39
- new: The Second Crown of Kraefell, 15–44
- new: Free Pistead, 37–47
- new: The Sworn Men of Pistead, 47–50
- new: The rule of Vi Thrinli over the Sworn Men of Pistead, 50–51
- new: The rule of Nothmeand Trearol over the Peirnhewick Crown, 51–51

**Seed 1234** — 12 scopes at ruleset-3, 13 at ruleset-4.

- gone: The War of the Trostead Compact's Grudge: the Trostead Compact against House Buldbei, 4–6
- gone: The the Trostead Compact Aggression: the Trostead Compact against the Galweall League, 7–10
- gone: House Buldbei, 3–36
- gone: The Galweall League, 22–34
- gone: The Trostead Compact, 22–41
- gone: The Gaehollow Charter, 30–41
- gone: The Trostead Compact, 42–48
- gone: Greater Themoor, 46–50
- gone: The rule of Skind Pesvi over Greater Themoor, 46–51
- gone: The rule of Peansith Rae over the Trostead Compact, 48–51
- new: The Long Quarrel over Pei Delve: the Trostead Compact against House Buldbei, 9–11
- new: The War of the Galweall League's Grudge: the Galweall League against the Trostead Compact, 19–21
- new: House Buldbei, 3–22
- new: The Rising of Pellweagate, 16–45
- new: The Galweall League, 22–41
- new: The Trostead Compact, 22–30
- new: House Buldbei, 23–42
- new: The Galweall League, 42–51
- new: House Buldbei, 43–51
- new: The rule of Find Bound over House Buldbei, 50–51
- new: The rule of Fear Mel over the Galweall League, 51–51

**Seed 2025** — 9 scopes at ruleset-3, 12 at ruleset-4.

- gone: The Thea Seam War: the Waeslefell Compact against the Baesveireach Compact, 3–7
- gone: The War for Skomere: the Waeslefell Compact against the Deafil League, 21–24
- gone: The Deafil League, 2–33
- gone: The Baesveireach Compact, 3–7
- gone: The Second Crown of Baesveireach, 17–30
- gone: The rule of Thraebaern Ramdem over the Waeslefell Compact, 51–51
- new: The Thea Seam War: the Waeslefell Compact against the Baesveireach Compact, 4–10
- new: The the Waeslefell Compact Aggression: the Waeslefell Compact against the Baesveireach Compact, 26–30
- new: The Deafil League, 2–35
- new: The Baesveireach Compact, 4–30
- new: Free Kremoor, 28–51
- new: Greater Baesveireach, 33–51
- new: The Skomere Charter, 47–51
- new: The rule of Til Saeld over Free Kremoor, 49–51
- new: The rule of Haeth Tha over the Waeslefell Compact, 51–51

## Verdict

- Panel holdouts: 20 (at or above the guard's ten)
- Heaviest rule: action at 7 scope(s), 35% of the panel
- Distinct rules attributed: 8
- Per-seed rate range=[15, 46] width=31, width 31 points against the rule's 20
- Went non-zero to zero on survivors: none
- Findings from rules that extracted nothing: 22

**Escalate.** Halts.
