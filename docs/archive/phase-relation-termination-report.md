# Phase report — relation termination, step one

Run against `docs/phase-relation-termination.md`. **Halted at the §7 halt after step one, as
instructed.** Step two (relation termination, ruleset 6) and step three (the monotonic sweep) are
untouched.

**Entry state:** ruleset 4, 584 tests green.
**Exit state:** ruleset 5, **594 green, 5 failing by design** — all five are the sealed-baseline
replay, which now names the baseline set the ruleset bump owes. Nothing else regressed.

---

## The verdict: additive-only **holds**, on all five seeds

The world did not move. `DIPLO.ALLIANCE_BROKEN` is emitted beside the war declaration that already
destroyed the alliance, with no state delta, no draw from the stream and no arc, and every event of
every sealed ruleset-4 baseline still appears with its payload and its causal edges intact.

| seed | baseline events | new events | inserted | all insertions `ALLIANCE_BROKEN` | causal graph over baseline events |
|---|---|---|---|---|---|
| 7 | 524 | 526 | 2 | yes | unchanged |
| 42 | 869 | 873 | 4 | yes | unchanged |
| 99 | 702 | 704 | 2 | yes | unchanged |
| 1234 | 859 | 864 | 5 | yes | unchanged |
| 2025 | 696 | 698 | 2 | yes | unchanged |

15 breaks total — **exactly the 15 the §1 audit predicted** from replaying alliance edges on the
unmodified stream (2, 4, 2, 5, 2 per seed, matching seed for seed). The emitter fires precisely where
the audit said the trigger site was reached and nowhere else.

Asserted by `tests/WorldBuilder.Tests/AdditiveRecordTests.cs`, 15 tests, three theories: the
alignment is insertion-only and every insertion is this kind; every baseline event still cites the
same events it always cited, mapped through the renumbering; and the emitter actually fires on every
seed, carries no relation delta, sits on no arc, and has no dangling cause.

### One correction to the assertion as written

The brief asks that every baseline event appear "unchanged". Taken literally that is unachievable for
*any* insertion, and the reason is worth recording rather than working around:

- `Event.Id` is the log position.
- `Event.Key` is FNV over (year, kind, participants, **sequence**), where sequence counts emissions
  within the year — so inserting one event rekeys every later event *in that year*.
- `Event.Causes` is expressed in those same renumbered ids.

So the comparison is on world content — year, kind, participants, outcome, scope, significance,
origin, arc, payload, witnesses — and causal edges are compared **through the alignment**. That is
stricter than comparing them literally: it demands not merely that an event still cites two things,
but that it cites the same two events. The `Key` finding is worth carrying separately: its own doc
comment says it exists so "a cached render survives a v2 retcon that shifts every downstream `Id`",
and the `sequence` term means it does not survive an *insertion* into the same year. That is a
render-cache invalidation nobody has needed yet and will need at step two.

---

## The free negative control (§5): causal variety does **not** move

With additive-only confirmed the simulation is unchanged, so the metric must not respond. It does
not, on any seed:

| seed | distinct deep-chain shapes | deep chains | events |
|---|---|---|---|
| 7 | 45 → **45** | 56 → 56 | 524 → 526 |
| 42 | 99 → **99** | 132 → 132 | 869 → 873 |
| 99 | 69 → **69** | 87 → 87 | 702 → 704 |
| 1234 | 97 → **97** | 156 → 156 | 859 → 864 |
| 2025 | 66 → **66** | 84 → 84 | 696 → 698 |

**The metric is counting causal structure, not record density.** Given how much of the roadmap rests
on it, that is worth having for free.

One caveat against over-reading it. The inserted events are not outside the causal graph — each cites
the war and the marriage — so this is not the trivial case of adding disconnected nodes. But nothing
cites *them*, so they terminate chains rather than extending them, and a stronger test would insert a
record-only event that something else then cites. This result is a real negative control for the
density hypothesis and is not a general proof that the metric ignores added nodes.

---

## Break circumstances (§1): completely skewed, reported not fixed

Of 15 breaks across the panel:

| tie origin | count |
|---|---|
| dynastic (created by a cross-faction marriage) | **15** |
| negotiated (created by `DIPLO.ALLIANCE_FORMED`) | 0 |
| origin unresolved, no cause cited | 0 |

The §1 audit found 5 diplomatic alliance edges against 42 dynastic ones. None of the five is ever
broken by a war declaration, so the `tie` field is constant in practice — **a payload field with one
reachable value, which is this project's most familiar shape.** Per §1 this is reported as a latent
fabrication risk rather than fixed: no renderer reads `tie` today, so nothing can currently learn
"alliances are dynastic" as a universal, but any consumer that does read it would.

The `causes` design works as intended. `Relation.Cause` is set once at creation and never updated,
so it resolves to the wedding for a dynastic tie and would resolve to the accepted proposal for a
negotiated one. No break needed the no-cause fallback, but the fallback is there and tested — an
unresolvable origin is omitted rather than guessed.

Rendered output, unchanged template:

```
[Y0029] DIPLO.ALLIANCE_BROKEN  the Galweall League (f:2) breaks its alliance with
                               the Trostead Compact (f:1)   <= e:408,e:387
```

---

## The parked failures (§4): neither moved

Seed 7 `distinct deep-chain shapes` **45, still 45** against the 60 it is measured against. Seed 99
**69, still 69**. Both unchanged, which the additive-only verdict already implies. No attribution
offered and none available — this step changed no mechanic.

---

## What is owed and was not done

**Baselines are not re-cut, and the ruleset is bumped anyway.** These came apart on inspection and
the split needs your decision.

The bump to 5 is done and documented at `Provenance.cs`, including the point §1 asked to be able to
point at later: **4 → 5 is an additive record change with no simulation change**, the only bump of
that counter which is. Leaving the counter at 4 was not an option — a world written now is a
different file, and a header claiming ruleset 4 would be indistinguishable from a real ruleset-4
world. A ruleset mismatch on read is a note, not a block, so nothing that reads the ruleset-4
archives broke.

The re-cut is a different size of job than it looks. `BaselineArchive.Contents` requires
`chronicle-{seed}.md`, `chronicle-{seed}.findings.json` and `renders.json` in every sealed set, and
those are LLM-rendered artefacts — cutting `baselines/ruleset-5/` means running the chronicle
pipeline through ollama/qwen3.6 for five seeds and archiving prose nobody has read. That is real
inference cost the brief did not budget into a step it describes as record-only, so I stopped rather
than spend it.

**Consequence, stated plainly:** `TheEngineStillReproducesTheSealedBaselines` now targets
`ruleset-{Ruleset.Version}` and fails on all five seeds with *"no sealed baselines/ruleset-5/seed-N —
the ruleset bumped and the set it owes has not been cut"*. That is an accurate description of the
state, not a defect, and it resolves the moment the set is cut. The claim the test used to carry —
that the engine still produces the archived worlds — is carried better in the meantime by
`AdditiveRecordTests`, which checks it against the ruleset-4 files that still exist.

Everything else stayed at ruleset-4 baselines deliberately: `HoldoutTests` (a ruleset-4 vs ruleset-3
comparison, historical), `Baselines.Ruleset4` (prose), `SchemaInclusionTests` (vocabulary),
`CorpusWorldIndependenceTests`.

---

## Halt conditions

| condition | state |
|---|---|
| After step one, always, with the additive-only verdict per seed | **halted here** — verdict above |
| Additive-only failing on any seed | **held** — passes on all five |
| Layer 1 dynamics invariants regressing | **held** — no dynamics test moved |
| Instrumentation invariance failing, or a log-hash change from a probe | **held** — `NoCombinationOfSinksChangesTheWorld` green on all five |
| Constants argued from the mechanic | not reached — step one introduces no constant |
| The monotonic sweep too long to repair | not reached — step three not begun |

---

## Queue

- **Cut `baselines/ruleset-5/`**, or decide to defer it until after step two and accept five red
  tests in the interim. This is the only thing blocking a green suite.
- **`Event.Key` is not insertion-stable within a year.** Carried out of the correction above. Step
  two changes worlds wholesale so it will not bite there, but the render cache is keyed on it and the
  assumption is written down as though it holds generally.
- **`tie` has one reachable value.** Revisit if a renderer ever reads it, or if step two's changes
  make diplomatic alliances more common.

---

## Files

| file | change |
|---|---|
| `src/WorldBuilder.Core/Rules/ActionPhase.cs` | `RecordBrokenAlliance`, called from `DeclareWar` |
| `src/WorldBuilder.Core/Provenance.cs` | ruleset 4 → 5, with the additive-record-change note |
| `tests/WorldBuilder.Tests/AdditiveRecordTests.cs` | new — 15 tests |
| `tests/WorldBuilder.Tests/InstrumentationInvarianceTests.cs` | replay retargeted to the current ruleset |

No renderer, checker rule, threshold or `SimConfig` value was touched. Per §1 no checker rule was
added for the new kind: emit first, observe across seeds, then decide whether it joins Tier 3.
