# Fix Round 13 — build layer 5, then fix extraction

## Context

Round 12 was applied. **The prose is essentially byte-identical to the previous render** — same sections, same sentences, same figures — so this is a clean controlled comparison of checker changes alone. That makes the diagnosis unusually clear.

The resolution path was genuinely fixed. Three scopes now check everything they extract, against unchanged prose:

| scope | rule | before | after |
|---|---|---|---|
| Kebarrow 22–41 | date-agreement | 13 / 8 | **13 / 13** |
| Sworn Men of Meigate | date-agreement | 6 / 3 | **6 / 6** |
| Griwick 24–36 | date-agreement | 5 / 4 | **5 / 5** |

`unresolvable` and `accounted` shipped, the front-matter noise is gone, and `rule-inert` findings rose from 22 to 32 with every collapsed rule and scope named precisely.

**But the extracted-versus-checked gap closed mostly by extracting less, not by checking more.**

| rule | extracted before | extracted now | checked now | unresolvable |
|---|---|---|---|---|
| coined-term | 588 | 588 | 588 | 0 |
| count-enumeration | 11 | 8 | 8 | 0 |
| count-narration | 11 | 11 | 11 | 0 |
| summary-body | 62 | 40 | 40 | 0 |
| date-agreement | 71 | 42 | 42 | 0 |
| partition-sum | **33** | **2** | 2 | 0 |

`unresolvable` is zero everywhere and `accounted` is true throughout. The invariant holds, and it holds trivially, because there is nothing extracted to be unresolvable about.

**That is a defect in the specification, not in the implementation.** `extracted == checked + unresolvable` is satisfiable by extracting nothing, and round 12's brief wrote it as though it were not. `checker-spec.md` §2.5 now pairs it with an extraction floor; both halves are required and neither is worth anything alone.

---

## 1. PRIORITY: build layer 5's coverage diff — it is now a twenty-line job

The comparison that found this regression was two `tier1.json` files and a per-scope diff of `extracted`. That is the whole of layer 5's coverage half.

```
for each scope, for each rule:
    assert extracted >= previous_extracted
    assert extracted == checked + unresolvable
```

Store the accepted round's `tier1.json` as `golden/tier1-{seed}-r{n}.json`. Compare on every run. Fail the build on either assertion.

**This would have failed round 13 before the chronicle was written.** No render, no reading, no review conversation — `partition-sum` at 2 against a stored 33 is a failing build on its own.

Build it before fixing the extraction regression below, so that the fix can be confirmed not to cost extraction somewhere else. The last two rounds each fixed one silent failure by introducing another, and each time it took a full render plus a hand review to notice. That loop is the thing to break, and it breaks here.

Note that a floor needs a policy for legitimate drops. When prose genuinely changes and a section really does contain fewer countable claims, the floor should be re-baselined deliberately — a stored golden that a human updates, not a threshold that drifts. Make re-baselining an explicit action rather than something that happens by rerunning.

---

## 2. Where extraction collapsed

Against unchanged prose:

| scope | rule | before | after |
|---|---|---|---|
| Kebarrow 2–21 | date-agreement | 12 / 0 | **0 / 0** |
| Kebarrow 2–21 | summary-body | 12 / 0 | **0 / 0** |
| Kebarrow 2–21 | partition-sum | 1 / 0 | **0 / 0** |
| Wurn League 2–21 | date-agreement | 5 / 0 | **0 / 0** |
| Wurn League 2–21 | summary-body | 4 / 0 | **0 / 0** |
| Wurn League 2–21 | partition-sum | 5 / 1 | 1 / 1 |
| Griwick 24–36 | partition-sum | 5 / 0 | **0 / 0** |
| Hadale Commune | date-agreement | 4 / 1 | 1 / 1 |
| Hadale Commune | partition-sum | 4 / 0 | **0 / 0** |
| Vea Lode 29–48 | date-agreement | 14 / 5 | 11 / 11 |
| Kebarrow 42–51 | date-agreement | 4 / 2 | 2 / 2 |
| Kebarrow 42–51 | partition-sum | 5 / 0 | **0 / 0** |
| Kebarrow 42–51 | summary-body | 2 / 0 | **0 / 0** |
| Heth Fal reign | partition-sum | 3 / 0 | **0 / 0** |
| Heth Fal reign | count-enumeration | 2 / 0 | **0 / 0** |

Kebarrow 2–21 had the worst gap last round and is now entirely inert on three rules, against prose containing eight dates, a summary paragraph, and a partition.

### A hypothesis, offered as a lead rather than a conclusion

The two sections where `date-agreement` fell to zero — Kebarrow 2–21 and the Wurn League 2–21 — are the only two in the document that write dates as *"in year 4"*, *"in year 15"*, *"until year 7"*. Every section that held or improved uses the bare form: *"in 23"*, *"in 25"*, *"in 32, 33, and 34"*.

That suggests the date pattern was narrowed while the resolution path was being fixed. It is not the whole story — Hadale (4 → 1) and Kebarrow 42–51 (4 → 2) both use bare numerals and still dropped — so treat it as one thread to pull rather than the answer.

I have been wrong on a pattern hypothesis of exactly this shape before: in round 11 I attributed Tier 1's silence to spelled-out numerals, which turned out to be handled correctly all along. Check it rather than acting on it.

### `partition-sum` is the larger loss

33 to 2 across the document, against prose that still contains every one of these:

- *"The period saw seven rulers, five of them killed"* (Kebarrow 2–21)
- *"a plague that killed 477 people and caused 524 to flee"* followed by 185, 133, 156 and 296, 208 (Griwick 24–36)
- *"drove four men out of the Compact and left three others declared outlaws"* (Kebarrow 42–51)
- *"Five people held the seat… Four were killed, one remained holding at the end"* (Vea Lode 29–48)
- *"three rulers hold the seat for an average of eight years each"* with four returners and three outcomes (Hadale)

Whatever changed, it took out the most valuable rule in Tier 1 almost entirely.

---

## 3. Round 12's defects are all still present, and still unflagged

The prose did not change, so this is expected rather than a new failure — but they remain the acceptance cases and none can be confirmed fixed until the rules that catch them are extracting again.

| defect | scope | rule that should catch it |
|---|---|---|
| *"477 people and 524 to flee"* against its own 474 / 504 breakdown | Griwick 24–36 | partition-sum |
| *"three wars against the Griwick Compact"* — one war, three battles | Kebarrow 22–41 | count-narration |
| four men cast out, *"three others"* outlawed, *"Their"* covering both | Kebarrow 42–51 | partition-sum |
| famine for 33–35 stated twice, once in words and once in digits | Vea Lode 29–48 | duplicate detection |
| *"Three of these returners"* — two were | Hadale Commune | count-enumeration |
| *"who had been motivated by the earlier raid"* | Kebarrow 22–41 | invented-mind lexicon |

`invented-mind` is the odd one out: it is not an extraction-count problem. It fired on "exploiting" in an excluded passage in the same run and missed "motivated by" in a passage that entered canon. That is a lexicon gap, and the lexicon-completeness test from `test-suite-spec.md` is the fix — every marker in every list must fire its rule on an identical sentence with only the marker swapped.

---

## 4. Order of work

1. **Layer 5 coverage diff.** Twenty lines, and it fails this build.
2. **Fix the extraction regression**, with layer 5 confirming the fix costs nothing elsewhere.
3. **Layer 3 — the corpus.** The six defects above are the acceptance cases.
4. **Layer 2's lexicon-completeness tests**, which close `invented-mind` and prevent the next `included`.

---

## Definition of done — amended

Unchanged from round 12 except for the first point, which now has both halves:

1. **`coverage-sound` holds for every rule in every scope** — `extracted == checked + unresolvable` **and** `extracted >= previous_extracted`, with re-baselining an explicit human action.
2. **Tier 1 fires on every corpus case, and on none of the corrected versions.**
3. **Golden diff green** — no figure moved against the previous accepted render.
4. **A hand review finds nothing the checker missed.**

---

## What I want from you

Item 1, and only item 1, before anything else.

The last three rounds have the same shape and it is worth naming. Round 11: rules never received input. Round 12: rules received input and discarded it. Round 13: rules stopped receiving input again. Same defect class three times, in three different pieces of plumbing, and each fix created the next.

That is not a badly built checker. It is a mechanism with several silent paths and no test asserting any of them stay open. Which is exactly what the suite exists for — and it is the reason I would now stop specifying checker behaviour and start specifying tests. Everything after item 1 in this brief is a test, not a rule.
