# Fix Round 9 — the short one

## Context

Round 8 was applied and seed 42 re-rendered against the same log (694 events in the view). E7 landed: the `.log` header now carries the note about hidden bookkeeping rows at the point where the measurement is taken. I have not re-measured economy coupling from the view.

**Every round 8 item closed.**

- The Danpa/Seirn summary no longer contradicts its body — it now correctly says Turaer Danpa took the seat *after* the murder of Befu Seirn rather than by it.
- Contester and heir are correct, including both rare upheld-claim cases: Thurnean Kourn over Veillpea Dourn at Y34, and Heillvar Maer over Math Ham at Y20.
- The Wurn League has its own section and its collapse is attributed to the right power.
- Meigate's raid statistics are back at faction scope.
- The Griwick plague is exact: 185, 133 and 156 dead, 296 and 208 fled, three years.
- Voudreirn Wer's Y48 date is corrected. Battles now appear in the Meigate and Griwick sections. Griwick is "finished in 35".
- Every power that existed now has a section.

**The new sections verify well.** The Wurn League checks out completely — six battles, one won and five lost; three seat-holders averaging six years; five raids sent and five suffered; three places lost; four marriages to the Kebarrow Compact in years 3 and 4 and one to the Griwick Compact in year 5. Vea Lode's succession chain is exact through four violent handovers in four years. Laehiford's fourteen exile returns, five courtings-away, seven raids with the full list matching. Kebarrow 22–41's twenty courtings-away.

Fifteen sections, and the defect list is six items and one open question. None is structural.

---

## 1. A raid that took nothing, described as plunder

```
[Y0019]  CONFLICT.RAID  the Kebarrow Compact (f:2) raids Hadale (p:2), carrying off 0 ore and killing 16
[Y0043]  CONFLICT.RAID  the Vea Lode Covenant (f:7) raids Meigate (p:6), carrying off 0 ore and killing 22
```

Kebarrow 2–21: *"The Compact sent six raids, two of which carried off plunder: Laehiford in 4 and Hadale in 19."*
Vea Lode 29–48: *"one against Meigate in 43, which carried off plunder."*
The Wurn League section repeats the Kebarrow claim.

Five raids in the log carry off zero. The classifier looks binary — beaten off, or not beaten off — so a raid that got through and took nothing falls into the plunder bucket by default.

**This is an engine fix, and it is a one-liner with a real payoff.** There are three outcomes, not two:

- beaten off
- got through with a haul
- got through and took nothing

The third currently has no name, and it is the more interesting result: a raid that reached its target, killed sixteen people, and came home empty is a different event from one that came home with grain. Give it a distinct outcome value and the render follows for free.

The counts are all correct — only the word is wrong.

---

## 2. Hadale marriages: the count, the prose, and the log all disagree

Hadale Commune: *"Two marriages bound the commune to other powers: Sor Pean married Thres Thrild in 38, linking it to the Vea Lode Covenant, and Ta Poveil married Kaes Rou in 48, linking it to the Sworn Men of Meigate. A second marriage to the Sworn Men of Meigate occurred in 49."*

Three problems in one passage:

- **The count says two; the passage itself names three** (38, 48, 49).
- **The log has eight marriages binding the Hadale Commune.** Three have the Commune as first-named party (Y37, Y48, Y49), which is probably what is being counted — but that is three, not two.
- **Sor Pean married Thres Thrild in Y37, not Y38.**

```
[Y0037]  Sor Pean (a:54) marries Thres Thrild (a:57), binding the Hadale Commune (f:6) to the Vea Lode Covenant (f:7)
[Y0048]  Ta Poveil (a:76) marries Kaes Rou (a:83), binding the Hadale Commune (f:6) to the Sworn Men of Meigate (f:4)
[Y0049]  Gaernfear Tes (a:89) marries Kus Breim (a:90), binding the Hadale Commune (f:6) to the Sworn Men of Meigate (f:4)
```

Worth settling which direction the marriage statistic counts. Eight bindings involve the Commune; three name it first. Both are defensible figures, but the prose says "bound the commune to other powers", and by that description all eight qualify.

---

## 3. Kebarrow 2–21 never narrates its second war

The section states *"two wars against the Wurn League, fighting three battles which it won"* and then narrates only the Y7 and Y8 battles.

Absent from the section entirely:

```
[Y0020]  DIPLO.WAR_DECLARED  the Kebarrow Compact declares war on the Wurn League over long-standing grievance
[Y0020]  CONFLICT.BATTLE     the Kebarrow Compact and its allies defeat the Wurn League at Hadale (p:2) (106 dead)
[Y0020]  CONFLICT.CONQUEST   the Kebarrow Compact takes Hadale (p:2) from the Wurn League
[Y0020]  POLITY.COLLAPSE     the Wurn League is finished — landless, its last 21 followers scatter
[Y0021]  DIPLO.PEACE_SIGNED  the Kebarrow Compact and the Wurn League make peace after 1 years (collapse)
```

Hadale appears only in the aggregate line *"Two places were taken, Laehiford and Hadale."*

**The count and the enumeration disagree again** — three battles stated, two narrated. Same shape as round 8's Meigate raids, and round 7's fabricated third raid.

This also revises the bias I described in round 8. There I read the pattern as losses being dropped while gains were kept. This is a win being dropped — the Compact destroying one of the two founding powers of the world. The actual pattern is narrower: **where a scope contains two of something, the second one goes.** Two wars, one narrated. That is worth checking directly, because it suggests a length or salience cutoff rather than a directional bias.

The Wurn League's own section covers its destruction, so the fact is not lost from the book. But a reader of the Kebarrow chapter would not know it happened.

---

## 4. A revolt date regressed

Griwick 4–23: *"The Compact's standing fell to nothing, leading to uprisings at Vea Lode in year 15 and Threi Cut in year 15."*

```
[Y0013]  POLITY.REVOLT  Threi Cut (p:8) rises against the Griwick Compact, whose standing had fallen very low
[Y0015]  POLITY.REVOLT  Vea Lode (p:7) rises against the Griwick Compact, whose standing had fallen to nothing
```

Threi Cut was Y13. **The previous render had this right** — *"leading Threi Cut to rise against it in year 13, followed by Vea Lode rising against it in year 15"* — so this is a regression.

The mechanism is the familiar one: two similar events collapsing onto a single date, exactly as with Thres Thrild (round 8) and Voudreirn Wer (round 8). Confirm the checker's year validation covers `POLITY.REVOLT` and not only assassinations and successions.

Secondary: the section flattens both revolts to *"standing fell to nothing"*, but Y13 says "very low" and Y15 says "to nothing". The escalation between them is the actual story of Griwick's decline, and the log states it explicitly. Standing descriptors are particulars.

---

## 5. Two facts fused into one clause

Sworn Men of Meigate: *"Tor Nathgoull, who took the seat in 48 when his house ended."*

He took the seat in 48. The house ended in 50, when the Vea Lode Covenant took Meigate. Two events two years apart have been welded into one relative clause.

The rest of the sentence's list is correct — Renbeir Surn 19–25, Kreathbeas Waeth 25–48 — so this is a phrasing collapse rather than a data error.

---

## 6. A date the log does not carry

Griwick 4–23: *"Pouldrir Ho, who had held the seat since year 1, was killed."*

The log's earliest event is Y2. Either the engine carries a founding year that the event log does not expose, or the year is invented. Worth establishing which, because if the engine does hold pre-Y2 state, that is a source of particulars the checker cannot validate against the log — and that gap matters more than this one sentence.

---

## 7. An invented particular

Vea Lode 49–51: *"the Covenant defeated the Sworn Men of Meigate at Meigate, killing 149 men."*

The log says 149 dead. Gender is a particular and is not supplied.

---

## 8. Open question, not a defect

Vea Lode 29–48 states it suffered eleven raids. Counting raids on places it held at the time — Vea Lode throughout, Threi Cut from Y34, Griwick from Y35 — I count twelve, all beaten off.

The difference is plausibly an ownership-boundary convention I am guessing at rather than a miscount. Confirm which figure is right and, if mine is wrong, say what the convention is — it will come up again in every territory-scoped statistic.

---

## Not checked

Famine and harvest totals are the yearly accounts and are largely hidden from the `.log` view. I have not attempted to verify them. The Vea Lode figures in particular — 42 dead and 90 driven out across five years — exceed what is visible, which is what I would expect if they are correct.

---

## Evaluation

Re-render seed 42 and report:

1. No raid with a zero haul described as carrying off plunder.
2. Every count matching the enumeration that accompanies it.
3. Every war, battle, conquest and collapse inside a scope's window narrated in that scope.
4. Every date matching its event, with revolt years included in the year check.
5. No two events fused into a single clause.
6. Whether the engine carries state earlier than the first logged event.
7. The Vea Lode raid-count convention, stated.

Hold the benchmarks: the Wurn League section, Vea Lode's succession chain, Laehiford's raid and exile figures, Kebarrow 22–41's courtings-away, and the Griwick plague are all currently correct and are the standard to protect.

---

## What I want from you

Item 1 is the only engine change and it is small. Items 3 and 4 are the two that keep recurring — a count without its enumeration, and two similar events collapsing onto one date — and both are mechanically detectable without understanding the prose. See `checker-spec.md`, which is the more important document of the two.
