# Second-reader pass, part 3 — the questions, and what the holdout rate is measuring

Against the ruleset-8 staging at seal `d5925c04…`.

---

## 1. Two causal questions walk back to the creation of a place

| question | staged answer | what `e:9` is |
|---|---|---|
| Why did the Griwick Compact declare war on the Wurn League in year 5? | `e:9` | `GENESIS.PLACE` — Threi Cut comes into existence |
| Why did the Wurn League take Threi Cut in year 2? | `e:9` | the same row |

*Because the place exists* is not a cause. Two of the four causal questions terminate on a genesis row, which is a stopping condition rather than an explanation.

**And `e:9` is in `record-bookkeeping.md`** — the file the sheet is told not to draw on. So the split rule and the causal trace disagree about what belongs to the history: one excludes genesis rows as accounting, the other walks into them and stops there.

Both need fixing, and they are the same fix. **A causal chain should not terminate on a genesis row**; if walking back reaches one, the chain has run out of causes and the honest answer is that the record gives none — which is a different statement from naming `e:9`. Either drop these two candidates or have them stop at the last non-genesis link and say so.

The other two are sound: Meigate's secession walks to `e:506`, Sou Dra's exile from the Kebarrow Compact, and the Wurn League's end walks to `e:637`, the Griwick conquest of Hadale. Both are real causes.

## 2. The causal answers are pointers, not answers

All four read `the recorded causes, walked back: e:506`.

That is not checkable. *"Why did Meigate break away in year 29?"* has the answer *"because Sou Dra was exiled from the Kebarrow Compact"* — a claim a reader can hold against the record and see fail. A record id is a lookup instruction: any response mentioning `e:506` satisfies it, and a response that names the wrong person while citing the right record passes.

Every other category in the file states its answer in words. These four should too, with the id beside it rather than instead of it.

**This is the field that makes a question able to fail.** The staging brief asked for *what a wrong answer would look like* on every candidate; for these four there is no way to write it.

---

## 3. The holdout rate is a draw, and a halt condition was cleared on it

The same world, staged twice, three weeks of rulesets apart but with **all 27 state components identical and one event differing by two payload keys**:

| | held out | which scopes |
|---|---|---|
| ruleset 7 staging | **4 of 13** | Griwick 4–23 · Griwick 24–43 · Vea Lode 29–39 · Griwick 44–51 |
| ruleset 8 staging | **7 of 13** | Kebarrow 2–21 · Wurn 2–21 · Wurn 22–35 · Griwick 24–43 · Griwick 44–51 · Drarka's reign · Paernmel's reign |

Two scopes are held out in both — and **the rules that fired on them changed**: Griwick 24–43 went from `count-narration, date` to `date`; Griwick 44–51 from `count-enumeration` to `partition-sum`. Griwick 4–23 and Vea Lode 29–39 became clean. Five new scopes fell out.

The cause is already established: a cold cut has no cache to inherit, so a non-deterministic model rewrote every section. That is not in dispute. **What follows from it is.**

**A ruleset bump changes worlds, so every cross-ruleset cut is cold.** Which means the panel comparisons — 36% at ruleset 6, 34.5% at 7, 44.8% at 8 — are not a series. They are three independent draws from a distribution nobody has characterised.

That matters because the rate has been used as a halt condition. *"The panel rate is 36% against ruleset 5's 33% — the halt condition on an unlike rate is clear"* cleared a gate by comparing two samples. It happened to clear, and on this evidence it would have cleared or failed roughly at random.

**Two things worth separating.** The counter-shapes findings that came out of `wb holdouts` were about code structure — call sites, floors, which rules have instrumentation — and those stand; they were never rate comparisons. What does not stand is any inference from the rate itself.

**What to do about it.** Either characterise the draw-to-draw variance by re-rendering one world N times and measuring the spread — real inference cost, and it would tell you what an unlike rate even means — or **retire the rate as a halt condition** and say why. The second is cheap and honest. Comparing it across warm cuts is fine, since the ruleset-7 → 8 recut reproduced byte for byte; comparing it across cold cuts is not.

There is a third consequence worth stating plainly because it touches a settled principle. *Cached renders are canon* — and canon is therefore a draw. Whichever sample happened to be cached is the world's history, and a second cut of the same world would have held out a different half of it.

## 4. A practical consequence for the session

**18 of 30 candidates now draw on a held-out scope, up from 5.** Not a defect — the flag is working — but it means most of the question set has no chronicle passage behind it, and that is a property of this draw rather than of the questions.

---

## 5. Checked and agreeing

- **The three repaired terminal spans reach the questions correctly**: Wurn ends `Stonand Ker 33–34`, Kebarrow `Diweith Mound 38–39`, Vea Lode `Bu Rumpirn 36–39`.
- **The four-column role/outcome split reproduces exactly** on all three staged questions: Reweld Wul 1 failed on him / 1 killed him / 0 ordered / 1 failed order (`e:117`, `e:155`, `e:221`); Math Ham 1 / 1 / 1 / 0 (`e:138`, `e:161`, `e:179`); Sou Dra 0 / 1 / 2 / 0 (`e:483`, `e:595`, `e:681`). The distinction the earlier pass asked for is live and the numbers are right.
- **The famine figure** — `e:712`, Meigate, Y38, `deaths=61`, and the question names the year, so the multi-episode ambiguity is closed.
- **Involvement counts** — Wurn League raids 16, battles 7, exiles 10, unchanged and correct.
- **Every seat-year question names an interior year**: 31, 36, 48, 47, 37, none on a hold's edge.
