# Insert for `WORLDBUILDER-PROJECT.md`

You updated §4, §6, §7 and §9 on your copy after my last pass, so my copy is stale and I'm not rewriting the file — paste these in rather than taking a whole new version.

---

## For §6, the checker — add after the Tiers subsection

### Counter shapes

`extracted` is not one quantity. It is three, and for two of the three shapes it is undefined rather than zero. Reporting all three as `extracted` is what makes the coverage invariants go vacuous exactly where they matter.

| shape | population | counters | `rule-inert` means |
|---|---|---|---|
| **Requirement** — engine supplies a list, rule checks the prose mentions it | items in the list | `required / satisfied / missing` | list non-empty and nothing checked |
| **Vocabulary scan** — marker from a constant list, support looked up in the body | marker hits in the prose | `scanned == fired + supported` | marker present, neither outcome produced |
| **Extraction** — pulls a candidate assertion out of the sentence | assertions extracted | `extracted / checked / unresolvable` | construction present, nothing extracted |

**The vocabulary-scan shape carries the failure history.** Every historical silent-path defect lived there — `"included"` absent from the partiality-marker list, `people`/`exiles`/`returns` absent from the countables lexicon. So `scanned == fired + supported` is the highest-value invariant in the scheme, and it is the one that does not yet exist. Until it does, a lexicon gap presents as nothing at all.

ACCOUNTING and FLOOR as originally stated apply to the extraction shape only. Requirement and vocabulary rules do not get a re-baseline when instrumented — they get a *first* baseline of a different quantity, and the distinction should stay visible in the record.

**A rule slot created by `Fired` alone is unfalsifiable.** `coverage` and `outcome` have no `Extracted` call sites, so `Inert()` flags them on every passage forever. A detector that alarms unconditionally is worse than no detector: it trains you to skip the output.

---

## For §4, lessons — three to add

**A doc comment asserting a property the implementation lacks is the same family as an error message printing the wrong limit.** `Coverage.cs:40` states the read-versus-passed premise cleanly and nothing enforces it, so it misled a reader who had the code open. Both are a claim in the record that nothing checks — same failure, different surface.

**Each arm of a pre-committed decision rule must be shown reachable against existing data before the measurement.** Two defects in one rule: a 20-point range criterion the population had never met (ruleset 3 was `width=42`), and an over-firing arm that could not vary at all, because a blocking rule fires only where it causes a holdout, so its count on surviving scopes is 0 at every ruleset. A dry run over existing baselines catches both for free. The degeneracy guard covers a statistic that varies too little; it does not cover one that cannot vary.

**Sidecars are the provenance record for findings.** A panel run that discards them cannot be interrogated afterwards — which emitter produced which row becomes unanswerable without a re-run. Retain by default.

---

## Also worth recording somewhere durable

`InstrumentationInvarianceTests` now asserts the engine reproduces all five sealed ruleset-4 baselines event for event. Nothing asserted that before, and every measurement against those baselines rested on it. Same family as everything else this phase turned up: a load-bearing property that no test held.

Four event kinds are declared, named and rendered but emitted by nothing — `ECONOMY.TRADE_COLLAPSE`, `DIPLO.ALLIANCE_BROKEN`, `CONFLICT.SIEGE`, `INTRIGUE.GRIEVANCE_SETTLED`. Covert-coup structural zero, four more times.
