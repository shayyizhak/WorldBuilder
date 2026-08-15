# v0 fix brief — round 2

Evaluation of the current batch (seeds 7, 42, 99, 1234, 2025 — 2,236 events, years 2–51,
zero parse failures) against the round-1 targets.

**Verdict: both carry-over items from round 1 are fixed and the causal layer is
substantially healthier. Every remaining problem has the same shape — events that
occur and then have no effect on anything.** Four fixes below, in priority order.

---

## What round 1 fixed (do not regress these)

| Metric | Round 1 | Round 2 | Now | Target held? |
|---|---|---|---|---|
| Verbatim repeat rate | 11–22% | 8.9% | 9.5% mean (4.9–14.1%) | yes, but see Fix 3 |
| Collapses per faction | up to 10 | 1 | 1 | yes |
| Distinct deep-chain shapes | 78 | 328 | 601 | yes |
| Max causal depth | — | — | 10–18 per seed | yes |
| Spurious `LIFE.MARRIAGE` edges | present | present | **0** | **fixed** |
| Economy → politics coupling | absent | deferred | **live** | **fixed** |

The economic coupling is the headline improvement. Famine now produces 49 raids,
16 court realignments, 14 appointments, 7 coup plots, 6 legitimacy crises and 3
revolts across the batch. It is what pushed max depth to 18 and nearly doubled
shape diversity. Faction populations now grow (3→8 via secession) rather than only
shrinking.

Also confirmed healthy: 65 of the 198 actors born in-simulation later reach office,
so the population model is doing real work.

---

## Fix 1 — coup plots must terminate (priority: highest)

**Measured now:** 121 `POLITY.COUP_PLOTTED`, 57 `POLITY.COUP_RESOLVED`, of which only
26 cite a plot. 95 plots have no recorded outcome. Of those plotters, 56 died before
resolving and 10 vanish with no trace at all.

**Diagnosis.** Two separate mechanisms are being conflated in the log:

- 26 resolutions read *"X's conspiracy against Y is uncovered after N years"* and
  correctly cite a `COUP_PLOTTED`.
- 31 resolutions read *"X challenges Y for the Z and wins/loses"* and cite **no plot**.
  These appear to be open challenges, which is legitimate as a separate mechanism —
  but they are sharing an event type with plot resolutions, which makes plots look
  resolved when they aren't.

Meanwhile a plot whose plotter dies is cancelled silently. Nothing in the log says so,
so a reader (and, shortly, the query layer) cannot distinguish "plot still pending" from
"plot died with its plotter" from "plot leaked".

**Change:**

1. Split the event type. Keep `POLITY.COUP_RESOLVED` for outcomes of a plot — it must
   always cite its `COUP_PLOTTED`. Emit open challenges as `POLITY.CHALLENGE` with its
   own outcome text.
2. When a plotter dies with an active plot, emit
   `POLITY.PLOT_DIES_WITH_PLOTTER` citing both the plot and the death event.
3. Give plots a lifetime. If a plot neither resolves nor dies within N years, emit
   `POLITY.PLOT_LAPSES` citing the plot. Choose N so that steady-state pending plots
   stay under ~10% of plots created.
4. Assert in the engine: every `COUP_PLOTTED` eventually has exactly one terminating
   event citing it. Fail the run if a plot is still open at simulation end.

**Why this first:** it converts 95 dead ends into causal chain material at almost no
cost, and it removes an ambiguity that would otherwise poison the v1.2 query layer —
"what happened to the conspiracy against Y" currently has no answer in the data.

**Target:** ≥95% of plots have a terminating event that cites them; `COUP_RESOLVED`
citing a plot = 100%.

---

## Fix 2 — plague is unfinished (priority: high)

**Measured now:** 15 `ECONOMY.PLAGUE`, **0** `ECONOMY.PLAGUE_ENDS`. Famine has a
resolution event (53 famines → 30 ends); plague does not.

Scale is also wrong by roughly an order of magnitude:

| Disaster | n | min deaths | median | max |
|---|---|---|---|---|
| Plague | 15 | 9 | 189 | 522 |
| Famine | 53 | 2 | 7 | 8 |

These two are not measuring the same quantity. Famine deaths look like they are derived
from grain shortfall; plague deaths look like they are derived from raw settlement
population. A plague that kills 522 while the worst famine on record kills 8 makes the
economic layer incoherent, and it will read as absurd once the render layer turns it
into prose.

**Change:**

1. Implement `ECONOMY.PLAGUE_ENDS`, mirroring `ECONOMY.FAMINE_ENDS` — cite the
   originating plague, report duration.
2. Make plague multi-year like famine (`plague at P, year 2: ...`), so it can escalate
   and abate rather than firing as a single spike.
3. Put both disasters on the same denominator. Deaths should be a fraction of the
   settlement's population in both cases. Pick the fraction so a severe plague is
   perhaps 3–5× a severe famine, not 27×.
4. Plague should drive migration the way famine does (`N abandon the place`).

**Target:** plague ends within a bounded number of years in ≥90% of cases; median
plague deaths within 5× median famine deaths.

---

## STOP HERE — re-run and re-measure before continuing

Fixes 1 and 2 are bug fixes with unambiguous correct behaviour. Fixes 3 and 4 are
design passes that change the texture of the world, and their effects are much harder
to read if they land in the same batch.

Re-run all five seeds, re-measure, and confirm:

- plots terminating ≥95%
- plague resolving, scale corrected
- repeat rate, chain depth, shape diversity and collapse-per-faction have **not**
  regressed from the table at the top

Then continue.

---

## Fix 3 — 31% of the log has no consequence (priority: medium)

**Measured now:** 688 of 2,236 events belong to a type whose out-degree in the causal
graph is exactly **zero**.

| Type | Count | Out-degree | Should plausibly cause |
|---|---|---|---|
| `LIFE.BIRTH` | 198 | 0 | succession — heirs already reach office, the edge is simply missing |
| `POLITY.APPOINTMENT` | 168 | 0 | courts support, later succession, resentment in the passed-over |
| `POLITY.EXILE_RETURN` | 101 | 0 | plots, court realignment — a returning exile is a live threat |
| `ECONOMY.TRADE_PACT` | 85 | 0 | famine relief, alliance formation, grievance when broken |
| `DIPLO.INSULT` | 52 | 0 | escalation toward war |
| `ECONOMY.FAMINE_ENDS` | 30 | 0 | recovery, migration return |
| `DIPLO.PEACE_SIGNED` | 20 | 0 | alliance, trade, resentment |
| `ECONOMY.BUMPER_HARVEST` | 17 | 0 | trade, population growth, tribute capacity |
| `POLITY.COLLAPSE` | 11 | 0 | land redistribution, successor states |
| `INTRIGUE.BETRAYAL` | 4 | 0 | exile, death, court shift |
| `DIPLO.ALLIANCE_FORMED` | 2 | 0 | joint war, tribute |

**This is also where the repeat rate now lives.** The overall rate is flat at 9.5%, but
the repetition is concentrated almost entirely in the consequence-free diplomacy: one
faction pair exchanges the same insult 6 times and the same tribute demand 5 times
across a run. Nothing suppresses repetition because nothing records that the gesture
already happened. Give these events state effects and the duplication resolves itself —
do **not** fix the repeat rate with a phrasing-variation pass.

**Change:** for each type above, either (a) give it a state effect that can be cited by
a later event, or (b) stop emitting it. Two specific ones worth calling out:

- **`LIFE.BIRTH`** — the actors clearly matter (65 of 198 reach office). Have
  `POLITY.SUCCESSION` cite the heir's birth event when the succession mode is
  primogeniture. Cheap, and it produces genuinely long dynastic chains.
- **`DIPLO.INSULT` / `DIPLO.TRIBUTE_DEMANDED`** — these should accumulate a grievance
  score between faction pairs, and that score should be a precondition for
  `DIPLO.WAR_DECLARED`. Repetition then becomes escalation instead of noise.

**Target:** dead-end share below 10% of events; repeat rate below 6% mean with no
seed above 8%, achieved through state effects rather than phrasing variety.

---

## Fix 4 — diplomacy is hostility-only (priority: low, do last)

**Measured now:** 99 hostile gestures (52 insults, 47 tribute demands), 21 wars, 20
peace treaties — and **2** alliances across the entire batch.

No coalitions form, so there is no balance-of-power dynamic: a strong faction is never
opposed collectively, only serially. This is the main reason the late-game state in
several seeds is a slow grind rather than a shifting board.

Hold this until after Fix 3 lands, because trade pacts and peace treaties acquiring real
effects will change alliance incentives on their own, and it is worth seeing how much of
this problem solves itself.

**Change (after re-measuring):** alliance formation driven by shared threat — a faction
that is losing a war, or that borders a faction which has recently conquered, should
seek allies. Alliances should then be citable by `CONFLICT.BATTLE` (joint action) and
breakable (`DIPLO.ALLIANCE_BROKEN`) as a grievance source.

**Target:** alliances within the same order of magnitude as wars declared; at least one
three-faction coalition across the five seeds.

---

## Metric summary — where we are and where to get to

| Metric | Now | After fixes 1–2 | After fixes 3–4 |
|---|---|---|---|
| Plots with a terminating event | 21% | ≥95% | ≥95% |
| `COUP_RESOLVED` citing its plot | 46% | 100% | 100% |
| Plagues resolving | 0/15 | ≥90% | ≥90% |
| Plague : famine median deaths | 27× | ≤5× | ≤5× |
| Dead-end share of log | 31% | ~28% | <10% |
| Verbatim repeat rate | 9.5% mean | ≤9.5% | <6% mean, <8% any seed |
| Alliances vs wars | 2 : 21 | 2 : 21 | same order of magnitude |
| Max causal depth | 10–18 | ≥10–18 | ≥10–18 |
| Distinct deep-chain shapes | 601 | ≥601 | ≥601 |
| Collapses per faction | ≤1 | ≤1 | ≤1 |

---

## Carry-over notes

- **Marriage causal edges: resolved.** Marriages are now root events with no incoming
  causes, and the spurious zero-shared-actor edges are gone. No action.
- **Seed 7 is thin** — 3 factions and 320 events against 5–8 factions and 420–610
  elsewhere. Probably legitimate seed variance rather than a bug, but worth a glance
  after Fix 3 to confirm it is not a faction-generation floor problem.
- **Do not let render output feed back into simulation state.** Unchanged constraint
  from the v1 brief; these fixes are all engine-side and should not touch the render
  path.
