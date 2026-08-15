# v1 Fix Round 3 — move arithmetic into the engine

## Context

Fix round 2 was applied and seed 42 re-rendered. **The particulars/patterns distinction worked.** Aggregation is restored without the fabricated motivations of round 1.

The best example, from the Kebarrow Compact 42–51 section:

> *"The Kebarrow Compact endured eight years of violent instability under a single ruler, Paernmel Has, whose tenure was defined by repeated attempts on his life and his own retaliatory killings. The period began with an assassination attempt in year 43 and ended with Has's murder in year 51."*

That characterises a period, invents nothing, and beats the raw log. The Meigate and Kebarrow 22–41 openings do the same. This is round 1's readability with round 2's discipline, which was the target.

**Also fixed:**

- **Truncation:** gone. All sections terminate properly.
- **Ambiguous succession labels:** the engine now emits `(the named heir's claim set aside)` (13 instances) and `(the named heir's claim upheld)` (2), replacing the ambiguous `(claim overturned)`.
- **Fabricated particulars:** largely under control. One hard invention found this round versus several last round.

The new problem is that permitting pattern statements led to the model computing them. It is unreliable at that. This round moves the arithmetic into the engine.

---

## 1. PRIORITY: compute period statistics in the engine

Nearly every derived number in the chronicle is wrong.

| Chronicle claim | Actual |
|---|---|
| "Kebarrow 22–41: **seventeen years**" | 20 |
| "seven individuals holding the seat, **averaging one year each**" | 7 correct; average ~3 |
| "Kebarrow 2–21: four individuals, **averaging three years each**" | 4 correct; average 5 |
| Meigate: "**three rulers**, each serving an average of **eight years**" | 5 rulers; ~10.7 years |
| Meigate: "**nine raids** … **all** of which were beaten off" | 8 raids, 6 beaten off |
| "Paernmel, who had held the seat **since year 15**" | Y39, via open challenge (`e:729`) |

Some are correct — the 186 famine deaths (64+75+47), the 32-year Meigate span, twelve exiles for f:2 in 22–41, and "peace due to collapse" in Y21. The hit rate is not good enough for figures stated as fact.

There is also an internal contradiction within one paragraph of the first war section: *"Three battles occurred across three years of fighting"* followed by *"The quarrel … produced four clashes."*

### The fix

Counting across a long event list is precisely what a language model is unreliable at, and it is trivial in C#. **Compute period statistics in the engine and pass them to the renderer as structured facts to be stated, not derived.**

Suggested derived inputs per render scope:

- Period span in years (and state the inclusive/exclusive convention explicitly)
- Ruler count, mean and median tenure
- Cause-of-departure distribution (natural death, assassination, exile, challenge, execution)
- Battle count, war count, total war years
- Raids launched, raids beaten off, raids successful
- Territory gained and lost
- Famine years and total deaths
- Assassination attempts, successes, failures
- Exiles, returns, executions

Then instruct the renderer that **numerical claims may only restate supplied figures**. It must not count, sum, average, or compute intervals itself.

### Structural trap when implementing this

The seat changes hands **two ways**: `POLITY.SUCCESSION`, and `POLITY.CHALLENGE ... and takes the seat`.

The Sworn Men of Meigate have 3 `SUCCESSION` events but 5 actual rulers — Renbeir Surn held the seat from founding, and Treild Haen took it by challenge in Y50. Neither is a `SUCCESSION` event. The model counted `SUCCESSION` events and reported three rulers.

If the engine-side calculation makes the same mistake it will reproduce the error more confidently and harder to spot. **Any ruler-count or tenure calculation must consider both event types**, plus founding and any other seat-acquisition path. Audit for other multi-path state changes with the same problem before writing the aggregation code.

---

## 2. Pattern statements must describe the world, not the record

The war sections regressed into telemetry:

> *"The conflict lasted three years and involved six recorded events. Three battles occurred across three years of fighting. The Griwick Compact gained one place and lost none. The quarrel between the Wurn League and the Griwick Compact produced four clashes."*

That is a stat block in sentence form. *"Involved six recorded events"* is a fact about the log file, not about the world — no chronicler would write it.

Add to the render prompt: **pattern statements describe events in the world, never the record of them.** Forbid references to the log, event counts as such, records, entries, or data. "Six recorded events" and "the records show six events in this short span" (which also appears, in the Wuldweald section) are both violations.

The distinction to convey: *"the Compact was defeated in three successive battles"* describes the world. *"Three battle events occurred"* describes the log.

---

## 3. A corrupted actor name

From Kebarrow 22–41:

> *"Tor Nathwound Ska murdered Theald Va in 29"*

Two adjacent actors in the source — `Tor Nathgoull (a:33)` and `Wilwound Ska (a:39)` — were fused into one person who does not exist. The actual events at Y29 are:

```
[Y0029] w:33  CONFLICT.ASSASSINATION  Tor Nathgoull (a:33)'s attempt on Theald Va (a:30) fails and is traced back  <= e:427  e:493
[Y0029] w:31  CONFLICT.ASSASSINATION  Wilwound Ska (a:39) has Theald Va (a:30) murdered at Hadale (p:2)  e:495
```

This is the only hard fabrication of a particular found this round, and the automated fabrication check would catch it — every proper noun in the prose must appear in the source event set. **Run that check as an automated pass rather than reading the output.** It should have been the thing that found this, not manual review.

---

## 4. Cache determinism — still outstanding, third round

The renders no longer contradict on outcome, but they still disagree on recorded mechanism.

Section "The rule of Wuldweald Valdrith":

> *"Wuldweald's challenge resulted in the named heir's claim being set aside"* — matches `e:1031`

Section "The Kebarrow Compact, 42–51":

> *"Wuldweald's claim was upheld, Hehum was cast out"* — the opposite label

Both renders describe `e:1030`–`e:1032`. The engine emits `(the named heir's claim set aside)`.

This has now persisted across three rounds. Resolve it:

- Cache key must be a deterministic function of the input event set plus render scope, so one input cannot produce two cache entries.
- Set an explicit fixed seed and low temperature for rendering. Do not inherit Qwen 3.6's shipped defaults (temperature 1, presence penalty 1.5).
- Re-rendering must be an explicit action that replaces a cache entry, never an accidental second write.
- Add a test: render the same scope twice, diff, assert identical.

---

## Evaluation

Re-render seed 42 and report:

1. **Numerical accuracy: 100%.** Every figure in the prose must match an engine-supplied statistic. Report any that do not.
2. **Fabrication rate, automated.** Every proper noun, year, and number in the prose must appear in the source event set or the supplied statistics. Report count and list failures.
3. **Zero log-referential language** — no "recorded events", "the records show", "entries", or equivalent.
4. **Zero contradictions across renders**, verified by rendering the same scope twice and diffing.
5. **Aggregation quality retained.** The Kebarrow 42–51 and Meigate opening paragraphs are the current benchmark; the round 3 output must be at least as good.

---

## What I want from you

Item 1 is the round. Items 2 through 4 are mechanical.

Push back if any of this is wrong. In particular: if computing the full statistics set per render scope turns out to be awkward against the current event-log structure — for instance if attributing raids or battles to a faction-period requires state reconstruction you do not currently have — say so before implementing. A smaller set of reliably computable statistics is better than a complete set that is subtly wrong, since wrong engine-supplied figures are worse than model-computed ones: they will be stated with full confidence and cached as canon.
