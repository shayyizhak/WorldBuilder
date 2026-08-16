# Stage 6 — the geography substrate. Phase report.

Engine `1.2.0`, ruleset `3` → `4`. Five seeds: 7, 42, 99, 1234, 2025.

This phase was a build rather than a diagnosis, and the budget model in §5 of the
loop prompt was written for that. What follows is the record of what was built,
what it cost, what was found on the way, and the one judgement §9 asks for.

---

## 1. What was built

**4.1 — the bundle writer and the header extension.** `WorldHeader` now carries a
sha256 per stored artefact and a fingerprint for the render cache. `WorldBundle`
writes and verifies them, and `WorldBundle.Open` is the single entry point every
reader goes through, so no command can acquire a log without both the
compatibility check and the integrity check having run.

A mismatch throws. That is the deliberate exception to this engine's otherwise
uniform "report it and open anyway" policy, and the reason is that every other
provenance mismatch leaves a readable world behind it and this one does not: a
board whose hash has moved silently changes every distance a history was built
out of, and nothing downstream looks unusual. Verified end to end — write,
verify, alter one byte, verify again (exit 1), open (exit 1, with the two hashes
named).

**4.2 — the physical layer, imported.** `Board` holds cells, terrain, biome,
per-cell move cost and a symmetric adjacency graph, with all-pairs shortest
paths over it. `AzgaarImport` reads Azgaar's cell export; its output is
consumed and no generator code is linked, compiled in or vendored, so the
GPL-3.0 question §8.6 reserves for escalation never arose.

`BoardIo` writes the artefact by hand for the same reason `JsonlIo` does — the
file is hashed into the world header, so field order, number formatting and line
endings must be properties of the writer rather than of the machine. `maps/**`
is pinned `-text`; the working tree and the git blob hash identically, so a
fresh clone reproduces every world's board.

Structural checks run at construction. Asymmetric adjacency is the one worth
naming: it produces a distance that depends on which way it is asked, and both
directions return a plausible number.

**4.3 — the world on the board.** Positions are assigned at worldgen and
recorded on the genesis event. The alternatives were considered and are recorded
in `Siting`: deriving at load time would make a town's position a function of
whichever build opened the file, and there is no existing adjacency to seed from
— a `Place` has a parent and nothing else.

Settlements take habitable ground and mines take highland, which is not
decoration. It means the richest places on the map are also the hardest to
reach, which was already true of them economically: a mine cannot feed itself,
and now it is also up a mountain.

The region carries no cell, deliberately. It is the ground the board is made of
rather than a point on it — never marched on, held, raided or fought over — and
a cell for it would put a spurious position into every distance the engine
measures. §9's "every place has a position" is asserted over every place that
can be travelled to, with the exception stated rather than assumed.

**Positions changed nothing else, and that is checkable.** Siting draws on an
RNG purpose of its own, so it consumes nothing the population, yield and
treasury draws were consuming. All five seeds produce a byte-identical history
to ruleset 3; the only differences in the record are the `cell` field on each
place and the board's fingerprint on the genesis event.

| seed | shapes r3 | shapes at 4.3 | repeat r3 | repeat at 4.3 |
|---|---|---|---|---|
| 7 | 42 | 42 | 11% | 11% |
| 42 | 88 | 88 | 3% | 3% |
| 99 | 74 | 74 | 6% | 6% |
| 1234 | 64 | 64 | 9% | 9% |
| 2025 | 56 | 56 | 5% | 5% |

**4.4 — four mechanics, and no fifth.** Raid targeting, war declaration,
conquest holdability, and the pairing rules. Each measured separately, in the
order the loop prompt lists them.

**No threshold in `SimConfig` moved.** The mechanism that made that possible is
the substance of the design rather than a convenience: proximity is a percentage
where 100 is a typical separation, every consumer multiplies by it and divides
by a hundred, and so a pair at an ordinary distance scores exactly what it
scored before geography existed. Near and far are differences from the world,
not from a number somebody chose. This is the direct descendant of the raid
mechanic's undocumented flat 25 — a constant that had no defence when it was
finally found.

---

## 2. Step 0 — the ruleset-3 anchor

Five baselines, at `baselines/ruleset-3/seed-{7,42,99,1234,2025}`, each with a
manifest and a seal. All carry `verification: stability-anchor-only`; none
claims more. Nobody has read these worlds.

Engine `1.2.0` at `324c6b2`, ruleset `3` — read out of each world file's own
header rather than from the build that cut the baseline, so a tool run later
from a tree with geography in it cannot attribute the artefacts to the wrong
engine.

**The checker fingerprint comes out at `60f5b325` on all five, which is
byte-for-byte the figure computed by hand for the v1 baseline.** That is the
cross-check worth having: a new tool reproducing a number nobody gave it. It
also settles a question §7 would otherwise need answering separately — the
checker's source is unchanged across this phase, so no rule can have gone inert
as a consequence of it.

**Layer 5 unskipped and passed on all five: 0 failed, 0 noted.** The current
side of each diff was rebuilt from the stored render cache with
`wb book --check-only`, which constructs no client and so cannot repair a cache
miss by generating. Both sides are sidecars written by the checker that produced
them; no inference ran in the comparison.

**The v1 baseline is untouched** — `git status` reports no change under
`baselines/v1/`, and its seal still verifies.

**And Layer 5 handles the new mismatch correctly.** Run under the ruleset-4
build against a ruleset-3 anchor, it skips with the reason stated, which is
§9's last exit criterion:

> SKIPPED — baseline is ruleset 3 and this build runs ruleset 4. The rules
> changed what the simulation produces, so the stored world and the current one
> are different worlds and a diff between them means nothing.

**One process note, stated because it matters to how much the anchor is worth.**
§1 says this comes before any geography work. Generation ran for the whole
phase and the code was written alongside it, so the two overlapped in wall-clock
time. What §1 is actually protecting is intact: every archived artefact was
produced by a pristine build of `324c6b2` in a separate worktree, before a line
of geography existed, and the Layer 5 verification was run with that same
binary. The anchor is a ruleset-3 anchor in substance and not only in label.
Generation also failed four times on model timeouts and was resumed from the
render cache, which cost nothing but wall-clock: a resumed run re-serves every
passage already written and pays only for what is missing.

---

## 3. §6 — the hypothesis, tested deliberately

The prediction was written to `out/stage-6/predictions.md` before `wb test
dynamics` had been run once in this phase. No figure below was seen before the
prediction was written down.

### What was predicted

That the hypothesis — *causal variety tracks how many mechanics have genuinely
reachable branches* — would **not** be supported, because three of the four
changes are weighting inputs rather than new branches. Specifically: 4.4.1 flat,
4.4.2 **down** on at least three of five seeds and the largest single move,
4.4.3 up a little, 4.4.4 flat; and **net down on at least three of five seeds**.
Verbatim repeat rate predicted **up**, and worse on the seed already failing it.

### What happened

`distinct deep-chain shapes`, per seed, after each change:

| seed | r3 | 4.4.1 raid | 4.4.2 war | 4.4.3 conquest | 4.4.4 pairing | net |
|---|---|---|---|---|---|---|
| 7 | 42 | 42 | 54 | 54 | 45 | **+3** |
| 42 | 88 | 88 | 100 | 100 | 99 | **+11** |
| 99 | 74 | 74 | 60 | 59 | 69 | **−5** |
| 1234 | 64 | 62 | 67 | 67 | 97 | **+33** |
| 2025 | 56 | 56 | 59 | 61 | 66 | **+10** |

`verbatim repeat rate`:

| seed | r3 | net |
|---|---|---|
| 7 | 11% (failing) | **5%** |
| 42 | 3% | 7% |
| 99 | 6% | 6% |
| 1234 | 9% | 6% |
| 2025 | 5% | 8% |

### The verdict, stated plainly

**The prediction was falsified, and comprehensively.**

- 4.4.1 held: flat within ±2 on every seed, inside the ±4 predicted.
- 4.4.2 was predicted **down on three of five** and went **up on four of five**.
  It was correctly predicted to be the largest single move.
- 4.4.3 moved two points or less on every seed. The prediction was conditional
  on 4.4.2 having fallen, so it cannot be scored as written.
- 4.4.4 was predicted flat within ±4 and moved between −16 and +30. Falsified.
- **Net was predicted down on three of five seeds and was up on four of five.**
- Verbatim repeat was predicted up. It fell on the seed that was failing, from
  11% to 5%, and that metric now holds everywhere.

### What that says about the hypothesis

The reasoning behind the prediction was that only 4.4.3 adds a *branch* — an
outcome a mechanic could not previously produce — and that the other three
merely re-weight outcomes that already existed. That reasoning was sound as far
as it went and the conclusion drawn from it was wrong, which means the
hypothesis as stated is **incomplete rather than refuted**: causal variety rose
sharply from changes that added no branches at all.

The pre-registered alternative is what happened, and it is worth quoting from
the prediction document because it was written before the measurement:

> distance makes *which* neighbour a house fights a stable fact about the world
> rather than a fresh roll, and stable facts are what let chains grow long.

The mechanism is visible in the repeat-rate result, which is the cleanest
evidence in the phase. Two previous rounds diagnosed the seed-7 repetition
correctly and neither fixed it; it was parked as unattributed. A house with no
map picked its rival by grievance alone, and grievance is sticky, so the same
two names transacted forever — the same insult, the same raid, the same refused
demand. Distance did not stop them repeating. It gave the world *more than one
plausible pairing to repeat with*.

So the revised statement, offered as a hypothesis rather than a conclusion:
**causal variety tracks how many distinct, stable configurations the world can
be in — of which reachable branches are one source and a differentiated map is
another.** That is a wider claim than the original and it now has two kinds of
evidence behind it rather than one.

**Seed 99 is the counter-example and is not explained away.** It fell 74 → 69,
and the whole of the fall happened at 4.4.2 (74 → 60) with 4.4.4 recovering most
of it. Seed 99 is the seed where distance most reduced the number of wars. Not
diagnosed; recorded.

---

## 4. §7 — downstream checks

**Layer 3 green.** 34 rows, 34 pass, 0 not caught, 0 false positives.

**No rule went inert, and this is asserted rather than asserted-of.** The
current ruleset-4 build was pointed at the ruleset-3 render cache with
`wb book --check-only`, which constructs no client and so cannot repair a miss
by generating. It reproduced `chronicle-42.md` **byte-identically** and
`chronicle-42.findings.json` **byte-identically**. The coverage block is
therefore unchanged across the phase, which is the strongest available form of
"no rule stopped reading something it used to read": the checker's code did not
change, and its output over the same prose is bit-for-bit what it was.

Coverage for the rule families touching changed mechanics (`action` covers
raids, wars and conquests; `outcome`, `quantity` and `date` carry their figures):
recorded in full at `out/stage-6/measurements/07-coverage-seed-42.txt`. No
family reads zero where the construction is present.

**Every mechanic whose distribution changed gained a Layer 1 metric.** One shape
for all four rather than a bespoke figure each — the near/far split of what the
mechanic actually did — on the established outcome-spread bar of "no more than
90% one way" rather than a bar invented for the occasion. Each reports its `n`.
Reachability is asserted by the bar itself: a mechanic that only ever acted
nearby reads 100% one way and fails.

| metric | pooled | n |
|---|---|---|
| raid targeting reach | 64% one way (50 near, 89 far) | 139 |
| war declaration reach | 86% one way (3 near, 20 far) | 23 |
| conquest reach | 66% one way (9 near, 18 far) | 27 |
| alliance reach | 50% one way (6 near, 6 far) | 12 |
| marriage reach | 66% one way (13 near, 26 far) | 39 |

War declaration at 86% is the tightest and is watched rather than acted on. Its
`n` is 23, where the achievable values are four points apart, so it is not a
figure to tune against.

Two further structural invariants: **places on the board** (every place that can
be travelled to has exactly one cell, on the board, on land, and no two share
one) and **distance can vary** (the proximity range across a world's places must
be a range and not a single value — the reachability guard for every rule that
multiplies by a proximity).

**Layer 1 overall: one failure, down from three.** Ruleset 3 failed
`distinct deep-chain shapes` on seeds 7 and 2025, and `verbatim repeat rate` on
seed 7. Ruleset 4 fails `distinct deep-chain shapes` on seed 7 alone (45 against
a bar of 60). `verbatim repeat rate` left `KnownFailing` by holding, which is
the only sanctioned way out; `distinct deep-chain shapes` stays, with its
category, rationale and owning round.

**Places now have properties the renderer can reach — and it cannot reach them.**
Reported as §7 asks, because this is a new fabrication class with no coverage:

- The context pack describes a place as `"<name> (p:n) — a settlement"`. It
  carries no terrain, no cell, no distance and no adjacency. Nothing geographic
  is exposed to the model today.
- Measured rather than assumed: across four rendered chronicles and 12,180 words
  of prose, the spatial vocabulary sweep returns **one** hit — "beyond its
  borders", used figuratively about projecting force. Zero uses of north, south,
  coast, river, mountains, valley, road, distance, travel or leagues.
- **The checker cannot verify a claim about either.** None of the sixteen rules
  reads a cell or a terrain. If a passage said "the distant Vea Lode Covenant"
  or "across the mountains", the checker would treat it as ordinary prose.

So the exposure is currently structural-zero and entirely unguarded. The moment
a pack carries terrain — which is the natural next step, since a mine in the
mountains is exactly the sort of particular that makes prose concrete — the
model gains a whole vocabulary it can be wrong in, with nothing watching. Per
§7, building that rule is the next phase's decision and not this one's. It is
recorded here as the recommendation it is.

**Fixtures still read the sealed baseline.** They did, and one caller did not —
see finding 2.

---

## 5. Findings, in rank order

### 1. The proximity scale was calibrated against a distance no world contains

**Found by a metric written in the same phase, which is the whole argument for
writing it.**

Proximity was defined against `Board.ReferenceCost`, the median separation over
every pair of land cells. That is correct about the board and useless about the
world: `Siting` spreads places deliberately, choosing each new site as far as
possible from everything already placed, so every pair of places sits well
beyond the board's median. Every proximity that ever occurred came out below
100, and four mechanics documented as "centred on an ordinary distance" were in
fact discounted at every distance that existed.

It was invisible in the code — the arithmetic is right, the comments are
coherent, the tests on a hand-built board passed — and unmissable in the
figures. War declaration reported **0 near and 29 far across the whole panel**,
which is a branch that cannot fire wearing a percentage. That is the same defect
class as `CoupDecidedPct`: a plausible number, tuned against, from a numerator no
path could move.

The reference is now the median between the places a world actually has. All
four attributions in §3 were re-measured from scratch under it; the numbers in
this report are the corrected ones. `Board.Proximity` was deleted rather than
kept beside `Geography.Proximity`, because two distance functions is exactly
what §2 pre-committed against.

**Cost: one full re-measurement of the phase's headline result.** Worth stating,
because the alternative was shipping four mechanics whose documentation
described behaviour they did not have.

### 2. `wb test corpus` had been throwing since ruleset 2

A corpus row is a fabrication found by hand in prose about one particular world.
The command re-simulated to obtain that world, which silently converted every
row into an assertion about whatever the current rules produce — so the first
genuine ruleset change moved the world out from under all thirty-four of them,
and the command died with an unhandled `InvalidDataException` on a scope that no
longer existed.

**The fix had already been made, in the other copy.** The test suite learned
this at ruleset 2 and pinned its fixture to the archived record, with a long
comment explaining why. `wb test corpus` was never touched. One idea implemented
twice, fixed once, failing in the copy nobody ran — which is the silent-path
family in a place that is not a checker rule, for the third recorded time.

The resolver now lives in one place and both callers use it. A missing scope is
also now reported as a failing row rather than thrown out of the process, where
it took the other thirty-three rows with it.

**This was not caused by geography.** Confirmed by running the pristine
ruleset-3 binary, which fails identically.

### 3. The dynamics panel counted every world twice

`panelLogs.Add(view.Log)` appeared twice in `TestDynamics`, so every pooled
distribution metric had double its true `n`. The percentages were unaffected —
both branches doubled — so the figures read correctly and the sample size
justifying them was a fiction. That matters precisely here, where "assert the
rate only where `n` supports it" is the rule these metrics exist under. Found
while adding the geography metrics beside them.

### 4. `verbatim repeat rate` was closed by something that was not aimed at it

Recorded as a finding because the diagnosis is worth keeping. See §3.

---

## 6. Budget and scope

**Four mechanics, no fifth.** No mechanic outside §4.4's list gained a distance
input. Two adjacent temptations were noted and refused, as §5 anticipated they
would be:

- **Tribute target selection** (parked, previous phase: houses demand of whoever
  they resent rather than whoever is weak). Distance would make this look
  fixable — demand of a *near* weak house — and it is not in scope. The
  interaction is real: proximity now shapes who a house is at war with, which
  shapes who it resents, which shapes who it demands of. Noted, not touched.
- **Heir selection criteria** (parked, previous phase: loyalty names the
  candidate, ambition wins the contest). Geography does not touch it. Noted for
  completeness because §5 named it.

Battle resolution, siege, defection, exile return and trade all take place
between parties at a distance and none of them gained a distance input. That is
the budget working.

**Findings not about geography were characterised and parked, not fixed** —
except where they fall under §5's "always in scope" clause, which findings 1, 2
and 3 do: a metric that cannot vary, a check that had stopped running, and a
denominator that was not real.

---

## 7. Escalations

**None.** No question of semantic intent arose that §2 did not answer; no fifth
mechanic needed a distance input; no threshold value had to change; the sealed
v1 baseline was not modified and its seal still verifies; no accounting identity
failed to balance; and no licence question arose, because nothing was embedded.

One decision was taken rather than escalated, and is recorded here because a
reader may reasonably want to have been asked: **the stored board was made by
`wb map make` rather than imported from Azgaar**, because this machine has no
Azgaar export and the generator is a browser application that cannot be driven
headlessly. §2 settles that the map is a stored artefact and that Azgaar is the
primary source; it does not say what to do when no export is to hand. The
importer for the real path is built and tested, the artefact format is the same
for both, and a board says in its own provenance which kind it is — so replacing
it later is a drop-in. Per §8's closing instruction this was decided and
recorded rather than halted on.

---

## 8. The judgement §9 asks for

**Does the world read better with geography in it? Yes — modestly, structurally,
and not in the way I expected.**

The evidence is the chain-shape and repeat-rate numbers in §3 plus a read of one
scope. The scope was rendered under ruleset 4 on seed 42: the Griwick Compact,
years 2–21, 42 events, 2 suspect tokens of 142 — one the known
`ambiguous-short-name` style note, one an `outside-the-window` year. Neither is
geographic.

Set beside the comparable ruleset-3 passage, the difference is specific enough
to name.

**Ruleset 3.** *"all three raids it sent out against Vea Lode, Meigate, and
Threi Cut between 24 and 29 were beaten off"* — three targets, unrelated to each
other and to anything the power was doing. Its battles are at Laehiford, Hadale,
Meigate and Kebarrow: four places, no theatre.

**Ruleset 4.** The war is declared over Threi Cut, and then fought at Threi Cut
in 5, 6 and 7 before the Compact takes it. The next battle is at Laehiford, and
the Compact takes that too. Its raids fall on Kebarrow, Laehiford, Hadale and
Meigate — which are, by the end, the places it holds. The final map is a
connected block rather than a scatter.

**That is what geography bought: conflict acquired a place.** A war is now about
somewhere and is fought there; a conquest is next to what you already hold; a
house's enemies are its neighbours. None of that was true before, and all of it
is the kind of thing a reader notices as coherence without being able to say
why.

**The honest qualification is that the prose contains no geography at all.** No
terrain, no distance, no direction — the render pack carries none, and the
sweep in §4 found one figurative use of "borders" in 12,180 words. A reader gets
the *pattern* geography imposes and none of its vocabulary. The improvement is
in the shape of the history, not in its description, and the descriptive half is
sitting there unbuilt with no checker rule waiting for it.

**And one seed got worse.** Seed 99's causal variety fell 74 → 69, and seed 7
still fails the chain-shape bar at 45 against 60, though it improved from 42 and
its repeat rate went from failing to comfortably passing. A phase that improved
four seeds and cost one is a phase that improved things, but "the world reads
better" is not true of every world.

**The strongest single piece of evidence is the one nobody was aiming at.**
`verbatim repeat rate` had survived two rounds that diagnosed it correctly and
fixed the wrong thing, and was parked as unattributed. It closed here, without
being targeted, because a house with a map has more than one plausible enemy.
That is what "less able to support a campaign" looked like in practice, and it
is now measurably gone.
