# v1.2 Generation — loop prompt

Run this unattended. Halt only on the conditions in **Halt**. Do not stop to ask for direction on anything covered below.

---

## Where things stand

Retrieval is done: 16 of 16 questions retrieve correct event sets, zero secret events across all sixteen, 301 tests green. Query parsing, subject resolution, and causal traversal work.

What remains is generation — turning retrieved events into an answer — plus two structural fixes that block it.

---

## Step 1 — Fix the pack builder (blocking)

`ContextPackBuilder.FromEvents` re-applies `IsRenderable`, discarding every bookkeeping event that retrieval worked to find. Question 1 retrieves the two erosion rows that explain the secession and the pack drops both before the model sees them.

This is the `.log`-versus-record error made permanent in a pipeline stage. Retrieval reads the record; the pack re-imposes the view.

**Fix:** a separate `causes` section in the pack, distinct from the events list.

**Pass what the rows establish, not the rows.** A bookkeeping row is a measurement, not an event, and passing it as an event is what produced *"a harvest count at Meigate revealed a grain shortage"* in the chronicle. The causes section should read as state — *"the Compact's standing had fallen to nothing by 26"* — giving generation what it needs to explain without giving it anything to narrate.

Structural separation matters more than wording here: the section must be shaped so it cannot be mistaken for the event list under prompt drift.

## Step 2 — Carry role and outcome structurally (blocking)

Question 4 retrieves seven records about Paernmel Has. The correct answer of four requires two independent distinctions:

- **role** — 5 records where he is the target, 2 where he is the perpetrator (Veillpea Dourn Y46, Thres Thrild Y47)
- **outcome** — of the 5 targeting him, 4 failed (Y43, Y45, Y46, Y49) and 1 succeeded (Y51, Wuldweald Valdrith)

Role alone yields five, which is wrong and looks right. The pack must carry both as structured fields, not leave them to be inferred from record text.

Apply generally: any record where an actor appears in more than one role, and any event type with a success/failure outcome, carries those as fields on the pack entry.

## Step 3 — Validate planner-emitted verbatim fields

The planner mistyped the subject in 3 of 16 questions on a field it was told to copy verbatim. Subject resolution now falls back to matching against the question text, which is correct.

Extend the principle to every verbatim field. **Years are the priority**: a mistyped year produces no resolver failure and no empty set — just a confident answer about the wrong window. Validate any year the planner emits against the question text; if it does not appear there, treat the query as unresolvable rather than running it.

Keep the existing rule: never fuzzy-match a planner string against the record. A miss is recoverable; a confident resolution to the wrong entity is not.

## Step 4 — Build generation

Render the pack under the v1.1 rules. All of these are established and binding:

- No particular — name, place, date, number, motive, action — absent from the pack
- Figures stated at the scope they were computed for
- No log-referential language: no "the records show", "N events matched"
- Pattern statements characterise, they do not compute
- Causes section informs the explanation; it is never narrated as events

## Step 5 — Checker on the answer path

Before that, **split `unresolvable`**. It currently covers two opposite situations: *the lookup could not be performed* and *the lookup was performed and the thing is not in the record*. The second is a fabrication and must fire.

In the chronicle this was a quiet miss. In a query it is a wrong answer to a direct question. Carry the offending span in the `unresolved` block so it is diagnosable.

Then run the checker on every generated answer, with the retrieved set standing in for the scope's event set. Rules that matter most: `tenure`, `succession`, `action`, `date`, `outcome`, `departure`, `invented-mind`.

**Disposal differs from the chronicle.** A chronicle excludes a passage and prints a note in its place; it has fifteen sections and losing one is survivable. A query has one answer and nowhere to put a warning.

- **Fatal finding** → do not return the prose. Return the retrieved facts plainly, or state that the question cannot be answered reliably.
- **Non-fatal finding** → return the answer, log the finding.
- **Never** return prose carrying a known fabrication, however hedged.

## Step 6 — Empty results and false premises

Both already work at retrieval. Confirm they survive generation:

- Retrieval returns nothing → the model is never called. Hard code path.
- False premise → rejected before retrieval, and the rejection is stated plainly rather than explained around.

Neither may produce a plausible partial answer.

---

## Halt

Halt and report when **all** of these hold:

1. All 16 suite questions answered correctly, judged against the expected answers below.
2. Zero secret events in any answer.
3. Zero fatal checker findings on any answer.
4. `coverage-sound` holds on the answer path: `extracted == checked + unresolvable` per rule, and no rule inert on an answer containing prose.
5. Full test suite green.

Also halt immediately, without completing the run, if:

- A fix requires changing what an expected answer is. Report the disagreement; do not adjust the suite to match the output.
- The same defect recurs after being fixed and tested twice.
- Retrieval regresses — any of the 16 sets changes.

## Expected answers

| # | question | expected |
|---|---|---|
| 1 | Why did Hadale break from the Kebarrow Compact? | Y27 secession, caused by the Compact's **own** raid on Griwick being beaten off. Not an attack repelled — a failed attack. Erosion of standing may inform the explanation. |
| 2 | Why did the Wurn League end? | Y20 — Kebarrow took Hadale, leaving it landless; collapse, then peace at Y21 |
| 3 | Why did Threi Cut rise against the Vea Lode Covenant in 51? | the death of its ruler Keithfal Naell |
| 4 | How many times was Paernmel Has the target of an attempt? | **Four failed attempts** (Y43 Stonand Ker, Y45 Keithfal Naell, Y46 Throll Kell, Y49 Drouldthas Stour). Not five — the Y51 killing succeeded. Not seven — two records are killings he ordered. |
| 5 | Which powers broke away, and from whom? | Meigate Y19, Laehiford Y20, Hadale Y27 from Kebarrow; Vea Lode Y29 from Griwick |
| 6 | Which powers were destroyed? | Wurn League Y20, Griwick Compact Y35, Sworn Men of Meigate Y50 |
| 7 | Who ruled the Vea Lode Covenant? | Stald Gearngoll (29), Veillpea Dourn (45), Thres Thrild (46), Gatros Hearn (47), Keithfal Naell (48), Herpeim Raern (50) |
| 8 | How many died in the plague at Griwick? | 474 over three years — 185, 133, 156; 504 fled |
| 9 | What happened to the Drelthorn League? | nothing — no such power |
| 10 | Who ruled the Sworn Men of Meigate in year 5? | nothing — founded Y19 |
| 11 | Who ruled the Hadale Commune in year 51? | Durnrin Drar |
| 12 | Why did Stonand Ker lose the seat of the Kebarrow Compact? | false premise — he never held it |
| 13 | Why did Hehum Skul's reign end? | false premise — named heir, claim set aside, never ruled |
| 14 | When did the Kebarrow Compact conquer Griwick? | false premise — Vea Lode took Griwick in Y35 |
| 15 | Who attempted to kill Sothkel Sald in year 35? | must name nobody — the record is unattributed and secret |
| 16 | Who conspired against Paernmel Has? | the three uncovered plots only, at their uncovering years |

Question 11 is a near-miss for question 10 and is answerable. "Returns nothing" must not become the safe default.

---

## Two rules for the work itself

**Every rule test enters at the outermost callable production uses.** Two tests passed this round while the code failed: the conspiracy test hand-fed `POLITY.COUP_PLOTTED`, which the planner never emits; the mistyped-subject test called `Ground`, which `AskAsync` did not. A test that feeds an input the production caller never produces is worse than no test — it converts silence into false confidence.

**Assert extraction, not just absence of failure.** A test asserting "no finding fired" passes when the rule is inert. Every positive case must assert the rule examined something.

Both are the rounds 11–14 family: the rule was correct and the input never reached it. It has now appeared in the checker three times and the query layer twice. Assume it will appear again and write the test that would catch it.

---

## Reporting

On halt, report:

- The 16 answers in full
- Checker findings per answer
- Coverage table for the answer path
- Any expected answer you believe is wrong, with reasoning
- Any defect fixed more than once, and what the second fix was

Do not summarise the answers — I want to read them. Prose quality is the one thing here I cannot check mechanically, and it is the reason this halts rather than continuing to v2.
