# Brief — Layer 5 restoration and the skipped-row visibility fix

**Not a phase.** Four bounded items, no mechanics change, no new checker rules, ruleset stays at 5. Sits between step one and step two of `phase-relation-termination.md`.

**Order matters for one reason only:** item 2 should land before item 1, so that the cut is verified by a top line that cannot lie about whether Layer 5 ran. Otherwise the order is free.

---

## 1. Cut `baselines/ruleset-5/`

Five seeds, full contents: log, board, seal, `chronicle-{seed}.md`, `.findings.json`, `renders.json`. Real inference through ollama/`qwen3.6`.

This is the deferral I argued for and was wrong about. The argument — that step two rekeys events and expires the render half — applies to *every* ruleset change, so it would have argued against ever cutting a render baseline before the last change the project makes. Expiry at the next bump is the standing cost of having Layer 5, not a reason specific to this bump.

**Split the archive contract while you are in there.** `BaselineArchive.Contents` currently conflates two artefacts with different costs and different consumers:

- **Log baseline** — log, board, seal. Free. Read by the replay test.
- **Render baseline** — chronicle, findings, `renders.json`. Costs inference. Read only by Layer 5's golden diff.

Make the render half **declared** rather than assumed: a set states which halves it carries, and a consumer asking for a half that isn't there gets a named failure rather than a skip. Cut both halves at ruleset 5 — the split is about making the dependency visible, not about using it to defer again.

**Do not re-verify the prose.** These are machine baselines. The hand-verified reference set is a separate artefact and is still deferred behind step two.

---

## 2. A layer that did not run must not report as passed

`TestGoldenAgainstBaseline` returning 0 on ruleset mismatch is correct and stays. The defect is the top line summarising over it.

```
4 of 5 layers ran; layer 5 SKIPPED — baseline is ruleset 4, build runs ruleset 5
```

Not `all layers passed`. **The top line reports what ran, not only what failed.**

Same family as `Inert()` firing by construction and as an error message printing a limit that did not apply: a summary that is accurate about failures and silent about coverage reads as a clean bill of health. Apply the same test to any other top line in the harness that aggregates over skippable work — if a summary can say "passed" while a component did not execute, it has this defect.

Land this **before** item 1, so the cut is confirmed by a top line that can't hide a skip.

---

## 3. Skipped rows emit their reason

`if (was.Extracted == 0) continue` at `GoldenDiff.cs:129` skips 129 of 208 rows on seed 42. FLOOR protection is silently absent for 62% of rows and the skip leaves no trace. That is the condition `rule-inert` was invented to make loud, unfixed one layer up.

Emit a row per skip, carrying its reason:

- `no floor: not instrumented` — the rule has no `Extracted` call site anywhere. Permanent under the current scheme; resolves only when the per-shape counter contract gives it `required` or `scanned`.
- `no floor: zero at baseline` — instrumented, but this document never reached the call site. Circumstantial and may differ per seed.

Report the two counts separately per rule. Then **run it across all five seeds**, because the split is the interesting part: `quantity` and `tenure` are 0/13 here but instrumented, so if they carry a floor on another seed, this is a scope-selection artefact rather than a rule gap — and scope selection already changed at ruleset 4 (13 scopes against v1's 15).

Attach the resulting figures to the counter-shapes card (`https://trello.com/c/xtTiX4V2`). `coverage`, `outcome` and `shape` at 0/13 with no call sites anywhere is exactly what that card's shapes 1 and 2 exist to fix.

---

## 4. The §4 rule list is generated, not written

The project doc lists six rules as lacking floor protection: `action`, `date`, `quantity`, `tenure`, `outcome`, `coverage`. Measured: `action` is 5/13 and `date` is 4/13 (both protected in some scopes), and `shape` is 0/13 and absent from the list. **Wrong in both directions.**

That signature matters. A stale list drifts one way; a list wrong in both directions was written from reasoning about which rules *ought* to be uninstrumented rather than from measurement.

Emit the table from the code — `wb holdouts` already has the shape — across all five seeds, and have the doc carry generated output with the command and date that produced it.

**Standing rule to add to §4:** *A list in the documentation describing a measurable property of the code should be emitted by the code.* Same family as the doc comment asserting a property nothing enforces.

---

## 5. Halt conditions

- Item 1: any seed's render pipeline failing, or a chronicle failing the checker at a rate unlike ruleset 4 — that is a signal about ruleset 5, not about the cut
- Item 3: the two skip reasons not partitioning the skips, i.e. a skipped row fitting neither
- Any top line found in item 2's sweep that can report success over unexecuted work — report the full list rather than fixing them all silently
- Suite not returning to green after item 1

## 6. Report

Baseline cut confirmation per seed with the ruleset-5 holdout rate beside ruleset 4's. The new top line, and any other summaries found with the same defect. The skip-reason table across five seeds, split two ways, with `quantity` and `tenure` called out. The generated rule list, with the command that produced it.

---

## A note on where these findings came from

Three times this session I reasoned from a presentation view rather than the record: the roadmap instead of the code on `TRADE_COLLAPSE`; the handoff's "Layer 5 passing 0/0" instead of `GoldenDiff`; and an argument for deferring the render baseline that would have generalised to never cutting one. **Read the record, never the presentation view** is already the project's rule for measuring the world. It applies to reading the project's own documents, and it applies to me.
