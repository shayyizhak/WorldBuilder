# Reference set — verification protocol (ruleset 7)

**Supersedes `reference-set-verification.md`**, which was written for ruleset 4 on the old panel. Three things changed: the ruleset, the panel (seed 99 → seed 1), and the world.

**Still not a loop-prompt.** This is entirely semantic intent, so a loop would halt on every step. It is a protocol for a human session.

---

## 0. Gate

Do not start until `brief-goalbook-into-the-fold.md` returns with **additive-only holding** and `baselines/ruleset-7/` cut.

**Pre-committed branch.** If additive-only failed, ruleset 7 changed worlds and this protocol is stale before it starts — stop, and re-plan. Do not begin verifying a world that is about to be replaced. That was the whole reason `GoalBook` was sequenced first.

**Not a gate:** the absent-vs-unknown type work (`https://trello.com/c/QiADoVAB`). If it lands first it improves the query layer's ability to express the withheld case in §4; if it lands after, §4 records the gap. Either way this proceeds.

---

## 1. What is being built

The reference world is **seed 42** — longest-standing, richest, 878 events across 13 scopes and 5 powers at ruleset 6. Three artefacts:

1. A ruleset-7 seed 42 reference facts sheet
2. Sixteen query questions with verified answers and enumerated supporting records
3. One canonical withheld-not-absent case

**The v1 §8 facts are not superseded.** They document the sealed v1 record, which Layer 3 depends on permanently. Both sets exist, labelled.

**Seed 1 has never been read by anyone.** It entered the panel by screening, not by inspection. Questions may draw on it, but do not assume anything about its history — and note that its `seized` count is 1, which is thin.

---

## 2. Order of work — highest downstream dependence first

A session that runs short should leave the most load-bearing items done.

1. **Seats and ruler lists.** Nothing downstream questions these. Verify from the record directly, not from the derivation — the ruleset-4 derivation collapsed contested transfers by adjacency rather than by year, and although no staged row moved, the class of error is the one nothing catches.
2. **Powers, foundings, secessions, collapses.**
3. **Counts and spans.** Deaths, flights, raids, battles, killings.
4. **Recurring false premises** — who never held a seat, whose claim was set aside, which power never took which. These are what let a query suite fail.
5. **The sixteen questions.**
6. **The withheld-not-absent case.**

**New at ruleset 6 and worth verifying deliberately:** terminated relations. A tie that ended is a different fact from one that never existed, and the log is the only place that distinction lives — there is no tombstone in `RelationGraph`, by design. Any fact about who traded with whom must state the span, not just the pair.

---

## 3. Rules for the facts sheet

**Read the record, not the `.log` view.** The view hides bookkeeping rows and much of the economy's causal influence runs through them.

**State the derivation, not just the number.** `474 = 185 + 133 + 156, plague deaths Y26–28, records e:…`, not `474 dead`. A count without its derivation cannot later distinguish "the world changed" from "the way we counted changed" — which now matters more, since the world has changed three times.

**Every figure carries its scope.** Faction-lifetime figures labelled as such.

**Dispersion self-identifies.** Keep emission labels (`sd=`, `range=[a, b] width=`, `cv=`, `ci95=`, `var=`) when transcribing. Do not strip them.

**Role and outcome both decide a count.** The v1 Paernmel Has case is the template — seven records named him and the true count depended on whether he was subject or agent and whether the attempt succeeded. Expect an analogue; write the discriminator explicitly.

**Seal every verified row** with the log hash it was verified against, so a later ruleset change invalidates the sheet visibly rather than silently.

**Mark unverified explicitly.** A row that ran out of session is `unverified`, never blank. Blank reads as verified.

---

## 4. Rules for the sixteen questions

**Classification is not stable run to run.** The same question with a byte-identical body was classified causal in one run and factual in another, changing retrieval from three records to one.

- **Suite-eligible** only if the correct answer is reachable under *both* classifications. Verify under both retrieval paths where they differ.
- A question whose answer depends on classification is **not discarded** — flag it `classification-sensitive` and keep it as a deliberate probe, outside the sixteen.

**Each question carries:** text, the single defensible answer, enumerated supporting records, the classification(s) tested, and **what a wrong answer would look like.** That last field is what makes a question able to fail.

**At least three questions with a negative premise**, built on §2 item 4. A suite of only answerable questions cannot detect a layer that answers everything.

**Verbatim fields resolve against the question text or the record**, never against the planner's string. The planner mistypes verbatim fields at a meaningful rate, and a mistyped year produces no failure signal — just a plausible answer about the wrong decade. Note any question turning on a year.

**One question requiring a supplied figure to be restated rather than summarised.** A supplied figure going unused is caught by nothing; until a rule covers it, a suite question is the only detector.

**One question on a terminated relation**, since that is new engine behaviour and the answer must distinguish *ended* from *never existed*.

---

## 5. The withheld-not-absent case

Criteria for adoption:

- The subject is otherwise queryable, so "absent" is a plausible wrong answer rather than an obviously wrong one
- The honest answer is "this is withheld", and both failure modes are reachable: answering "absent", and leaking the content
- It survives the classification instability in §4

**Pre-committed:** a candidate the query layer cannot currently distinguish from absent is **not adopted**. Record it, record the vocabulary gap, pick the next candidate. Adopting a case the layer structurally cannot pass converts a design gap into a permanent red test, which trains you to ignore it.

If no candidate passes, that is a finding rather than a failed session — Stage 11's premise is exactly this distinction, and learning the query path cannot yet express it is worth knowing early.

---

## 6. Stopping and recording

Stop when §2 items 1–4 are complete, even if the questions are not. The facts sheet is what everything else derives from; questions can be staged later against a verified sheet, but a sheet verified against unverified questions is worthless.

At the end write down: what was verified, what was left unverified, which seal it was verified against, and **any row where you were uncertain but recorded a verdict anyway.** That last list is the one worth keeping — it is where the next wrong-entry-nothing-questions will come from.
