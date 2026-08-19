# Brief — repair the staged reference material

**One pass, no decisions required.** Every item below is a derivation fix with a determined answer. Ruleset stays at 7, no mechanics change, no checker rule, no `SimConfig` edit, no baseline cut.

**Source:** an independent re-derivation of `facts-sheet.md`, `questions.md` and `secrets.md` from `record-history.md` at seal `abd551f9…`. Details in `facts-sheet-second-reader.md`.

**What this does not do.** Two derivations agreeing is weaker than one person reading. Nothing here may be marked `verified`; the human session still happens, on better material.

---

## 1. The facts sheet

### 1.1 A tenure ends when the faction does

The terminal hold of each seat is closed at the end of the record with no branch for a seat that stopped existing.

| seat | collapse | holder | claimed | correct |
|---|---|---|---|---|
| the Wurn League (f:1) | Y34 `e:638` | Stonand Ker | 33–51 | **33–34** |
| the Kebarrow Compact (f:2) | Y39 `e:735` | Diweith Mound | 38–51 | **38–39** |
| the Vea Lode Covenant (f:5) | Y39 `e:737` | Bu Rumpirn | 36–51 | **36–39** |

Griwick and Meigate survive and their `still holding` terminals are correct — so the fix is a branch, not a change to the general rule.

Read `e:737` while doing this: the Vea Lode collapse cites `because=e:725`, Bu Rumpirn's own death. The record says his death ended the faction; the sheet had him ruling twelve years past it.

### 1.2 Departure resolves against person **and** faction, inside the tenure window

The current rule searches the person's whole life. That is how `e:870` — Stonand Ker killed in Y47 as **Griwick's** leader — closed a **Wurn** tenure that ended in Y34, and closed the Griwick tenure as well. One death event, two holds, right once.

New rule: **a departure is resolved only from records naming this person and this faction within the tenure's own years.**

Two of the three then repair themselves on their own merits, which is the check that the rule is right rather than merely different:

- Bu Rumpirn — `e:725`, Y39, f:5, in window → keeps `died`
- Diweith Mound — `e:732`, Y39, f:2, in window → keeps `cast out`
- Stonand Ker — nothing names him and f:1 in 33–34 → falls through

### 1.3 A term for the fall-through: `(faction ended)`

Cast out / killed / died / still holding has no term for *the seat stopped existing*, which is what happened to all three. Use `(faction ended)`. It states the fact and implies nothing about the person.

Only Stonand Ker reaches it once 1.2 is in.

### 1.4 Alliance spans read payload keys, not just `ALLIANCE_FORMED`

Section 3's terminated-relations table gives every alliance start as `?`. There are 4 `DIPLO.ALLIANCE_FORMED` records in the world and **34 payload keys setting `rel:f:_:f:_:Alliance`**, almost all on `LIFE.MARRIAGE` — consistent with step two's finding that 42 of 47 alliance edges are dynastic.

The Wurn↔Griwick alliance is set at `e:48`, Y3, so `? – 5` is `3 – 5`.

This is `RelationTrajectory`'s bug inverted: that one read payload keys and missed war and peace, which are applied in code; this one reads event kinds and misses alliances, which are applied in payload. **Fold the record through `EventReducer`** here too, as that fix did, rather than adding a second reader of payload keys.

A `?` that means *the derivation did not look there* is the absent-versus-unknown conflation. After the fix, any remaining `?` must mean the record genuinely does not say — and should be labelled to say so.

---

## 2. The questions

### 2.1 Three answers inherit the bad spans

`Who has ruled the Wurn League?`, `…the Kebarrow Compact?` and `…the Vea Lode Covenant?` all carry the Y51 terminals. They regenerate correctly once §1.1 lands — confirm rather than hand-edit.

### 2.2 Every "who ruled in year N" question picks a transition year

All five, without exception:

| question | year | holders that year |
|---|---|---|
| Wurn League | 33 | Drarka Draernthun 32–33 **and** Stonand Ker 33– |
| Kebarrow Compact | 38 | Beas Krouthea 35–38 **and** Diweith Mound 38– |
| Griwick Compact | 50 | Raes Go 49–50 **and** Paernmel Has 50– |
| Meigate Covenant | 46 | Diweith Mound 44–46 **and** Drarka Draernthun 46– |
| Vea Lode Covenant | 36 | Sou Dra 33–36 **and** Bu Rumpirn 36– |

The staged answer is the incoming ruler every time. The outgoing ruler is equally supported by the record, so **none of these five questions can fail correctly** — same class as the Meigate famine question already caught, and five of thirty candidates.

Fix: **pick a year strictly inside a hold.** Where a hold is a single year and no interior year exists, choose a different hold rather than emitting the question.

Add the boundary years back as a *separate*, deliberately ambiguous probe if they are wanted — but not inside the sixteen, and labelled as testing whether the layer names both.

### 2.3 Regenerate and re-check coverage

After 2.1 and 2.2 the counts move. Re-run the coverage table and confirm 24+ candidates, 3+ negative premise, 1+ supplied figure, 1+ terminated relation still hold.

---

## 3. The secrets — a finding to record, not chase

**Every secret in seed 42 is a `POLITY.COUP_PLOTTED`.** All 30 of them. Four of the five staged candidates are that kind and the fifth is a lapse of one, and all five return the same string: *"Whatever passed there was never made public."*

So the bench is one kind of secret, one template, five times — and **that is a property of the world, not a staging defect.** The breadth worth having (a secret about an event rather than a person; a case where the subject is queryable and the target is not) is not available at seed 42 because the engine only makes one kind of secret.

Record it in `secrets.md` as a stated limitation. Adopt candidate 1 as canonical. **Do not go looking for breadth that does not exist** — and note that a single-kind secret vocabulary is the skewed-distribution shape, which belongs on the backlog rather than in this brief.

---

## 4. Re-stage

Regenerate all three artefacts against the same seal. Everything stays `verified: no`.

Diff the new sheet against the old and **report every row that moved.** A row moving that is not explained by §1 or §2 is a finding.

---

## 5. Tests worth pinning

Each of these is a property the panel can check, and each corresponds to a defect found here:

- **No tenure extends past its faction's collapse year.**
- **No departure record names a faction other than the one whose seat it closes**, or falls outside the tenure's years.
- **No "who ruled in year N" question names a year in which two people held the seat.**
- **A relation span states `?` only where no record sets that relation** — not merely where no event kind does.

The first three run across the whole panel, not just seed 42. Seed 42 is where these were found; nothing suggests it is where they only occur.

---

## 6. Checked and agreeing — do not redo

Independently re-derived and matching, so a human session can spend its attention elsewhere:

- **All five ruler lists**, hold for hold, name for name, start year for start year, with matching seat-moving record counts (15 / 13 / 22 / 4 / 3). Only the three terminal end-years differ.
- **All 15 contested transfers** are same person, same seat, same year. The collapse rule is right on all 15, and Thold Valmaer's genuine double tenure at Griwick (23–24, 24–28) is correctly *not* collapsed.
- **Event counts** — killings 28, natural deaths 21, raids 42, battles 13, conquests 6, exiles 31.
- **Plague** — `477 = 184 + 134 + 159` at Griwick Y26–28; those are the only Griwick plague records, and Laehiford 85 and Hadale 78 are the only others in the world.
- **Famine** — Vea Lode `44 = 11 + 13 + 9 + 7 + 4` record by record; the thirteen famine rows sum to 225, the total across every `ECONOMY.FAMINE` record.
- **Secessions** 2 (both Y29), **collapses** 3 (Y34, Y39, Y39), **partitions** none.
- **All four false premises** — Kou Peis, Deargund Keirem, Thosruld Lul and Turaer Danpa appear in no seat-taking record on any seat, secessions included.
- **The Threi Cut premise** — `e:43` Y2 the Wurn League takes it, `e:122` Y7 the Griwick Compact takes it from them. Kebarrow never holds it. Both halves of the claim are right.
- **The role-and-outcome split** — Stonand Ker: 7 assassination records name him, 5 as the one who ordered, 1 failed attempt on him (`e:624`), 1 that killed him (`e:869`). The staged answer is right.
- **Involvement counts** — Wurn League raids 16, battles 7, exiles 10.

**None of this is verification.** It is a second derivation agreeing with the first, which rules out arithmetic and transcription but not a shared misreading of what a record means.

---

## 7. Halt conditions

- A row moving in §4's diff that §1 or §2 does not explain
- A hold with no interior year for §2.2, on any seat, leaving a coverage requirement unmet
- The `?`-elimination in §1.4 leaving spans still unresolved after folding through `EventReducer` — report which and why
- Any of §5's tests failing on a panel seed other than 42 in a way §1 does not describe
