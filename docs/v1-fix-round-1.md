# v1 Fix Round 1 — fabrication, secrets, and render scope

## Context

v1.0 and v1.1 are built. Ollama + Qwen 3.6 is rendering, and a chronicle was produced for seed 42 covering years 1–51 with four render scopes: causal chain, war, faction, and reign.

The headline result is good: **faction- and reign-scoped aggregation genuinely beats the raw event log.** The Kebarrow Compact passage compresses roughly twenty years and dozens of events into a narrative with real shape, ending on a judgement about the period as a whole that appears in no single event. That was the one thing v1 had to prove and it proved it.

Both evaluation criteria were then run properly against `world-42.log`. One passed, one failed.

- **Chronicle beats the log:** passes for faction and reign scopes, fails for chain scope.
- **Fabrication rate near zero:** fails. Multiple invented facts, two date errors, and one contradiction between two renders of the same input.

This round fixes the fabrication vector and two structural problems that surfaced with it. **Do not build v1.2 query yet.**

---

## 1. Engine bug: dangling causal references

This is a v0 engine defect, not a render defect, and it is the direct cause of the worst fabrications. It was missed in earlier analysis because the chain-depth scripts filtered unresolvable causes with `if cause in events`, silently dropping them — so previously reported depth figures were measured on a graph with holes in it.

Roughly 9% of causal edges reference event IDs that do not appear in the log:

| World | Events | Distinct causes referenced | Dangling causes | Dangling edge instances |
|---|---|---|---|---|
| world-42 | 699 | 341 | 66 | 175 |
| world-99 | 519 | 291 | 71 | 186 |
| world-1234 | 422 | 240 | 69 | 139 |
| world-2025 | 441 | 251 | 67 | 124 |
| world-7 | 344 | 203 | 65 | 114 |

Concrete cases in `world-42.log`:

- `e:151` (`ECONOMY.FAMINE` at Meigate, Y10) cites `<= e:150`. No event `e:150` exists in the log.
- `e:277` (`CONFLICT.RAID` on Vea Lode beaten off, Y19) cites `<= e:151,e:261`. No event `e:261` exists in the log.

**Determine which of these is true, then fix accordingly:**

- **(a) The events exist in the engine but are filtered out of the log** — e.g. below a significance threshold. If so, the log emitter is producing a file with broken referential integrity, and anything consuming the log (including the renderer) will hit unresolvable IDs. Either emit referenced events regardless of significance, or emit a resolvable stub.
- **(b) The edges are written with bad IDs** — a real causality bug. Fix at the point where causes are attached.

Add a **referential integrity check** to the build: every `<=` reference must resolve to an event present in the same log. This should fail the build, not warn.

Re-run the chain-depth and shape metrics after fixing. The previously reported figures (max depth 9–19, 387 distinct deep-chain shapes) need to be re-measured on a complete graph.

---

## 2. Missing input must render as omission, never as invention

When the renderer received the unresolvable IDs above, it invented content rather than leaving a gap. Both of these appear in the chronicle and neither corresponds to any event:

- *"a harvest count at Meigate revealed a grain shortage"*
- *"old grudges cooled and the standing of the Kebarrow Compact eroded"*

The second appears in **both** chain renders, so it is a reproducible failure mode, not a one-off.

**Rule: if an input reference cannot be resolved, the renderer omits it silently. It must never generate connective or explanatory text to fill the gap.**

Make this a test rather than a prompt instruction alone: feed a render request containing a deliberately broken cause reference and assert the output contains no text corresponding to it.

---

## 3. Invented motivation and mislabelled outcomes

Beyond the dangling-reference cases, the renderer is inventing causal and psychological content. v1 forbids texture; these all violate it.

From the reign section:

- *"his violent seizure of power from Kou Peis in Y0031"* — the actual events are `POLITY.SUCCESSION_DISPUTED ... (rule: election)` (`e:549`), `POLITY.SUCCESSION ... (claim overturned)` (`e:550`), and `POLITY.EXILE ... the losing claim` (`e:551`). **An election was rendered as a violent coup.** This is the most serious single error in the chronicle because it inverts the meaning of a recorded event.
- *"Le Vild's paranoia led him to attempt the murder of..."* — invented motivation
- *"years of simmering resentment among the nobility"* — invented motivation
- *"systematically rooting out conspirators ... executing some"* — invented; the log records exiles and assassinations with specific outcomes, not executions

Tighten the render prompt so that succession mechanism (`election`, `primogeniture`, `strongest`, `coup`, `claim upheld`, `claim overturned`) is treated as **load-bearing detail that must be reported accurately**, and so that no motivation, emotion, or intent may be attributed to any actor unless it appears in an event.

Add these succession-rule labels to the fabrication check as exact-match assertions.

**Note what did work:** both war sections are completely clean. Declarations, battle sites, casualty counts (32, 142, 124), conquests, and both peace treaties with durations and stated causes all trace correctly to source events. The model is capable of accuracy — it fails specifically where the input has gaps or where it is tempted to explain.

---

## 4. Date arithmetic errors

Both chain renders misdate events, differently:

- **Chain v1** dates the Vea Lode raid to year 18. It is Y19 (`e:277`). It then states Meigate seceded "the following year" — but the secession (`e:283`) is also Y19.
- **Chain v2** dates the raid correctly to 19, then states Meigate broke "the following year" = 20. Still Y19.

The relative-time connective tissue ("two years later", "the following year") is being generated rather than computed. **Derive all intervals in the engine and pass them to the renderer as facts, or forbid relative time expressions entirely and use absolute years only.** The model should not be doing arithmetic.

---

## 5. The renderer is leaking secrets

`e:669` reads:

```
[Y0036] w:49  CONFLICT.ASSASSINATION  an attempt on Stald Gearngoll (a:80) fails at Vea Lode (p:7), unattributed  [secret]  <= e:519  e:669
```

The chronicle renders this as *"an unattributed attempt on Stald Gearngoll failed at Vea Lode, a consequence of Beas Krouthea's prior alignment with the Covenant"* — it **de-anonymised an explicitly unattributed event** and narrated a `[secret]` one as public history.

This is happening throughout. Beas Krouthea's secret conspiracy (`e:643`, `[secret]`) and Thosruld Lul's plots (`e:563`, `[secret]`) are both narrated openly.

The `[secret]` flag exists in the log and the renderer ignores it entirely. Because cached renders are canon, every passage generated this way bakes secret-leakage permanently into the world's text, and the v3 epistemic layer will have to unpick it.

**Add a visibility filter to the render pipeline now.** For v1 this can be as simple as excluding all `[secret]`-flagged events from render input. The full per-agent knowledge model is v3; this is the placeholder that keeps v3 buildable.

Separately: never attribute an event the log records as `unattributed`. That is a fabrication regardless of the secret flag.

---

## 6. Two renders of the same input produced different canon

The chronicle contains two renders of the same causal chain (labelled 10–40 and 10–36). They disagree on fact: v1 states Beas Krouthea's conspiracy was uncovered in 36 and he was exiled (`e:678`, `e:679`); v2 omits the exile entirely and substitutes the unattributed assassination attempt.

Both were generated from the same events. Both would be cached. Only one is correct.

This is what makes "cached renders are canon" load-bearing rather than aspirational. **Settle cache keying and sampling determinism before generating at volume:**

- Cache key must be a deterministic function of the input event set and the render scope, so the same request cannot produce two cache entries.
- Set an explicit fixed seed and low temperature for rendering. Qwen 3.6's shipped defaults (temperature 1, presence penalty 1.5) are wrong for this and must not be inherited.
- Re-rendering must be an explicit action that replaces a cache entry, never an accidental second write.

---

## 7. Drop the chain render scope

Faction and reign scopes work. Chain scope does not — it is transliteration, not aggregation:

> *"In 22, Saern Meastouth won Paernrom Sir away from the ruler of the Kebarrow Compact, and two years after that, in 24, Paernrom Sir won Paernmel Has away from the same ruler."*

That is one prose sentence per event with "two years later" as connective tissue. It is longer than the log lines it replaces and harder to scan. It fails the "chronicle beats the log" criterion outright.

The lesson: **the value comes from summarising many events, not from walking a chain.** Chain-tracing is a query-answering shape — the right output when a user asks *why did X happen* and wants the steps. Keep it for v1.2 in that role. Remove it as a chronicle format.

Retain and develop: **faction**, **reign**, **war**, and **year** scopes.

---

## 8. Output style consistency

Lower priority, but currently inconsistent across sections of the same document:

- Year format: `Y0031` in the reign section, "in 31" elsewhere. Pick one.
- Numbers: "forty-five dead" and "142 combatants" appear in adjacent sections. Pick one convention.
- The Kebarrow Compact section is a single unbroken ~400-word paragraph. Set a maximum paragraph length.
- Heading capitalisation varies ("the War for Threi Cut" vs "The Kebarrow Compact").

---

## Evaluation

Re-render seed 42 after the above and report:

1. **Fabrication rate, as an automated check** rather than a manual read. Extract every proper noun, year, and number from the rendered prose and assert each appears in the source event set. Report the count and list the failures.
2. **Secret leakage: zero.** Assert no `[secret]`-flagged event contributes content to any render.
3. **Referential integrity: zero dangling causes**, enforced at build time.
4. **Re-measured chain depth and shape counts** on the repaired graph, since the previous figures were computed on an incomplete one.

---

## What I want from you

Fix items 1 through 6 and re-render seed 42, then report the four evaluation results before starting v1.2 query.

Push back if any of this is wrong. In particular: if you think the dangling references are intentional (significance filtering working as designed) rather than a bug, say so and propose how the renderer should handle a legitimately absent cause. And if excluding all `[secret]` events makes the chronicle noticeably thinner or less coherent, say so — that is useful information about how much of the political layer is currently secret, and it may argue for rendering secrets as visible-but-unattributed rather than omitting them.
