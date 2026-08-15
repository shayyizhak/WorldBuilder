# Fix Round 8 — summaries, outcomes, and the party a label doesn't name

## Context

Round 7 was applied and seed 42 re-rendered against the same log (694 events), so this is a clean comparison of render changes.

**Three significant fixes landed:**

- **The unverified split is done, and done the right way.** Failed passages are out of canon in `chronicle-42.unverified.md`, the chronicle states plainly that no verified account exists for that scope, and the note that each passage was written twice before exclusion is a good addition — it distinguishes a hard failure from a bad roll.
- **Reign scoping is fixed.** Heth Fal now renders as *"over the Sworn Men of Laehiford, 39–51"*, correctly titled, and the raid count reads three (the in-window raids) rather than the faction's lifetime seven. Item 1 of round 7 is closed.
- **Founding rulers are recorded at secession.** `Meigate breaks from the Kebarrow Compact as the Sworn Men of Meigate, with Renbeir Surn (a:58) taking its seat`, and the same for Stald Gearngoll at Vea Lode. Meigate's ruler list and ten-year average tenure now both verify. Item 2 closed.

The checker also caught the Thres Thrild year error it missed last round.

**Dynamics unchanged and healthy:** zero dangling references, 5.8% repeat rate, maximum causal depth 15, 91 distinct two-step shapes, 168 of 524 edges crossing domains.

Three new faction scopes appeared this round — Griwick 4–23, Griwick 24–36, Meigate 19–51 — and most of what follows is in them. New scope, old failure classes.

---

## 1. PRIORITY: section-opening summaries are not checked against their own bodies

The Griwick Compact 4–23 section opens:

> *"…and ended with Turaer Danpa holding the seat after killing Befu Seirn."*

Two paragraphs later, the same section states:

> *"Bu Rumpirn had Befu Seirn murdered in year 23, and Turaer Danpa took the seat by the strongest claim."*

The body is correct:

```
[Y0021]  LIFE.DEATH_VIOLENT  Heillvar Maer (a:29), ruler of the Griwick Compact (f:3), is killed by Turaer Danpa (a:14) at Griwick (p:4)
[Y0023]  LIFE.DEATH_VIOLENT  Befu Seirn (a:26), ruler of the Griwick Compact (f:3), is killed by Bu Rumpirn (a:18) at Griwick (p:4)
[Y0023]  POLITY.SUCCESSION   Turaer Danpa (a:14) takes the seat of the Griwick Compact (f:3) (by the strongest claim)
```

Turaer Danpa killed **Heillvar Maer**, in 21. Bu Rumpirn killed Befu Seirn, in 23. The opening fuses Danpa's real killing with the succession he gained two years later.

**This is the Stonand Ker construction exactly** — a killing and a seat-taking that sit adjacent in the log, joined into a relationship the log does not carry. It has now appeared in rounds 3, 4, 5 and 8.

What is new and useful: **the body paragraph gets it right.** The opening summary is evidently generated separately and is not validated against the passage it introduces. That is a much cheaper check than full statement validation:

- Every claim in a section-opening summary must be entailed by a claim in the body, or be a supplied statistic.
- Where the summary and the body disagree about who did what to whom, the section fails.

This would have caught the defect without any new understanding of the event log. Add it as its own check, and add the Danpa/Seirn case to the regression set.

---

## 2. Contester and heir swapped in a succession dispute

Griwick Compact 24–36: *"Thurnean Kourn took the seat after contesting Veillpea Dourn's claim. Veillpea Dourn was cast out."*

```
[Y0034]  POLITY.SUCCESSION_DISPUTED  Veillpea Dourn (a:53) contests Thurnean Kourn (a:47)'s claim to the Griwick Compact (f:3) (rule: strongest)
[Y0034]  POLITY.SUCCESSION           Thurnean Kourn (a:47) takes the seat of the Griwick Compact (f:3) (the named heir's claim upheld)
[Y0034]  POLITY.EXILE                Veillpea Dourn (a:53) is cast out of the Griwick Compact (f:3) — the losing claim
```

Dourn contested. Kourn was the named heir and **his claim was upheld** — he did not contest anything. The roles are reversed.

The likely cause is worth checking directly: *claim upheld* is rare in this world, and *the named heir's claim set aside* is the overwhelming majority case. The renderer appears to have applied the common pattern rather than reading this event's own outcome clause. That makes it the same family as the round-6 challenge inversion, now in successions rather than challenges.

Two consequences follow. First, `the named heir's claim upheld` needs the same treatment `wins` / `loses` got on challenges — it is an outcome, and outcomes are particulars. Second, and more general: **any event type with a strongly skewed outcome distribution is a fabrication risk**, because the model can score well by guessing the majority case. Worth auditing which other event types have that shape.

---

## 3. A collapse attributed to the wrong power — and the period's climax dropped

Kebarrow Compact 2–21: *"The Compact took Hadale from the Wurn League in year 20, but peace was made with the Wurn League in year 21 as the Compact's standing collapsed."*

```
[Y0020]  CONFLICT.CONQUEST   the Kebarrow Compact (f:2) takes Hadale (p:2) from the Wurn League (f:1)
[Y0020]  POLITY.COLLAPSE     the Wurn League (f:1) is finished — landless, its last 21 followers scatter
[Y0021]  DIPLO.PEACE_SIGNED  the Kebarrow Compact (f:2) and the Wurn League (f:1) make peace after 1 years (collapse)
```

The **Wurn League** collapsed. Taking Hadale is what left it landless and finished it.

The section inverts this into the Compact collapsing, and then never mentions the Wurn League's destruction at all — one of the two founding powers of the world, wiped out by the faction the section is about, in the last year of the period it covers. It is the climax of the scope and it is both absent and reversed.

**The vector is the `(collapse)` reason code, which does not name a party.** This is the ambiguous-label problem from rounds 1 and 4 in a new place. Two rounds ago the same event rendered correctly as *"peace was made in year 21 after the Wurn League's collapse"* — same input, opposite canon across runs.

Two fixes:

- **Engine:** name the party in the reason code — `(collapse: f:1)` or equivalent — for every reason code that implicitly refers to one side. Audit the rest for the same shape.
- **Render:** a `POLITY.COLLAPSE` event inside a scope's window is never optional. The destruction of a power is not a detail that can be dropped for length.

---

## 4. Faction-lifetime statistics narrated as reign statistics

Sworn Men of Meigate: *"Under Kreathbeas, the Sworn Men sent eight raids against the Griwick Compact and the Vea Lode Covenant."*

Kreathbeas Waeth held the seat 25–48. The raids in that window number six, all beaten off. The two that carried plunder were under **Renbeir Surn**:

```
[Y0020]  the Sworn Men of Meigate raids Laehiford (p:5), carrying off 39 grain and killing 22
[Y0023]  the Sworn Men of Meigate raids Hadale (p:2), carrying off 33 grain and killing 12
```

Neither target belongs to the Griwick Compact or the Vea Lode Covenant either, so the enumeration of opponents is wrong along with the count.

The next sentence gives the 2-plunder / 6-beaten-off split correctly, which shows the figures are right **at faction scope** and have simply been attached to a reign. Statistics carry a scope; the prose must not silently re-attribute them to a narrower one. If a reign-scoped figure is wanted, the engine must compute it for that reign.

---

## 5. Vague prose where exact figures exist

Griwick Compact 24–36: *"A plague broke out at Griwick in 26, killing hundreds and driving many away over the next two years."*

```
[Y0026]  ECONOMY.PLAGUE       plague breaks out at Griwick (p:4) and takes 185
[Y0027]  ECONOMY.PLAGUE       plague at Griwick (p:4), year 2: 133 dead and 296 flee
[Y0028]  ECONOMY.PLAGUE       plague at Griwick (p:4), year 3: 156 dead and 208 flee
[Y0029]  ECONOMY.PLAGUE_ENDS  the pestilence at Griwick (p:4) burns out after 3 years
```

474 dead, 504 fled, over **three** years, not two. This is the largest single catastrophe in the world's history and the reason Griwick was too weak to hold Vea Lode four years later.

This is the inverse of the round-3 problem. There the model computed figures it should have been given; here it has been given figures and declined to use them. The rule needs stating in both directions: **supplied figures must be stated, not summarised into "hundreds" or "many."** Vagueness is not a safe default — it discards the only content the model can state with certainty.

---

## 6. A wrong year in the Heth Fal reign

*"Voudreirn Wer won Baedros Mam away from the ruler in 49. Teillmol Lund won Baedros Mam away in 49."*

```
[Y0048]  POLITY.COURTS_SUPPORT  Voudreirn Wer (a:59) wins Baedros Mam (a:85) away from the ruler of the Sworn Men of Laehiford (f:5)
[Y0049]  POLITY.COURTS_SUPPORT  Teillmol Lund (a:35) wins Baedros Mam (a:85) away from the ruler of the Sworn Men of Laehiford (f:5)
```

The first was 48. Two similar events a year apart, and the earlier has been pulled onto the later's date — the same collapse that produced the Thres Thrild error the checker caught last round. Worth confirming the checker's year validation covers `COURTS_SUPPORT` and not only assassinations.

---

## 7. Transliteration and ordering in the reign scope

The third paragraph of the Heth Fal reign is a list: seven exile returns, five `COURTS_SUPPORT` events, one marriage and one death, one sentence each, in scrambled order — 43, 44, 46–47, 48, 49, 49, 49, 50, 51, 51, then back to 50 for Teillmol Lund's death, which appears after actions he took the year before.

Three separate problems in one paragraph:

- **Ordering.** Events must be narrated in chronological order unless there is a stated reason not to.
- **Incomplete enumeration.** The count of seven exile returns is correct, but only six are named — Herpeim Raern's Y39 return is omitted, which is notable because he returned and was cast out in the same year, having lost the succession to Heth Fal. That is a story, not a stray fact.
- **Inconsistent inclusion.** One Laehiford marriage (Y44) is reported; three others in the window (Y47, and two in Y48) are not. Either marriages are in scope or they are not.

Round 7 predicted this might resolve once the scope was filtered to the correct faction. It did not — the filtering is now correct and the transliteration persists, so it is a separate defect. `POLITY.COURTS_SUPPORT` events in particular are being narrated individually when they are a pattern: five defections from one ruler in four years is a characterisation, not a list.

---

## 8. Battles omitted where they decide the outcome

**Meigate:** *"The power declined through famine and war."* The section never mentions the two battles that destroyed it:

```
[Y0049]  CONFLICT.BATTLE  the Vea Lode Covenant defeats the Sworn Men of Meigate at Meigate (p:6) (149 dead)
[Y0050]  CONFLICT.BATTLE  the Vea Lode Covenant defeats the Sworn Men of Meigate at Meigate (p:6) (124 dead)
```

**Griwick 24–36:** the war declared in 32 is reported without the three defeats at Kebarrow that followed it, which the Kebarrow section does report.

A battle inside a scope's window, involving the scope's own faction, is not optional — particularly a defeat. The pattern across both cases is that **losses are being dropped while gains are kept**, which quietly biases every faction's history toward competence.

---

## 9. Scope gap: the Wurn League has no section

Every power that existed gets a faction scope except the Wurn League, which held Threi Cut, Laehiford and Hadale, fought two wars, lost all three places, and was destroyed in year 20.

Check what determines scope selection. If it requires a `POLITY.SECESSION` founding event, powers present at world start will never qualify — which would also explain why the Kebarrow Compact and Griwick Compact both appear but the Wurn League does not, if the former two are being picked up by some other route.

---

## 10. Minor

*"The Griwick Compact ended its rule in 36"* — the collapse is Y35; year 36 is the peace signed with the remnant. The section heading 24–36 is right, the sentence is off by one.

---

## 11. Still open from previous rounds

**Economy coupling.** 18 `ECONOMY` → non-`ECONOMY` causal edges out of 524, against 75 recorded at v0 run 3. Flagged in `engine-fix-round-1.md`, unchanged through rounds 7 and 8. This needs confirming or dismissing before v1 closes — if the coupling has genuinely eroded it is a dynamics regression, and dynamics regressions are more expensive than prose defects.

---

## Evaluation

Re-render seed 42 and report:

1. **No section-opening summary contradicts its own body**, with the Danpa/Seirn case in the regression set.
2. **Every succession and challenge outcome matches its event's own clause**, including the rare upheld-claim case.
3. **Every collapse, conquest and battle inside a scope's window appears in that scope**, wins and losses alike.
4. **Every statistic stated at the scope it was computed for.**
5. **Every supplied figure stated, not summarised into a vague quantity.**
6. **Chronological ordering within paragraphs**, and enumerations that match their own counts.
7. **Every power that existed gets a faction scope**, including the Wurn League.
8. Economy edge count, with a note on whether it moved.

Hold the benchmarks: the two war scopes, the Heth Fal reign's raid and exile-return figures, Meigate's ruler list and famine total, and the Wuldweald reign are all currently correct.

---

## What I want from you

Item 1 is the round, and it is cheap — comparing a summary against the body it introduces requires no new machinery, and it catches the single most persistent fabrication class in this project.

Item 2 raises a question worth an answer beyond the fix: **which other event types have strongly skewed outcome distributions?** The model can score well on those by guessing the majority case, and every one of them is a latent version of this bug. If you can produce that list from the engine, it tells us where to look next.

Item 3's engine half — naming the party in reason codes — is the third time an ambiguous label has produced a fabrication. It may be worth a general pass over reason codes rather than fixing `(collapse)` alone.
