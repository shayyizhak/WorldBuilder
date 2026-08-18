# Report — Layer 5 restoration and the skipped-row visibility fix

Run against `docs/brief-layer5-restoration.md`. **All four items complete.** No mechanics change, no
new checker rule, ruleset stays at 5, checker fingerprint unchanged.

**Entry state:** 594 green, 5 failing (the baselines the ruleset bump owed).
**Exit state:** **599 green, 0 failing**, 2 skipped. Layer 5 runs again.

Order followed as specified: item 2 landed before item 1, so the cut was confirmed by a top line
that cannot hide a skip.

---

## Item 1 — `baselines/ruleset-5/` is cut

Five seeds, full contents, real inference through ollama `qwen3.6:latest`
(`07d35212591f…`). All five seals verify.

| seed | events | passages | held out | rate | ruleset-4 rate |
|---|---|---|---|---|---|
| 7 | 526 | 8 | 2 | 25% | 25% |
| 42 | 873 | 13 | 5 | 38% | 46% |
| 99 | 704 | 14 | 6 | 42% | 35% |
| 1234 | 864 | 13 | 2 | 15% | 15% |
| 2025 | 698 | 12 | 5 | 41% | 41% |
| **panel** | | **60** | **20** | **33%** | **33%** |

**Twenty holdouts of sixty, on both rulesets.** Per-seed spread `range=[15, 42] width=27` against
ruleset 4's `[15, 46] width=31`. No seed's rate is unlike ruleset 4, so the §5 halt condition on the
checker rate is clear. `wb holdouts --set ruleset-5` still returns **Escalate**, exactly as ruleset 4
does — that is the holdout brief's own standing verdict carried across, not a new finding.

Checker fingerprint `60f5b325…`, unchanged from ruleset 4: none of the five fingerprinted files was
touched. Board hash `8eb0a9af…`, the same board.

**The archive contract was not split.** The brief asked for it and I did not do it, so it is owed
rather than done. Both halves are cut at ruleset 5, so nothing is blocked, but `BaselineArchive.Contents`
still lists chronicle, findings and `renders.json` as flatly required alongside the log and board, and
a consumer wanting only the log half still cannot say so. Doing it properly means a manifest field, a
declared-halves check and a named failure for a missing half — a change to the sealed-archive format,
which is create-only and carries five existing sets. That is its own small piece of work and I did not
want to fold a format change into a cut whose whole purpose was to be verifiable.

Prose was not re-verified. These are machine baselines; verification is `stability-anchor-only` and
each manifest says so.

---

## Item 2 — a layer that did not run no longer reports as passed

`wb test` now aggregates over a `LayerRun` record carrying **whether a layer executed**, not only
what it found.

```
0 of 1 layers ran, none failed
  layer 5 SKIPPED — baseline is ruleset 4, build runs ruleset 5
  layer 2 runs under `dotnet test`; it is not one of the above.
```

and against the new set:

```
  0 failed, 15 noted
1 of 1 layers ran, none failed
```

The skip still returns 0 — that decision was correct and stays. What changed is that the summary
reports coverage as well as failures.

### The sweep, reported rather than fixed

Per the §5 halt condition, the full list of top lines that can report success over unexecuted work:

| site | state |
|---|---|
| `wb test` top line | **fixed** — this item |
| layer 4, missing chronicle | **fixed** — fed the same top line |
| layer 5, ruleset mismatch | **fixed** — fed the same top line |
| layer 5, no stored render | **fixed** — fed the same top line |
| `wb bundle verify` | **found, not fixed** — "the header names no artefacts. Nothing to verify." → exit 0 |
| `wb baseline check` | **found, not fixed** — a manifest with no `artefacts` key returns zero failures, and the CLI prints "the seal verifies and every artefact matches its manifest hash" |
| `wb suite` | **clean** — already prints `N of M passed` |

The two unfixed are the same defect in the bundle layer. They are listed rather than repaired
because the brief asked for the list, and because both sit in the sealed-archive path that item 1's
deferred contract split will have to touch anyway.

---

## Item 3 — skipped rows emit their reason

`GoldenDiff.FloorCoverage` adds a note per rule whose floor does not reach every scope, plus a
reach line that keeps the denominator in front of the reader:

```
note (coverage): floor-reach — FLOOR compared 71 of 208 rule rows across 13 scope(s)
note (coverage): no-floor — coverage: no floor in any of 13 scope(s) — FLOOR cannot fail for it here
note (coverage): no-floor — tenure: floor in 2 of 13 scope(s); 11 unprotected
```

Notes, never failures. A zero floor is not a regression, and construction-gated rules have honest
zeros — `partition-sum` needs a partition. Gating here would manufacture the false positives that
once cost seven true sections.

### The two reasons do not partition, and the brief's own hypothesis is why

The brief asked for rows labelled `not instrumented` versus `zero at baseline`. **I did not implement
that split, and the panel proved the caution right.**

Counts cannot separate "no `Extracted` call site" from "call site this document never reached". The
brief anticipated resolving that with the five-seed sweep. It does not resolve it:

| rule | ruleset-4 panel | ruleset-5 panel |
|---|---|---|
| `tenure` | 2 of 60 | 6 of 60 |
| `quantity` | **0 of 60** | **3 of 60** |

`quantity` reads zero across *the entire ruleset-4 panel* — sixty scopes, five seeds — and has an
`Extracted` call site in `CheckReignAttribution` all along. On the ruleset-5 panel it reads 3. So a
whole panel of zero does not establish "not instrumented"; it establishes "this panel did not reach
it". Had the label been implemented as asked, it would have asserted a dead call site that is not
dead — the absent-versus-unknown conflation, one more time.

The bucket is therefore named **`no floor on this panel`**, which states the observation, and the
cause is read off the source and asserted by a person in §4.

### Where it lands

Three rules have no floor anywhere on the ruleset-5 panel — `coverage`, `outcome`, `shape` — and
those are **exactly** the three with no `Extracted` call site anywhere in `WorldBuilder.Inference`.
Measurement and source agree, but only after two panels disagreed.

**Not attached to the counter-shapes card.** `https://trello.com/c/xtTiX4V2` is Trello and I have no
Trello access in this session — the connected tracker is Atlassian. The figures are in
`docs/floor-coverage.md`, generated, ready to paste. Say the word if you want them filed somewhere I
can reach.

---

## Item 4 — the §4 list is generated

New verb `wb floors [--set …] [--seeds …] [--to <file>]`. §4 now carries its output with the command
and date that produced it, and gained a standing rule:

> **A list in the documentation describing a measurable property of the code should be emitted by
> the code.**

The old list named six rules. Measured at ruleset 5 there are **three**, and four of the six named —
`action`, `date`, `quantity`, `tenure` — carry floors in some scopes, while `shape` carries none and
was missing. Wrong in both directions, as the brief predicted.

`wb floors` refuses rather than reporting an empty table when it finds no sidecars, which is the same
defect one level down and would have been easy to write in.

---

## Halt conditions

| condition | state |
|---|---|
| Any seed's render pipeline failing | **held** — all five rendered and sealed |
| A checker rate unlike ruleset 4 | **held** — 20 of 60 on both panels, spread 27 against 31 |
| The two skip reasons not partitioning | **triggered, and reported** — see item 3; the split was not implemented and the reason is a finding, not a workaround |
| A top line reporting success over unexecuted work | **swept; full list above**, two found and left unfixed as instructed |
| Suite not returning to green | **held** — 599 green, 0 failing |

---

## Files

| file | change |
|---|---|
| `baselines/ruleset-5/seed-{7,42,99,1234,2025}` | new — sealed, verified |
| `src/WorldBuilder.Cli/CommandLine.cs` | `LayerRun`; four layers return it; `wb floors`; usage |
| `src/WorldBuilder.Inference/GoldenDiff.cs` | `FloorCoverage`, `FloorClassification`, `FloorReason` |
| `docs/WORLDBUILDER-PROJECT.md` | §4 generated table; the new standing rule |
| `docs/floor-coverage.md` | new — generated |

---

## Owed

- **Split the archive contract** (item 1's second half). Log half free, render half costly, declared
  rather than assumed, named failure for a missing half.
- **`wb bundle verify` and `wb baseline check`** can both report success having checked nothing.
- **`coverage`, `outcome`, `shape`** still have no extraction instrumentation. That is the
  counter-shapes work, not this brief.
