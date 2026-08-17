# v0 Fix Round 1 — dynamics, not features

## Context

This is the world builder engine (v0: symbolic simulation, no AI, no prose, no UI). The first run is complete and produced 50-year logs across five seeds (7, 42, 99, 1234, 2025). Those logs were analysed. The architecture is sound — causal edges are recorded and traversable, significance weights work, secrets are flagged. **Do not restructure anything.**

The problem is dynamics. The logs are busy but inert: events repeat verbatim, most political action fizzles, and apparent causal depth is largely one actor's lifecycle pipeline rather than genuine cross-domain causation.

**This round fixes dynamics only. Do not add new entity types, event types, resources, factions, or a name generator.** Adding content now would mask whether these fixes worked.

---

## Evidence from the analysed logs

Measured across all five seeds:

- **Causal depth is fake.** Max chain depth is 7, but only 78 distinct deep-chain shapes exist across five worlds, dominated by templates like `MARRIAGE > BIRTH > COUP_PLOTTED > COUP_RESOLVED > EXILE > EXILE_RETURN` (10+ occurrences) and `MARRIAGE > BIRTH > MARRIAGE > BIRTH > MARRIAGE` (12). These are single-actor state transitions, not history.
- **Zombie factions.** In world-2025 the Deafil League emits `POLITY.COLLAPSE` ten times. A collapsed faction keeps receiving defectors and returning exiles, then re-collapses. The same actor (Fur Roundpoull) defects to it three separate times after it is "finished".
- **11–22% of all events are verbatim repeats** (same type, same description modulo numbers). Famine recurs at the same place 5–7 times; bumper harvest 7 times; `POLITY.LEGITIMACY_CRISIS` resolves by spending exactly 50 silver every single time.
- **Famines don't resolve.** 66 `ECONOMY.FAMINE` versus 31 `ECONOMY.FAMINE_ENDS`. Places oscillate instead of changing state.
- **Politics fizzles.** Of 173 coups: 23 succeed, 26 lose, 44 are uncovered, 80 "come to nothing". Of 168 assassinations, 121 fail and only 47 kill.
- **Some causal edges are spurious.** Example: `famine at Sti Seam (p:7)` recorded as cause of `the Kreagemoor Republic's raid on Goummeidale (p:2) is beaten off`. Co-occurrence recorded as causation.

---

## Fixes, in priority order

### 1. Collapse must be terminal

When a faction emits `POLITY.COLLAPSE`, remove it from every selection pool in the same tick: no defection targets, no exile-return destinations, no tribute demands, no trade partners, no marriage bindings, no war declarations for or against it.

A collapsed faction should never appear as an actor or a target again. If its identity needs to persist for historical reference, mark it dead and exclude it at pool-construction time rather than filtering at each call site.

Expected effect: eliminates the repeated-collapse loop and removes the spurious `EXILE_RETURN > COLLAPSE` chain shape.

### 2. Repetition must change state

Currently a place can starve indefinitely at the same severity. Repeated events must either escalate or resolve.

- **Famine:** track consecutive famine years per place. On the second consecutive year, apply a persistent penalty (population migrates out, carrying capacity drops, or the place is abandoned). A place should not be able to sustain 5+ identical famines.
- **Legitimacy crisis:** the cost must scale with how many times that faction has already used it, and it must be able to fail. A fixed 50-silver payment every time is a no-op dressed as an event.
- **General rule:** if the same (type, subject) pair fires more than twice with no change in outcome, that mechanic needs either an escalating cost or an absorbing state. Apply this test to every recurring event type, not just the two above.

### 3. Audit causal edge assignment

Some `<=` edges record co-occurrence rather than causation. Review how causes are attached when an event is generated.

An edge should only be recorded where the referenced event materially influenced this event's occurrence or outcome — it fed the precondition, supplied the resource, or set the state that triggered it. Proximity in time or shared tick is not sufficient.

If an event has no genuine cause, record no cause. An empty cause list is more honest and more useful than a fabricated edge, because the whole value of the log is that traversing it yields true answers.

### 4. Raise the stakes on coups and assassinations

Roughly half of all political events currently resolve as non-events. Reduce the *frequency* of attempts and raise the *consequence* of each.

- Coup success should be meaningfully influenced by the plotter's accumulated support (`POLITY.COURTS_SUPPORT` should be doing real work here). Target a materially higher success rate than the current 13%.
- Remove or sharply reduce the "come to nothing" outcome — it is currently the single most common coup result and generates pure noise.
- Assassination attempts should be rarer and deadlier.

The goal is fewer political events with real consequences, not more political events.

---

## Determinism

Same seed must still produce a byte-identical log. Re-verify after these changes:

- No `Dictionary`/`HashSet` enumeration order dependence in any selection pool — this is a live risk given fix 1 changes how pools are built.
- No `DateTime.Now`, `Guid.NewGuid()`, or unordered parallelism in the tick loop.
- Run each seed twice and diff. This should be part of the build, not a manual check.

---

## Test run

After the fixes, re-run **the same five seeds** (7, 42, 99, 1234, 2025) for the same 50 years, with the same output format. Do not change the log format — the previous logs need to remain comparable.

Then report the following metrics, before and after:

| Metric | Current | Target |
|---|---|---|
| Verbatim repeat rate (same type + description modulo numbers) | 11–22% | under 5% |
| Repeated `POLITY.COLLAPSE` per faction | up to 10 | exactly 1 |
| `FAMINE` : `FAMINE_ENDS` ratio | 2.1 : 1 | approaching 1 : 1, or famine terminates in a state change |
| Distinct deep-chain shapes (depth ≥ 5) across all 5 seeds | 78 | materially higher |
| Share of deep chains that are single-actor lifecycle sequences | dominant | minority |
| Coup success rate | 13% | higher, and driven by accumulated support |
| Events with recorded causes that survive the fix-3 audit | unknown | report the count removed |

Also produce, for each seed, the three deepest causal chains as a readable trace (year, type, description per step) so they can be judged qualitatively rather than only by count.

---

## What I want from you

Implement fixes 1 and 2 first, re-run, and report the metrics before touching 3 and 4 — fix 1 alone may resolve a large share of the repeat rate, and I want to see its isolated effect.

Push back if any of this is wrong. In particular: if you think the fizzle rate in fix 4 is intentional realism rather than noise, or if fix 2's escalation rule would break something in the tick loop I'm not seeing, say so before implementing. I would rather argue about it now than unpick it later.
