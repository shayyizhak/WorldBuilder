# v1 Fix Round 2 — restore aggregation without restoring fabrication

## Context

Fix round 1 was applied and seed 42 was re-rendered. The mechanical fixes largely worked. One regression appeared, and it is the most important item in this document.

**What was fixed:**

- **Dangling causal references: 0** in `world-42.log`, down from 66 distinct (175 edge instances). Both fabrications traced to unresolvable IDs last round — the invented "harvest count at Meigate" and "old grudges cooled and the standing eroded" — are gone from the chronicle.
- **Secret filtering works.** All five `[secret]` unattributed assassination attempts (`e:142`, `e:588`, `e:639`, `e:669`, `e:825`) are absent from the chronicle. The Stald Gearngoll de-anonymisation is gone.
- **Chain render scope was dropped**, as instructed.
- **Executions are now real events.** "Naell was put to death for his conspiracy" traces to `e:904` (`LIFE.DEATH_VIOLENT ... executed for conspiracy`). Last round's fabrication is now a recorded fact.
- **Spot-checked numbers all trace correctly:** the 75/47/238 Meigate famine figures (`e:791`, `e:818`), the 39 grain and 22 killed at Laehiford (`e:302`), and the full Y7–Y9 war sequence including both peace treaties.

**What regressed:** aggregation quality collapsed. Details in section 1.

---

## 1. PRIORITY: fixing fabrication killed the aggregation

This is the round's real work. Everything else in this document is mechanical.

### What happened

Last round's Kebarrow Compact section closed on *"the cycle of violent succession that defined the era"* — a judgement about a whole period that beat the raw log outright. That was the result that justified v1.

This round's equivalent section reads:

> *"In 22, the Griwick Compact's raid on Hadale was beaten off. By 23, the Kebarrow Compact bound itself to the Sworn Men of Meigate through the marriage of Saern Meastouth and Renbeir Surn, and to the Sworn Men of Laehiford through the marriage of Tor Nathgoull and Realsis Leirpu. That same year, Saern Meastouth challenged Weallhous Dreld openly for the Kebarrow Compact and was beaten."*

That is one prose sentence per event. It is the transliteration failure mode that got the chain scope deleted, now appearing inside the faction scope — which was the scope that was working.

### Why it happened

What made last round's prose good was substantially the invented connective interpretation. Tightening the anti-fabrication rules removed the invention, and the model responded by ceasing to interpret at all and falling back to restating events in order.

The current prompt effectively says "assert nothing that is not in the events." Under that instruction, transliteration is the compliant answer. Aggregation is forbidden by implication.

### The fix: distinguish particulars from patterns

The rule needs to be two rules, not one:

**FORBIDDEN — inventing particulars.** No name, place, date, number, motive, emotion, intent, or action that does not appear in the source events. *"His paranoia led him to attempt the murder"* invents an interior state. *"Systematically rooting out conspirators"* invents a policy. Both stay forbidden.

**REQUIRED — characterising patterns across the supplied events.** Frequency, recurrence, escalation, duration, how a period ended relative to how it began, which dynamic repeats. *"The cycle of violent succession that defined the era"* is not fabrication — it is arithmetic stated in prose (seven rulers in twenty years, five of them killed). It asserts no new particular.

The test for any sentence: **does it introduce a fact not present in the input (forbidden), or does it describe the shape of facts that are present (required)?**

### Evidence the model can already do this

Two sentences in this run are correct pattern statements:

- *"The Kebarrow Compact and its allies defeated the Griwick Compact at Kebarrow three times"* — correctly collapses repeated battle events into a count.
- *"In 43, a second year of famine left 47 dead and 238 abandoning the place"* — correctly recognises continuation.

The capability is there. It is operating at sentence level and needs to reach paragraph and section level.

### What to do

Rewrite the render prompt around this distinction, then re-render the Kebarrow Compact faction scope and compare against both prior versions:

- Round 1 output (good prose, fabricated particulars)
- Round 2 output (accurate, transliterated)
- Round 3 target (accurate **and** aggregated)

Consider giving the renderer explicit derived inputs to characterise — ruler count for the period, mean tenure, cause-of-departure distribution, event-type frequencies, war and famine durations. Computing these in the engine and passing them as facts means pattern statements are grounded in supplied numbers rather than requiring the model to count reliably across a long input.

---

## 2. Output truncation

The "Kebarrow Compact, 22–41" section ends mid-sentence: *"In 38, Throll"*.

This is a `max_tokens` cutoff, and it silently produced incomplete canon — the render was cached despite being unfinished.

- Raise `max_tokens` for section-length renders.
- Add a completeness assertion: a render that terminates on length rather than on a stop condition must **fail and not be cached**. Truncated output must never become canon.

---

## 3. Same contradiction class, third occurrence

Two renders of the Y51 succession disagree:

> *"Wuldweald Valdrith took the seat of the Kebarrow Compact after his claim was upheld and Hehum's was overturned"* — correct

> *"Valdrith contested Hehum Skul's claim to the seat under the rule of election. Valdrith's claim was overturned, and he took the seat."* — wrong, and internally contradictory

Source events:

```
[Y0051] w:78  POLITY.SUCCESSION_DISPUTED  Wuldweald Valdrith (a:91) contests Hehum Skul (a:72)'s claim to the Kebarrow Compact (f:2) (rule: election)  <= e:1021  e:1030
[Y0051] w:78  POLITY.SUCCESSION           Wuldweald Valdrith (a:91) takes the seat of the Kebarrow Compact (f:2) (claim overturned)  <= e:1030  e:1031
[Y0051]       POLITY.EXILE                Hehum Skul (a:72) is cast out of the Kebarrow Compact (f:2) — the losing claim  <= e:1030  e:1032
```

Two separate causes, both need fixing:

**(a) Cache keying and sampling determinism — still outstanding from round 1.** The same input must not be renderable twice into two cache entries. Cache key must be a deterministic function of the input event set plus render scope. Set an explicit fixed seed and low temperature; do not inherit Qwen 3.6's shipped defaults (temperature 1, presence penalty 1.5).

**(b) The engine label is ambiguous.** `(claim overturned)` does not say whose claim. The renderer read it correctly once and backwards once, which is the expected outcome for an ambiguous input. Rename to `(rival claim overturned)` or equivalent.

Ambiguous engine labels are a fabrication vector independent of model behaviour. Audit the other succession labels — `primogeniture`, `strongest`, `election`, `coup`, `claim upheld` — for the same problem.

---

## 4. Secret-flagging gap in the engine

`POLITY.COUP_PLOTTED` carries `[secret]` (42 instances in world-42). `POLITY.PLOT_LAPSES` does not.

Result: the chronicle reports *"A conspiracy by Drouldthas Stour against Paernmel lapsed after three years because the target was already dead"* — a conspiracy that was never uncovered by anyone, narrated as public history. Source: `e:1028`, unflagged.

**Resolution events must inherit the secrecy of what they resolve**, with one exception: where the resolution *is* the discovery (`COUP_RESOLVED ... is uncovered`), the event is legitimately public from that point.

Apply to `PLOT_LAPSES` and `PLOT_DIES_WITH_PLOTTER`. This is an engine flagging fix, not a render fix — the visibility filter is working correctly on what it is given.

---

## 5. Confirm how the dangling references were fixed

Events went 699 → 691 and distinct causes referenced went 341 → 274.

That pattern suggests the broken edges were **removed** rather than the missing events **restored**. If the original diagnosis was option (a) — real events being filtered out of the log below a significance threshold — then genuine causal links have now been deleted rather than surfaced, and the causality graph is thinner than the simulation actually produced.

Confirm which happened. If edges were dropped, reconsider: the referenced events should be emitted (possibly as low-significance stubs) so the graph stays complete.

Either way, keep the referential integrity check in the build.

---

## Evaluation

Re-render seed 42 and report:

1. **Aggregation quality.** Kebarrow Compact faction scope, side by side with the round 1 and round 2 outputs. The target is round 1's readability with round 2's accuracy.
2. **Fabrication rate, automated.** Every proper noun, year, and number in the prose must appear in the source event set. Report count and list failures. Pattern statements are not failures — assess them separately against the particulars/patterns rule.
3. **Zero truncated renders**, enforced.
4. **Zero contradictions across renders** of the same event, checked by rendering the same scope twice and diffing.
5. **Zero secret leakage**, including via resolution events.

---

## What I want from you

Item 1 is the round. Items 2 through 5 are mechanical and should be quick.

Push back if any of this is wrong. In particular: if you think the particulars/patterns distinction is too fuzzy to hold in a prompt and would prefer the engine to compute pattern facts explicitly and pass them in as structured input, say so — that may well be the more robust design, and it is worth arguing before implementing.
