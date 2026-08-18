# Phase report — the four dead event kinds

Run against `docs/phase-four-dead-kinds.md`. **Halted at the §1 halt condition, as instructed.** No
emitter written, no rule changed, no threshold moved. This report is the audit table and the
surviving set, and nothing beyond it.

**Second gate, still shut.** §0 marks the whole brief contingent on a decision not yet taken —
Stage 6's economy half and Stage 5's workbench staying parked. The §1 audit is read-only and its
declared purpose is to shrink or shelve the phase, so running it does not pre-empt that decision.
§2 is blocked on *both* gates: the §1 halt and the contingency.

**Entry state:** ruleset 4, 584 tests green (553 + 31, 2 skipped), tree otherwise unchanged.
**Exit state:** identical. Nothing in `src/` or `tests/` was touched.

---

## §0 confirmed, exactly

The enum carries 46 kinds. A 50-year run on each of the five panel seeds emits 42 distinct kinds
across 3 650 events (headers confirm `ruleset_version: 4` on all five). The difference is exactly the
four named — no fifth structural zero, and none of the four appears on any seed.

Each of the four is declared in `Events.cs`, has a real one-line renderer in `EventTemplates.cs`,
and is referenced from no rule, no reducer branch and no test. Vocabulary and renderer paid for,
emission missing, four times over.

---

## §1 audit table

| kind | state it needs | exists? | trigger evaluable from existing state? | interacts with | verdict |
|---|---|---|---|---|---|
| `DIPLO.ALLIANCE_BROKEN` | a live alliance between two factions | **yes** — `RelationKind.Alliance` edges | **yes** — the break already happens, silently | `DIPLO.WAR_DECLARED`, `POLITY.COLLAPSE`, `LIFE.MARRIAGE` | **survives** |
| `ECONOMY.TRADE_COLLAPSE` | a standing trade relationship | **yes** — `RelationKind.Trade` edges | **yes** — and the edge currently only ever grows | `DIPLO.WAR_DECLARED`, `POLITY.COLLAPSE`, `ECONOMY.TRADE_PACT` | **survives** |
| `INTRIGUE.GRIEVANCE_SETTLED` | a grudge that can be discharged | **yes** — `RelationKind.Grievance` edges | **yes**, but needs a new settlement *rule*, not just an emission point | `DIPLO.PEACE_SIGNED`, `LIFE.MARRIAGE` | **survives, weakest** |
| `CONFLICT.SIEGE` | an open siege: besieger, place, start year, resolution | **no** — nothing anywhere | no | — | **dropped** |

**Surviving set: `ALLIANCE_BROKEN`, `TRADE_COLLAPSE`, `GRIEVANCE_SETTLED`. Dropped: `SIEGE`.**

---

## Per kind

### `DIPLO.ALLIANCE_BROKEN` — substrate present, break point already firing

The alliance is severed *today*, at [ActionPhase.cs:624-625](src/WorldBuilder.Core/Rules/ActionPhase.cs#L624-L625):
war declaration carries two `RelDel(…Alliance)` keys. The edge dies inside the `WAR_DECLARED`
payload and nothing in the log or the prose ever says an alliance ended. This is not a kind needing
a new trigger — it is a trigger that already fires with its event missing.

Measured on the ruleset-4 stream, replaying alliance edges from the log payloads:

| seed | wars declared | severing a **live** alliance | collapses holding a live alliance |
|---|---|---|---|
| 7 | 3 | 2 | 2 of 2 |
| 42 | 5 | 4 | 3 of 3 |
| 99 | 7 | 2 | 3 of 5 |
| 1234 | 6 | 5 | 2 of 2 |
| 2025 | 3 | 2 | 1 of 1 |
| **total** | **24** | **15** | **11 of 13** |

Two branches, both reachable on every seed without any change to the engine: **severed** (an ally
declares war — 15 occurrences) and **lapsed** (the ally is destroyed — 11). The second is currently
worse than silent: collapse removes no relations, so a destroyed power keeps its alliances on the
books forever. Final live edge counts are non-zero on all five seeds partly for that reason.

**Finding the brief did not anticipate.** Alliances in this world are overwhelmingly *dynastic*, not
diplomatic. Of 47 alliance edges created across the five seeds, **42 come from cross-faction
marriage** ([LifePhase.cs:115](src/WorldBuilder.Core/Rules/LifePhase.cs#L115)) and only **5** from
the `FormAlliance` goal. `DIPLO.ALLIANCE_FORMED` fires 13 times and most of those are refusals. So
the `causes` edge for a broken alliance will usually want to point at a marriage, not at a pact —
and §2.3 should be written knowing that, because pointing it at `ALLIANCE_FORMED` would leave the
majority of breaks citing nothing.

### `ECONOMY.TRADE_COLLAPSE` — the brief's expected shape is refuted

§1 predicted this one "almost certainly does not [have substrate] and is the economy half in
disguise". That is wrong, and the brief asked to be told so.

`RelationKind.Trade` is a standing relationship with its own renderer semantics — "X and Y agree
terms of trade" — created at +25 by a pact and +8 by a purchase, and read as an input to alliance
appeal. It is persistent state that already exists. What is missing is not state but a rule: **the
Trade edge is monotonic.** Nothing anywhere reduces or removes it. Peak live edges equals final live
edges on all five seeds (3, 5, 5, 6, 7). Two factions who traded once in year 6 are still recorded
as trading partners in year 50, through two wars and a conquest.

| seed | wars declared | between factions with a **live trade tie** | collapses holding a live trade tie |
|---|---|---|---|
| 7 | 3 | 3 | 2 of 2 |
| 42 | 5 | 5 | 3 of 3 |
| 99 | 7 | 5 | 5 of 5 |
| 1234 | 6 | 6 | 2 of 2 |
| 2025 | 3 | 2 | 1 of 1 |
| **total** | **24** | **21** | **13 of 13** |

Two branches, both reachable on every seed: **broken by war** (21) and **ended with the partner**
(13). No new persistent state, no economy model, no goods flow — the rendered claim is about the
relationship, and the relationship is already there. This is the cheapest of the three after
`ALLIANCE_BROKEN`, and it also fixes a standing defect: a permanent trade edge is wrong on its own
terms.

### `INTRIGUE.GRIEVANCE_SETTLED` — substrate present, but the trigger is a new mechanic

Grievance is the richest relation in the engine: written from 20-odd sites, decayed every tick at
`GrievanceRetentionPct`, partially cancelled on peace. The state is unambiguously there.

What is *not* there is any point where a grudge is settled. Two measurements:

- **Decay never finishes the job.** 260 grievance edges cross 40 across the five seeds. **Zero** ever
  reach 0. Writing `GRIEVANCE_SETTLED` on a decay-to-zero transition would produce a rule that can
  never fire — the covert-coup shape the phase exists to remove, reintroduced.
- **Peace is partial by design.** `SignPeace` cancels exactly a third
  ([ResolutionPhase.cs:248-251](src/WorldBuilder.Core/Rules/ResolutionPhase.cs#L248-L251)), and all
  23 peace signings across the panel leave a residual grudge. Emitting `GRIEVANCE_SETTLED` there
  would double-name an event that already exists and already renders.

There is one existing point with the right shape and a healthy population: **a cross-faction
marriage between two factions that already hold a grudge.** 33 of 42 such marriages qualify, on every
seed (4, 15, 3, 7, 4). Today the grudge only *discourages* the match by 8 weight
([LifePhase.cs:140](src/WorldBuilder.Core/Rules/LifePhase.cs#L140)) and nothing settles.

This needs no new persistent state, so it does not meet the §1 drop rule. But it is a **new
mechanic**, not an emission of an existing one, which makes its §2 materially larger than the other
two — a constant to argue, a branch structure to design, and a change to how marriage interacts with
grievance. Flagged rather than dropped; the call is the brief's, not mine.

### `CONFLICT.SIEGE` — dropped

No siege state exists anywhere in `src/`. `Place` carries population, yield, stockpile, controller and
cell; no garrison, no besieger, no under-siege flag. `ArcKind` has War, Famine, Feud, Plot,
Succession, Plague — no Siege. The only occurrence of the word outside the enum and the renderer is a
comment in `ChooseField` explaining that armies do *not* besiege the same field forever.

A siege is definitionally multi-year — laid in one year, relieved or fallen in another — and that
requires persisting besieger, place and start year across ticks. Emitting it as a same-tick flavour
line before a battle would give it one reachable outcome and no causal issue, failing §2.2 and
adding a leaf that renames `CONFLICT.BATTLE`.

**Dropped under the §1 pre-committed rule.** Backlog card below.

---

## §6 queue additions earned by this step

**Backlog card — `CONFLICT.SIEGE` deferred.**
State required: an open-siege record (besieger faction, target place, start year) surviving across
ticks, plus a resolution path with at least two outcomes (relieved / fell). Most natural home is a new
`ArcKind.Siege` carrying the place on `Sides`, which puts it with the arc machinery rather than on
`Place`. **Owning stage: whichever stage takes conflict depth** — not this phase, and not designed
here.

**Second card — alliances and trade survive the death of a party.**
`POLITY.COLLAPSE` removes no relations. A destroyed faction keeps its alliance and trade edges
permanently (11 of 13 collapses hold a live alliance, 13 of 13 a live trade tie). This is a defect
independent of the four dead kinds and would be a defect even if this phase is shelved. It is also
where `ALLIANCE_BROKEN`'s and `TRADE_COLLAPSE`'s second branches live, so the two should be settled
together or the branch design will be built on top of a bug.

---

## What §2 must not skip, on the evidence here

Recorded now because the audit surfaced it, not as work done:

1. **The mechanic-change budget binds.** The standing budget (`docs/phase-carry-forward.md`, "Hard
   budget") is *no new mechanics, no new checker rules, ruleset stays at 4*. Three emitters plus §4's
   explicit move to ruleset 5 is squarely a mechanic change. §2 says escalate rather than override.
   **This is an escalation, not a proceed.**
2. **Renderer check, partially done.** All four renderers are real one-liners, not stubs. None takes
   an optional numeric input, so the omission-vs-connective-text risk is narrow — but all three
   surviving renderers interpolate `{obj}` through `Label`, which yields the literal string
   `"someone"` for `EntityId.None`. "trade between the Wurn League and someone breaks down" is
   connective text with a missing input. At every trigger point identified above the object is a real
   faction, so this is a guard to assert, not a bug to fix.
3. **Branch reachability is demonstrated pre-change, which is the strong form.** The counts above are
   on the ruleset-4 stream, so they show the trigger *sites* are reached frequently without the
   change being made first. They are not predictions of post-change counts — adding an emitter moves
   the RNG stream, which §2 already expects. §2's demonstration requirement still has to be met on
   the changed stream.
4. **No checker rule, confirmed correct.** §2's instruction to add none matches what the extraction
   floor does: a rule written before its construction can occur extracts 0 forever and `rule-inert`
   cannot distinguish it from a rule that is merely quiet.

---

## Halt conditions

| condition | state |
|---|---|
| After §1, always, with the audit table and surviving set | **halted here** — this document |
| Mechanic-change budget binding | **binds** — see item 1 above; escalated, not overridden |
| Layer 1 dynamics invariants regressing | not reached — no change made, 584 tests green |
| remaining §5 conditions | not reached — all are §2/§3 conditions |

The two parked failures in §5 were not touched and did not move: no run in this step changed the
stream. Seed 7 `distinct deep-chain shapes` and seed 99's 74 → 69 stand where they were.

---

## Deliverable

Three of four survive: **`ALLIANCE_BROKEN`**, **`TRADE_COLLAPSE`**, **`GRIEVANCE_SETTLED`**.
**`SIEGE` is dropped** and carded.

Ordered by cost, cheapest first — `ALLIANCE_BROKEN` (the break already fires; only the event is
missing), `TRADE_COLLAPSE` (needs one rule to end an edge that only ever grows), then
`GRIEVANCE_SETTLED` (needs a settlement mechanic designed). Shipping the first two and carding the
third would be, in the brief's own words, a success rather than a failure.

Nothing beyond this table is decided here, and no code was written.
