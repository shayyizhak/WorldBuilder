# Phase loop — Stage 6, the geography substrate

Follows the engine dynamics phase. Same shape: method and decision rules pre-committed, queue extensible, halts only for semantic intent.

**This phase is a build, not a diagnosis.** The last one was five fixes to things that were wrong; this one adds something that was never there. The budget model in §5 differs accordingly.

Write a checkpoint report to `out/` after each queue item. Do not halt for them.

---

## 1. Step 0 — cut a ruleset-3 baseline first

**Before any geography work.** This is a prerequisite, not a nicety.

Layer 5 has skipped since ruleset 2 — correctly, since diffing against a ruleset-1 baseline compares different worlds. But that means two rulesets of change have landed with **no golden anchor at all**, and this phase is about to change the world again, more than either previous ruleset did. Going into the largest build since v1 with regression protection dark is the wrong order.

Requirements:

- **All five seeds:** 7, 42, 99, 1234, 2025.
- **`verification: stability-anchor-only`**, never `hand-verified`. Nobody has read these worlds. A golden diff needs its anchor stable, not correct — but the distinction must stay legible, and only the sealed v1 seed-42 baseline is hand-verified.
- **Create-only**, per the existing rule. The v1 baseline is untouched and stays sealed; this sits beside it.
- **Manifest carries `ruleset_version: "3"`**, `engine_version`, `engine_commit`, the checker fingerprint, and a sha256 per artefact.
- This requires **generation** — the one place in this phase where inference runs.
- **Layer 5 then unskips** for ruleset 3 and must pass against the new anchor before the next queue item begins.

**Halt when:** five baselines exist, each with a manifest and a seal; Layer 5 runs and passes against ruleset 3; the v1 baseline is unchanged and `.sealed` still verifies.

---

## 2. Pre-committed decisions

Settled. Do not escalate these.

**Import the physical layer; simulate the political layer on top.** Terrain, biomes, adjacency and travel cost are imported. Who holds what, and who fights whom over it, is simulated. The generator supplies a board, never a history.

**Generation is one-time. Store the artefact; never regenerate from a seed.** Map generators are not reproducible across their own versions — this is the same class as model variance, and the answer is the same one Stage 3 reached. The imported map becomes a stored artefact in the world file, hashed into the header. `world_seed` does not reproduce it.

**Watabou TownGeneratorOS is GPL-3.0 — do not embed it.** Its *outputs* are permissively licensed, so the hosted tool is fine. Azgaar supplies a cell-adjacency graph directly and is the primary source for the region layer. MFCG has a de-facto JSON API for settlement detail if it is wanted later.

**Geography before economy.** Distance gates conflict, trade, alliance and — at Stage 11 — rumour. Economy coupling is a later phase and is out of scope here.

**Travel cost is a property of the board, not of a mechanic.** One distance function, consulted by every rule that needs it. Not a per-mechanic notion of nearness.

---

## 3. The Stage 3 carry-forward lands here

The world header's **artefact hashes and render-cache fingerprint** were deferred because `wb run` knew nothing about renders and no world *bundle* existed. An imported map is exactly the artefact that forces one: it must be stored with the world, hashed into the header, and travel with it.

So this phase builds the bundle writer. Requirements:

- The header carries a hash per stored artefact, and a render-cache fingerprint.
- Opening a bundle verifies the hashes and **fails loudly on mismatch** — a map that does not match its hash is not a map to proceed with.
- The existing opening policy still applies: newer ruleset refuses, older opens with a note, no provenance opens with a note.

---

## 4. The queue

**4.1 — The bundle writer and the header extension.** §3. Needed before a map can be stored at all.

**4.2 — Import the physical layer.** Terrain, biomes, cell adjacency, travel cost, as an engine-readable structure. Store it; hash it; open it; verify it. **No rule consumes it yet** — this item ends with geography present and inert, which is a checkable state and a safe place to stop.

**4.3 — Place the existing world on the board.** Existing places (`p:1`–`p:8` on seed 42) acquire positions. Decide and record how: derived from the map, assigned at worldgen, or seeded from existing adjacency if any is implicit. Assert every place has exactly one position and every position is on the board.

**4.4 — Distance enters the rules, one mechanic at a time.** Each is a separate measurement, because several will move the same populations:

- **Raid targeting** — the obvious first, since target memory already exists and distance is the natural second input. A house that raids across the map is a house with no geography.
- **War declaration** — who can reach whom.
- **Conquest** — whether a taken place is holdable.
- **Alliance and marriage** — proximity as a precondition rather than a coincidence.

Measure each separately. Report the outcome distribution of every affected mechanic before and after, per seed.

**4.5 — Whatever 4.4 surfaces**, adjudicated in rank order against §5's bar.

---

## 5. Budget and the bar

A build phase's risk is not endless fixing but **scope creep into mechanics that geography merely touches**.

**Budget: the four mechanics named in 4.4, and no fifth.** A mechanic not on that list does not gain a distance input in this phase, however natural it looks. Adding one is an escalation.

Findings that are not about geography are **characterised and parked**, not fixed. The bar is unchanged: does this make the world less interesting to read, or less able to support a campaign. Two parked findings already exist from the last phase — tribute target selection and heir selection criteria — and both will look temptingly adjacent once distance exists. Neither is in scope. Note the interaction and move on.

Always in scope, never against the budget: unreachable branches, metrics that cannot vary, metrics that report zero without meaning none, accounting identities that do not balance.

---

## 6. The hypothesis to test deliberately

The last phase left a hypothesis with three data points: **causal variety tracks how many mechanics have genuinely reachable branches.** `distinct deep-chain shapes` fell 54 → 44 during the raid work and rose 44 → 55 across two later changes that did not target it.

This phase adds reachable branches to four mechanics. That is a real test rather than an incidental one.

- **Predict, before measuring**, what happens to `distinct deep-chain shapes` and `verbatim repeat rate` on each seed.
- Record the prediction, then measure after each of 4.4's four changes.
- Report whether the hypothesis held, and say plainly if it did not.

A falsified prediction is a good outcome and must be reported as one. The last phase had one and it was the most useful line in the report.

---

## 7. Downstream checks

After the map exists and after any rules change:

- **Layer 3 green**, every corpus case still firing its rule.
- **Coverage block reported** for any rule family touching a changed mechanic. A rule going inert is the silent-path signature.
- **Every mechanic whose distribution changed gains or keeps a Layer 1 metric**, with `n`, reachability, and a positive control if it asserts absence.
- **Places now have properties the renderer can reach.** Check whether terrain or distance leaks into prose, and whether the checker can verify a claim about either. A fabricated distance is a new fabrication class and nothing currently checks for it. Report the exposure; building the rule is the next phase's decision, not this one's.
- **Fixtures still read the sealed baseline.** The ruleset-3 baseline joins it as a second source; neither is ever re-simulated.

---

## 8. Escalate — halt and report — only for these

1. **A question of semantic intent** the docs, the roadmap and §2 do not answer. What a mechanic is *for*.
2. **A fifth mechanic would need a distance input.**
3. **A threshold value would have to change.**
4. **The sealed v1 baseline would be modified**, or the ruleset-3 baseline needs recutting mid-phase.
5. **An accounting identity does not balance** and the shortfall cannot be named.
6. **A licence question** — anything that would embed rather than consume a generator's output.

Everything else: decide, record the reasoning, continue. Do not halt to confirm an adjudication, to ask whether a constant is reasonable, or to check whether a finding is in scope — §2, §5 and §7 answer those.

---

## 9. Phase exit

- A ruleset-3 baseline exists for five seeds and Layer 5 passes against it.
- The bundle writer exists; artefact hashes and the render-cache fingerprint are in the header; a mismatched hash fails loudly.
- A map is imported, stored, hashed, and verified on open.
- Every place has a position; every position is on the board.
- Four mechanics consume distance, each measured separately, with before-and-after distributions per seed.
- Every Layer 1 metric holds or carries its category, rationale and owning round.
- Layer 3 green; no rule inert.
- The hypothesis in §6 is tested and the result reported either way.
- Ruleset bumped once, header carries it, Layer 5 handles the mismatch correctly.

**Then the phase report**, including a stated judgement: **does the world read better with geography in it?** Not "is the code correct" — whether distance made the history more interesting. That is the actual test, and the evidence for it is the chain-shape and repeat-rate numbers plus a read of one chronicle scope.

---

## 10. Prohibitions

1. No threshold value changes.
2. No rationale after its measurement.
3. Nothing leaves `KnownFailing` by hand.
4. The sealed v1 baseline is read-only.
5. No regeneration of the map from a seed. It is a stored artefact.
6. No embedding of GPL-3.0 generator code.
7. No fifth mechanic gains a distance input.
8. No economy work. That is a later phase.
