# Fix Round 7 — reign scoping, list construction, and the checker

## Context

`engine-fix-round-1.md` and `v1-fix-round-6.md` were applied and seed 42 re-rendered. A lot landed.

**Confirmed fixed:**

- **Challenge outcomes are now explicit in the log** — `wins, and takes the seat from X` versus `loses, and Weallhous Dreld...`. The round-6 priority item (Dreld's rule described as ended by a challenge he won) is gone.
- **Membership state fixed.** `Gatros Hearn, who had already left, is declared outlaw by the Sworn Men of Meigate` — actors are no longer cast out of factions they have left.
- **Internal killing count at 22–41 is now six**, correct.
- **Departure partitions are exhaustive *and* correctly categorised.** 2–21: five killed, one cast out, one holding, of seven. 22–41: five killed, five cast out, one holding, of eleven. Both verify.
- **Full faction names in war scopes.** The ambiguous-Compact problem is fixed where it was flagged.

**New figures that verify exactly:** Laehiford's seven raids sent / five beaten off / two carrying plunder; Laehiford's ten-year average tenure; Kebarrow 2–21's nine suffered raids (territory-aware, correctly counting Meigate and Laehiford while they were still Kebarrow's) and one year of hunger.

**Dynamics are healthy:** 694 events, zero dangling references, 5.8% repeat rate, maximum causal depth 15, 91 distinct two-step chain shapes, 168 of 524 edges crossing domains.

The fabrication checker is new and is the right architectural move. It is currently letting through worse defects than it catches, which is item 8 below.

This brief covers engine, render and checker together. The items are labelled so they can be worked in any order, but the reign-scoping fix (item 1) is engine-side and blocks the render-side reign work.

---

## 1. ENGINE — PRIORITY: reign scope is keyed on actor, not on (actor, faction, interval)

The section titled **"The rule of Heth Fal of the Sworn Men of Laehiford"** opens by stating he took the seat of the **Kebarrow Compact** in year 33.

Heth Fal held two different seats at two different times:

```
[Y0033]  POLITY.SUCCESSION  Heth Fal (a:37) takes the seat of the Kebarrow Compact (f:2) (the named heir's claim set aside)
[Y0035]  POLITY.EXILE       Heth Fal (a:37) is cast out of the Kebarrow Compact (f:2) — attempted murder
[Y0037]  POLITY.EXILE_RETURN Heth Fal (a:37) returns from exile and takes service with the Sworn Men of Laehiford (f:5)
[Y0039]  POLITY.SUCCESSION  Heth Fal (a:37) takes the seat of the Sworn Men of Laehiford (f:5) (the named heir's claim set aside)
```

The section renders the **Kebarrow reign (Y33–35)** under the **Laehiford title**. It then pulls in Laehiford's business throughout — the plague at Laehiford, Laehiford's failed raid on Griwick, Laehiford spending fifty silver to buy back goodwill, Drarka Draernthun raised to steward of Laehiford — none of which belongs to the reign being described. It closes by noting he was cast out of the Kebarrow Compact, under a Laehiford heading.

**This is the round-3 structural trap in a new shape.** There the wrong assumption was one seat change per succession event. Here it is one seat per actor.

**Fix:** the reign scope must be keyed on `(actor, faction, start_year, end_year)`. An actor who holds two seats produces two reign scopes, never one. Event selection must filter to the faction of that specific reign, not to every event the actor appears in.

Regression test: Heth Fal produces exactly two reign scopes — Kebarrow Y33–35 and Laehiford Y39–51 — and no event from one appears in the other.

---

## 2. ENGINE — seat-holding is not being inferred from plot targets

The Sworn Men of Laehiford section never identifies **Realsis Leirpu** as ruler, though he held the seat from secession at Y20 until Y32 — twelve of the faction's thirty-two years. He appears only as *"Realsis Leirpu's attempt on Thold Valmaer failed. Realsis was cast out"*, which reads as an ordinary member's downfall and leaves the Y32 succession unexplained.

The evidence that he held the seat:

```
[Y0032]  POLITY.COUP_PLOTTED  Sou Dra (a:22) begins conspiring against Realsis Leirpu (a:61) of the Sworn Men of Laehiford (f:5)  [secret]
```

This is the same `against X of the [faction]` construction that identifies Heth Fal as seat-holder at Y51. **Seat-holding is inferable from plot targets, not only from `POLITY.SUCCESSION` events** — which matters for founding rulers, who take the seat at secession without a succession event.

Check how the founding ruler of a breakaway faction is recorded at all. If secession does not emit a seat-taking event, that is the root cause and it affects every breakaway faction, not just this one.

Note that the `[secret]` flag on the plot must not leak: the fact of Leirpu holding the seat is public, the conspiracy against him is not.

---

## 3. ENGINE — two statistics still wrong

**Laehiford:** *"The leadership changed hands three times."* There were three rulers — Realsis Leirpu, Math Ham, Heth Fal — and therefore **two** changes. Decide which quantity is being reported and make the wording match it. The ten-year average tenure derived from the same set is correct, so the ruler count is right and only the transitions arithmetic is off.

**Kebarrow 42–51:** *"Five members were cast out for attempted murder, and three of those were later declared outlaws for conspiracy."* Four were cast out for attempted murder:

```
[Y0043]  Stonand Ker — attempted murder
[Y0045]  Keithfal Naell — attempted murder
[Y0046]  Throll Kell — attempted murder
[Y0049]  Drouldthas Stour — attempted murder
[Y0051]  Hehum Skul — the losing claim
```

Five cast out in total, four for that reason. The exile reason field is being dropped when the total is computed. The "three declared outlaws" half is correct.

---

## 4. RENDER — elided lists propagate one verb across different fates

Kebarrow 22–41: *"Le Vild was cast out in 33, Heth Fal in 35, Nael War in 37, and Paernrom Sir in 38, before Kondruth Tru was cast out in 39."*

```
[Y0037]  LIFE.DEATH_VIOLENT  Nael War (a:8), ruler of the Kebarrow Compact (f:2), is killed by Neildvarn Tramern (a:32)
[Y0038]  LIFE.DEATH_VIOLENT  Paernrom Sir (a:24), ruler of the Kebarrow Compact (f:2), is killed by Throll Kell (a:43)
```

Both were killed, not cast out. **The same section's own statistic says five killed and five cast out, which is correct** — the prose contradicts the figure two paragraphs later.

The construction is the problem: "X in 33, Y in 35, Z in 37" carries the verb from the first item silently across the rest. Where a list spans items with different fates, each item must state its own. Treat a person's manner of departure as a particular, not a pattern.

---

## 5. RENDER — invented particulars inside a correct count

Kebarrow 22–41: *"The Compact also suffered three raids: one by the Sworn Men of Meigate on Hadale in 23, one by the Sworn Men of Laehiford on Kebarrow in 23, and one by the Griwick Compact on Kebarrow in 32."*

The third does not exist. The actual raid is:

```
[Y0022]  CONFLICT.RAID  the Griwick Compact (f:3)'s raid on Hadale (p:2) is beaten off
```

Wrong place and wrong year. There is no Griwick raid on Kebarrow in year 32 anywhere in the log.

The count of three is correct — the enumeration invented particulars to fill it, assembling them from nouns present elsewhere in the document. This is the exact failure class the checker exists to catch, and it used only in-vocabulary terms to do it, which is why the checker missed it (see item 8).

**Rule to enforce:** where a statistic is accompanied by an enumeration, every item in the enumeration must be drawn from the same event set the statistic was computed over. If the enumeration cannot be populated from that set, give the figure without the list.

---

## 6. RENDER — a date error and a dropped event in 42–51

*"That same year, Paernmel Has ordered the murder of Veillpea Dourn at Vea Lode and Thres Thrild at Griwick."*

```
[Y0046]  CONFLICT.ASSASSINATION  Paernmel Has (a:50) has Veillpea Dourn (a:53) murdered at Vea Lode (p:7)
[Y0047]  CONFLICT.ASSASSINATION  Paernmel Has (a:50) has Thres Thrild (a:57) murdered at Griwick (p:4)
```

Thres Thrild was Y47. Two events a year apart have been collapsed into one sentence sharing a date.

Also dropped: `[Y0050] Hehum Skul marries Lethsel Troldmirn, binding the Kebarrow Compact to the Hadale Commune`. The section reports the Y48 Laehiford marriages and omits this one, so the closing picture of the Compact's alliances is incomplete.

---

## 7. RENDER — the Heth Fal reign is transliteration

Independent of the wrong-faction problem: *"The period in year 35 saw Drarka Draernthun return from exile to serve the Sworn Men of Laehiford. Thosruld Lul's attempt on Heth Fal failed and was traced back. Nael War was raised to steward of the Kebarrow Compact. Heth Fal's own attempt on Thurnean Kourn failed..."*

That is every Y35 event in log order, relevant or not, one sentence each. The round-2 failure mode returning in the reign scope.

The Wuldweald Valdrith section directly below it is proper narrative from a smaller event set, so the capability is intact — this is a scope-specific regression. Once item 1 filters the event set to the correct faction, re-check whether the transliteration persists; it may be a symptom of the scope being handed a large undifferentiated pile rather than a separate defect.

---

## 8. CHECKER — validate statements, not vocabulary

Three flags fired this run and all three are legitimate:

- `ambiguous-short-name` on 2–21 and 22–41 — correct, two powers called "Compact"
- `unsupported-manner` on 22–41 — correct, prose says "coup" where the log says open challenge

But the misses are more severe than the catches. Ambiguous short names are a readability problem. A reign under the wrong faction, a fabricated raid, and killed rulers described as exiled are fabrication proper.

The pattern in what fired — "coup" at 22–41, "seizure" at 42–51 — suggests the checker is **scanning for suspect vocabulary rather than validating statements against events**. Every miss in this round used only in-vocabulary terms and real names.

Extend it to relationship-level assertions, which is what the round-5 succession test already established the shape for:

- Every `X was cast out` / `X was killed` claim checks against that actor's actual departure event.
- Every enumerated raid, battle, or exile checks place and year against a real event.
- Every reign statement checks that the actor held *that* seat during *that* interval.
- Every date attached to a named action checks against the event's year.

**Also fix the message template.** The 42–51 flag reads *"prose claims 'seizure' but the records never say 'coup'"* — two different terms in one template slot. The judgement underneath is sound; the rendering is broken.

---

## 9. DESIGN QUESTION — unverified prose in canon

Failed passages are currently kept in the document behind a `Not verified` block. Good for debugging, but it means **canon now contains text known to be false**, which collides directly with "cached renders are canon".

Pick one:

- **Exclude failed passages.** The scope reports as unavailable and the chronicle is clean by construction. Failures go to a separate diagnostic output.
- **Declare the document a debug artefact**, and produce a separate clean render that is the actual canon.

Right now it is both, and the render cache will inherit the ambiguity. This is worth settling before v1.2, because query answers will be drawn from the same machinery and a query has no place to put a warning block.

---

## 10. Two measurements

**Meigate is absent from this render.** It was the most exact section in the previous run — eight raids sent, six beaten off, two with plunder, one suffered, nine exile returns, 186 dead across three years of hunger, all verifying. It also carried the phantom `One place was lost` statistic, which I therefore cannot confirm is fixed. Re-include it once as a regression benchmark.

**Economy coupling has not moved.** 18 `ECONOMY` → non-`ECONOMY` causal edges out of 524, against 75 recorded at v0 run 3. Flagged in `engine-fix-round-1.md` and still open. Confirm or dismiss before v1 closes — if the coupling has genuinely eroded, that is a dynamics regression and belongs ahead of v1.2.

---

## Evaluation

Re-render seed 42 and report:

1. Heth Fal produces two reign scopes, correctly titled, with no event bleeding between them.
2. Every founding ruler of a breakaway faction is identified as ruler.
3. Every enumerated item verifies place, year and actor against a real event.
4. No list assigns one fate to actors who met different ones.
5. No section's prose contradicts its own statistics.
6. Checker catches all five defects above if reintroduced.
7. Meigate re-included and still exact.
8. Economy edge count, with a note on whether it moved.

Hold the benchmarks: the two war scopes, Laehiford's raid figures, the 2–21 and 22–41 departure partitions, and the Wuldweald reign are all currently correct.

---

## What I want from you

Item 1 is the round. Items 4 and 5 are the same family — a particular reconstructed rather than copied — and may share a root cause with item 6; if they do, say so.

Item 8 is the one I want an opinion on rather than a fix. A checker that validates statements needs a structured representation of what the prose asserts, which is a bigger build than keyword scanning. If you think that is the wrong investment at v1 and the effort belongs in the render prompt instead, argue for it — but say what catches item 5 in that case, because the prompt has now failed to prevent invented particulars in three consecutive rounds.
