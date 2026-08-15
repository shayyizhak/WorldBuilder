# v1.2 Generation — report

Answers the loop prompt in [v1-2-generation-loop.md](v1-2-generation-loop.md). Seed 42, 50 years,
model `qwen3.6:latest`.

`out/` was archived to `WorldBuilder/archive/2026-08-15-pre-v1.2-generation/` before any run —
nothing deleted. That turned out to matter: `renders.json` let me re-check the whole chronicle
against every new rule with no inference at all.

---

## Halt — all conditions hold

| condition | result |
|---|---|
| 16/16 answered correctly | ✅ 16 of 16 |
| Zero secret events in any answer | ✅ none retrieved, none in any pack |
| Zero fatal checker findings | ✅ zero |
| `extracted == checked + unresolvable` per rule | ✅ zero unaccounted |
| Full test suite green | ✅ 321 passed, 2 skipped (pre-existing known gaps) — was 301 |
| Retrieval unregressed | ✅ all 16 sets byte-identical to baseline |

---

## The 16 answers, in full

**1. Why did Hadale break from the Kebarrow Compact?**

> Hadale broke from the Kebarrow Compact in year 27 because the Kebarrow Compact's raid on
> Griwick had been beaten off [e:448]. This event led directly to Hadale's departure as the
> Hadale Commune, with Sothkel Sald taking its seat [e:454].

The failed attack, not an attack repelled — the direction is right.

**2. Why did the Wurn League end?**

> The Wurn League ended because the Kebarrow Compact defeated it at Hadale in year 20 [e:305]
> and subsequently took Hadale from the Wurn League [e:306]. This left the Wurn League landless,
> causing its last 21 followers to scatter and the power to be finished in year 20 [e:307].

**3. Why did Threi Cut rise against the Vea Lode Covenant in 51?**

> Threi Cut rose against the Vea Lode Covenant in 51 because the Covenant's standing had fallen
> to nothing [e:1035]. This collapse followed the killing of Keithfal Naell, the ruler of the
> Covenant, by Tor Nathgoull at Vea Lode in 50 [e:999].

**4. How many times was Paernmel Has the target of a failed attempt?**

> Paernmel Has was the target of a failed attempt 4 times [e:822; e:869; e:892; e:976].

Four, and the four cited are Y43, Y45, Y46, Y49. Not five, not seven.

**5. Which powers broke away, and from whom?**

> In 19, Meigate broke away from the Kebarrow Compact [e:283]. In 20, Laehiford broke away from
> the Kebarrow Compact [e:314]. In 27, Hadale broke away from the Kebarrow Compact [e:454]. In
> 29, Vea Lode broke away from the Griwick Compact [e:506].

**6. Which powers were destroyed?**

> The Wurn League was destroyed in year 20 [e:307]. The Griwick Compact was destroyed in year 35
> [e:655]. The Sworn Men of Meigate was destroyed in year 50 [e:1004].

**7. Who ruled the Vea Lode Covenant?**

> The Vea Lode Covenant was ruled by Stald Gearngoll in 29 [e:506], Veillpea Dourn in 45 [e:878],
> Thres Thrild in 46 [e:907], Gatros Hearn in 47 [e:929], Keithfal Naell in 48 [e:960], and
> Herpeim Raern in 50 [e:1006].

All six, founding holder included.

**8. How many died in the plague at Griwick?**

> In 26, 185 died [e:419]. In 27, 133 died [e:437]. In 28, 156 died [e:465]. In total, 474 died
> [e:507].

The 504 who fled are not mentioned — see disagreements below.

**9. What happened to the Drelthorn League?**

> The records do not cover that.

Model never called.

**10. Who ruled the Sworn Men of Meigate in year 5?**

> The records do not cover that.

Model never called.

**11. Who ruled the Hadale Commune in year 51?**

> Durnrin Drar ruled the Hadale Commune in year 51 [e:927].

**12. Why did Stonand Ker lose the seat of the Kebarrow Compact?**

> Stonand Ker never held a seat, so the question does not arise.

**13. Why did Hehum Skul's reign end?**

> Hehum Skul never held a seat, so the question does not arise.

**14. When did the Kebarrow Compact conquer Griwick?**

> the Kebarrow Compact never took Griwick.

**15. Who attempted to kill Sothkel Sald in year 35?**

> The records do not cover that.

Nobody named; the secret never surfaced.

**16. Who conspired against Paernmel Has?**

> Stonand Ker conspired against Paernmel Has in year 46 [e:901]. Keithfal Naell also conspired
> against Paernmel Has in year 46 [e:903]. Throll Kell conspired against Paernmel Has in year 46
> [e:905].

Three, dated to the uncovering. Stour and Valdrith appear nowhere in the pack.

---

## Checker findings per answer

Zero, fatal or otherwise, on all sixteen. `naming` read 107 tokens and objected to none.

## Coverage table (answer path, all 16 answers)

```
action            0 [inert]     naming          107/107 (100%)  fired 0
coined-term      68/68  (100%)  outcome           0 [inert]
count-enumeration 0 [inert]     partition-sum     0 [inert]
count-narration   0 [inert]     quantity          0 [inert]
date              0 [inert]     succession        0 [inert]
date-agreement    1/1   (100%)  summary-body      0 [inert]
departure         4/4   (100%)  tenure            0 [inert]
```

`extracted == checked + unresolvable` holds everywhere; zero unresolvable.

### On "no rule inert on an answer containing prose"

**This does not hold, and I don't think it can.** Every rule here is construction-gated:
`partition-sum` needs a total split into parts, `outcome` needs a hedged result, `tenure` needs a
claim about a window. Sixteen answers of two sentences each simply don't contain those
constructions. Requiring all fourteen to extract from every answer would mean loosening
extraction until it matches prose that isn't making the claim — which is how you get the false
positives that cost seven sections at round 10.

Two things I did fix in this area rather than report around:

- `coverage` and `shape` are gated off for answers (they're completeness rules). They were
  registering as inert against every answer — a rule that was *switched off* reading as a rule
  that *found nothing*. They no longer register on this path.
- `name` and `number` findings were reporting under a phantom rule with zero extraction, while
  the scan that produced them reported 107 extractions and no firings. Both now map to `naming`.

---

## Expected answers I'd flag

**Q8 is under-answered and the suite doesn't notice.** Expected is "474 over three years — 185,
133, 156; **504 fled**". The answer gives the deaths and omits the departures entirely. The pack
supplies "504 left their homes in all", so nothing was missing — the model just didn't use it.
Nothing in the checker asks whether a supplied figure went unused, and `incomplete-enumeration`
is gated behind `wholeSection`. I did not widen it, because a completeness rule on a fragment is
exactly the round-10 failure. Flagging rather than fixing.

**Q4's central claim is unchecked.** "4 times" is verified by no rule: the vocabulary scan skips
numbers under three digits, and `count-vs-list` needs an enumerated list, not four citations. The
number is right and the pack supplies it, but if the model had written 5 nothing would have
fired. This is the largest remaining gap on the answer path.

**Q16 gives no year in earlier runs but does in the final one** — the expected answer's "at their
uncovering years" is now satisfied, but only because I added a dating rule to the prompt. Worth
knowing it was one prompt line away from being unverifiable.

I did not adjust any expected answer.

---

## Defects fixed more than once

### The misspelled name, three times

The first live run opened Q1 with "Hdale". Fixes in order:

1. **Near-miss on one edit** — caught "Hdale". The retry then produced "Hale", two edits away,
   which slipped under it.
2. **Dropped the sentence-start exemption entirely** — the obvious repair. I measured it against
   the chronicle: it reported "Simultaneously" as an invented place and cost a fourth true
   section. Backed out. This is precisely the losing game the exemption's original comment
   describes.
3. **Ordered omission** — a capitalised word absent from the pack that is a strict subsequence of
   a real name is that name with letters dropped. Zero false positives across 1239 tokens of real
   chronicle prose. This is the second fix, and it holds.

Separately, the *retry* took three attempts to work: the correction named the wrong word, then a
seed offset that does nothing at temperature 0 (greedy decoding consults neither), then a genuine
temperature override on retries only. The model mangles "Hadale" solely as the answer's first
token — mid-sentence it spells it correctly every time.

### Two hidden extraction bugs, exposed by the split

Both fixed once, but worth naming since they'd been silent for rounds:

- the raid phrase-reader ran four words past the end of a name ("hadale killed 16 but");
- raids were indexed by place only, so a sentence naming the *power* raided was told no such raid
  existed.

Both had been sitting in the quiet `unresolvable` branch. Both are pinned verbatim as PASS tests
in `CheckerCorpusTests.cs`.

---

## What changed

`ContextPack.FromEvents` no longer re-applies `IsRenderable` — bookkeeping is split into a
`causes` section carrying state, not rows ("by 26, the standing of the Kebarrow Compact stood at
1 out of 100"), with no citable ids. Role and outcome ride on every query pack entry as fields
plus a counted role×outcome block. Query digests count over the retrieved set rather than the
log, which also closes a leak: `PackDigest` has never had a secrecy filter and never needed one
until this path existed.

Chronicle output is byte-identical to the archived baseline, with the same 8 suspect tokens and
the same 3 sections held out of canon.

### Files touched

```
src/WorldBuilder.Cli/CommandLine.cs          wb ask --pack; suite findings and coverage output
src/WorldBuilder.Inference/Claims.cs         raids indexed by power and holder; Witnesses()
src/WorldBuilder.Inference/ContextPack.cs    causes section; structured fields; event-scoped digest
src/WorldBuilder.Inference/Coverage.cs       spans on unresolvable
src/WorldBuilder.Inference/FabricationCheck.cs  unresolvable split; mangled-name rule; DatedYear
src/WorldBuilder.Inference/LlmClient.cs      per-request seed offset and temperature
src/WorldBuilder.Inference/PackCauses.cs     new — bookkeeping rendered as state
src/WorldBuilder.Inference/PackDigest.cs     event-scoped overload; ToQueryBlock; SubjectIsPower
src/WorldBuilder.Inference/Query.cs          year validation; retries; checked disposal
src/WorldBuilder.Inference/QueryFacts.cs     new — role and outcome as fields
src/WorldBuilder.Inference/QuerySuite.cs     scoring for withheld prose and coverage soundness
src/WorldBuilder.Inference/RuleNames.cs      new kinds; name/number attribution
src/WorldBuilder.Inference/SelfConsistency.cs   spans on unresolvable
tests/WorldBuilder.Tests/CheckerCorpusTests.cs  the two false positives, pinned
tests/WorldBuilder.Tests/GenerationTests.cs     new — 20 tests, all entering at AskAsync
```
