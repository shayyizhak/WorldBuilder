# v1 Fix Round 5 — closing out the render layer

## Context

Round 4 was applied and seed 42 re-rendered. Most of it landed, and the prose is now genuinely good. This is the last fix round before v1.2 query.

**What was fixed:**

- **Duration conventions unified.** Every duration now uses the event's own field — "after three years", "after 2 years", "lasted one year before ending in collapse" (matching `make peace after 1 years (collapse)` at Y21). The competing computed-span figures are gone.
- **Tenure clamping fixed.** The reign section no longer says "since 51", and Kebarrow 22–41 correctly reports that Paernmel Has took the seat **in 39** — before the render window opened.
- **Statistics suppressed below the population threshold.** The Wuldweald reign section is now clean narrative with no n=1 stat block. That was the worst prose in the previous version.
- **Cross-render contradictions gone.** Both renders of the Y51 succession agree: Hehum Skul's claim set aside, Hehum cast out as losing claimant. This is the first round in five with no contradiction found — cache determinism appears resolved.
- **Raid counts correct.** Meigate: eight raids, six beaten off — exact match. Kebarrow 42–51: three raids, all beaten off. The world-scoped "fifteen raids" error is gone.
- **Exile counts correct.** Seven exiles for f:2 in 42–51, correctly including Stonand Ker and Throll Kell twice each (both were exiled for the attempt and again for the conspiracy).
- **Battle counts correct.** Three Kebarrow-versus-Wurn battles in years 2–21.

Four items remain. None is a design question.

---

## 1. PRIORITY: the Stonand Ker fabrication survived, rephrased

Round 4 flagged this. It is still present, reworded.

**Round 3 output:** *"Ska was murdered by Stonand Ker, who was in turn set aside by Le Vild."*

**Round 4 output:** *"Ska was killed by Stonand Ker, who was succeeded by Le Vild."*

Both assert that Stonand Ker held the seat. He never did. The actual Y31 events:

```
[Y0031]  CONFLICT.ASSASSINATION      Stonand Ker (a:40) has Wilwound Ska (a:39) murdered at Kebarrow (p:3)
[Y0031]  LIFE.DEATH_VIOLENT          Wilwound Ska (a:39), ruler of the Kebarrow Compact (f:2), is killed by Stonand Ker (a:40)
[Y0031]  POLITY.SUCCESSION_DISPUTED  Le Vild (a:44) contests Kou Peis (a:52)'s claim to the Kebarrow Compact (f:2) (rule: election)
[Y0031]  POLITY.SUCCESSION           Le Vild (a:44) takes the seat of the Kebarrow Compact (f:2) (the named heir's claim set aside)
[Y0031]  POLITY.EXILE                Kou Peis (a:52) is cast out of the Kebarrow Compact (f:2) — the losing claim
```

Le Vild set aside **Kou Peis's** claim. The renderer is joining two adjacent Y31 facts — Ker kills the ruler, Le Vild takes the seat — into a succession relationship that does not exist.

**The actor-pair validation specified in round 4 either was not built or does not cover this case. Establish which, and report it.** Two candidate causes worth checking:

- The check may only validate that both proper nouns appear *somewhere* in the source event set, rather than that the asserted **relationship** between them is supported.
- The check may not model succession relationships at all, only direct-action ones (X kills Y, X marries Y).

Either way, the validation must catch "A was succeeded by B" where no event links A's departure to B's acquisition. This is now the only hard fabrication remaining in the document, and it has survived two rounds of fixes, which suggests the check itself needs testing rather than the prompt.

Suggested regression test: assert that for any rendered succession statement, an event exists where the named predecessor held the seat and the named successor acquired it.

---

## 2. Faction-level killings are still role-blind

Role-awareness was fixed at actor level in round 4. It was not applied at faction level.

Kebarrow Compact 22–41 states: *"The Compact ordered six killings against others, including the murder of Leimmil Theall, Wilwound Ska, and Nael War."*

Of the three named:

- **Leimmil Theall** — killed at Griwick by Gatros Hearn. External. Correct.
- **Wilwound Ska** — *ruler of the Kebarrow Compact*, murdered by Stonand Ker, a Compact member. Internal purge.
- **Nael War** — *ruler of the Kebarrow Compact*, murdered by Neildvarn Tramern. Internal purge.

Two of the three were the Compact killing its own rulers, not killing "others". The count of six is mixing internal purges with external assassinations.

**Fix:** classify every killing by whether perpetrator and target share a faction at the time of the event. Report internal and external counts separately, and never describe an internal purge as directed "against others". The internal/external split is also more interesting historically than the combined total — a faction that murders its own rulers eight times reads very differently from one that assassinates eight rivals.

Apply the same faction-role classification to any other statistic that currently aggregates across both directions.

---

## 3. Minor omission in the Y46 conspiracies

Three conspiracies were uncovered at Y46:

```
[Y0046]  Stonand Ker (a:40)'s conspiracy against Paernmel Has (a:50) is uncovered after 4 years
[Y0046]  Keithfal Naell (a:68)'s conspiracy against Paernmel Has (a:50) is uncovered after 2 years
[Y0046]  Throll Kell (a:43)'s conspiracy against Paernmel Has (a:50) is uncovered after 1 years
```

The prose reports only two: *"both Stonand Ker and Keithfal Naell were cast out again for uncovered conspiracies"*. Throll Kell is dropped, though he was also exiled a second time that year (confirmed in the exile list).

Low severity, but worth understanding why one of three same-year same-type events was omitted — if the renderer is silently dropping events from a group, it will do so elsewhere less visibly.

---

## 4. Two arithmetic wobbles

- *"eleven rulers who each held the seat for an average of 1.9 years"* — twenty years over eleven rulers is 1.8.
- *"Four rulers were killed, three were replaced, and three were cast out"* — sums to ten against eleven rulers.

Neither is serious individually, but both suggest the cause-of-departure distribution is not computed from a partition guaranteed to cover every ruler. Ensure the categories are exhaustive and mutually exclusive, and that the counts always sum to the ruler total. If a ruler's departure does not fit a category — for instance the one still holding the seat at period end — give it an explicit category rather than leaving it uncounted.

Check the average calculation for an off-by-one in the tenure span (inclusive versus exclusive year counting), which is the likely source.

---

## Evaluation

Re-render seed 42 and report:

1. **Zero fabricated relationships**, with the succession-relationship regression test in place and demonstrably firing on the Stonand Ker case if reintroduced.
2. **Internal versus external killing counts** reported separately and verified against the log.
3. **Distribution counts sum to their totals** in every section.
4. **No silently dropped events** within a same-year group.
5. **Aggregation quality retained.** Kebarrow 2–21 and Meigate are the current benchmark and are good — do not regress them chasing the above.

---

## What I want from you

Item 1 is the round; it is the only hard fabrication left and it has survived two fix rounds, which points at the validation rather than the prompt. Items 2 through 4 are small.

Once these are clear, v1 render is done and the next build is v1.2 query.

Push back if any of this is wrong. In particular: if faction membership at time-of-event is not cheaply recoverable — because membership is a folded state rather than something carried on the event — say so. That is a real design question rather than a bug, and it will matter again for v2 and v3, so it is worth resolving properly now rather than approximating.
