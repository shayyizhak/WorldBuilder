# Loop-prompt — archive the v1 golden baseline (seed 42), revision 3

Supersedes `v1-baseline-archive-loop-r2.md`, which aborted correctly at §4a. That
abort was the loop working: it caught a sidecar the tree's own checker does not
produce, and no comparison of the prose could have seen it.

The three decisions r2 escalated are resolved below. Read §0.7 first — it changes
what §4a means.

---

## 0. Prohibitions

Unchanged from r2 except §0.7, which is new.

1. **No regeneration.** No render, no query, no generation. Generation is not
   reproducible run to run; a regenerated artefact is a different artefact.
2. **No inference calls.** Reading the local Ollama manifest store from disk is
   permitted and is not an inference call.
3. **No tidying.** Byte-for-byte copies. No reformatting, re-sorting,
   pretty-printing or whitespace normalisation of archived artefacts.
4. **No overwriting.** Create-only — §5.
5. **No repair.** Missing or malformed source artefact → abort and report.
6. **No retroactive query sidecar.** Do not modify `CmdSuite` to emit a findings
   file and then run it. §2.1 stands.
7. **Re-checking is not generation.** Running the checker over an archived
   `renders.json` through `wb book --check-only` involves no inference and is
   deterministic — proven three times in r2, identical every time. Its output is
   a derived artefact, recomputable at will. This is the distinction §0.1 turns
   on, and it is why the sidecar decision below is not a violation of it.

---

## 1. Prerequisites

**P1 — the tree is committed and clean.** Satisfied at r2 (`a6b5b7ec`, now
rooted at a single flattened repository). The three source edits from r2 —
`Directory.Build.props`, `CacheOnlyLlmClient`, the `--check-only` branch — must
be committed before this runs. Abort if the working tree is dirty.

**P2 — a version exists.** Satisfied: `1.2.0`, and the built assembly reports
`1.2.0+a6b5b7ec…`.

**P3 — a zero-inference checker entry point exists.** Satisfied:
`wb book --check-only`, holding a `CacheOnlyLlmClient` whose `CompleteAsync`
throws. The guarantee is structural rather than observational, which was the
point.

**P4 — `QuerySuite.cs` is pinned `-text`. NEW.** Add the pin to
`.gitattributes`, refresh the working tree, and verify the file hashes
`cb1990f77620467c…` at 9312 bytes — the LF form the repository stores. Abort if
it still hashes the 9478-byte CRLF value.

Without this the baseline records a hash that is a property of the checkout
rather than of the artefact, and a fresh clone cannot reproduce it.

---

## 2. The sidecar decision

**The reproduction is the baseline. The archived sidecar is archived as
history.**

The reasoning, recorded here because the next reader will re-ask it:

`out/` was archived to `archive/2026-08-15-pre-v1.2-generation/` **before any
v1.2 generation run**. The chronicle in that directory is byte-identical to what
came after, which is why it is still the right chronicle. The sidecar beside it
is not — it is pre-v1.2 checker output, and the directory holds a mix.

All five coverage deltas r2 found are the v1.2 generation round's two raid
extraction fixes landing:

- **Raids were indexed by place only**, so a sentence naming the raided *power*
  was told no such raid existed. This is why `action` on 42–51 now resolves and
  checks clean, where it was `unresolvable` with reason *"the records hold no
  raid on that target"*.
- **The raid phrase-reader ran four words past the end of a name**, worked
  example `"hadale killed 16 but"`. That is `e:278`, Y19 — *the Kebarrow Compact
  raids Hadale and kills 16, but takes nothing* — which falls inside the 2–21
  scope. The archived `extracted 1` there was a malformed extraction producing a
  target string matching no raid. Removing it is the fix working.

Both were `checked 0` in the archived sidecar, so no canon decision ever rested
on either, which is why the chronicle reproduces byte-identically. Both are
pinned as PASS tests in `CheckerCorpusTests.cs`.

The `name` → `naming` fold accounts for the two Griwick deltas and is documented
in `RuleNames.cs`. `succession` 2 → 4 is extraction getting broader.

So the reproduction is not a competing artefact. **It is the post-v1.2 sidecar
that was never written to disk.** Pinning the archived file instead would make
the golden anchor two known, fixed, test-covered bugs.

### What to archive

| Filename in archive | Content | Role |
|---|---|---|
| `chronicle-42.findings.json` | the reproduction from `--check-only` | **the anchor** |
| `chronicle-42.findings.pre-v1.2.json` | the archived 50935-byte file, verbatim | history, not an anchor |

The historical copy is kept because it is the evidence for this finding: a tree
internally inconsistent inside its own first commit, where version control
cannot show it. Mark it in the manifest with
`"role": "historical-not-anchor"` and state in `BASELINE.md` that it must never
be used as a diff target.

---

## 3. What to archive

Target: `baselines/v1/seed-42/` (create-only — §5). Sources and hashes are r2's
inventory; re-hash on copy and abort on mismatch.

| Artefact | Source | Note |
|---|---|---|
| `chronicle-42.md` | `archive/2026-08-15-pre-v1.2-generation/` | 22386 B, `9ab9be0b…` |
| `chronicle-42.unverified.md` | same | 4825 B, `42e38aea…` — the 3 held-out sections, evidence exclusion worked |
| `chronicle-42.findings.json` | **produced by §5a**, not copied | the anchor |
| `chronicle-42.findings.pre-v1.2.json` | `archive/…/chronicle-42.findings.json` | 50935 B, `61593ee5…` |
| `renders.json` | `archive/2026-08-15-pre-v1.2-generation/` | 346335 B, `2e198d90…` |
| `answers-final.txt` | `out/` | 5584 B — the v1 query artefact, see §3.1 |
| `retrieval-baseline.txt` | `out/` | 10323 B, contains one generated line — §3.2 |
| `QuerySuite.cs` | `src/WorldBuilder.Inference/` | 9312 B LF after P4 |
| `world-42.jsonl` | `out/` | 616220 B, the record, 1035 events |
| `world-42.log` | `out/` | 102208 B, the view, 694 rows |

**Directory naming.** Record in the manifest that the chronicle came from a
directory named `pre-v1.2-generation`, that the chronicle is nonetheless correct
(byte-identical post-v1.2, per both v1.2 reports), and that the sidecar in the
same directory was *not* — that is the whole point of §2 and it is exactly the
kind of thing a misleading directory name causes twice.

### 3.1 The query sidecar does not exist and is not being manufactured

No code path writes one; `CmdSuite` prints to stdout. `answers-final.txt` is the
v1 query artefact — all 16 answers, the per-answer finding lines (zero on all
sixteen), the coverage table.

Record as **structurally deficient**, in the manifest and in `BASELINE.md`:

> v1's query-side coverage exists only as unstructured captured stdout. It is
> readable but not machine-diffable. A rule going non-zero to zero on the query
> path cannot be detected by a golden diff against this baseline.

This run is the argument for fixing it, made from the other side: the chronicle
path had a machine-readable coverage block, and that block is the only reason
the drift was visible at all. `departure` going 4 → 0 on the query path had no
such block and nothing caught it. Stage 4 backlog; the next baseline gets it.

### 3.2 Retrieval: take the `out/` copy

`out/retrieval-baseline.txt` versus `archive/…/retrieval-42.txt` differ on one
line — Q11's planner echo, `"Hade Commune"` versus `"Hadaie Commune"`. Neither
is correct; the faction is **Hadale Commune**. Retrieved event sets are
identical across all 16.

Take the `out/` copy. Record that the file contains one generated line and is
therefore not fully deterministic.

*Backlog:* split retrieval sets (pure event-ID lists — deterministic, diffable,
zero-inference) from the planner echo. That one line is the entire reason §5b
degrades to skipped.

### 3.3 The question set is source

Copy `QuerySuite.cs` verbatim post-P4. Record that the question set is archived
as source rather than data. Teaching the suite to emit it is Stage 4.

---

## 4. Manifest

`baselines/v1/seed-42/manifest.json`:

```json
{
  "baseline_id": "v1-seed-42",
  "created_utc": "<ISO 8601>",
  "seed": 42,
  "verification": "hand-verified",
  "engine_version": "1.2.0",
  "engine_commit": "<git SHA at archive time>",
  "ruleset_version": null,
  "checker_fingerprint": "<sha256 over the checker sources — see below>",
  "checker_rule_count": 16,
  "checker_rules": ["action", "coined-term", "count-enumeration",
    "count-narration", "coverage", "date", "date-agreement", "departure",
    "naming", "outcome", "partition-sum", "quantity", "shape", "succession",
    "summary-body", "tenure"],
  "inference": {
    "runtime": "ollama",
    "model": "qwen3.6:latest",
    "model_digest": "07d35212591fc27746f0a317c975a6d68754fb38e9053d82e25f06057af28522"
  },
  "deficiencies": [
    "query-coverage-unstructured",
    "retrieval-contains-generated-echo-line"
  ],
  "artefacts": [
    { "filename": "…", "source_path": "…", "sha256": "…", "bytes": 0,
      "role": "anchor | historical-not-anchor | artefact" }
  ]
}
```

**`checker_fingerprint` is new and is the lesson of r2 generalised.** A sidecar
is a pure function of `(renders.json, checker code)`. The archived one could
drift undetected precisely because nothing recorded which checker produced it.
Hash the checker sources — at minimum `FabricationCheck.cs`, `RuleNames.cs`,
`Coverage.cs`, `SelfConsistency.cs`, `Claims.cs` — sorted by path, and store the
digest.

This is Stage 3's cached-render question arriving early in a form nobody
scheduled: not *what happens to cached renders when the ruleset changes* but
*what happens to cached findings*. Same shape. It argues the invalidation rule
should key on the hash of a derived artefact's inputs generally, not on engine
or ruleset version, and not only for renders.

`ruleset_version` may be null; nothing else may be.

`verification: "hand-verified"` is correct **only** for seed 42. Later baselines
for seeds 7, 99, 1234 and 2025 carry `"stability-anchor-only"`.

---

## 5. Verification

**5a. Produce the anchor.** Run `wb book --check-only` against a scratch
directory holding copies of the archived `renders.json` and `world-42.jsonl` —
never against the archive itself. Confirm:

- `chronicle-42.md` reproduces byte-identical to the archived copy
  (`9ab9be0b…`, 22386 B).
- `chronicle-42.unverified.md` reproduces byte-identical (`42e38aea…`, 4825 B).
- The archived `renders.json` is unchanged after the run (`2e198d90…`). A
  re-check rewrites the store if a cached render's machine verdict moves; none
  should.

Abort if any of the three fails. Those are the canon decisions, and they must
not have moved.

The findings file produced is the anchor. Copy it into the archive as
`chronicle-42.findings.json`.

**5b. Repeat.** Run 5a a second time. The two findings outputs must be
byte-identical to each other. Two consecutive identical runs is the evidence
standard; a single pass is a sample. r2 got three, all `972ba60d…`.

**5c. Do not compare against the pre-v1.2 sidecar.** It is expected to differ,
for the reasons in §2. Record the delta count in the archive report — five
coverage deltas across 3 of 15 scopes, 163 findings unchanged, 8 real, 4
blocking — as evidence the divergence is understood and bounded, not as a check
that must pass.

**5d. Retrieval reproduction — pre-declared skipped.** Record
`"retrieval_reproduction": "skipped-requires-inference"` per §3.2. Do not
attempt it.

---

## 6. Create-only enforcement

If `baselines/v1/seed-42/` exists, **abort immediately**. Do not merge, add
missing files, overwrite, or create a similarly-named sibling.

A new baseline requires moving the existing directory aside under a new name
first. This is what stops a floor moving by rerun.

---

## 7. Declaration

Write `baselines/v1/seed-42/BASELINE.md`:

- **What this is:** the v1 golden baseline for seed 42. Chronicle figures, ruler
  lists, tenure spans, counts and named years verified by hand; query suite
  16/16, zero secret leakage, zero fatal findings.
- **What it is not:** not regeneratable. Generation is not reproducible run to
  run. If it is lost, it is lost.
- **What is verified versus what is derived.** The prose is hand-verified. The
  findings sidecar is *derived* — recomputable from `renders.json` at zero
  inference cost, and reproduced here rather than copied. That distinction is
  why the sidecar could be replaced without weakening the baseline.
- **The pre-v1.2 sidecar** is present as history and must never be used as a
  diff target. State why: it was written by an older checker carrying two
  extraction bugs, both since fixed and both covered by tests, and it was
  committed alongside the checker that supersedes it as though the two matched.
- **The imported tree was internally inconsistent inside its own first commit.**
  Any future archaeology assuming commit one is a coherent snapshot will be
  wrong.
- **Known deficiencies**, verbatim from §3.1 and §3.2.
- **The 3 unverified sections** are archived as evidence of correct exclusion. If
  a future ruleset admits them to canon, that must surface as a diff.
- **Replacement policy:** create-only, per §6.

Then write `.sealed` containing the sha256 of `manifest.json`.

---

## 8. Halt conditions

1. P1–P4 satisfied.
2. `baselines/v1/seed-42/` exists and contains every §3 artefact.
3. Every archived file non-empty; sha256 matches its manifest entry.
4. `QuerySuite.cs` hashes `cb1990f77620467c…` at 9312 bytes.
5. `manifest.json` complete — no placeholders, null only on `ruleset_version`.
6. `checker_fingerprint` present and non-empty.
7. `checker_rule_count` is 16 with `checker_rules` enumerated.
8. 5a's three byte-identity checks passed.
9. 5b's two findings outputs byte-identical to each other.
10. 5d recorded as skipped.
11. `BASELINE.md` and `.sealed` exist; `.sealed` matches `manifest.json`.
12. `archive-report.md` written into the archive: every file with its source,
    all verification results, the §5c delta count, anything unexpected.

## 9. Abort conditions

- Any prerequisite unmet.
- Working tree dirty.
- `baselines/v1/seed-42/` already exists.
- Any §3 artefact missing, empty, or malformed.
- A hash mismatch against r2's inventory.
- `RuleNames.All` does not yield 16.
- Any of 5a's three byte-identity checks fails — chronicle, unverified file, or
  `renders.json` unchanged.
- 5b's two runs differ.
- Any step appears to require inference or regeneration.

On abort, leave the filesystem as found; delete any partial archive directory and
write the report to `out/`.
