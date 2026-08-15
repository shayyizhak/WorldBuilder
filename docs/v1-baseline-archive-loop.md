# Loop-prompt — archive the v1 golden baseline (seed 42)

Run unattended until every halt condition holds or an abort triggers.

This task is a **copy job with verification**. It is not a build, and it is not a
render. Read the prohibitions before doing anything.

---

## 0. Prohibitions

These are absolute. Violating any of them invalidates the entire artefact.

1. **No regeneration.** Do not re-run the render layer. Do not re-run the query
   layer. Do not re-run generation of any kind. The baseline is the artefacts
   that already exist on disk — the ones hand-verified across the v1 rounds.
   Generation is not reproducible run to run (proven at v1.2: identical request
   body, different classification), so a regenerated artefact is a *different*
   artefact than the verified one.
2. **No inference calls.** Zero calls to Ollama for the duration of this task.
   If any step appears to require inference, that step is wrong — abort and
   report instead.
3. **No tidying.** Do not reformat, reindent, re-sort, normalise line endings,
   pretty-print JSON, or strip trailing whitespace in any archived artefact.
   Byte-for-byte copies only.
4. **No overwriting.** If the target archive directory already exists, abort.
   See §5.
5. **No repair.** If a source artefact is missing or malformed, abort and
   report. Do not reconstruct it, do not substitute a similar file, do not
   generate a replacement.

---

## 1. What to archive

Target directory: `baselines/v1/seed-42/` (create-only — see §5).

Copy the following from their current locations, preserving filenames:

| Artefact | Notes |
|---|---|
| `chronicle-42.md` | the canon chronicle |
| `chronicle-42.unverified.md` | excluded passages — part of the baseline, not a failure |
| `findings.json` (chronicle) | chronicle sidecar |
| `findings.json` (query) | query sidecar — keep both, disambiguate by filename if they collide |
| `renders.json` | the render cache |
| query suite answers | all 16 |
| retrieval sets | all 16 |
| the query suite question set | the questions themselves, so the answers are interpretable |
| `.log` view and the full record | both — the record is the 1,035-row artefact, the `.log` is the 694-row view |

If any of these live somewhere unexpected, locate them and record the source
path in the manifest. Do not guess: if an artefact cannot be located with
confidence, abort per §0.5.

---

## 2. Manifest

Write `baselines/v1/seed-42/manifest.json`:

```json
{
  "baseline_id": "v1-seed-42",
  "created_utc": "<ISO 8601>",
  "seed": 42,
  "verification": "hand-verified",
  "engine_version": "<from build metadata>",
  "engine_commit": "<git SHA, plus dirty flag if the tree is not clean>",
  "ruleset_version": "<current value, or null if not yet introduced>",
  "checker_rule_count": 17,
  "inference": {
    "runtime": "ollama",
    "runtime_version": "<version string>",
    "model": "qwen3.6:latest",
    "model_digest": "<ollama digest>"
  },
  "artefacts": [
    {
      "filename": "<name in archive>",
      "source_path": "<where it was copied from>",
      "sha256": "<hex>",
      "bytes": 0
    }
  ]
}
```

Notes:

- `verification: "hand-verified"` is correct **only** for seed 42. Any later
  baseline for seeds 7, 99, 1234 or 2025 must carry
  `"verification": "stability-anchor-only"`, because no human has verified those
  artefacts. A golden diff needs its anchor to be stable, not correct — but the
  distinction must be legible to whoever reads it in a year.
- Record the inference runtime and model digest even though this task makes no
  inference calls. The baseline was *produced* under them, and Stage 3's
  world-file header will want exactly these fields.
- If the git tree is dirty, record that fact. Do not clean it.

---

## 3. Verification (the part that earns the archive)

Two checks, both zero-inference, both must pass.

**3a. Checker reproduction.** Re-run the checker against the archived
`renders.json` and compare the produced findings to the archived chronicle
sidecar. Require **byte-identical** output. This path involves no generation, so
it is genuinely deterministic; a mismatch is a bug, not variance. If it
mismatches, abort and report the diff — do not overwrite either file.

**3b. Retrieval reproduction.** Reproduce the retrieval sets for the 16 suite
questions from the archived record and compare to the archived retrieval sets.
Require byte-identical. Retrieval was byte-identical across runs at v1.2, so
this is a regression check on that property.

If 3b cannot be performed without invoking the planner (i.e. without inference),
skip it, and record `"retrieval_reproduction": "skipped-requires-inference"` in
the run report. Do not invoke inference to satisfy it.

**3c. Repeat.** Run 3a (and 3b if performed) a second time. Two consecutive
identical runs is the evidence standard on this project. A single pass is a
sample.

---

## 4. Declaration

After §3 passes, write `baselines/v1/seed-42/BASELINE.md` containing:

- What this is: the v1 golden baseline for seed 42, hand-verified across the v1
  render and query rounds.
- What it is not: it is not a regeneratable artefact. It cannot be reproduced by
  re-running generation, because generation is not reproducible run to run. If
  it is lost, it is lost.
- Scope of verification: chronicle figures, ruler lists, tenure spans, counts
  and named years were verified by hand; the query suite was 16/16 with zero
  secret leakage and zero fatal findings. The `.unverified.md` passages are
  archived as evidence of correct exclusion, not as canon.
- Replacement policy: this directory is create-only. To establish a new
  baseline, move this directory aside under a new name first. Nothing in the
  toolchain may overwrite it.
- The manifest hash list, or a pointer to it.

Then write `baselines/v1/seed-42/.sealed` containing the sha256 of
`manifest.json`. Any future tooling that reads a baseline should verify this
before trusting the contents.

---

## 5. Create-only enforcement

Before copying anything: if `baselines/v1/seed-42/` exists, **abort
immediately** with the message that a baseline already exists and must be moved
aside deliberately. Do not merge, do not add missing files to it, do not
overwrite, do not create `seed-42-v2/` or any similarly-named sibling as a
workaround.

This is the mechanism that stops a floor from moving by rerun. It replaces the
human gate; it does not weaken it.

---

## 6. Halt conditions

Halt successfully when **all** of the following hold:

1. `baselines/v1/seed-42/` exists and contains every artefact in §1.
2. Every archived file is non-empty and its sha256 matches its manifest entry.
3. `manifest.json` is complete — no `null` or placeholder values except
   `ruleset_version`, which may legitimately be null.
4. §3a passed twice with byte-identical output both times.
5. §3b passed twice, or is recorded as skipped-requires-inference.
6. `BASELINE.md` and `.sealed` exist and `.sealed` matches `manifest.json`.
7. A run report has been written to `baselines/v1/seed-42/archive-report.md`
   listing every file copied with its source path, both verification results,
   and anything encountered that was unexpected.

## 7. Abort conditions

Abort and report — do not attempt to resolve — on any of:

- The archive directory already exists (§5).
- Any §1 artefact cannot be located, or is empty, or is malformed.
- §3a produces any diff, however small.
- The two repeat runs in §3c differ from each other.
- Any step appears to require inference.
- Any step appears to require regenerating an artefact.

On abort, leave the filesystem as found. If a partial archive directory was
created before the abort, delete it — a partial baseline that looks complete is
worse than none.
