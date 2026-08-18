# Ruleset-4 reference set — verification protocol

**This is not a loop-prompt.** Loop-prompts halt for questions of semantic intent; this document is nothing *but* semantic intent, so it halts constantly by nature and automating it would be a category error. It is a working protocol for a human session: what to verify, in what order, and what counts as verified.

**Gate.** Do not start until `phase-preverification-machine.md` has returned. Its report names which staged rows are invalid. Starting before that risks verifying rows built from a derivation known to be suspect.

**Pre-committed branch.** If item 2 part (b) found a crossing where a v1 hand-verified ruler list disagreed with the records, then ruler lists are verified **from the record directly** in this session, never from the derivation output, and the v1 entry stays marked suspect until re-verified by hand as its own task.

---

## 1. What is being built, and what it replaces

Ruleset-4 seed 42 is **a different history**, not a stale one. Positions are assigned at worldgen and four mechanics consume distance, so the stream is consumed differently. This is a rebuild from nothing, not a re-verification.

Three artefacts come out of this session:

1. A ruleset-4 seed 42 reference facts sheet
2. Sixteen query questions with verified answers and enumerated supporting records
3. One canonical withheld-not-absent case

The v1 §8 facts are **not** superseded — they remain the documentation of the sealed v1 record, which Layer 3 permanently depends on. They stop being a description of the live world. Both sets exist, labelled.

---

## 2. Order of work — highest downstream dependence first

Verify in this order, so that a session that runs short still leaves the most load-bearing items done.

1. **Seats and ruler lists.** Nothing downstream questions these. A wrong ruler list propagates into questions, into Layer 4, and into every future comparison silently.
2. **Powers, foundings, secessions, collapses.** The skeleton every other fact hangs on.
3. **Counts and spans.** Deaths, flights, raids, battles, killings. Each carries a derivation.
4. **Recurring false premises.** The negative facts — who never held a seat, whose claim was set aside, which power never took which. These are what make a query suite able to fail.
5. **The sixteen questions.**
6. **The withheld-not-absent case.**

---

## 3. Rules for the facts sheet

**Read the record, not the `.log` view.** The view hides bookkeeping rows and much of the economy's causal influence runs through them. Two confidently wrong measurements came from exactly this. Every fact cites record IDs from the full record.

**State the derivation, not just the number.** A count without its derivation cannot later distinguish "the world changed" from "the way we counted changed." Write `474 = 185 + 133 + 156, plague deaths Y26–28, records e:…` rather than `474 dead`.

**Every figure carries its scope.** A faction-lifetime figure is labelled as faction-lifetime. Scope-mismatched statistics recurred three times across the render rounds and are a live defect class.

**Dispersion self-identifies.** Any spread figure keeps its emission label (`sd=`, `range=[a, b] width=`, `cv=`, `ci95=`, `var=`). Do not strip these when transcribing into the sheet — an ambiguous figure is a fabrication vector regardless of who reads it next.

**Role and outcome both decide a count.** The v1 Paernmel Has case is the template: seven records named him, and the true count depended on whether he was subject or agent and whether the attempt succeeded. Expect at least one ruleset-4 analogue and write the discriminator explicitly.

**Seal every verified row.** Record the log hash / seal the row was verified against. A later ruleset change then invalidates the sheet *visibly* rather than silently.

**Mark unverified explicitly.** A row that ran out of session is `unverified`, never blank. Blank reads as verified to a future reader.

---

## 4. Rules for the sixteen questions

**Classification is not stable run-to-run.** The same question with a byte-identical body was classified causal in one run and factual in another, changing retrieval from three records to one. Therefore:

- A question is **suite-eligible** only if its correct answer is reachable under *both* classifications. Verify it under both retrieval paths where they differ.
- A question whose answer depends on classification is **not** discarded — it is flagged `classification-sensitive` and kept as a deliberate probe, outside the sixteen.

**Each question carries:** the question text, the single defensible answer, the enumerated supporting records, the classification(s) tested, and what a wrong answer would look like. That last field is what makes the question able to fail rather than merely able to pass.

**At least three questions should have a negative premise** — built on the false-premise facts from §2 item 4. A suite of only answerable questions cannot detect a layer that answers everything.

**Verbatim fields resolve against the question text or the record, never against the planner's string.** The planner mistypes verbatim fields at a meaningful rate. Years matter most: a mistyped year produces no failure signal, just a plausible answer about the wrong decade. When staging a question that turns on a year, note it.

**One question should require a supplied figure to be restated rather than summarised.** A supplied figure going unused is currently caught by nothing — a v1 answer omitted a flight count the pack had supplied. Until a rule covers it, a suite question is the only detector.

---

## 5. The withheld-not-absent case

Five candidates are ranked in the staged set. Criteria for the one that gets adopted:

- The subject is otherwise queryable, so that "absent" is a *plausible* wrong answer rather than an obviously wrong one
- The honest answer is "this is withheld," and both failure modes are reachable: answering "absent," and leaking the content
- The case survives the classification instability in §4

**Pre-committed:** a candidate the query layer cannot currently distinguish from absent is **not adopted**. Record it, record the vocabulary gap, and pick the next candidate. Adopting a case the layer structurally cannot pass converts a design gap into a permanent red test, which trains you to ignore it.

If no candidate passes, that is a finding, not a failure of the session: the v3 epistemic layer's entire premise is that not-known and not-true differ, and discovering the query path cannot yet express it is exactly the kind of thing worth knowing before Stage 11.

---

## 6. Stopping and recording

Stop when §2 items 1–4 are complete, even if the questions are not. The facts sheet is the artefact everything else is derived from; questions can be staged in a later session against a verified sheet, but a sheet verified against unverified questions is worthless.

At the end, write down: what was verified, what was left unverified, what seal it was verified against, and any row where you were uncertain but recorded a verdict anyway. That last list is the one worth keeping — it is where the next wrong-entry-nothing-questions will come from.
