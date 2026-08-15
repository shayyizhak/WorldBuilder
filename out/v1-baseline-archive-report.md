# v1 baseline archive, seed 42 — **ABORTED**

Answers the loop prompt in [docs/v1-baseline-archive-loop.md](../docs/v1-baseline-archive-loop.md).

**Nothing was copied, created, moved or modified.** `baselines/` does not exist and was never
created. The abort was reached during §1 location, before the first copy, so there is no partial
archive to delete. This report is the only file written, and it is in `out/` rather than
`baselines/v1/seed-42/archive-report.md` because §7 requires the filesystem be left as found.

Zero calls to Ollama were made. No generation of any kind was run.

---

## Why it aborted

Two independent §7 conditions, both on the same artefact.

### `findings.json` (query) does not exist, and never has

§1 requires a query-side findings sidecar alongside the chronicle one. There is no such file
anywhere in the tree, and no code path that would write one:

- The only writer of a `*.findings.json` in the codebase is the chronicle path —
  [CommandLine.cs:1008](src/WorldBuilder.Cli/CommandLine.cs#L1008), inside `CmdBook`. It produced
  `chronicle-42.findings.json`, which is present and intact.
- `CmdSuite` — [CommandLine.cs:1109-1196](src/WorldBuilder.Cli/CommandLine.cs#L1109-L1196) — is the
  query path. It prints findings, withheld notes, unsoundness and the coverage table to stdout and
  **writes no file at all**. Every `File.WriteAllText` site in `src/` was checked; there are nine,
  and none is on the query path.
- The nearest thing on disk is [out/answers-final.txt](out/answers-final.txt): captured suite
  stdout carrying all 16 answers, the per-answer finding lines (zero on all sixteen) and the
  coverage table. It holds the same information in prose. It is **not** the sidecar, and §0.5
  forbids substituting a similar file.

So: **§7 — "any §1 artefact cannot be located."**

The only way to produce the sidecar is to re-run `wb suite`, which is generation against the model.
That is **§7 — "any step appears to require inference"**, and §0.2 besides.

Both conditions say abort and report rather than resolve. Neither is repairable inside this task's
prohibitions: it needs a code change (have `CmdSuite` write its findings and coverage to a sidecar)
and then a suite run, which is a generation round, not an archive job — and a suite run today would
produce a *different* artefact from the hand-verified one, for exactly the reason §0.1 gives.

---

## Blockers found on the way, which would have stopped the halt anyway

Recorded because fixing only the sidecar will not get this loop to green.

**1. §3a cannot be performed with zero Ollama calls as the toolchain stands.**
`wb book` opens with `Warm()` — [CommandLine.cs:779-795](src/WorldBuilder.Cli/CommandLine.cs#L779-L795)
— which sends a real completion (`"Reply with one word."`) before any section is rendered. Re-running
the checker from an archived `renders.json` is otherwise genuinely inference-free: every section hits
the render cache and only the check re-runs. But the command as invoked makes one inference call,
which §0.2 forbids in terms.

`Warm` swallows `HttpRequestException`/`TaskCanceledException`, so pointing `--endpoint` at a dead
port makes the step zero-inference *and* fails loudly on any cache miss — which is the enforcement
§0.1 wants. That is a workaround, not a fix. The fix is a `--no-warm` flag or a checker-only entry
point that never constructs a client.

**2. `engine_commit` cannot be filled.** The repository has no commits — `master` has never had one,
and every path in the tree is untracked. This is not the dirty-tree case §2 anticipates; there is no
SHA to record, dirty or otherwise. Halt condition 3 permits `null` only for `ruleset_version`.

**3. `engine_version` cannot be filled.** `Directory.Build.props` carries no `Version`,
`AssemblyVersion` or `InformationalVersion`, and nothing else sets one. There is no build metadata
to read.

**4. `checker_rule_count: 17` is wrong by one — the real count is 16.** `RuleNames.All` returns the
distinct owners in [RuleNames.cs](src/WorldBuilder.Inference/RuleNames.cs): `action`, `coined-term`,
`count-enumeration`, `count-narration`, `coverage`, `date`, `date-agreement`, `departure`, `naming`,
`outcome`, `partition-sum`, `quantity`, `shape`, `succession`, `summary-body`, `tenure`. The
`unsupported-link` kind added in the answer-quality round maps onto `action` rather than adding a
seventeenth rule, which is likely where 17 came from. Both v1.2 coverage tables agree: 14 rules on
the answer path plus `coverage` and `shape`, gated off there as completeness rules.

**5. §2 and §0.2 contradict each other on the inference block.** `runtime_version` and
`model_digest` come from the Ollama daemon (`ollama list`, `/api/tags`), which is a call to Ollama
even though it is not inference. Not attempted. Worth noting the digest can be read from the local
manifest store on disk without touching the daemon, which satisfies both clauses.

---

## Inventory as located, so the search need not be repeated

Everything below was located with confidence and hashed. Sizes in bytes; sha256 truncated to 16 for
reading — full values are reproducible with `sha256sum`.

| §1 artefact | located at | bytes | sha256 |
|---|---|---|---|
| `chronicle-42.md` | `archive/2026-08-15-pre-v1.2-generation/` | 22386 | `9ab9be0b359c2a6b…` |
| `chronicle-42.unverified.md` | `archive/2026-08-15-pre-v1.2-generation/` | 4825 | `42e38aea3d5edfdf…` |
| `findings.json` (chronicle) | `archive/2026-08-15-pre-v1.2-generation/chronicle-42.findings.json` | 50935 | `61593ee507c3e417…` |
| `findings.json` (query) | **absent — see above** | — | — |
| `renders.json` | `archive/2026-08-15-pre-v1.2-generation/` | 346335 | `2e198d907cfd5ca7…` |
| query suite answers (16) | `out/answers-final.txt` | 5584 | `3d5461797110b7e5…` |
| retrieval sets (16) | `out/retrieval-baseline.txt` | 10323 | `79d0c27035f051eb…` |
| the question set | `src/WorldBuilder.Inference/QuerySuite.cs` (`QuerySuite.ForSeed42`) | 9312 | `cb1990f77620467c…` † |
| the full record | `out/world-42.jsonl` | 616220 | `c5ef7936c783bc2f…` |
| the `.log` view | `out/world-42.log` | 102208 | `7c9013ed91970ec1…` |

† Measured before the tree was placed under git. `archive/` and `out/` are pinned with `-text` in
`.gitattributes` and hash exactly as listed above; `src/` is not, so with `core.autocrlf` on, a
checkout writes CRLF and `QuerySuite.cs` hashes `4d4a6df0a63870a3…` at 9478 bytes on disk. The
9312-byte LF form above is what the repository stores. If the question set is ever archived as a
baseline artefact, it needs the same `-text` pin or the hash is a property of the checkout rather
than of the artefact.

Checks run against that inventory:

- **The row counts in §1 are exactly right.** `world-42.jsonl` is a header line plus 1035 events;
  `world-42.log` is 694 event rows (4 comment lines, 50 blank separators, 694 rows = 748 lines), and
  its own header states "694 of 1035 events."
- **`out/` and the archive hold byte-identical copies** of `world-42.jsonl` and `world-42.log` —
  same sha256 both places. No ambiguity about which to take.
- `renders.json` is well-formed: 198 render records, one per line, each with a `packKey`.
- `chronicle-42.findings.json` covers 15 scopes; `chronicle-42.md` has 15 section headings with 3
  marked unavailable, and `chronicle-42.unverified.md` holds exactly those 3. Consistent.
- All located artefacts are non-empty.

### Two things a future run should decide rather than guess

**The chronicle lives only under a directory named `pre-v1.2-generation`.** That reads like the wrong
copy and is not: both v1.2 reports state the post-v1.2 chronicle is byte-identical to this one — same
8 suspect tokens, same 3 sections held out — and no chronicle was written to `out/` afterwards. It is
the baseline. The directory name is misleading and will mislead someone again.

**Two retrieval files exist and they are not identical.** `out/retrieval-baseline.txt` (10323 B) and
`archive/…/retrieval-42.txt` (10325 B) differ on exactly one line, question 11's planner echo:
`<- "Hadaie Commune"` in the archived copy versus `<- "Hade Commune"` in the newer one. The retrieved
event sets are identical in all 16 — which is what both reports mean by "byte-identical to baseline."
The difference is the planner's own misspelling of the subject, i.e. run-to-run generation variance
in the echo line, not a retrieval regression. I took the newer `out/` copy as the artefact above.
Note that this also settles §3b in advance: reproducing that file requires the planner, so §3b is
`skipped-requires-inference` whenever this loop is next run.

**The question set is source code, not data.** `QuerySuite.ForSeed42` is a C# literal list carrying
each question's text, expectation and note. Archiving it means archiving a `.cs` file, or teaching
the suite to emit the question set. Worth an explicit decision before it is copied.

---

## §3 was not attempted

§7 says abort and report, do not attempt to resolve. Verification runs after the copy, and the copy
never happened. §3a is additionally blocked by the `Warm()` call above.
