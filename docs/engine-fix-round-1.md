# Engine Fix Round 1 — statistics, membership, coupling

## Context

Round 5 was applied and seed 42 re-rendered. The render layer is now in good shape: **zero fabricated proper nouns across the whole document, zero dangling causal references, zero secret leaks.** The Stonand Ker fabrication that survived three rounds is gone. All three Y46 conspiracies are named. Departure partitions sum correctly.

Several things now verify exactly, which is worth recording as a baseline:

- **Meigate faction stats are perfect.** Eight raids sent, six beaten off, two carrying plunder, one raid suffered, nine exile returns, 186 dead across three years of hunger (64 + 75 + 47). All exact.
- **Faction-lifetime scoping works.** The Meigate stats correctly exclude the Y6 Wurn raid and Y13 Griwick raid on Meigate-the-place, because the faction did not exist until Y19.
- **Kebarrow 2–21 war stats are exact.** Two wars against the Wurn League (w:3 Y7–9, w:18 Y20–21), three battles, three years at war.
- **Tenure averages are right.** 2.7 is 19/7, 1.7 is 19/11 — sum-of-tenures, consistently applied across scopes. The round-4 arithmetic complaint is resolved.
- **Internal/external killing classification works at 42–51.** Two killings against rivals (Veillpea Dourn, Thres Thrild — both Vea Lode Covenant rulers), one murder from within (Paernmel Has by Wuldweald Valdrith). Correct.

Because statistics moved into the engine at round 3, most of what remains is engine-side. The renderer is faithfully reporting figures it was handed. The render-side items are in a separate brief.

---

## 1. Internal killing count undercounts at 22–41

Kebarrow Compact 22–41 states: *"Four people within the Compact were murdered from within, including Weallhous Dreld, Wilwound Ska, Nael War, and Paernrom Sir."*

The actual count is six. Missing:

```
[Y0023]  LIFE.DEATH_VIOLENT  Saern Meastouth (a:28), commoner of the Kebarrow Compact (f:2), is killed by Weallhous Dreld (a:25) at Kebarrow (p:3)
[Y0029]  LIFE.DEATH_VIOLENT  Theald Va (a:30), ruler of the Kebarrow Compact (f:2), is killed by Wilwound Ska (a:39) at Hadale (p:2)
```

Both perpetrators were Compact members at the time — Dreld held the seat, and Ska took the seat the same year he killed Va.

**Theald Va is named as a violently-ended ruler in the same paragraph.** The stat and the narrative contradict each other inside one block of prose, which suggests the stat is filtered differently from the narrative event set.

Two candidates worth checking: whether the count is restricted to rulers (which would give five, not four, so it does not explain the number on its own), and whether it requires an explicit `CONFLICT.ASSASSINATION` event — Dreld's death at Y25 has only a `LIFE.DEATH_VIOLENT` with no paired assassination event, so a filter keyed on assassinations would produce a different wrong number again.

---

## 2. External killing counts are missing from some scopes

42–51 reports both internal and external. 22–41 reports only internal — despite the prose opening with *"Internal purges and external killings characterized the latter half of the period"* and then never giving an external figure.

The two Kebarrow-ordered external killings in that window:

```
[Y0025]  Gatros Hearn (a:27) has Leimmil Theall (a:38) murdered at Griwick (p:4)   — ruler of the Griwick Compact
[Y0034]  Heth Fal (a:37) has Thold Valmaer (a:15) murdered at Griwick (p:4)        — ruler of the Griwick Compact
```

Apply the internal/external classification uniformly to every faction scope, and emit both figures whether or not either is zero. A scope that reports one and silently omits the other invites exactly the reading error the classification was introduced to fix.

---

## 3. Window clamping is one-sided

Kebarrow 2–21 states: *"It took Laehiford and Hadale from the Wurn League but lost both places."*

```
[Y0007]  CONFLICT.CONQUEST  the Kebarrow Compact takes Laehiford (p:5) from the Wurn League
[Y0020]  CONFLICT.CONQUEST  the Kebarrow Compact takes Hadale (p:2) from the Wurn League
[Y0020]  POLITY.SECESSION   Laehiford (p:5) breaks from the Kebarrow Compact as the Sworn Men of Laehiford
[Y0027]  POLITY.SECESSION   Hadale (p:2) breaks from the Kebarrow Compact as the Hadale Commune
```

Laehiford was lost inside the window. **Hadale was lost at Y27, seven years outside it.** Acquisitions are clamped to the render window; losses are not.

This is the same class as the round-4 tenure bug ("since 51" when the actual date was Y39). Audit every stat that spans a window boundary — acquisitions, losses, tenures, war durations, famine spans — and confirm each clamps on both ends.

---

## 4. A statistic with no source event

The Meigate section closes with: *"One place was lost."*

Meigate holds p:6 continuously from secession at Y19 through Y51. There is no `CONFLICT.CONQUEST` against f:4, no `POLITY.SECESSION` from f:4, and no `POLITY.REVOLT` against f:4 anywhere in the log. The faction is intact at the end of the period with Beas Krouthea holding the seat.

This figure has no source at all. Find what produced it — a default, a template that always emits, or a stat computed against the wrong faction id. A stat that fires with no supporting event is worse than a wrong count, because nothing in the prose signals that it is unmoored.

---

## 5. Departure category mislabelled

Meigate: *"five people held the seat: two died, one was replaced, one was cast out, and one was still holding."*

The partition sums to five correctly — the round-4 exhaustiveness fix held. But the assignment is wrong:

```
[Y0025]  Renbeir Surn — LIFE.DEATH_NATURAL          died
[Y0048]  Kreathbeas Waeth — LIFE.DEATH_NATURAL      died
[Y0050]  Herpeim Raern — POLITY.EXILE (losing side of a coup)   cast out
[Y0051]  Treild Haen — POLITY.EXILE (attempted murder)          cast out
         Beas Krouthea — took the seat Y51                      still holding
```

Two were cast out, not one. Nobody was "replaced". Check how a ruler who loses a challenge and is then exiled gets categorised — it looks like the challenge loss and the exile are being counted as two different departure types for the same person.

---

## 6. Membership state: actors cast out of factions they have left

```
[Y0046]  POLITY.EXILE_RETURN  Stonand Ker (a:40) returns from exile and takes service with the Sworn Men of Laehiford (f:5)
[Y0046]  POLITY.COUP_RESOLVED Stonand Ker (a:40)'s conspiracy against Paernmel Has (a:50) is uncovered after 4 years
[Y0046]  POLITY.EXILE         Stonand Ker (a:40) is cast out of the Kebarrow Compact (f:2) — conspiracy against the seat
```

He joins Laehiford and is then cast out of the Kebarrow Compact in the same year. Throll Kell has the same shape twice in Y46 — exiled for the attempt, then exiled again for the conspiracy, having already left.

Arguably a faction can outlaw someone who has fled, and if that is the intent then the event wording should say so ("declared outlaw by" rather than "cast out of"). But if it is a state bug, it inflates every exile count.

**This is the faction-membership-at-event-time question surfacing in the simulation itself rather than only in the render layer.** Settle it now:

- Is membership carried on the event, or is it folded state reconstructed by replay?
- If folded, how expensive is a point-in-time membership query?

The internal/external killing classification in item 1 depends on the answer. So does perpetrator/target scoping generally, and it recurs in Stage 6 (distance and alliance), Stage 7 (adjudicating authored facts about who belonged to what), and Stage 11 (per-agent knowledge is per-agent-in-a-social-position). This is worth resolving properly rather than approximating.

---

## 7. Economy coupling may have regressed

Current seed 42: **18 `ECONOMY` → non-`ECONOMY` causal edges out of 521 total.** The figure recorded at v0 run 3 was 75.

Caveat, stated plainly: the parser used for that earlier figure was silently dropping comma-separated multi-cause edges (`<= e:84,e:36`). That bug **undercounts**, so it cannot explain a drop from 75 to 18 — the gap runs the wrong way. But the log content has also changed across several rounds, so the two numbers are not measuring the same run.

Re-measure with the current engine and confirm. Economy participation was a v0 acceptance criterion and it should not be allowed to erode quietly while attention is on the render layer. If it has genuinely dropped, that is a dynamics regression and belongs ahead of v1.2.

Overall dynamics are otherwise healthy on this run: 5.9% verbatim repeat rate, maximum causal depth 14, 92 distinct two-step chain shapes, 167 of 521 edges crossing domains.

---

## 8. Ambiguous labels remain a fabrication vector

`POLITY.CHALLENGE` carries its outcome inside the description text:

```
[Y0023]  Saern Meastouth challenges Weallhous Dreld openly for the Kebarrow Compact and is beaten
[Y0025]  Gatros Hearn challenges Weallhous Dreld openly for the Kebarrow Compact and takes the seat
```

Two opposite outcomes sharing a sentence stem, distinguished only by the trailing clause. The renderer inverted one of them this round (see the render brief).

Consider exposing outcome as a structured field on the event rather than as a prose clause. This has been a recurring theme since round 1 — "claim overturned" not saying whose claim, durations in two conventions — and it is cheaper to fix in the event schema than to keep guarding against in the prompt.

---

## Evaluation

Re-run seed 42 and report:

1. Internal and external killing counts for every faction scope, verified against the log.
2. Every window-spanning statistic clamped on both ends.
3. No statistic emitted without a source event.
4. Departure categories exhaustive, mutually exclusive, and correctly assigned.
5. Membership decision documented, and exile events consistent with it.
6. `ECONOMY` → non-`ECONOMY` edge count, with a note on whether it moved.

Do not regress the Meigate stat block or the Kebarrow 2–21 war figures — both are currently exact and are the benchmark.

---

## What I want from you

Items 1 through 5 are bugs. Item 6 is the design question and is the one I actually want an opinion on — push back if per-event membership is the wrong shape, and say what you would carry instead. Item 7 is a measurement to confirm or dismiss. Item 8 is a schema change worth doing only if it is cheap now; if it is not, say so and it waits.
