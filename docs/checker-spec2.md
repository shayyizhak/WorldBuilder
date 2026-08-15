# The fabrication checker — specification

## Why this document exists

The checker was introduced at round 7 and is the right architectural idea: prose that fails validation is kept out of canon rather than corrected by hand. The mechanism works — round 8 excluded a passage, the chronicle said plainly that no verified account existed, and the failure was preserved for inspection.

**But the round 9 numbers are the problem.**

| | Round 8 | Round 9 |
|---|---|---|
| Sections rendered | 11 | 15 |
| Checker flags raised | 3 | **0** |
| Defects found by hand | 8 | 6 |

Round 9 passed clean and contained six defects. That is a 100% miss rate on a render the checker certified.

The checker did not get worse. The defects moved. Everything it caught — `unsupported-manner` on "coup" and "seizure", `ambiguous-short-name` on two powers called "Compact" — is a **vocabulary** hit: a suspicious word appearing in the prose. Every defect in round 9 was expressible in entirely ordinary language:

- a raid described as carrying off plunder when the haul was zero
- a marriage count that contradicts the marriages named in its own sentence
- a war that a section's own statistic says it fought, and never narrates
- a revolt dated 15 when the log says 13

No suspicious word appears in any of them. A keyword scanner cannot see this class, and this class is now the whole population.

This matters more for v1.2 than for the chronicle. A chronicle can exclude a bad passage and print a note in its place. **A query answer has nowhere to put a warning block** — "why did X lose the seat" either gets answered or it does not, and the person asking has no log open beside them.

---

## The central design claim

Statement-level validation sounds like it needs the checker to understand prose. Most of it does not.

**Three of round 9's six defects, and three of round 8's eight, are detectable by arithmetic alone** — comparing a number the prose states against the number of things the prose then lists. No world model, no event lookup, no semantics. If a section says "two marriages" and then names three, that is a contradiction visible from the text alone.

So the checker splits into three tiers of increasing cost, and the cheapest tier catches the most.

---

## Tier 1 — internal consistency

**Needs: the rendered text only. No event access.**

This tier asks a single question: does the passage contradict itself?

### 1.1 Count versus enumeration

Where the prose states a cardinal number and then enumerates instances of that thing, the enumeration must match the count, or must be explicitly marked as partial.

Failures this catches:

- *"Two marriages bound the commune… [names three]"* (round 9)
- *"three battles which it won. In 7… In 8…"* — two narrated (round 9)
- *"The Compact also suffered three raids: [one of the three does not exist]"* (round 7)
- *"Four people were murdered from within, including [exactly four named]"* — "including" used before an exhaustive list (round 7)

Rule: an enumeration is either **exhaustive** (count must equal items) or **explicitly partial** ("including", "among them", "such as"), in which case items must be fewer than the count. Items equal to the count with a partiality marker is itself a failure, and so is items exceeding the count under any framing.

### 1.2 Partition sums

Where a set is divided into categories, the categories must sum to the total.

Already fixed by hand at round 4 and round 7; this tier makes the fix permanent rather than a thing to re-check each round.

### 1.3 Internal date agreement

Where the same event is dated twice within a document, the dates must agree.

### 1.4 Summary versus body

Every claim in a section-opening summary must be entailed by a claim in the body, or be a supplied statistic.

This was round 8's priority item and it caught the Danpa/Seirn fabrication — where the body paragraph got the killing right and the opening summary invented a relationship. It is worth stating as a Tier 1 rule because it needs no event access: the body is the reference.

**Tier 1 is arithmetic and text comparison. It should be built first and it should be fast enough to run on every render.**

---

## Tier 2 — statement validation against events

**Needs: structured extraction of what the prose asserts, then lookup.**

This is the real build. The checker must convert prose claims into a structured form comparable against events.

### 2.1 What to extract

The recurring failure classes tell you exactly which assertion types matter. Every fabrication across nine rounds falls into one of these:

| Assertion | Shape | Failures it would have caught |
|---|---|---|
| **Action** | actor, verb, target, year, place | Stonand Ker killing (r3–5), Danpa/Seirn (r8), fabricated Griwick raid (r7) |
| **Succession** | predecessor, successor, faction, year | Stonand Ker succession (r3–5), Stonand-Ker/Le Vild (r5) |
| **Outcome** | event, result | challenge inversion (r6), contester/heir swap (r8) |
| **Departure** | actor, manner, year | rulers killed described as cast out (r7) |
| **Tenure** | actor, faction, start, end | reign under wrong faction (r7), "since year 1" (r9) |
| **Quantity** | figure, scope, unit | faction stats as reign stats (r8), zero-haul plunder (r9) |
| **Date** | any named action, year | Thres Thrild (r7), Voudreirn Wer (r8), Threi Cut revolt (r9) |

### 2.2 The validation rules

- **Action:** an event must exist with that actor, that verb class, that target. Year and place must match.
- **Succession:** an event must show the named predecessor holding the seat, and an event must show the named successor acquiring it. Adjacency in the log is not evidence of a relationship — this is the rule that would have killed the project's most persistent bug three rounds earlier.
- **Outcome:** must match the event's own outcome field. Never inferred from the majority case.
- **Departure:** must match the actor's actual departure event — killed, cast out, died, still holding.
- **Tenure:** the actor must hold *that* seat across *that* interval. Two seats produce two reigns.
- **Quantity:** must match a supplied statistic **at the same scope**. A faction figure restated inside a reign passage fails even when the number is right.
- **Date:** every year attached to a named action must match its event's year. Apply to **all** event types — the three date errors so far were in assassination, courts-support, and revolt, which is to say the check must not be enumerated per type.

### 2.3 Extraction approach

Two-call pattern, as with adjudication: let the model read the passage and emit assertions freely, then extract to schema under constrained decoding. The extraction model is checking prose it did not write, which is a genuinely different task from generating it — and a smaller model may be sufficient, since the job is parsing rather than composition.

**Important property, with a correction:** extraction failures do not produce *false* failures — an unparsed sentence goes unchecked rather than wrongly flagged. That remains true and it is why the checker's error mode should be silence rather than noise; a checker that cries wolf gets ignored and the whole mechanism is worthless.

**But silence is not safe, and round 11 proved it.** Tier 1 was inert for five reasons and every one of them was silent:

1. `"included"` was absent from the partiality-marker list; only `"including"` was present. Round 7's case said "including" and fired. Round 11's said "included" and did not.
2. `people`, `exiles` and `returns` were absent from the countables lexicon, so no rule ever examined a roster of people.
3. Rule 1.4 did not exist.
4. Rule 1.3 assumed the name preceded the date, so *"the murder of X in 47"* was invisible.
5. Normalisation did not strip possessives, so *"Realsis Leirpu's"* yielded the subject `leirpu's`, matching nobody. This is why 1.4 still reported nothing after it was written, and it took a throwaway probe to find.

Four of the five are the same category: **the rule was correct and the input never reached it.** A lexicon gap, a normalisation gap, and a pattern gap all produce exactly the output of a clean passage. Nothing distinguishes "checked and found nothing" from "never checked".

### 2.4 Coverage reporting — the fix for silent inertness

Every run emits, per scope, **how many assertions each rule extracted and checked**, alongside the findings.

```json
{
  "scope": "The Sworn Men of Laehiford, 20-51",
  "coverage": {
    "count-enumeration": { "extracted": 6, "checked": 6, "fired": 1 },
    "partition-sum":     { "extracted": 2, "checked": 2, "fired": 0 },
    "date-agreement":    { "extracted": 19, "checked": 19, "fired": 0 },
    "summary-body":      { "extracted": 0, "checked": 0, "fired": 0 }
  }
}
```

**A rule that extracts zero assertions from a scope containing prose is itself a finding.** Emit it as `rule-inert` — non-fatal, non-blocking, but present in the sidecar. In the example above, `summary-body` extracting nothing from a five-paragraph section is the signature of cause 5, and it is visible without reading a word of the chronicle.

This is cheap to implement and it converts the entire class of silent-inertness bugs into loud ones. It also gives the golden-diff layer something far more stable to compare than prose: extraction counts should be roughly constant across renders of the same log, so a rule that checked six counts last round and two this round has a lexicon problem.

---

## Tier 3 — coverage

**Needs: the scope's event set, and a definition of significance.**

Tiers 1 and 2 catch things the prose says wrongly. Tier 3 catches things the prose does not say.

Round 8 and round 9 both had omission defects — Meigate's two lost battles, Griwick's three defeats, the Wurn League's destruction, Kebarrow's entire second war. None of these is a false statement. All of them make a section misleading.

Rule: certain event classes are **mandatory within a scope's window**. Proposed list:

- `POLITY.COLLAPSE` — a power ending
- `CONFLICT.CONQUEST` — a place changing hands
- `POLITY.SECESSION` — a power being born
- `DIPLO.WAR_DECLARED` and its `DIPLO.PEACE_SIGNED`
- `CONFLICT.BATTLE` — every one, win or loss
- `LIFE.DEATH_VIOLENT` where the target held a seat

If one of these falls inside a scope's window and involves the scope's subject, and the passage does not mention it, the passage fails.

**Round 9's evidence suggests why this happens:** the pattern is not that losses get dropped, it is that *the second instance* gets dropped. Two wars, one narrated. Worth instrumenting — if the renderer is applying a length or salience cutoff, coverage failures will cluster at the end of long scopes, and that is a prompt fix rather than a checker problem.

---

## Failure reporting

Two things to fix in the current output.

**The message template is malformed.** Round 7 produced *"prose claims 'seizure' but the records never say 'coup'"* — two different terms in one slot. The judgement was sound; the rendering was not. Every failure message should carry: the rule that fired, the offending span of prose, and the event or figure it was checked against.

**Failure output belongs in a machine-readable form as well as prose.** The unverified file is good for a human reading it. A JSON sidecar is what lets findings be counted across seeds, which is what makes this a harness metric rather than a per-round conversation.

The shipped format — `{rule, scope, span, detail, blocking, fatal}` — is right and should stay. Two additions:

- **Exclusions must appear.** A passage kept out of canon is a finding with `fatal: true`. Round 10 excluded a passage while the sidecar reported `[]`; the two outputs must never disagree again.
- **A `coverage` block per scope**, as specified in 2.4, plus a `rule-inert` finding for any rule that extracted nothing from a scope containing prose.

The sidecar then answers three questions rather than one: what failed, what passed, and what was never examined. Only the third was missing, and it was the one that mattered.

---

## Build order

1. **Tier 1 in full.** Cheap, no event access, catches half of round 9. Run on every render.
2. **Tier 2 date and quantity checks.** These need event lookup but no relationship modelling, and they cover three of the last four rounds' recurring errors.
3. **Tier 2 action, succession, outcome, departure, tenure.** The full extraction build.
4. **Tier 3 coverage.**

Steps 1 and 2 are worth doing before v1.2 regardless of what happens with 3 and 4, because the query layer inherits all of it.

---

## Regression corpus

Nine rounds have produced a hand-verified set of known fabrications with known-correct answers. That is a test corpus and it should be captured before it is lost to conversation history:

| Case | Rule | Round |
|---|---|---|
| Ska killed by Stonand Ker, "succeeded by Le Vild" | succession | 3–5 |
| Dreld's rule ended by a challenge he won | outcome | 6 |
| Heth Fal's reign rendered under the wrong faction | tenure | 7 |
| Nael War and Paernrom Sir killed, described as cast out | departure | 7 |
| Griwick raid on Kebarrow in 32 — does not exist | action | 7 |
| "including" before an exhaustive list of four | Tier 1.1 | 7 |
| Danpa killing Seirn — summary contradicts body | Tier 1.4 | 8 |
| Kourn "contested" when his claim was upheld | outcome | 8 |
| Compact's standing collapsed — it was the Wurn League's | action | 8 |
| Meigate's faction raid stats inside a reign | quantity | 8 |
| Thres Thrild murdered in 46 — was 47 | date | 8 |
| Voudreirn Wer won Baedros Mam in 49 — was 48 | date | 8 |
| "Two marriages" then three named | Tier 1.1 | 9 |
| "three battles which it won", two narrated | Tier 1.1 | 9 |
| Threi Cut revolt in 15 — was 13 | date | 9 |
| Zero-haul raid described as plunder | quantity | 9 |
| Tor Nathgoull took the seat "when his house ended" | date | 9 |
| Kebarrow's second war unnarrated | Tier 3 | 9 |

Each of these should fire its named rule when reintroduced. That set is the checker's acceptance test.

---

## What I want from you

Build Tier 1 first and report what it catches on the *current* round 9 render before fixing anything. That number is the interesting one: if Tier 1 alone flags three of six defects with no event access, it settles the question of whether statement validation is worth the build.

Push back if the tiering is wrong. In particular: if you think Tier 2 extraction is unreliable enough that its false-failure rate would exceed its catch rate, say so — a checker that wrongly excludes good passages is worse than no checker, because the chronicle loses content and the failures stop being read.
