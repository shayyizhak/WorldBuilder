# v1 golden baseline — seed 42

Engine `1.2.0`, commit `62689cab52be061c416aaaf55e4223bba344da6b`. Sealed
2026-08-15. Hash list and provenance for every file: [manifest.json](manifest.json).

---

## What this is

The v1 golden baseline for seed 42, hand-verified across the v1 render and query
rounds.

Chronicle figures, ruler lists, tenure spans, counts and named years were checked
by hand against the record. The query suite runs 16 of 16, with zero secret
leakage and zero fatal findings. The chronicle is 15 passages carrying 8 suspect
tokens, of which 3 sections are held out of canon.

This is the floor. A future change that moves any of it should have to say so
out loud.

## What it is not

**Not regeneratable.** It cannot be reproduced by re-running generation, because
generation is not reproducible run to run — an identical request body has
produced a different classification, which is the finding v1.2 rests on. Every
file here is the output of a particular run that will not happen again.

If it is lost, it is lost. That is the whole reason it is pinned rather than
described.

## What is verified, and what is derived

The distinction matters, and it is the one that let this baseline be cut at all.

**The prose is hand-verified.** `chronicle-42.md`, `chronicle-42.unverified.md`,
`renders.json`, `answers-final.txt`, `retrieval-baseline.txt`, the world record
and its log are all products of generation. They are archived as bytes because
bytes are the only form in which they survive.

**The findings sidecar is derived.** `chronicle-42.findings.json` is a pure
function of `(renders.json, checker code)` — no inference, fully deterministic,
recomputable at will through `wb book --check-only`. It was therefore
*reproduced* for this baseline rather than copied, and that substitution does not
weaken the anchor, because a derived artefact's authority comes from its inputs
rather than from its history.

`checker_fingerprint` in the manifest records exactly which checker produced it.
That field exists because of what follows.

## The pre-v1.2 sidecar, and why it is here

`chronicle-42.findings.pre-v1.2.json` carries role `historical-not-anchor`.

**It must never be used as a diff target.** It is present as evidence, not as a
reference.

It was written by an older checker carrying two extraction bugs in the raid
reader — raids indexed by place only, so a sentence naming the raided *power* was
told no such raid existed; and a phrase-reader running four words past the end of
a name, worked example `"hadale killed 16 but"`. Both are fixed. Both are pinned
as PASS tests in `CheckerCorpusTests.cs`. Pinning that file as the anchor would
have made the golden floor enshrine two known, fixed, test-covered bugs.

The file differs from the anchor by five coverage deltas across 3 of 15 scopes.
All 163 findings are identical — 8 real, 4 blocking, same kinds against the same
scopes. Both bug sites were `checked 0` in the old sidecar, so no canon decision
ever rested on either, which is why the chronicle reproduces byte-identically
either way.

**The imported tree was internally inconsistent inside its own first commit.**
That sidecar and the checker that supersedes it entered version control in the
same commit, as though the two matched. Any future archaeology that assumes
commit one is a coherent snapshot will be wrong.

## Known deficiencies

State them plainly. A baseline whose weaknesses are undocumented is worse than
one with none, because it will be trusted uniformly.

**Query-side coverage is unstructured.**

> v1's query-side coverage exists only as unstructured captured stdout. It is
> readable but not machine-diffable. A rule going non-zero to zero on the query
> path cannot be detected by a golden diff against this baseline.

That is not a footnote. `departure` extraction went 4 → 0 between two v1.2 rounds
and nothing caught it. This baseline is itself the argument for fixing it, made
from the other side: the chronicle path *had* a machine-readable coverage block,
and that block is the only reason the sidecar drift above was ever visible. The
fix — `CmdSuite` writing a sidecar with the same
`{rule, scope, span, detail, blocking, fatal}` shape plus the per-scope coverage
block — is a Stage 4 item. The next baseline gets it. This one records its
absence.

**Retrieval contains a generated line.** `retrieval-baseline.txt` carries
question 11's planner echo, which is generation output and varies run to run —
`"Hade Commune"` here, `"Hadaie Commune"` in the older copy. Neither is correct;
the faction is **Hadale Commune**. The retrieved event sets are identical across
all 16 questions, and Q11 retrieved correctly regardless, which is
resolve-against-the-record working as designed. Because that one line needs the
planner to reproduce, retrieval reproduction is recorded as
`skipped-requires-inference`. Splitting the event-ID lists from the echo would
convert a permanently-skipped check into a permanently-runnable one; that is
backlog.

## The 3 unverified sections

`chronicle-42.unverified.md` holds the three sections held out of canon — the
Griwick Compact 4–23, the Vea Lode Covenant 49–51, and the rule of Wuldweald
Valdrith over the Kebarrow Compact, 51–51.

They are archived as **evidence that exclusion worked**, not as a record of
failure. If a future ruleset admits any of them to canon, that must surface as a
diff rather than as a quiet improvement in the section count.

## Provenance note

The chronicle, the held-out sections and `renders.json` came from a directory
named `archive/2026-08-15-pre-v1.2-generation/`. The name is misleading and has
already misled once.

The chronicle from that directory *is* the baseline: both v1.2 reports state the
post-v1.2 chronicle is byte-identical, and no chronicle was written to `out/`
afterwards. The sidecar from that directory is *not*, for the reasons above. The
directory holds a mix, and that is exactly the kind of thing a misleading
directory name causes twice.

## Replacement policy

**Create-only.** Establishing a new baseline requires moving this directory aside
under a new name first. Nothing here is merged into, added to, or overwritten in
place.

This is the mechanism that stops a floor moving by rerun. A baseline that can be
regenerated over is not a baseline; it is a cache of the most recent opinion.

## Sealing

`.sealed` holds the sha256 of `manifest.json`. Tooling that reads this baseline
should verify it before trusting the contents.
