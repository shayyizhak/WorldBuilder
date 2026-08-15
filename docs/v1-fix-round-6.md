# v1 Fix Round 6 — the last render items

## Context

Round 5 landed. The seed 42 chronicle now has **zero fabricated proper nouns, zero dangling causal references, and zero secret leaks.** The Stonand Ker succession fabrication — which survived rounds 3, 4 and 5 — is gone. All three Y46 conspiracies are named rather than two. Departure partitions sum.

Most of what remains from this round's review is engine-side and is in a separate brief (`engine-fix-round-1.md`). This brief is the render layer only. It is short.

The two new war-scope renders both verify completely against the log — every date, faction, place and casualty figure. Worth noting since the scope is new.

---

## 1. PRIORITY: a challenge outcome read backwards

Kebarrow Compact 22–41 opens its second paragraph with:

> *"The rule of Weallhous Dreld ended when he was beaten in an open challenge by Saern Meastouth, who was then killed by Dreld."*

The source events:

```
[Y0023]  POLITY.CHALLENGE     Saern Meastouth (a:28) challenges Weallhous Dreld (a:25) openly for the Kebarrow Compact (f:2) and is beaten
[Y0023]  LIFE.DEATH_VIOLENT   Saern Meastouth (a:28), commoner of the Kebarrow Compact (f:2), is killed by Weallhous Dreld (a:25) at Kebarrow (p:3)
[Y0025]  POLITY.CHALLENGE     Gatros Hearn (a:27) challenges Weallhous Dreld (a:25) openly for the Kebarrow Compact (f:2) and takes the seat
[Y0025]  LIFE.DEATH_VIOLENT   Weallhous Dreld (a:25), ruler of the Kebarrow Compact (f:2), is killed by Gatros Hearn (a:27) at Kebarrow (p:3)
```

Meastouth challenged, **was beaten**, and was then killed. Dreld's rule ended two years later at Gatros Hearn's hands.

The next sentence in the same paragraph says *"Gatros Hearn subsequently took the seat by beating Dreld in a challenge"* — which is correct, and directly contradicts the sentence before it. **The paragraph disagrees with itself.**

This is the round-1 class returning: an election rendered as a coup, an outcome inverted. `POLITY.CHALLENGE` events distinguish success from failure only by a trailing clause on an otherwise identical sentence stem, which makes them easy to flip.

Two fixes, and I would do both:

- **Prompt side:** when rendering a challenge, the outcome clause is a particular, not a pattern. It may not be paraphrased into a different outcome, and a challenge that was beaten may never be described as ending the incumbent's rule.
- **Validation side:** assert that any rendered statement about a rule *ending* is anchored to the event that actually ended it. A regression test on this case specifically — Dreld's rule ends at Y25 by Gatros Hearn, never at Y23 by Saern Meastouth.

The engine brief also proposes exposing challenge outcome as a structured field rather than a prose clause, which would remove the ambiguity at source. If that lands, this becomes much easier to hold.

---

## 2. A causal edge described in the wrong direction

Kebarrow Compact 22–41: *"Hadale broke away from the Compact to form the Hadale Commune after a raid on the Compact was beaten off."*

```
[Y0027]  CONFLICT.RAID     the Kebarrow Compact (f:2)'s raid on Griwick (p:4) is beaten off
[Y0027]  POLITY.SECESSION  Hadale (p:2) breaks from the Kebarrow Compact (f:2) as the Hadale Commune  <= (the above)
```

The causing event is **the Compact's own raid failing**. The prose reads as though someone attacked the Compact and was repelled — which is a success, and makes the secession that follows look arbitrary.

The actual chain is much better history: a failed attack costs legitimacy, and a province walks. Getting the direction right is the difference between a non-sequitur and a causal explanation.

Perpetrator/target direction has now been a problem in three places — killings, raids, and this. Treat the direction of any action event as a particular that must be preserved, in the same category as names and dates.

---

## 3. A duration attached to the wrong interval

Sworn Men of Meigate: *"Gatros Hearn returned from exile in 28, but his attempt on Kreathbeas Waeth failed in 41, leading to his casting out for attempted murder. A conspiracy by Gatros Hearn was uncovered two years later, resulting in a second casting out."*

```
[Y0039]  POLITY.COUP_PLOTTED  Gatros Hearn (a:27) begins conspiring against Kreathbeas Waeth (a:66) of the Sworn Men of Meigate (f:4)  [secret]
[Y0041]  POLITY.EXILE         Gatros Hearn (a:27) is cast out of the Sworn Men of Meigate (f:4) — attempted murder
[Y0041]  POLITY.EXILE         Gatros Hearn (a:27) is cast out of the Sworn Men of Meigate (f:4) — conspiracy against the seat
```

**Both exiles are Y41.** The "two years" is the conspiracy's own duration — plotted Y39, resolved Y41 — which has been reattached as the gap between the two castings-out.

This is the round-4 duration-convention problem in a new place. The rule already established holds here: use the event's own duration field, and never compute an interval between two events to stand in for it.

---

## 4. "Including" before an exhaustive list

22–41: *"Four people within the Compact were murdered from within, including Weallhous Dreld, Wilwound Ska, Nael War, and Paernrom Sir."*

The count is four and exactly four are named, so "including" is wrong — it signals to the reader that more exist. (The count is separately wrong; that is in the engine brief.)

Where a list is exhaustive, say so plainly. Where it is a sample of a larger set, the prose must make the size of the larger set clear. This matters more than it looks, because a reader who cannot tell which they are looking at cannot use the chronicle as a reference.

---

## 5. Document assembly

Two problems that are neither fabrication nor arithmetic.

**Sections are not in chronological order.** The document runs: two wars (Y5–9), then the reign of Wuldweald Valdrith (Y51), then Kebarrow 2–21, 22–41, 42–51, then Meigate. The Y51 reign sitting third, before the Y2 faction section, reads as an error rather than a choice. Either order scopes chronologically by start year, or group by kind (wars, then factions, then reigns) with the grouping made explicit by headings.

**Faction names are ambiguous in the war scopes.** The first war section uses "the Compact" throughout to mean the **Griwick** Compact — in a document where "the Compact" everywhere else means Kebarrow. Any scope where two same-suffix factions appear must use full faction names on every reference. This is a low-cost fix that prevents a high-cost misreading.

While there: both war sections are titled "the War for Threi Cut", disambiguated only by "of 7". Titles should carry the belligerents, not just the prize.

---

## 6. Something to notice, not to fix

The Kebarrow Compact declared its Y7 war *for Threi Cut*, fought it entirely at Laehiford and Hadale, took both, and never took Threi Cut — the Griwick Compact did, in the other war running at the same time.

That is a genuine historical irony and it sits unremarked. It is also exactly the kind of observation the particulars/patterns rule permits: it is a characterisation of the supplied events, inventing nothing. Whether the renderer can be got to notice things at this level without also being got to invent them is the open question of the whole render layer — worth a look, but not at the cost of any of the constraints above.

---

## Evaluation

Re-render seed 42 and report:

1. **Zero inverted outcomes.** The Dreld/Meastouth case verified, and no paragraph contradicting itself.
2. **Every causal statement in the correct direction**, checked against the `<=` edges rather than against adjacency.
3. **Every duration drawn from an event's own field.**
4. **Exhaustive lists not hedged with "including".**
5. **Chronological or explicitly grouped section order**, full faction names in any scope with more than one Compact.

Hold the benchmarks: Meigate, Kebarrow 2–21, and the two war scopes are all currently exact and must stay that way.

---

## What I want from you

Item 1 is the round. Items 2 and 3 are the same fabrication family — a particular being reconstructed instead of copied — and may share a root cause with it; if they do, say so, because one fix is better than three.

Once these are clear the render layer is done and v1.2 query starts. The query brief is written and waiting.
