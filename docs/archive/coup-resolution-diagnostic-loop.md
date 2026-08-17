# Loop-prompt — coup resolution: diagnosis, not repair

The harness's first act was to find a live simulation defect that months of hand review missed. This round finds out what it is. **It does not fix it.**

Run unattended until the halt conditions hold or an abort triggers.

---

## 0. What the harness found, and what the record adds

Layer 1 asserts `coup success > 15%` and `covert coup path success > 0`. Both fail on all five seeds: **0% success, everywhere.** The covert-coup invariant had been counting exposures as successes and reporting green against zero wins, which is why this survived so long.

Seed 42's record shows the shape is not one defect but two:

- **42 `POLITY.COUP_PLOTTED`, 7 `POLITY.COUP_RESOLVED`, all 7 `Failed`.**
- Resolution lag on those 7 is **1–4 years** — Y35→36, Y39→41, Y40→42, Y41→45, Y42→46, Y44→46, Y45→46.
- **No plot before year 35 ever resolves.** Twenty-seven plots from Y8 to Y33 sat for decades against a 1–4 year clock and never came out either way.
- Four of the seven resolutions are the Paernmel Has cluster (`a:50`, Y46).

So the 35 unresolved plots are not a horizon artefact. Something gates resolution and it was shut for the world's first three decades.

**Two defects, in this order:**

1. **Resolution mostly does not fire.** A missing state transition — plots enter a state they never leave.
2. **When it fires, the outcome is always `Failed`.**

---

## 1. Prohibitions

1. **Change no probability, weight, threshold or rate.** Not one. If defect 1 means resolution fires on a narrow slice of plots, then the observed success rate is measured over a biased sample and tuning it now tunes against the wrong population.
2. **Do not touch the `KnownFailing` list.** Both coup invariants stay in it, still failing. The list shrinks when a threshold starts holding on its own, never because a threshold moved.
3. **Do not fix defect 2.** Even if the cause is obvious on the way past. Record it and stop. Its population is not known until defect 1 is understood.
4. **Do not modify the sealed baseline** at `baselines/v1/seed-42/`. It is read-only and create-only.
5. **Instrument, do not restructure.** If the resolver needs redesigning, that is the *next* round's brief, written against this round's findings.

---

## 2. Prerequisite — the ruleset version decision

**Settle this before writing code**, because this round leads directly to a rule change and it is the first real divergence.

`ruleset_version` currently duplicates `engine_version` at `1.2.0`. That holds only while the two move together. A coup fix is a rule change that will ship without an engine release, so they are about to separate.

Two acceptable answers:

- **They are one thing.** Drop `ruleset_version` from the header, and say in the header spec that engine version implies ruleset. Simple, and wrong the moment a rules-only release happens.
- **They are two that currently coincide.** Give the ruleset its own numbering now — `ruleset_version: "1"` or `"2026.1"`, deliberately *not* matching `1.2.0`, so the duplication cannot be mistaken for a constraint.

The second is recommended. Either way, record the decision in the header spec, and abort if this is unresolved when the round starts.

---

## 3. The work — account for every plot

For every `POLITY.COUP_PLOTTED` in the log, the engine must be able to say what happened to it and why.

**Instrument the resolver** so that each plot, each time it is considered, records:

- whether it was **examined at all** in that tick;
- if not examined, the reason it was skipped — the collection it was absent from, the guard that excluded it, the index it was not in;
- if examined, the gate that was evaluated and the value that failed it;
- if resolved, the outcome and the lag.

**The distinction that matters is examined-versus-not.** Right now a plot that never resolves is indistinguishable from a plot the resolver never looked at. That is the same conflation as `unresolvable` in the checker and the empty-result sentence in the query layer — not-checked and not-true reported identically. Third venue, same defect class. Assume the answer is that a large share were never examined.

**Emit this as a per-run accounting block**, in the shape the coverage block already uses:

```
plotted / examined / resolved / unresolved-with-reason / unexamined
```

with `plotted == resolved + unresolved-with-reason + unexamined`, asserted. An unexamined plot with no recorded reason is an accounting failure, not a row to skip — the same rule as a dangling causal edge.

**Do not add this to the event log.** It is diagnostic instrumentation about the simulation, not world history. Events are what happened in the world; this is what happened in the engine. Write it beside the run, not into it.

---

## 4. What to report

- The accounting block for all five seeds: **7, 42, 99, 1234, 2025.**
- **The reason distribution** — how many plots fell out at each gate, ranked. This is the finding.
- Whether the pre-Y35 cutoff on seed 42 is real and, if so, what changes at Y35. Candidates worth checking rather than assuming: whether resolution requires the target to currently hold a seat, whether it requires the plotter to be alive and in the faction, whether plots are indexed by something that only becomes populated later, whether a collection is rebuilt per tick and loses entries.
- For the 7 that did resolve: what they have in common that the 35 do not.
- **Defect 2, characterised but not fixed** — where the outcome is decided, what the decision reads, and whether any code path can produce a win at all. "No path exists" and "a path exists and never wins" are different findings and the difference decides the next round's shape.
- Whether the same shape holds on the other four seeds, or whether seed 42 is unusual.

---

## 5. Halt conditions

1. The §2 ruleset-version decision is recorded.
2. Every plot on all five seeds is accounted for: examined, or skipped with a named reason.
3. `plotted == resolved + unresolved-with-reason + unexamined` holds per seed.
4. The reason distribution is reported, ranked, per seed.
5. Defect 2 is characterised — the deciding code path named, and a statement of whether a win is reachable at all.
6. No probability, weight, threshold or rate has changed. No entry has left `KnownFailing`.
7. The sealed baseline is untouched and `.sealed` still verifies.
8. The full suite is green apart from the two coup invariants, which must still fail.

## 6. Abort conditions

- The ruleset-version decision is unresolved.
- The instrumentation cannot distinguish examined from unexamined without restructuring the resolver — report that, because it is itself the finding.
- Any accounting identity fails to balance and the shortfall cannot be named.
- Any change would alter simulation behaviour rather than observe it. Instrumentation that changes the world is not instrumentation.

---

## 7. Why this round is diagnosis only

The temptation is to fix defect 2 first, because it is the invariant showing red and the cause may be a single comparison. That would be the wrong order: the success rate is currently measured over whichever slice of plots the resolver happens to reach, and tuning a rate against a biased sample produces a number that looks right and means nothing.

Exit criterion, per the standing rule that every stage ends on a harness number rather than a feeling: **no plot is unexplained.** Not "coups work now" — that comes next, with a population you can trust.
