# v1 Fix Round 4 — correctness of engine-supplied statistics

## Context

Round 3 was applied and seed 42 re-rendered. Moving arithmetic into the engine was the right call, and the specific trap flagged last round was fixed correctly.

**What was fixed:**

- **The `SUCCESSION` / `CHALLENGE` double-path trap is handled.** The Sworn Men of Meigate now correctly report **five rulers** (previously three). Kebarrow 2–21 reports seven seat-holders with five killed — both correct once open challenges are counted alongside successions. Kebarrow 42–51 correctly reports two.
- **Other statistics now verify:** thirty-three years for Meigate (19–51 inclusive), twenty years for both Kebarrow periods, 186 famine deaths across years 26/42/43, three battles against the Griwick Compact at Kebarrow, and three raids all beaten off in 42–51.
- **Aggregation quality retained.** The Kebarrow 22–41 and Meigate opening paragraphs still read as history.
- **Log-referential language is gone.** No more "recorded events" or "the records show".

**What went wrong:** several engine-supplied statistics are computed incorrectly, and because they are engine-supplied the renderer states them with full confidence. This is the failure mode raised at the end of the round 3 brief. Wrong figures from the engine are worse than wrong figures from the model, because nothing in the pipeline questions them.

This round is about correctness of the statistics themselves.

---

## 1. PRIORITY: incorrect statistics

### 1a. Assassination counts do not distinguish perpetrator from target

The chronicle states: *"Paernmel Has faced seven attempts on his life, three of which resulted in the death of the attacker."*

Actual `CONFLICT.ASSASSINATION` events involving Paernmel Has (`a:50`):

```
Y43  Stonand Ker (a:40)'s attempt on Paernmel Has (a:50) fails and is traced back
Y45  Keithfal Naell (a:68)'s attempt on Paernmel Has (a:50) fails and is traced back
Y46  Throll Kell (a:43)'s attempt on Paernmel Has (a:50) fails and is traced back
Y46  Paernmel Has (a:50) has Veillpea Dourn (a:53) murdered at Vea Lode (p:7)
Y47  Paernmel Has (a:50) has Thres Thrild (a:57) murdered at Griwick (p:4)
Y49  Drouldthas Stour (a:67)'s attempt on Paernmel Has (a:50) fails and is traced back
Y51  Wuldweald Valdrith (a:91) has Paernmel Has (a:50) murdered at Kebarrow (p:3)
```

Attempts **on** him: five (four failed, one fatal). The count of seven includes the two murders **he ordered**. The statistic is matching on actor presence in the event rather than on actor role.

Also: *"three of which resulted in the death of the attacker"* is false. No attacker died in any attempt. Keithfal Naell was executed (`e:904`), but for conspiracy, not for the attempt.

**Fix:** every actor-scoped statistic must be role-aware — perpetrator, target, or bystander. Audit all of them for this class of error, not just assassinations. This likely affects exile counts, murder counts, and any "X faced N of Y" phrasing.

### 1b. Raid counts are wrong and wrongly attributed

The chronicle states, in the Kebarrow Compact 2–21 section: *"fifteen raids occurred in total, six beaten off and nine succeeding."*

Actual raids in years 2–21 across the whole world: **17 total, 7 beaten off, 10 succeeded.**

Two problems. The count is wrong, and it is world-scoped while being presented inside a faction section as though it describes the Kebarrow Compact. Faction-scoped statistics must be filtered to that faction — as raider, as target, or explicitly stated as both.

### 1c. Tenure is being clamped to the render scope

The reign section states: *"Paernmel had held the seat since 51."* He took the seat in **year 39**, by open challenge (`e:729`).

The Kebarrow 42–51 section states: *"Paernmel Has held the seat for the entire period, from 42 to 51."* Also wrong in the same way — 42 is the window start, not the start of his rule.

**Fix:** tenure must be computed from the actual seat-acquisition event, looking back before the render window. A ruler who held power before the period began has a start date before the period, and the prose should reflect that ("had held the seat since 39").

### 1d. Two conflicting duration conventions

The war section states: *"The conflict lasted 4 years, running from year 5 to year 8 inclusive."* The peace event itself reads `make peace after 3 years (exhaustion)`.

Both figures come from the engine and disagree by one.

Worse, they conflict **within the same document**: the Kebarrow 2–21 section says the Wurn League war *"ended in peace after two years due to exhaustion"* (using the event field), while the dedicated war section for the same war says it *"lasted 3 years"* (using the computed span).

**Fix:** pick one convention and use it everywhere. Recommended: use the duration already carried on the `DIPLO.PEACE_SIGNED` event, since that value is rendered directly elsewhere in the prose. Derive any span figure from it rather than computing independently. Audit for other statistics that duplicate a value already present on an event.

---

## 2. A fabricated succession link

From Kebarrow 22–41:

> *"Ska was murdered by Stonand Ker, who was in turn set aside by Le Vild."*

Stonand Ker never held the seat. The actual Y31 events:

```
[Y0031]  CONFLICT.ASSASSINATION      Stonand Ker (a:40) has Wilwound Ska (a:39) murdered at Kebarrow (p:3)
[Y0031]  LIFE.DEATH_VIOLENT          Wilwound Ska (a:39), ruler of the Kebarrow Compact (f:2), is killed by Stonand Ker (a:40)
[Y0031]  POLITY.SUCCESSION_DISPUTED  Le Vild (a:44) contests Kou Peis (a:52)'s claim to the Kebarrow Compact (f:2) (rule: election)
[Y0031]  POLITY.SUCCESSION           Le Vild (a:44) takes the seat of the Kebarrow Compact (f:2) (the named heir's claim set aside)
[Y0031]  POLITY.EXILE                Kou Peis (a:52) is cast out of the Kebarrow Compact (f:2) — the losing claim
```

Le Vild set aside **Kou Peis's** claim, not Stonand Ker's. The renderer joined two adjacent facts into a causal chain that does not exist.

The automated fabrication check should catch this: it asserts a relationship between two named actors that no event supports. If the check only validates that proper nouns *appear* somewhere in the source, extend it to validate that asserted actor-pairs appear together in at least one event.

---

## 3. Suppress statistics below a population threshold

The reign scope produced this:

> *"One person held the seat and was killed; one person held the seat and remained holding it. One attempt on a life killed its target. One person was cast out."*

That is the statistics block read aloud, and it is the worst prose in the document. The reign in question covers a single year, so every statistic has n=1 and conveys nothing.

**Fix:** suppress derived statistics entirely when a scope is too small for them to be meaningful — suggested threshold five years or ten events, tune as needed. Below that, render events narratively with no pattern statements at all. Statistics require a population to be about.

Related: the phrasing pattern "One person held the seat and was killed; one person held the seat and remained holding it" suggests the renderer is enumerating a distribution verbatim. Even above the threshold, distributions should be characterised, not listed.

---

## 4. Cache determinism — outstanding since round 1, fourth occurrence

Still not resolved. Requirements unchanged:

- Cache key must be a deterministic function of the input event set plus render scope, so one input cannot produce two cache entries.
- Explicit fixed seed and low temperature. Do not inherit Qwen 3.6's shipped defaults (temperature 1, presence penalty 1.5).
- Re-rendering must be an explicit action that replaces a cache entry, never an accidental second write.
- Add a test: render the same scope twice, diff, assert identical.

**Treat this as blocking.** Several of the contradictions found across four rounds have been two renders of the same events disagreeing with each other. Until one input reliably produces one output, every other fix is being evaluated against a moving target.

---

## Evaluation

Re-render seed 42 and report:

1. **Statistical accuracy: 100%.** Every engine-supplied figure verified against the log by an independent check — not by the same code that produced it. Report any mismatches.
2. **Fabrication rate, automated**, extended to validate asserted actor-pairs against events, not just proper-noun presence.
3. **Zero duration conflicts** — one convention, verified across all sections of the document.
4. **Zero statistics in scopes below the population threshold.**
5. **Zero contradictions across renders**, verified by rendering the same scope twice and diffing.
6. **Aggregation quality retained.** Kebarrow 22–41 and Meigate remain the benchmark.

---

## What I want from you

Section 1 is the round. Sections 2 through 4 are mechanical, but section 4 is blocking and has been deferred three times.

Push back if any of this is wrong. In particular: if role-aware statistics require event-schema changes — for example if `CONFLICT.ASSASSINATION` does not currently distinguish perpetrator from target in a machine-readable way, and the distinction only exists in the description string — say so. That would be worth fixing at the schema level rather than by parsing prose, and it is the kind of change better made now than after v2 depends on it.
