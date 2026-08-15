# Loop-prompt — archive the v1 golden baseline (seed 42), revision 2

Supersedes `v1-baseline-archive-loop.md`, which aborted correctly. Five defects
in that document are fixed here; three of them were errors in the prompt, not in
the tree. The abort report is the authority on what exists — this revision does
not re-ask questions it already answered.

Run unattended until every halt condition holds or an abort triggers.

This task is a **copy job with verification**. It is not a build, and it is not
a render.

---

## 0. Prohibitions

Absolute. Violating any invalidates the artefact.

1. **No regeneration.** Do not re-run render, query, or generation of any kind.
   The baseline is what already exists on disk — the artefacts hand-verified
   across the v1 rounds. Generation is not reproducible run to run (proven at
   v1.2), so a regenerated artefact is a *different* artefact.
2. **No inference calls.** Zero completions for the duration. Reading the local
   Ollama manifest store from disk is permitted and is not an inference call;
   querying the daemon is not needed for anything here.
3. **No tidying.** No reformatting, reindenting, re-sorting, line-ending
   normalisation, JSON pretty-printing or whitespace stripping in any archived
   artefact. Byte-for-byte copies.
4. **No overwriting.** Create-only. See §5.
5. **No repair.** If a source artefact is missing or malformed, abort and
   report. Do not reconstruct, substitute or generate a replacement.
6. **No retroactive sidecar.** Specifically: do not modify `CmdSuite` to emit a
   query-side findings file and then run it. That produces an artefact no human
   has verified, under a model whose output is not reproducible. The gap is
   recorded honestly instead — see §1 and §4.

---

## 1. Prerequisites

Check all three before touching anything. Each is minutes of work, and each
exists because revision 1 could not fill a required field.

**P1 — the tree is committed.** The repository must have at least one commit and
a clean working tree. Abort if `git rev-parse HEAD` fails.

*Rationale, and it is the important one:* this baseline is hand-verified and
unreproducible by construction. Until it is under version control it exists in
exactly one place. Archiving an untracked tree is putting a lock on a door with
no walls.

If the tree is uncommitted, that is the abort message, and it is a two-minute
fix, not a blocker to escalate.

**P2 — a version exists.** `Directory.Build.props` must carry a `Version` (and
ideally `InformationalVersion`). Abort if `engine_version` cannot be read from
build metadata.

This is Stage 3's first concrete act arriving early. `v1.2.0` is the correct
value.

**P3 — a zero-inference checker entry point exists.** `wb book` opens with
`Warm()`, which sends a real completion before any cached section is read.
Revision 1's §3a was therefore not performable under §0.2 — a spec bug.

Required: a `--no-warm` flag, or a checker-only entry point that never
constructs an inference client. Abort if neither exists.

Do **not** substitute the dead-endpoint workaround here. Pointing `--endpoint`
at a closed port does make the step provably inference-free, and it is a genuinely
good *test* of render-cache completeness — worth keeping as one. It is not the
archive path, because it makes correctness depend on a misconfiguration.

---

## 2. What to archive

Target: `baselines/v1/seed-42/` (create-only — §5).

Locations below are from the abort report's inventory and were located with
confidence. Re-hash on copy and compare against the report's values where given;
a mismatch is an abort.

| Artefact | Source | Note |
|---|---|---|
| `chronicle-42.md` | `archive/2026-08-15-pre-v1.2-generation/` | see naming note below |
| `chronicle-42.unverified.md` | same | the 3 held-out sections — archived as evidence exclusion worked, not as failure |
| `chronicle-42.findings.json` | same | chronicle sidecar, 15 scopes |
| `renders.json` | same | 198 records, one per line |
| `answers-final.txt` | `out/` | **the v1 query artefact** — see §2.1 |
| `retrieval-baseline.txt` | `out/` | take the `out/` copy, not the archive copy — see §2.2 |
| `QuerySuite.cs` | `src/WorldBuilder.Inference/` | the question set, as source — see §2.3 |
| `world-42.jsonl` | `out/` | the record, 1035 events + header |
| `world-42.log` | `out/` | the view, 694 rows |

`out/` and `archive/` hold byte-identical copies of both world files; either is
fine, take `out/`.

**Directory naming.** The chronicle lives only under a directory named
`pre-v1.2-generation`. It is nonetheless the baseline: both v1.2 reports state
the post-v1.2 chronicle is byte-identical — same 8 suspect tokens, same 3
sections held out — and no chronicle was written to `out/` afterwards. Record
this in the manifest's `notes` field so the next reader does not have to
re-derive it.

### 2.1 The query sidecar does not exist, and is not being manufactured

Revision 1 required a `findings.json` on the query path. No such file exists and
no code path writes one: `CmdSuite` prints findings, withheld notes, unsoundness
and the coverage table to stdout and writes nothing.

`answers-final.txt` carries the same information as captured stdout — all 16
answers, per-answer finding lines (zero on all sixteen), and the coverage table.
It is the v1 query artefact. Archive it as that.

It must be recorded as **structurally deficient**, in the manifest and in
BASELINE.md, in these terms:

> v1's query-side coverage exists only as unstructured captured stdout. It is
> readable but not machine-diffable. A rule going non-zero to zero on the query
> path cannot be detected by a golden diff against this baseline.

That is not a footnote. It is a plain-language statement of a known live
failure: `departure` extraction went 4 → 0 between two v1.2 rounds and nothing
caught it. Layer 5 of the test suite spec calls for diffing the coverage block
precisely because that transition is the silent-path signature — and on the
query path there has never been a block to diff.

The fix — have `CmdSuite` write a sidecar with the same
`{rule, scope, span, detail, blocking, fatal}` shape plus the per-scope coverage
block — belongs in the Stage 4 backlog. The *next* baseline gets it. This one
records its absence.

### 2.2 Retrieval: take the newer copy

Two files exist and differ on one line — question 11's planner echo,
`"Hadaie Commune"` in `archive/…/retrieval-42.txt` versus `"Hade Commune"` in
`out/retrieval-baseline.txt`. The retrieved event sets are identical across all
16.

Neither echo is correct: the faction is **Hadale Commune**. The planner mistyped
a verbatim field in both runs, differently. That is the known slip rate, and Q11
retrieved correctly regardless, which is resolve-against-the-record working as
designed.

Take the `out/` copy. Record in the manifest that the file contains one
generated line and is therefore not fully deterministic.

*Backlog item, not for this task:* split retrieval sets (pure event-ID lists —
deterministic, diffable, zero-inference, permanently checkable) from the planner
echo (a generation artefact). That single echo line is the entire reason §4b
below degrades to skipped. Splitting them converts a permanently-skipped check
into a permanently-runnable one.

### 2.3 The question set is source code

`QuerySuite.ForSeed42` is a C# literal list carrying each question's text,
expectation and note. Copy the `.cs` file verbatim into the archive and record
in the manifest that the question set is archived as source rather than data.

Teaching the suite to emit the question set as JSON is a Stage 4 item. Do not do
it here.

---

## 3. Manifest

Write `baselines/v1/seed-42/manifest.json`:

```json
{
  "baseline_id": "v1-seed-42",
  "created_utc": "<ISO 8601>",
  "seed": 42,
  "verification": "hand-verified",
  "engine_version": "<from Directory.Build.props — P2>",
  "engine_commit": "<git SHA — P1>",
  "ruleset_version": null,
  "checker_rule_count": 16,
  "checker_rules": ["action", "coined-term", "count-enumeration",
    "count-narration", "coverage", "date", "date-agreement", "departure",
    "naming", "outcome", "partition-sum", "quantity", "shape", "succession",
    "summary-body", "tenure"],
  "inference": {
    "runtime": "ollama",
    "model": "qwen3.6:latest",
    "model_digest": "<from the local manifest store on disk>"
  },
  "deficiencies": [
    "query-coverage-unstructured",
    "retrieval-contains-generated-echo-line"
  ],
  "notes": [
    "Chronicle sourced from a directory named pre-v1.2-generation; it is the baseline — post-v1.2 output is byte-identical per both v1.2 reports.",
    "Question set archived as C# source (QuerySuite.cs), not as data."
  ],
  "artefacts": [
    {
      "filename": "<name in archive>",
      "source_path": "<copied from>",
      "sha256": "<hex>",
      "bytes": 0
    }
  ]
}
```

**`checker_rule_count` is 16.** Revision 1 said 17. That was wrong, and it came
from a stale figure in a reference document — `unsupported-link` maps onto
`action` rather than adding a rule. Enumerate `RuleNames.All` and abort if the
count is not 16; if the code has legitimately changed since the report, that is
a finding worth stopping for.

`ruleset_version` may be null; nothing else may be.

`verification: "hand-verified"` is correct **only** for seed 42. Baselines for
seeds 7, 99, 1234 and 2025 must carry `"stability-anchor-only"`. A golden diff
needs its anchor stable, not correct — but the distinction must stay legible.

---

## 4. Verification

**4a. Checker reproduction.** Re-run the checker against the archived
`renders.json` via the P3 zero-inference entry point. Compare to the archived
`chronicle-42.findings.json`. Require byte-identical. Every section hits the
render cache; only the check re-runs. A mismatch is a bug, not variance — abort
and report the diff, overwrite nothing.

**4b. Retrieval reproduction — pre-declared skipped.** Reproducing
`retrieval-baseline.txt` requires the planner, because of the echo line (§2.2).
Record `"retrieval_reproduction": "skipped-requires-inference"` and do not
attempt it. Do not invoke inference to satisfy it.

**4c. Repeat.** Run 4a a second time. Two consecutive identical runs is the
evidence standard here; a single pass is a sample.

---

## 5. Create-only enforcement

Before copying: if `baselines/v1/seed-42/` exists, **abort immediately**. Do not
merge, do not add missing files, do not overwrite, do not create a
similarly-named sibling as a workaround.

To establish a new baseline later, the existing directory must be moved aside
under a new name first. This is the mechanism that stops a floor moving by
rerun.

---

## 6. Declaration

After §4, write `baselines/v1/seed-42/BASELINE.md`:

- **What this is:** the v1 golden baseline for seed 42, hand-verified across the
  v1 render and query rounds. Chronicle figures, ruler lists, tenure spans,
  counts and named years verified by hand; query suite 16/16 with zero secret
  leakage and zero fatal findings.
- **What it is not:** not regeneratable. It cannot be reproduced by re-running
  generation, because generation is not reproducible run to run. If it is lost,
  it is lost.
- **Known deficiencies**, verbatim from §2.1 and §2.2 — the unstructured query
  coverage and the generated echo line. State them plainly; a baseline whose
  weaknesses are undocumented is worse than one with none, because it will be
  trusted uniformly.
- **The 3 unverified sections** are archived as evidence of correct exclusion.
  If a future ruleset admits them to canon, that must surface as a diff.
- **Replacement policy:** create-only, per §5.
- Pointer to the manifest hash list.

Then write `.sealed` containing the sha256 of `manifest.json`. Future tooling
reading a baseline should verify this before trusting the contents.

---

## 7. Halt conditions

Halt successfully when all hold:

1. P1–P3 satisfied.
2. `baselines/v1/seed-42/` exists and contains every §2 artefact.
3. Every archived file is non-empty and its sha256 matches its manifest entry.
4. `manifest.json` complete — no placeholder or null values except
   `ruleset_version`.
5. `checker_rule_count` is 16 and `checker_rules` enumerates them.
6. 4a passed twice, byte-identical both times.
7. 4b recorded as skipped-requires-inference.
8. `BASELINE.md` and `.sealed` exist; `.sealed` matches `manifest.json`.
9. `baselines/v1/seed-42/archive-report.md` written: every file copied with its
   source path, both verification results, and anything unexpected.

## 8. Abort conditions

Abort and report — do not resolve — on any of:

- Any prerequisite unmet (§1).
- The archive directory already exists (§5).
- Any §2 artefact missing, empty, or malformed.
- A hash mismatch against the abort report's inventory values.
- `RuleNames.All` does not yield 16 rules.
- 4a produces any diff, however small.
- The two 4c runs differ from each other.
- Any step appears to require inference or regeneration.

On abort, leave the filesystem as found. Delete any partial archive directory —
a partial baseline that looks complete is worse than none. Write the report to
`out/` rather than into the archive path.
