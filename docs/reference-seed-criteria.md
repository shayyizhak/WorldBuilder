# Reference-seed criteria

**Written before any candidate world was examined**, per §4 and §6 of
`docs/archive/brief-closing-ruleset-6.md`. Committed on its own so that the ordering is checkable rather
than asserted. The only worlds looked at before this was written are the five current reference
seeds — already examined at length in the two previous reports — and the 90-seed measurement panel,
which supplies "what typical looks like" and contains no candidate.

---

## 1. What the reference panel is for

It exists so a person can read prose against a record. Five worlds is about as much as anyone will
read. It is **not** a sample of the engine's behaviour — that is what the measurement panel is for,
and conflating the two is the mistake `docs/panel-prereg.md` §1 was written to stop.

So a reference seed must be **a world that exercises the pipeline being verified**. It does not have
to be a *pleasant* world, and it must not be selected for being one.

---

## 2. What typical looks like

From the 90-seed measurement panel, null arm, each seed on its own board
(`docs/ruleset-6/war-panel.txt`):

| | median | q1 | q3 |
|---|---|---|---|
| events | 709 | 646 | 790 |
| distinct deep-chain shapes | 63 | 51 | 72 |
| runaway year | 35 | 18 | 52 |

Two of those are worth stating out loud before they are used, because both bear on §3:

- **The `≥ 60` shape bar sits at the median of ordinary worlds.** Half the engine's worlds fail it.
- **The Y40 runaway bar fails on more than half of ordinary worlds** — q1 is Y18, median Y35.

---

## 3. The criteria

A reference seed must satisfy all of these, at the ruleset it is being cut for.

> **R1. Both coup branches present in the record.** At least one `POLITY.COUP_RESOLVED` with
> `mode=exposed`, and at least one with `mode=seized`.
>
> The record must contain the constructions the verification layers read. `PlotLedgerTests` asserts
> both branches per seed, and the whole reason it exists is that the `seized` path was once
> structurally zero and nothing noticed. A reference world missing a branch cannot support the
> assertion it is there to support.

> **R2. The world fills the book's default scopes.** At least 2 war arcs, at least 2 reigns
> available, and at least 2 factions carrying `Major` events.
>
> Hand verification reads the book. A world that cannot fill the scopes `wb book` builds by default
> is verifying a smaller pipeline than the one that ships.

> **R3. Event count within ±35% of the measurement panel's median** — that is, **461 to 957**.
>
> A world less than two-thirds or more than half again the typical length is being used to verify
> the pipeline at a scope the pipeline will not normally see. The band is wide on purpose: it is
> there to exclude a world of half the usual size, not to pick a modal one.

---

## 4. Two of the brief's suggestions are rejected, with the reason

§4 offered "no runaway before Y40" and "at least two houses standing at the end". **Neither is
adopted, and this is a disagreement rather than an oversight.**

Both are the **brake problem**, which §5 of the same brief explicitly places outside this work.
Measured on ordinary worlds, the Y40 bar fails on more than half of them and its lower quartile is
Y18; three of the five current reference seeds finish ruleset 6 with one house standing. So a panel
selected on either criterion would be a panel selected to not exhibit an unfixed world-design
defect — and the defect would then be invisible in exactly the five worlds anyone reads.

That is the shape this phase has now met seven times: *a property that holds on the panel by
coincidence of the panel's construction cannot be detected by any test on that panel.* Choosing
reference seeds so that hegemony arrives late would manufacture the eighth instance deliberately.

**The brake problem stays visible in the reference panel, and stays on the list.**

---

## 5. The search procedure, fixed before looking

Where a current seed fails a criterion it is replaced by:

> **the lowest seed value ≥ 1 that satisfies every criterion in §3**, skipping the current
> reference seeds (7, 42, 99, 1234, 2025) and both measurement-panel ranges
> (9000001–9000207, 9100001–9100090).

Ascending from 1 is the most auditable rule available: there is no room in it for a world to be
chosen after its history was read. Whatever it returns is what is taken, and if the first passing
seed is a dull world, that is the correct outcome — the panel is not for picking pretty histories.

---

## 6. What is not being changed

**The reference panel keeps the stored board.** The brief correctly notes that a shared board is how
five re-fold sites reading the repository's board instead of the world's own stayed invisible. Giving
the reference seeds per-seed boards would invalidate the board hash in every sealed set and in the
v1 record, which Layer 3 depends on permanently. Recorded as owed, not done here.

**The sealed ruleset-3, ruleset-4 and ruleset-5 sets keep the seeds they contain.** A sealed set is a
record of what was verified then. If the live panel changes, the historical sets do not, and every
check that reads one keeps reading the seeds it holds — which is a distinction the code did not have
and now needs.
