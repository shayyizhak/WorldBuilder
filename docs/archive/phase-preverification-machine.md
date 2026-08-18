# Phase — pre-verification machine work

**Loop-prompt.** Run unattended until the queue is empty or a halt condition fires. Three seeded items; the queue extends itself under the rules in §5.

**Why this phase exists.** The ruleset-4 reference set is about to be built by hand. Every item here is upstream of that: item 2 decides whether the ruler-list derivation can be trusted, item 3 decides whether Layer 4's assertions have been testing anything, item 1 decides whether the chronicle scopes being verified are the right ones. A wrong entry in a hand-verified set is the one error class nothing downstream questions, so all three land before human hours are spent.

**Standing constraints for this phase**

- Any probe or instrument added here must satisfy instrumentation invariance: log hash identical with and without, across all five seeds. Assert it; do not assume it.
- Any new test enters at the outermost callable production uses. A test feeding an input the production caller never produces is worse than no test.
- Rationale is written before measurement, not after.
- Report `extracted / checked / unresolvable / fired` wherever a rule is involved.

---

## 1. Holdout distribution across the five ruleset-4 seeds, grouped by rule

**Question.** The ruleset-4 chronicle holds out 6 of 13 scopes where v1 held out 3 of 15. Is that the checker working harder on a harder world, or one or two rules over-firing?

**Method**

1. For each of the five ruleset-4 seeds, emit one row per held-out scope: `{seed, scope, rules_fired[], blocking, fatal}`. Take this from the sidecar, not by re-deriving.
2. Emit per-seed denominators: total scopes, held-out scopes, holdout rate.
3. Emit the scope *list* per seed and diff it against the corresponding ruleset-3 baseline's scope list. The denominator moved from 15 to 13, so scope selection changed too — report what changed and by what criterion, since selection-by-weight is a known systematic dropper of powers whose stories concluded.
4. For each rule, emit its firing count on non-held-out scopes at ruleset 4 and at ruleset 3, same seeds.

**Degeneracy guard.** If total holdouts across the panel is fewer than 10, the grouping question is underpowered. Report counts and the scope diff, declare the distribution question void, and continue. Do not read a pattern out of single-digit totals.

**Pre-committed decision rules** (guard permitting)

- **Over-firing suspected** — a single rule accounts for ≥ 60% of panel holdouts *and* that rule's non-holdout firing count rose from ruleset 3. Name the rule, HALT, escalate.
- **Checker working** — holdouts attribute to ≥ 4 distinct rules *and* per-seed holdout rate range ≤ 20 points. Record, continue.
- **Anything else** — record the distribution, HALT, escalate as prose judgement.

A rule whose non-holdout firing count went to zero from a non-zero ruleset-3 figure is the silent-path signature and escalates immediately regardless of the above.

---

## 2. Vea Lode contested-transfer check

**Question, in two parts.** A contested transfer emits two records. (a) Does the ruleset-4 ruler-list derivation collapse them, and (b) — the part that actually matters — did any v1 hand-verified ruler list cross a contested transfer and silently double-count?

**Part (b) runs first.** It concerns the sealed v1 record, which Layer 3 permanently depends on for 20 of 28 scoped rows.

1. Enumerate every contested transfer in the sealed v1 record: seat, year, both record IDs.
2. Intersect against every hand-verified ruler list in the v1 reference facts — Vea Lode explicitly, and every other list carried there.
3. For each intersection, check the hand-verified list against the records directly.

**Pre-committed:** any crossing found where the hand-verified list disagrees with the records → that reference entry is marked **suspect**, every Layer 3 row depending on it is enumerated, HALT, escalate. Do not repair the entry unattended; a hand-verified fact is only re-verified by hand.

Any crossing found where the list agrees → record the crossing as checked, continue.

**Part (a), the ruleset-4 derivation.**

4. Run the derivation across all five ruleset-4 seeds; flag every case where the same person appears twice on the same seat.
5. Classify each: **same seat, same person, same year** → one contested transfer, collapse. **Same seat, same person, different years** → genuine second tenure, must not collapse.
6. Any case fitting neither shape → HALT, escalate. Do not invent a third rule.

Fix the derivation to collapse only the first shape. Add a test at the outermost entry point with both shapes present, asserting the collapse *and* asserting the second tenure survives — a test that only asserts "no duplicate" passes when the derivation drops both.

---

## 3. Schema assertion — every field a consumer reads exists in the emitter's vocabulary

**Why.** Layer 4 read `took`/`haul`/`plunder`; the engine writes `loot`. The three-way raid split had been two-way since the layer was written and nothing failed, because every assertion was about the accounting rather than the values. Two of the last four defects were field-name mismatches. This is the silent-path family in the independent verifier itself.

**Method**

1. Enumerate the emitter's field vocabulary per event kind, from the emitter, not from documentation.
2. Enumerate every field name read by each consumer: Layer 4, the checker's Tier 2 and Tier 3 rules, query retrieval, the chronicle pack builder, `BaselineArchive`.
3. Assert set inclusion: reads ⊆ emitted, per event kind.
4. Land this as a standing test, not a one-time scan. It must run at the outermost entry point and fail loudly on a name the emitter does not write.

**Classify every mismatch found**

- **Dead read, assertion never fired** — the `loot` class. State explicitly what the assertion had actually been testing while the field was absent, and which past greens were therefore vacuous. **HALT, escalate.** A vacuous green retroactively voids the record it was standing in for.
- **Typo with a working fallback** — fix, record.
- **Renamed field** — fix both sides, record, and note when the rename happened relative to the last green.

**Halt condition.** Any dead read in Layer 4 or the checker halts the phase, because it means an independent verifier has been reporting on nothing and the scope of that has to be bounded before anything else proceeds.

---

## 4. Halt conditions, consolidated

Halt and escalate on:

- Item 1 over-firing verdict, middle case, or any rule going non-zero → zero
- Item 2 part (b) crossing with disagreement, or a duplicate fitting neither shape in part (a)
- Item 3 dead read anywhere in Layer 4 or the checker
- Instrumentation invariance failing on any probe added here
- Any test added in this phase that cannot be entered at the outermost production entry point

Halt cleanly when the queue is empty with none of the above.

---

## 5. Self-extending queue

Append to the queue, and work it before declaring the phase complete:

- Item 3 finds any mismatch → append: re-run the affected layer's full assertion set with the field names corrected, and report which previously-green assertions changed verdict. Report this as a count of *changed* verdicts, not as a pass rate.
- Item 2 part (a) fixes the derivation → append: regenerate the ruler lists for all five ruleset-4 seeds and diff against the pre-fix lists. Any list that changed is a list the staged reference-set candidate facts sheet was built from, and the sheet needs the corresponding rows re-staged.
- Item 1 escalates on a rule going non-zero → zero → append: locate the input path for that rule and determine where it stops, before proposing any rule change. The rule is usually correct and the input never reaches it.
- Any figure emitted by this phase that expresses dispersion → confirm it self-identifies at emission (`sd=`, `range=[a, b] width=`, `cv=`, `ci95=`, `var=`). An unlabelled dispersion figure is a fabrication vector regardless of who reads it next.

---

## 6. Report

One report, to `/mnt/user-data/outputs/`, containing:

- Per-item verdict against the pre-committed rules above, with the rule quoted and the figure that triggered it
- The holdout table and the scope-selection diff
- The contested-transfer intersection table, including crossings that agreed
- The schema mismatch table with every mismatch classified
- Queue items appended and their outcomes
- Explicitly: **which staged reference-set rows are now invalid** and need re-staging before hand verification begins

That last line is the phase's actual deliverable. Everything else feeds it.
