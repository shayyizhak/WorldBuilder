# v1.2 Answer Quality — loop prompt

Run unattended. Halt only on the conditions in **Halt**.

This is the last v1 loop. Retrieval and correctness are done — 16 of 16, zero leaks, zero fatal findings, retrieval byte-identical to baseline. What remains is how the answers read.

Two things are settled and not up for revisiting:

- **Your rejection of "no rule inert on an answer containing prose" was correct.** Construction-gated rules cannot extract from prose that does not contain the construction, and loosening extraction to satisfy the condition is how the round-10 false positives were manufactured. Inert is a finding only when the construction is present. That is the condition from here on.
- **No expected answer changes.** If a fix appears to require it, halt and report.

---

## Step 1 — Empty results must branch by reason (blocking)

Three answers currently return the same sentence:

| # | question | current | why it is wrong |
|---|---|---|---|
| 9 | What happened to the Drelthorn League? | *"The records do not cover that."* | log-referential |
| 10 | Who ruled the Sworn Men of Meigate in year 5? | *"The records do not cover that."* | log-referential |
| 15 | Who attempted to kill Sothkel Sald in year 35? | *"The records do not cover that."* | log-referential **and false** |

The record covers question 15 precisely:

```json
{"id": 639, "year": 35, "kind": "CONFLICT.ASSASSINATION", "scope": "Secret",
 "outcome": "Failed", "parts": [{"r":"Subject","id":"a:27"}, {"r":"Object","id":"a:23"}]}
```

Gatros Hearn, failed, at Hadale. Saying the records do not cover it is a false statement about the world.

**This is the `unresolvable`-versus-fired conflation one layer out** — *absent* and *withheld* collapsed into one output. It is the fifth appearance of that family in this project.

It also matters beyond phrasing. The v3 epistemic layer's entire premise is that not-known and not-true are different things. A query layer that cannot express the difference cannot carry that layer later.

**Fix:** the empty path branches on why it is empty, with distinct phrasing per branch and no reference to records, logs or retrieval anywhere.

| branch | condition | shape of answer |
|---|---|---|
| `no-such-entity` | the subject resolves to nothing in the world | *"No such power appears in this world."* |
| `outside-lifetime` | the subject exists but not at the asked time | *"The Sworn Men of Meigate did not exist until 19."* |
| `withheld` | matching records exist but are all `scope: Secret` | *"Whoever made that attempt was never found out."* |
| `no-occurrence` | the subject and window are valid; nothing of that kind happened | plain statement that it did not happen |

The `withheld` branch must never name, count, or hint at the secret records — but it must also not deny they exist. *"Never found out"* is true, non-leaking, and better history than a denial.

**Tests:** one per branch, entering at `AskAsync`. Assert the branch taken and assert the answer contains none of *record*, *records*, *log*, *retrieved*, *data*, *entry*.

## Step 2 — Causal answers may state only links the record carries (blocking)

Question 3 currently answers:

> *"Threi Cut rose against the Vea Lode Covenant in 51 because the Covenant's standing had fallen to nothing [e:1035]. This collapse followed the killing of Keithfal Naell..."*

`e:1035`'s `causes` is `["e:999"]` — the death, directly. "Standing had fallen to nothing" is a descriptor on the revolt event, not a separate caused thing. The answer inserts standing as an intermediate cause and implies the killing produced it. Plausible; unsupported.

**Fix:** a causal answer may assert a link only where a `causes` edge exists between the two events named. Descriptors carried on an event may be stated as context but never as a link in the chain.

This is the `action` rule applied to causal claims, and it should be a rule rather than a prompt line — it is the same class as the fabricated succession links the chronicle produced for five rounds.

**Also fix the term overload.** "Collapse" is `POLITY.COLLAPSE`, which means a power is finished. The Vea Lode Covenant survives to 51 holding three places, and question 6 uses "destroyed" for that exact event kind. Reserve collapse and destroyed for `POLITY.COLLAPSE`; a legitimacy decline is a decline.

## Step 3 — Reign statements carry spans (blocking)

Question 7 gives accession years as though they were reigns:

> *"ruled by Stald Gearngoll in 29, Veillpea Dourn in 45, Thres Thrild in 46…"*

Stald Gearngoll ruled 29–45. Sixteen years and Thres Thrild's one year read identically.

**Fix:** a reign statement states a span, closed by the next accession or by the end of the record. This is the `tenure` rule, which already exists and already fired on a bad tenure claim in the chronicle — extend it to the answer path.

## Step 4 — The query prompt never received particulars/patterns

This is the substantive one, and it is a prompt fix rather than architecture.

Three rounds of chronicle work produced the distinction: **inventing particulars is forbidden; characterising patterns across the supplied records is required.** The query path was built without it, and the answers show it.

```
Stonand Ker conspired against Paernmel Has in year 46.
Keithfal Naell also conspired against Paernmel Has in year 46.
Throll Kell conspired against Paernmel Has in year 46.
```

Three conspiracies, one year, one target, three men each cast out for it. *"Three conspiracies against Paernmel Has were uncovered in 46"* is one sentence carrying more. The pattern is in the supplied set and the answer does not characterise it.

Question 5 has the same shape — four secessions, four identical templates, and no statement of the obvious pattern that three of the four were from the same power. Question 8 is four sentences of one figure each.

Question 4 is the inverse failure:

> *"Paernmel Has was the target of a failed attempt 4 times [e:822; e:869; e:892; e:976]."*

Correct, and it withholds everything a person asking would want — four citations and no names, no years, no sense that these were four separate men over six years.

**Fix:** carry the particulars/patterns rule into the query prompt, in the same terms the render prompt uses. Where the retrieved set shares a year, a target, an outcome or a cause, the answer characterises that rather than restating it per record. Where a count is the answer, the answer names what was counted.

The constraint is unchanged: no particular absent from the pack. Patterns are computed over what was supplied, never invented.

## Step 5 — Also use the causes section

Question 1's pack carries *"by 26, the standing of the Kebarrow Compact stood at 1 out of 100"* — built for that question. The answer cites only the failed raid and ignores it.

The pack-builder fix works; the prose does not use it. Where a causes section is present, the answer should draw on it. It remains non-citable and must never be narrated as events.

## Step 6 — Pin the two switched-off-rule fixes

Both were fixed this round and neither has a test:

- `coverage` and `shape` registering as inert on the answer path when they were gated off — a rule switched off reading as a rule that found nothing.
- `name` and `number` reporting under a phantom rule with zero extraction while the scan that produced them reported 107.

**This family has now appeared five times** — twice in Tier 1 lexicons, once in the resolution path, once in extraction collapse, once here. Each instance was silent and each was found by accident. Pin both verbatim.

---

## Halt

Halt and report when all hold:

1. All 16 answers correct, judged against the unchanged expected answers.
2. Zero secret events in any answer or pack; the `withheld` branch names nobody.
3. Zero fatal checker findings.
4. `extracted == checked + unresolvable` per rule; inert reported only where the construction is present.
5. No answer contains *record*, *records*, *log*, *retrieved*, *data*, or *entry*.
6. No causal answer asserts a link without a `causes` edge.
7. Retrieval unregressed — all 16 sets byte-identical to baseline.
8. Full suite green.

Halt immediately, without completing, if:

- A fix would require changing an expected answer.
- The same defect recurs after being fixed and tested twice.
- Retrieval regresses.
- Answer quality would require loosening a rule to pass. Report the tension instead.

## Known gaps — record, do not fix

Both were flagged in your last report and both are correct to leave:

- **Question 8 omits the 504 who fled.** The pack supplies it; the model did not use it. Nothing asks whether a supplied figure went unused, and `incomplete-enumeration` is gated behind `wholeSection`. A completeness rule on a fragment is the round-10 trap.
- **Question 4's "4 times" is verified by no rule.** The vocabulary scan skips numbers under three digits and `count-vs-list` needs an enumerated list. If the model wrote 5, nothing would fire.

Step 4 may fix question 8 incidentally — if the answer characterises the plague rather than listing it, the departures are likely to appear. That is a welcome side effect, not a reason to build a fragment completeness rule.

Both belong in the Stage 4 backlog.

---

## Reporting

- All 16 answers in full, not summarised.
- Which branch each empty answer took.
- Checker findings and the coverage table.
- Any expected answer you believe is wrong.
- Any defect fixed more than once.

Prose quality is the halt condition I cannot check mechanically. Steps 1 to 3 are assertions; step 4 is the one I need to read. That is the only reason this stops rather than running on.
