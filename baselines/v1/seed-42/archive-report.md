# v1 baseline archive, seed 42 — **COMPLETE**

Executes the loop prompt in
[docs/v1-baseline-archive-loop-r3.md](../../../docs/v1-baseline-archive-loop-r3.md).
Supersedes the r2 run, which aborted at §4a and whose report is at
[out/v1-baseline-archive-report-r2.md](../../../out/v1-baseline-archive-report-r2.md).

**Zero inference calls.** Both verification runs went through
`wb book --check-only`, which constructs no client at all — a cache miss throws
rather than generating. The model digest was read from the manifest file on disk.
Ollama was never contacted.

**Nothing was regenerated.** Nine of the ten artefacts are byte-for-byte copies.
The tenth, the findings anchor, was recomputed from the archived `renders.json`
by the checker — no inference, deterministic, and identical across both runs.

---

## Prerequisites

| | | |
|---|---|---|
| **P1** | tree committed and clean | four commits made this run; `git status` clean at archive time, HEAD `62689ca` |
| **P2** | version exists | `1.2.0`; built assembly reports `1.2.0+62689cab…` |
| **P3** | zero-inference checker entry point | `wb book --check-only`, holding a `CacheOnlyLlmClient` whose `CompleteAsync` throws |
| **P4** | `QuerySuite.cs` pinned `-text` | pinned in commit `c368a6f`; working tree refreshed; file now 9312 B, `cb1990f77620467c…` |

P1 required committing r2's three source edits, which had been left uncommitted
for review. Four commits, all on `main`, none pushed:

```
62689ca  Add the r2 and r3 baseline archive prompts
c368a6f  Pin the question set too, so its hash is a property of the artefact
be7e795  Add a checker entry point that cannot generate
fcd3641  Give the engine a version, so an artefact can name what produced it
```

P4 closed the one real gap r2 left. Before the pin, `QuerySuite.cs` hashed
`4d4a6df0…` at 9478 bytes on disk and `cb1990f7…` at 9312 in the repository, so
either figure would have recorded a property of the checkout. It now hashes the
9312-byte LF form on disk, and git reports it unmodified — the two agree.

---

## What was archived

Ten files, each re-hashed after copying and checked against r2's inventory. No
mismatches.

| Filename | Source | Bytes | sha256 | Role |
|---|---|---|---|---|
| `chronicle-42.md` | `archive/2026-08-15-pre-v1.2-generation/` | 22386 | `9ab9be0b359c2a6b…` | artefact |
| `chronicle-42.unverified.md` | same | 4825 | `42e38aea3d5edfdf…` | artefact |
| `chronicle-42.findings.json` | **produced by 5a** | 50778 | `972ba60df4eb1f95…` | **anchor** |
| `chronicle-42.findings.pre-v1.2.json` | `archive/…/chronicle-42.findings.json` | 50935 | `61593ee507c3e417…` | historical-not-anchor |
| `renders.json` | `archive/2026-08-15-pre-v1.2-generation/` | 346335 | `2e198d907cfd5ca7…` | artefact |
| `answers-final.txt` | `out/` | 5584 | `3d5461797110b7e5…` | artefact |
| `retrieval-baseline.txt` | `out/` | 10323 | `79d0c27035f051eb…` | artefact |
| `QuerySuite.cs` | `src/WorldBuilder.Inference/` | 9312 | `cb1990f77620467c…` | artefact |
| `world-42.jsonl` | `out/` | 616220 | `c5ef7936c783bc2f…` | artefact |
| `world-42.log` | `out/` | 102208 | `7c9013ed91970ec1…` | artefact |

Plus `manifest.json`, `BASELINE.md`, `.sealed` and this report.

`.sealed` holds `0695347413517d403320f0caacb68a82fedc89b5868054c77fad3360c42d8bb7`,
the sha256 of `manifest.json`, verified after writing.

---

## Verification

**5a — produce the anchor. All three byte-identity checks passed.**

| Check | Result |
|---|---|
| `chronicle-42.md` reproduces byte-identical | PASS — `9ab9be0b359c2a6b…`, 22386 B |
| `chronicle-42.unverified.md` reproduces byte-identical | PASS — `42e38aea3d5edfdf…`, 4825 B |
| archived `renders.json` unchanged by the run | PASS — `2e198d907cfd5ca7…` before and after |

Those are the canon decisions, and none of them moved. 15 passages, 8 suspect
tokens, 3 held out — the same three. The third check matters because a re-check
rewrites the store whenever a cached render's machine verdict moves; none moved.

Both runs used scratch copies of `renders.json` and `world-42.jsonl`, never the
archive itself:

```
wb book --out <scratch> --check-only --factions all
```

**5b — repeat. PASS.** The two findings outputs are byte-identical to each other,
both `972ba60df4eb1f95…` at 50778 bytes. With r2's three runs that is **five
identical reproductions across two separate builds**, one of them before the
repository was flattened and one after.

**5c — divergence from the pre-v1.2 sidecar, recorded not checked.** As §2 of the
prompt predicts, and bounded exactly as r2 measured:

- **5 coverage deltas across 3 of 15 scopes.**
- **163 findings unchanged** — 8 real, 4 blocking, same kinds against the same
  scopes in the same order.
- Two deltas are the two raid-extraction fixes: `action` on "The Kebarrow
  Compact, 42–51" now resolves and checks clean where it was `unresolvable`
  with reason *"the records hold no raid on that target"*; `action` on "The
  Kebarrow Compact, 2–21" is no longer extracted at all, the malformed
  extraction from `e:278` Y19 having been removed.
- Two are the `name` → `naming` fold, which is why the old file carries 17 rule
  keys on the Griwick scope and the anchor carries 16 on every scope.
- One is `succession` 2 → 4 on 42–51, extraction getting broader.

Both bug sites were `checked 0` in the old sidecar, so no canon decision rested
on either — which is precisely why the chronicle reproduces byte-identically
while the sidecar does not.

**5d — retrieval reproduction: `skipped-requires-inference`.** Pre-declared, not
attempted.

---

## `checker_fingerprint`

New this revision, and the generalisation of what r2 caught. A findings sidecar
is a pure function of `(renders.json, checker code)`; the archived one could
drift undetected precisely because nothing recorded which checker produced it.

```
60f5b325bf6a8a9728f5d817c963c47cd59c9fd04a47b78d5773fe670f221a03
```

Method, recorded in the manifest so it is reproducible: for each checker source,
the sha256 of its content **as stored in git** at `engine_commit` — LF, and so
independent of the checkout — one `path  sha256` line per file, sorted by path;
the fingerprint is the sha256 of that listing.

Computing it over git blob content rather than working-tree bytes is deliberate.
Hashing the files on disk would have made the fingerprint CRLF-dependent, which
is the exact defect P4 exists to close, reintroduced one field over.

The five inputs, all at commit `62689ca`:

| File | sha256 |
|---|---|
| `Claims.cs` | `70ca22ffc8d7183c…` |
| `Coverage.cs` | `04475289997bfcc1…` |
| `FabricationCheck.cs` | `ecab52a203515a12…` |
| `RuleNames.cs` | `8fcc50fc0e392acc…` |
| `SelfConsistency.cs` | `770c358d5b5e5306…` |

That is the prompt's stated minimum. Worth flagging for whoever generalises this:
the sidecar's content also depends on things outside the set — pack construction
(`ContextPack`, `PackDigest`), the prompt version that forms the cache key, and
the `Json` writer in `CommandLine.cs` that serialises the file. A change in any
of those would move the output without moving the fingerprint. The set is a floor,
not a closure, and the manifest names its members explicitly so the next reader
knows which.

---

## Checks that had to pass on the way

- **`RuleNames.All` yields 16**, confirmed two ways: by enumerating the distinct
  owners in source, and from the produced sidecar, where all 15 scopes report the
  same 16 rule names. They match the manifest's `checker_rules` list exactly.
- **Create-only held.** `baselines/v1/seed-42/` did not exist before this run.
- **Every archived file is non-empty** and hashes as its manifest entry records.
- **The manifest carries no placeholder or null value** except `ruleset_version`,
  and parses as JSON.

---

## Anything unexpected

**One.** The first attempt at the 5a/5b comparison used a PowerShell helper
function named `H`, which collided with the built-in alias for `Get-History`. The
hash variables came back empty, and the comparison of two empty strings reported
`PASS` on every check. The byte counts shown alongside were real, which is what
made it look plausible.

Caught and rerun with explicit `Get-FileHash` calls; every result in this report
comes from the rerun. Recorded because it is the same failure mode this whole
baseline exists to guard against — a check that passes without having examined
anything — and because it happened *inside the verification step of an
anti-silent-failure exercise*, which is worth a moment's humility.

## What this baseline still does not cover

Unchanged from r2, and both already in the backlog:

1. **The query path has no machine-diffable coverage block.** Deficiency
   `query-coverage-unstructured`. A rule going non-zero to zero there cannot be
   detected by a golden diff against this baseline — the failure that already
   happened once, with `departure` 4 → 0.
2. **`retrieval-baseline.txt` contains one generated line**, which is the entire
   reason 5d is permanently skipped rather than permanently runnable.
