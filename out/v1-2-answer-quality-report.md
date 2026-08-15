# v1.2 Answer Quality — report

Answers the loop prompt in [docs/v1-2-answer-quality-loop.md](../docs/v1-2-answer-quality-loop.md).
Seed 42, 50 years, model `qwen3.6:latest`.

---

## Halt — all conditions hold

| condition | result |
|---|---|
| 1. All 16 correct, expected answers unchanged | ✅ 16 of 16, twice |
| 2. Zero secret events; `withheld` names nobody | ✅ |
| 3. Zero fatal checker findings | ✅ |
| 4. `extracted == checked + unresolvable`; inert only where the construction is absent | ✅ |
| 5. No answer contains *record*, *log*, *retrieved*, *data*, *entry* | ✅ |
| 6. No causal answer asserts a link without a `causes` edge | ✅ |
| 7. Retrieval unregressed | ✅ byte-identical to baseline |
| 8. Full suite green | ✅ 330 passed, 2 skipped — was 321 |

Chronicle output is byte-identical to the archived baseline: same 8 suspect tokens, same 3
sections held out of canon.

**Two consecutive suite runs produced byte-identical answers.** That is worth stating because
generation here is not reproducible in general — see *Non-determinism* below.

---

## The 16 answers, in full

**1. Why did Hadale break from the Kebarrow Compact?**

> In 27, Hadale broke from the Kebarrow Compact because the Kebarrow Compact's raid on Griwick
> was beaten off [e:448].

**2. Why did the Wurn League end?**

> The Wurn League ended because the Kebarrow Compact defeated it at Hadale in 20 and took Hadale
> from it, leaving the Wurn League finished, landless, with its last 21 followers scattering
> [e:305; e:306; e:307]. This sequence was triggered by the Kebarrow Compact declaring war on the
> Wurn League in 20 over a long-standing grievance, which arose from Trem Lolkoll's failed
> attempt on Weallhous Dreld in 20 [e:300; e:298].

**3. Why did Threi Cut rise against the Vea Lode Covenant in 51?**

> Threi Cut rose against the Vea Lode Covenant in 51 because the Covenant's standing had fallen
> to nothing following the killing of its ruler, Keithfal Naell, by Tor Nathgoull in 50 [e:1035].

"Collapse" is gone; the standing is now context rather than a link in the chain.

**4. How many times was Paernmel Has the target of a failed attempt?**

> Paernmel Has was the target of four failed attempts: by Stonand Ker in 43, Keithfal Naell in
> 45, Throll Kell in 46, and Drouldthas Stour in 49 [e:822; e:869; e:892; e:976].

**5. Which powers broke away, and from whom?**

> In 19, Meigate broke away from the Kebarrow Compact to become the Sworn Men of Meigate [e:283].
> In 20, Laehiford broke away from the Kebarrow Compact to become the Sworn Men of Laehiford
> [e:314]. In 27, Hadale broke away from the Kebarrow Compact to become the Hadale Commune
> [e:454]. In 29, Vea Lode broke away from the Griwick Compact to become the Vea Lode Covenant
> [e:506].

**6. Which powers were destroyed?**

> The Wurn League was finished in 20 [e:307]. The Griwick Compact was finished in 35 [e:655].
> The Sworn Men of Meigate was finished in 50 [e:1004].

**7. Who ruled the Vea Lode Covenant?**

> The Vea Lode Covenant was ruled by Stald Gearngoll from 29 to 45 [e:506], Veillpea Dourn from
> 45 to 46 [e:878], Thres Thrild from 46 to 47 [e:907], Gatros Hearn from 47 to 48 [e:929],
> Keithfal Naell from 48 to 50 [e:960], and Herpeim Raern from 50 onwards [e:1006].

Spans, not accession dates. Gearngoll's sixteen years and Thrild's one no longer read alike.

**8. How many died in the plague at Griwick?**

> The plague at Griwick killed 474 people in total [e:419; e:437; e:465]. The deaths occurred in
> year 26 (185 dead), year 27 (133 dead), and year 28 (156 dead) [e:419; e:437; e:465].

**9. What happened to the Drelthorn League?** → branch `no-such-entity`

> There is no Drelthorn League in this world.

**10. Who ruled the Sworn Men of Meigate in year 5?** → branch `outside-lifetime`

> The Sworn Men of Meigate did not exist until 19.

**11. Who ruled the Hadale Commune in year 51?**

> Durnrin Drar ruled the Hadale Commune in year 51 [e:927].

**12. Why did Stonand Ker lose the seat of the Kebarrow Compact?**

> Stonand Ker never held a seat, so the question does not arise.

**13. Why did Hehum Skul's reign end?**

> Hehum Skul never held a seat, so the question does not arise.

**14. When did the Kebarrow Compact conquer Griwick?**

> the Kebarrow Compact never took Griwick.

**15. Who attempted to kill Sothkel Sald in year 35?** → branch `withheld`

> Whoever made that attempt was never found out.

True, names nobody, and does not deny an attempt the world does hold.

**16. Who conspired against Paernmel Has?**

> Three conspiracies against Paernmel Has were uncovered in 46, by Stonand Ker, Keithfal Naell
> and Throll Kell [e:901] [e:903] [e:905].

The pattern and its members in one sentence — the shape Step 4 asked for.

---

## Which branch each empty answer took

| # | branch | answer |
|---|---|---|
| 9 | `no-such-entity` | There is no Drelthorn League in this world. |
| 10 | `outside-lifetime` | The Sworn Men of Meigate did not exist until 19. |
| 15 | `withheld` | Whoever made that attempt was never found out. |

`no-occurrence` is the fourth branch and no suite question reaches it; it is covered by a test
rather than by the suite.

---

## Checker findings

Zero, fatal or otherwise, on all sixteen.

## Coverage table (all 16 answers)

```
action             0 [inert]      naming          131/131 (100%)  fired 0
coined-term       85/85  (100%)   outcome           0 [inert]
count-enumeration  0 [inert]      partition-sum     0 [inert]
count-narration    0 [inert]      quantity          0 [inert]
date               0 [inert]      succession        0 [inert]
date-agreement     1 → 1 unresolvable (0%)          summary-body      0 [inert]
departure          0 [inert]      tenure            6/6   (100%)
```

`extracted == checked + unresolvable` holds for every rule.

`tenure` reading 6 is new and is Step 3 working: the reign spans in answer 7 are now assertions
a rule can check, where six accession years were not.

The one `date-agreement` unresolvable carries its span, which is the diagnosability the last
round added:

```
1  no name after the phrase
   on: Threi Cut rose against the Vea Lode Covenant in 51 because the Covenant's
       standing had fallen to nothing foll…
```

That is correct: the phrase is followed by a possessive, not a name, so the rule genuinely cannot
resolve who the date belongs to. Recorded rather than silently dropped.

---

## Expected answers I believe are wrong

None. No expected answer was changed.

One tension worth flagging rather than a disagreement: **question 7's expected answer lists
accession years** — "Stald Gearngoll (29), Veillpea Dourn (45), …" — while Step 3 requires spans.
The answer now gives spans, which contain those accession years and add the closing year. I read
that as satisfying the expected answer rather than departing from it, but the two were written
against different rules and the expected answer is the older of them.

---

## Defects fixed more than once

**Answer completeness on question 16 — fixed twice, and the second fix was structural.**

The first fix was a prompt rule ("answer all of the question; naming one of three is a wrong
answer, not a short one"). It worked when tested and then regressed on the next suite run, from
an identical prompt. Prompt instructions were not going to make it reliable.

The second fix supplies the count in the pack: `QueryFacts` now states a plain count of two or
more records of one kind, which it previously suppressed as the set restating its own length. A
supplied figure is one the answer is required to state, so an answer naming one man now
contradicts its own material rather than merely under-serving the question. That is what produced
the sentence above, on two consecutive runs.

**A defect I introduced last round and found this round: the role fields were inverted for two
event kinds.** `LifeDeathViolent` records the victim as its subject and the killer as its object;
`PolitySuccessionDisputed` records the named heir as subject and the challenger as object. Both
came out backwards in the structured fields — a field written to settle "which of these two did
it" was confidently naming the wrong one. Fixed with an explicit per-kind table rather than by
reading the schema roles straight.

---

## Non-determinism, and why it matters for reading this report

Generation is not reproducible run to run, despite temperature 0 and a fixed sampling seed. Two
observations, both this round:

- Question 16 was answered correctly standalone and incompletely in the suite, from an identical
  prompt and pack.
- Asked standalone, the **planner** classified question 16 as causal; in the suite it classifies
  it as factual. That changes retrieval from three records to one.

The request bodies are byte-identical in both cases, so this is Ollama's own run-to-run variance
rather than anything in the engine. The practical consequences:

- `wb ask` on a single question is not a valid proxy for the suite. I used it during development
  and it misled me once; the suite is the only authority.
- "16 of 16" is a sample, not a proof. It is why this report cites **two consecutive suite runs
  with byte-identical answers** rather than one.
- Retrieval was verified against the baseline through the suite, where it is stable and identical.

---

## Known gaps — recorded, not fixed

Both were carried forward from the last round and both remain correct to leave:

- **Question 8's 504 who fled.** Step 4 did not fix it incidentally: the answer characterises the
  plague as a total plus a year-by-year split and still omits the departures. The pack supplies
  them. Building a fragment completeness rule to catch it is the round-10 trap.
- **Question 4's "four" is verified by no rule.** The vocabulary scan skips numbers under three
  digits and `count-vs-list` needs an enumerated list. The answer now names all four attackers
  and their years, so a wrong count would be visible to a reader — but still not to a rule.

Both belong in the Stage 4 backlog.

---

## What changed

**Step 1 — empty results branch by reason.** `EmptyReason` with four branches, chosen in
`WhyEmpty` and phrased in `Nothing`. The `withheld` branch is the only place in the query layer
that reads past `IsRetrievable`, and it reads a count and nothing else — no id, no description
and no participant escapes it. No branch mentions a record, a log or a search.

**Step 2 — causal links.** New `CausalLinks.Check`: where a sentence joins two cited records with
a causal connective, the record must carry a path between them. It reads the citations, which is
why it lives on the query path rather than in `FabricationCheck` — that check is deliberately
handed prose with citations stripped. Limit recorded in the code: it judges within one sentence
only, because resolving "this collapse" across a sentence boundary is guesswork, and a rule that
guesses accuses the innocent.

`CausalLinks.Terminology` reserves "collapse" and "destroyed" for a power that was finished.
**Answers only, and measured:** the same test over the chronicle reports "a decade of internal
collapse" and "a state of violent contraction" — figures of speech in prose that is already canon
— and costs a true section for each. A section has surrounding paragraphs that disambiguate a
metaphor; a two-sentence answer does not.

**Step 3 — reign spans.** Query packs list every tenure as a span, where the chronicle keeps its
elision. Both changes are gated to the query path.

**Steps 4 and 5 — particulars and patterns.** The render prompt's two-rules structure carried
into the query prompt, plus three things the answers required: that rule two never overrules rule
one (naming a pattern and dropping its members is worse than the list it replaced), that a count
names what was counted, and that the causes section should be used where it bears on the question.

**Step 6 — both switched-off-rule fixes pinned**, as `RulesGatedOffForAnAnswerDoNotReportAsInert`
and `AFindingIsCountedAgainstTheRuleThatProducedIt`.

**Not asked for, found on the way:** entity tags were reaching the prose — "the Wurn League
(f:1) was finished in 20". The model copies them perhaps a third of the time and ignores being
told not to; a tag is not a fabrication, so nothing fired and nothing retried. `Tidy` now strips
them. Square-bracket citations are left alone.

### Files touched

```
src/WorldBuilder.Inference/CausalLinks.cs     new — causal-edge rule and terminology rule
src/WorldBuilder.Inference/PackDigest.cs      per-tenure spans for query packs
src/WorldBuilder.Inference/Query.cs           empty branches; prompt; tag stripping; Verify
src/WorldBuilder.Inference/QueryFacts.cs      per-kind actor/target table; plain counts
src/WorldBuilder.Inference/RuleNames.cs       unsupported-link
src/WorldBuilder.Inference/FabricationCheck.cs   reverted an over-broad collapse check
tests/WorldBuilder.Tests/GenerationTests.cs   9 new tests, all entering at AskAsync
tests/WorldBuilder.Tests/QueryTests.cs        updated for the no-such-entity branch
```
