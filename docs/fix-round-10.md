# Fix Round 10 — the sidecar, and two regressions

## Context

Round 9 was applied and seed 42 re-rendered against the same log (694 events in the view, zero dangling references).

**Round 9 closed almost completely.**

- **The zero-haul raid fix is clean and consistent everywhere it applies.** *"suffered one raid that took nothing"*, *"killing 22 but taking nothing"*, *"all of which failed to take anything"*, *"All three were beaten off, carrying off no plunder"*. Four sections, four correct renderings of the third outcome. This was the engine change and it worked.
- **Hadale's marriages are now three (37, 48, 49)** with the Y37 date correct. The counting convention — first-named party — is applied consistently across Hadale, Laehiford and Kebarrow 22–41, and all three verify.
- **Kebarrow 2–21 now narrates its second war in full**: the Y20 declaration, the battle at Hadale with 106 dead, taking Hadale, and the Wurn League finished. This was the item I cared most about and it is completely fixed.
- **Threi Cut's revolt is back to Y13**, with the "standing fell very low" → "fell to nothing" escalation preserved between Y13 and Y15. That escalation is the story of Griwick's decline and the prose now carries it.
- Tor Nathgoull no longer takes the seat "when his house ended". "Since year 1" is gone. "149 men" is now "149 dead".

**A correction of my own.** I reported last round that the Wurn League had five marriages. It has six — I missed the Y20 Paernrom Sir marriage. The chronicle's figure was right and mine was wrong. That is the third measurement error I have made on this project, after the dropped dangling edges and the `.log` view. All three had the same shape: a filter or a scan that quietly omitted rows. It is an argument for Stage 4 running these counts rather than me.

The remaining list is six items. Item 1 is the round.

---

## 1. PRIORITY: the findings sidecar is empty on a render that excluded a passage

`chronicle-42_findings.json` contains `[]`.

The chronicle contains: *"### The rule of Heth Fal over the Sworn Men of Laehiford, 39–51 — No verified account of this period. The passage written for it did not check out against the records."*

**An exclusion is a finding.** These two outputs contradict each other. Either the sidecar is not wired to the exclusion path, or it records only non-fatal findings — in which case the fatal ones have no machine-readable form at all, and cannot be counted across seeds or tracked between rounds.

Fix: every check that fires writes a record, whether or not it excludes the passage. The record carries `{rule, scope, span, expected, actual, fatal}` as specified in `checker-spec.md`. A passage excluded from canon must appear in the sidecar with `fatal: true`.

**The second, larger reading of the same fact.** Zero findings were reported on a render containing the five defects below. Two of them — items 3 and 4 — are Tier 1 checks: a stated count that disagrees with the passage's own enumeration, requiring no event access whatsoever.

So either Tier 1 is not built, or it is built and not firing. Report which. This is the question `checker-spec.md` asked to be answered first, and the empty sidecar means it still has not been.

Running Tier 1 against the current render — before fixing anything below — remains the useful experiment. If it flags items 3 and 4 with no world model, that settles the value of the tiering. If it flags nothing, the rules need rewriting before any further build.

---

## 2. Contester and heir swapped — regression

Vea Lode Covenant 29–48: *"Kou Peis contested the succession but lost the election to Veillpea Dourn in 45 and was cast out."*

```
[Y0045]  POLITY.SUCCESSION_DISPUTED  Veillpea Dourn (a:53) contests the claim of Kou Peis (a:52), the named heir, to the Vea Lode Covenant (f:7)
[Y0045]  POLITY.SUCCESSION           Veillpea Dourn (a:53) takes the seat of the Vea Lode Covenant (f:7), setting aside the claim of the named heir Kou Peis (a:52)
[Y0045]  POLITY.EXILE                Kou Peis (a:52) is cast out of the Vea Lode Covenant (f:7) — the losing claim
```

Veillpea Dourn contested. Kou Peis was the named heir. The roles are reversed.

**This is round 8 item 2 regressed into a different section.** It was fixed there (Thurnean Kourn over Veillpea Dourn at Y34 renders correctly in this same document) and has reappeared here.

What has changed since round 8 is that the log wording is now fully explicit — `contests the claim of X, the named heir` names the role inline. There is no remaining ambiguity in the event to blame. So this is either a prompt failure or, more likely, the model defaulting to the frequent pattern: the contester usually wins and the heir is usually set aside, so "the one who took the seat must have contested" is a good guess that is wrong here.

This is exactly the skewed-outcome risk raised in round 8. It is a Tier 2 outcome check and it should be in the regression corpus.

---

## 3. Three holders stated, two narrated

The Wurn League, 2–21: *"Its rule passed through three holders, each of whom was cast out."*

The section then narrates Math Ham (7–17) and Trem Lolkoll (17–20). **Reweld Wul is entirely absent.**

```
[Y0005]  LIFE.MARRIAGE           Reweld Wul (a:1) marries Turaer Danpa (a:14), binding the Wurn League (f:1) to the Griwick Compact (f:3)
[Y0007]  CONFLICT.ASSASSINATION  Reweld Wul (a:1)'s attempt on Searn Sisrill (a:7) fails and is traced back
[Y0007]  POLITY.EXILE            Reweld Wul (a:1) is cast out of the Wurn League (f:1) — attempted murder
[Y0008]  POLITY.EXILE_RETURN     Reweld Wul (a:1) returns from exile and takes service with the Kebarrow Compact (f:2)
[Y0008]  POLITY.SUCCESSION       Reweld Wul (a:1) takes the seat of the Kebarrow Compact (f:2) (by election)
```

He held the Wurn seat from the start of the period until Y7, was cast out for a failed assassination, and then became ruler of the Kebarrow Compact — one of the better stories in the world, and the Kebarrow section covers his second career without the first.

Pure Tier 1.1: the count says three, the enumeration gives two.

**And it revises the omission pattern again.** Round 9's finding was that the *second* instance goes missing. Here it is the *first*. The two together suggest the renderer narrates a contiguous run and drops whatever falls outside it, rather than dropping by position. Worth checking whether the dropped item is always at one end of the window.

---

## 4. A world-scoped statistic inside a faction section

Kebarrow Compact 2–21: *"The period saw seven rulers, five of them killed, and three places taken from the Wurn League."*

The Kebarrow Compact took two places from the Wurn League:

```
[Y0007]  the Kebarrow Compact (f:2) takes Laehiford (p:5) from the Wurn League (f:1)
[Y0020]  the Kebarrow Compact (f:2) takes Hadale (p:2) from the Wurn League (f:1)
```

Three is the world total — the Griwick Compact took Threi Cut in Y7. The Wurn League's own section states it correctly: *"lost three places: Threi Cut, Laehiford, and Hadale."*

The same figure is right in one scope and leaked into another. This is the round-4 faction-scoping bug in a new place: a statistic computed about a *relationship between two powers* is being reported inside the section for one of them without filtering to that power's own actions.

Audit any statistic phrased as "X from Y" for the same shape.

---

## 5. One death rendered as two

Kebarrow Compact 2–21: *"Thra Bround was murdered by Nael War in year 18 and killed by Nael War at Meigate."*

```
[Y0018]  CONFLICT.ASSASSINATION  Nael War (a:8) has Thra Bround (a:19) murdered at Meigate (p:6)
[Y0018]  LIFE.DEATH_VIOLENT      Thra Bround (a:19), ruler of the Kebarrow Compact (f:2), is killed by Nael War (a:8) at Meigate (p:6)
```

The assassination/death pair is one killing recorded as two events. Here they are narrated as two consecutive happenings joined by "and", which reads as though he was murdered and then killed again.

Every other killing in the document collapses this pair correctly — Sothkel Sald, Sou Dra, Paernmel Has, Stald Gearngoll all render as single events. So this is not systematic, which makes it worth understanding rather than just patching: something about this instance defeated whatever normally merges them.

---

## 6. Invented motivation, and hedged outcomes

**Motivation.** Vea Lode 49–51: *"Threi Cut rose against the Covenant, exploiting this weakness."*

The causal claim in the same passage is sound — the revolt's cause edge points at Keithfal Naell's death, so *"a direct consequence of the vacuum left by the ruler's death"* is supported by the log. But "exploiting" attributes intent that no event carries. Motive remains a particular, and this is the first invented one in several rounds.

**Hedging.** Two places give a vague outcome where the log gives an exact one:

- Griwick 4–23: *"Raids on Hadale in years 7 and 22, and on Laehiford in year 12, were met with resistance or plunder"* — "or" leaves the reader unable to tell which raid did which.
- Griwick 24–36: *"The Compact suffered ten raids during this period, most beaten off."*

Round 8 established the rule for the plague: supplied figures must be stated, not summarised into vague quantities. It applies equally to outcomes. Now that raids have three distinct outcomes rather than two, a raid's result is always known and should always be given.

---

## 7. Still open, and minor

**Vea Lode raid count.** The section states eleven raids suffered. Counting raids on places it held at the time — Vea Lode throughout, Threi Cut from Y34, Griwick from Y35 — I count twelve, all beaten off. Unchanged from round 9. What is wanted is the convention, not a recount: if a raid on a held place does not count as a raid suffered by the holder, say what does.

**Number style.** The Sworn Men of Laehiford section spells figures as words — *"thirty-one grain"*, *"twenty-three"*, *"thirty-four ore"*, *"sixty-eight people"* — where every other section uses digits. Cosmetic, but it makes the document look like two documents.

**Wording.** Hadale Commune: *"The Commune ended in 51 with Durnrin Drar still holding the seat."* The period ended; the Commune did not. Elsewhere "finished" is used for powers that actually ended, so this reads as a contradiction with the same sentence's second half.

---

## Evaluation

Re-render seed 42 and report:

1. **The findings sidecar contains a record for every check that fired**, including exclusions, and Tier 1's result on the current render is reported before any fix is applied.
2. **No succession describes the heir as the contester**, with the Kou Peis case in the regression corpus.
3. **Every count matched by its enumeration**, or explicitly marked partial.
4. **No statistic reported at a scope wider than its section.**
5. **No single event narrated twice.**
6. **No invented motive; no hedged outcome where the log gives one.**

Hold the benchmarks, which are now substantial: the zero-haul raid phrasing in all four places, Kebarrow 2–21's second war, the Griwick plague figures, the Hadale marriage convention, Meigate's 10.3-year average, the Hadale 8.0-year average and departure partition, and Kebarrow 22–41's eight marriages and twenty courtings-away.

---

## What I want from you

Item 1 is the round, and the part I want is the report rather than the fix: **run Tier 1 against the current render and tell me what it catches.** Items 3 and 4 are the test. If Tier 1 flags them with no event access, the tiering in `checker-spec.md` is validated and the rest of the build follows. If it flags nothing, the rules are wrong and I would rather rewrite them than build Tier 2 on a foundation that does not work.

Item 2 is the more troubling one. It was fixed and came back, in a document where the correct rendering of the same pattern appears two sections earlier. A fix that holds in one scope and fails in another is a prompt-level fix, not a structural one — which is the argument for the checker rather than against it.
