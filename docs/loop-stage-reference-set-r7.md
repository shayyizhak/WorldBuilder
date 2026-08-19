# Loop-prompt — stage the ruleset-7 reference material

**Machine work only.** This prepares material for a human verification session; it verifies nothing and marks nothing as verified. Every artefact it produces is `unverified` until a person says otherwise.

**Reference world: seed 42**, against `baselines/ruleset-7/seed-42`. Record the seal every output is staged against — a later ruleset change must invalidate this material visibly.

**No mechanics change, no checker rule, no ruleset bump, no `SimConfig` edit.** If any step seems to need one, halt.

Output to `out/reference-set-r7/`.

---

## 1. Two checks first, because they change what gets staged

### 1.1 Do goal rows reach query retrieval?

Ruleset 7 added ~349 events that are engine bookkeeping — `GOALS.FORMED` and the retirement events. Run the existing query path over a handful of representative questions and report whether any retrieval set includes a goal row.

- **They do not reach retrieval** → record that, and proceed.
- **They do** → **HALT.** It changes what a correct answer looks like, and sixteen questions must not be built on the wrong assumption. Report which questions pulled them and how many rows.

### 1.2 Which of seed 42's scopes are held out?

The chronicle holds out some scopes; a question drawn from one has no passage behind it. Emit the scope list with held-out marked, and the rule that fired for each.

Report the seed-42 rate beside the panel's 20 of 58.

---

## 2. The record, with bookkeeping separated as a class

Emit seed 42's **full record** — not the `.log` view, which hides bookkeeping rows and much of the economy's causal influence.

Two files, and the split is by **class, not by case**:

- `record-history.md` — everything a chronicle could draw on
- `record-bookkeeping.md` — `GOALS.FORMED`, retirement events, yearly accounts, and anything else that is engine internals

**State the rule used to split**, and report the count in each. A hand-tuned exclusion list is a maintenance trap; a stated rule can be re-applied at ruleset 8.

Nothing in `record-bookkeeping.md` belongs in the facts sheet. It is emitted so a person can confirm the split was right, not so they read it.

---

## 3. The skeleton facts sheet

`facts-sheet.md`, laid out in the protocol's §2 order, **every row unverified**:

1. Seats and ruler lists
2. Powers, foundings, secessions, collapses
3. Counts and spans — deaths, flights, raids, battles, killings
4. Candidate false premises

Per row: the claim, the supporting record ids, **the derivation written out** (`474 = 185 + 133 + 156, plague deaths Y26–28, records e:…`, not `474 dead`), the scope the figure carries, and `verified: no`.

**Rules that apply to the staging, not just the verification:**

- Ruler lists derive **from the record directly**, never from the ruler-list derivation. Flag every seat where a contested transfer occurs, with both record ids, so the person can check the collapse by hand.
- Any dispersion figure keeps its emission label (`sd=`, `range=[a, b] width=`, `cv=`, `ci95=`, `var=`). Do not strip them.
- Where role and outcome both bear on a count — the Paernmel Has shape — **write the discriminator explicitly** and list every record that names the person, with their role and outcome, rather than a total.
- **Terminated relations state a span, not a pair.** There is no tombstone in `RelationGraph` by design, so the log is the only place *ended* differs from *never existed*. A trade or alliance fact without a span is incomplete.

For candidate false premises, stage claims that are *plausibly* true and are not: a person who never held a seat, an heir whose claim was set aside, a power that never took another. Give the records that make each tempting and the records that refute it.

---

## 4. Candidate questions

`questions.md`, at least 24 candidates so a person can select sixteen. Each carries: text, the answer, supporting record ids, and **what a wrong answer would look like**.

Classify each against the criteria the protocol sets:

- **Suite-eligible** — correct answer reachable under both classifications. Test under both retrieval paths where they differ and report the record counts each returned.
- **`classification-sensitive`** — answer depends on classification. Keep, flag, exclude from the sixteen.

Coverage requirements across the candidate set:

- At least three with a **negative premise**, drawn from §3 item 4
- At least one requiring a **supplied figure restated rather than summarised** — a supplied figure going unused is caught by nothing
- At least one on a **terminated relation**, where the answer must distinguish ended from never-existed
- Flag every question that **turns on a year**; a mistyped year produces no failure signal, only a plausible answer about the wrong decade

Mark any question drawn from a held-out scope, per §1.2.

---

## 5. Secret candidates

`secrets.md`, five ranked, each with:

- The secret record and its id
- Why the subject is otherwise queryable, so that "absent" is a plausible wrong answer rather than an obviously wrong one
- **Whether the query layer can currently distinguish it from absent** — run it and report what came back verbatim
- Both failure modes it would catch: answering "absent", and leaking the content

**Rank by whether the layer can express the distinction, not by how interesting the secret is.** A candidate the layer structurally cannot pass is not a viable test case; stage it with the gap recorded rather than dropping it.

---

## 6. Halt conditions

- Goal rows reaching query retrieval (§1.1)
- Fewer than 24 viable question candidates, or the coverage requirements in §4 unmet
- No secret candidate the query layer can distinguish from absent — report all five with what each returned
- A contested transfer whose two records fit neither the same-year nor the different-year shape
- Any step appearing to need a mechanics change, a checker rule, or a ruleset bump

## 7. Report

The two §1 answers. The record split rule and both counts. Row counts per facts-sheet section. Question candidates by classification, with the coverage requirements ticked off individually. The secret table with what the query layer actually returned for each. The seal everything was staged against.

**Nothing in this run is verified.** Say so in the report's first line.
