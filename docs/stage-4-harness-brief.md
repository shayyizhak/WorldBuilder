# Stage 4 — the automated quality harness

A project, not a round. Six steps, each with its own halt conditions. Halt and report after **each** step rather than running the whole thing through; a step that lands wrong should be caught before the next one builds on it.

Authoritative specs: **`checker-spec.md`** and **`test-suite-spec2.md`** — the newer of each pair. `checker-spec2.md` and `test-suite-spec.md` are superseded and should be renamed or deleted before they mislead. Where this brief and a spec disagree, the spec wins on rule content and this brief wins on sequencing.

---

## 0. Standing constraints

**The whole of Stage 4 is zero-inference.** Layer 4 needs a rendered chronicle, but one exists: `renders.json` in the sealed baseline, read through `wb book --check-only`. No step here calls the model. If a step appears to need inference, that step is wrong — halt and report.

**The sealed baseline is the anchor and is read-only.** `baselines/v1/seed-42/` is create-only. Nothing in this stage writes to it, and no test may update it. Re-baselining stays an explicit act of moving the directory aside.

**Every test enters at the outermost production entry point.** A test feeding an input the production caller never produces is worse than no test. This has bitten twice.

**Every positive case asserts extraction occurred**, not merely that no finding fired. A test asserting "nothing fired" passes when the rule is inert — the failure this whole stage exists to catch.

**Read the record, never the `.log` view.** 1,035 rows, not 694. A filter that drops rows fails loudly; assert rows-read equals rows-in-file.

---

## Step 1 — the query-side findings sidecar

**This is a Layer 5 prerequisite, not a backlog item.** Layer 5's value is diffing the coverage block rather than the prose, and the query path currently has no block to diff. `departure` extraction went 4 → 0 between two v1.2 rounds and nothing caught it, on precisely this path.

`CmdSuite` prints findings, withheld notes, unsoundness and the coverage table to stdout and writes no file. Give it a sidecar in the same shape the chronicle path already emits:

`{rule, scope, span, detail, blocking, fatal}` plus a per-scope `coverage` block carrying `extracted / checked / unresolvable / fired / accounted`.

Scope on the answer path is the question, not a chronicle section.

**Do not retro-fit this to the v1 baseline.** The baseline records `query-coverage-unstructured` as a deficiency and that record stands. The sidecar begins with the next suite run; the v1 answers stay as captured stdout.

**Halt when:** a suite run writes a well-formed sidecar; every rule appears in every scope's coverage block; `extracted == checked + unresolvable` holds throughout; the rules gated off on the answer path (`coverage`, `shape`) do not register as inert.

---

## Step 2 — Layer 2, checker rule unit tests

Per `test-suite-spec2.md`. Positive and negative case per Tier 1 rule, **plus** the lexicon and normalisation tests, which are the part that matters most and the part most likely to be skipped as boilerplate:

```
MARKERS      every entry in PartialMarkers fires 1.1 on one sentence with
             only the marker swapped
COUNTABLES   people / exiles / returns / raids / marriages all yield a count
WORD ORDER   1.3 extracts from "X was killed in 46" and "the murder of X in 47"
POSSESSIVE   "Realsis Leirpu's" normalises to "realsis leirpu"
NON-EMPTY    every rule extracts ≥ 1 assertion from a fixture written to
             contain one
```

Four of round 11's five silent causes were input never reaching a correct rule. None of them is visible in rule logic, so none of them is caught by a test of rule logic.

**Halt when:** every rule has both cases; all five lexicon/normalisation families are covered; every positive case asserts `extracted > 0`; the whole layer runs with no world and no model.

---

## Step 3 — Layer 5, golden diff

Diffs against `baselines/v1/seed-42/`, chronicle and query both.

**`coverage-sound` is a build-failing property, not a report.** Both halves, per rule per scope:

```
ACCOUNTING   extracted == checked + unresolvable
FLOOR        extracted >= previous_extracted
```

Each is trivially satisfiable alone — round 12 satisfied ACCOUNTING by collapsing extraction to 2; round 13 did the reverse. **FLOOR must be in this step's halt list**, because the one time it was specified but omitted from a halt list is the one time a 4 → 0 slipped through.

Diff rules:

- **Any figure that moves is a failure.** The log has not changed.
- **Any rule going non-zero to zero in any scope is failed outright.** One integer comparison; the exact signature of the silent-path family.
- **A sharp extraction drop is failed**, not merely surfaced.
- **Prose changes are reported for review, not failed** — renders legitimately vary.

**The baseline is never updated by this layer.** A failing diff is a failing build; making it pass means fixing the code or deliberately cutting a new baseline.

**Halt when:** the diff runs against the sealed baseline and passes; deliberately perturbing one figure fails it; deliberately zeroing one rule's extraction fails it; the baseline is byte-identical afterwards and `.sealed` still verifies.

---

## Step 4 — Layer 3, the regression corpus

31 cases from `test-suite-spec2.md`, one file per case in the format that spec gives. Both assertions per case: `passage` fires `expect_rule`, `corrected` fires nothing.

**Start with rows 10, 25 and 26** — fixed and then regressed, so the highest-value entries in the suite.

Two cases already exist as PASS tests in `CheckerCorpusTests.cs` (the raid phrase-reader over-read, and raids indexed by place only). Fold them into the corpus format rather than leaving them in a second place.

**Add a 32nd case from this year's work:** the Kebarrow 2–21 `action` extraction. The archived pre-v1.2 sidecar extracted 1 assertion there with reason *"the records hold no raid on that target"*; the current checker extracts 0, correctly, because the malformed phrase was the over-read. Pin it — a case where the *right* answer is zero extraction is a shape the corpus otherwise lacks entirely.

**Halt when:** all 31 encoded, both assertions per case, all green; the three repeat offenders are present; no case fires on its `corrected` form.

---

## Step 5 — Layer 1, dynamics invariants

Ten metrics, thresholds as given in `test-suite-spec2.md`. Across the full panel: **7, 42, 99, 1234, 2025.** A metric holding on 42 alone is an anecdote.

Two hard rules, both from reviewer error rather than code error: read the record not the view, and a filter that drops rows fails loudly.

**One amendment to the spec.** Ranking or thresholding by raw event count under-represents powers that ended — a power destroyed in year 20 had eighteen years to accumulate events where a survivor had fifty. Where a metric involves per-power selection, use a rate or give a floor to any power that held land or was destroyed. This matters again at Stage 8, so getting the habit right here is free.

**Halt when:** all ten metrics assert across all five seeds; row-count assertions hold on every read; a deliberately corrupted record fails the dangling-reference check.

---

## Step 6 — Layer 4, chronicle verified against the log

The largest build, and the one with a structural requirement.

**Layer 4 deliberately duplicates the checker.** The checker decides what enters canon; the suite decides whether the checker works. A checker that silently stops firing is invisible without an independent verifier.

`test-suite-spec2.md` says to state this in a comment so it does not get refactored away. **A comment will not hold** — prompt fixes decay and so do comments, and every lesson in this project points the same way. Make it structural: Layer 4 lives in an assembly that **does not reference `WorldBuilder.Inference`**. Then sharing an implementation is a compile error rather than a code-review judgement. Same move as `CacheOnlyLlmClient` — a guarantee that holds by construction rather than by maintenance.

Verify per section, per the spec: ruler lists (including founding holders from `POLITY.SECESSION` — the source missed until round 8), departure manner with an exhaustive partition, tenure spans clamped at both ends, raid counts split three ways with ownership resolved at event time, battle counts, killing counts split internal/external, marriage counts with the convention stated, every named year for every event type without exception, every proper noun.

**Statistics carry a scope.** Assert a figure quoted inside a reign passage was computed for that reign. This is corpus row 10, which has failed twice.

**Halt when:** Layer 4 runs against the sealed baseline chronicle and agrees with it; the assembly has no reference to `WorldBuilder.Inference`; a deliberately introduced scope error (faction figure inside a reign) is caught.

---

## Wiring

```
wb test checker      layer 2    no world, no model
wb test corpus       layer 3    no world, no model
wb test dynamics     layer 1    record only, five seeds
wb test golden       layer 5    baseline artefacts
wb test chronicle    layer 4    baseline render
wb test all
```

Layers 1–3 fast enough for every commit. All six steps zero-inference.

---

## Two things deliberately not in scope

**Splitting retrieval sets from the planner echo**, and **emitting the question set as data**. Both are real, both are in the backlog, and both are cheap — but both change artefacts the sealed baseline records hashes for. Do them in a round that cuts a new baseline, not in the round that builds the harness against the old one.

## Exit criterion

Per the project's own standing rule: a harness number, not a feeling. Stage 4 is done when a deliberately reintroduced defect from each of the six failure families — count/enumeration, date, scope, coverage-omission, silent-path, regression-against-golden — is caught by the layer that owns it, with no human reading prose.
