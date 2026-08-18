# Step two — the design, written before the runs that use it

Written against `docs/brief-step-two-relation-termination.md` §2, §3 and §4, **before any
ruleset-6 world was generated**. §4 requires the argument for every constant to exist before the
run that reads it, and the only way that claim is worth anything is if the argument is a separate
artefact with its own commit.

Entry state: ruleset 5, 599 green, 0 failing, 2 skipped.

---

## §2 — the shape of the capability

### What ends a relation

**One helper, `RelationEnds.End`, and no rule ends a relation any other way.** It takes the two
parties, the kind, a cause tag and the event that justifies it; it reads the live edge before
touching it, emits the event that *names* the ending for that kind, and carries the deletion in
that event's payload.

The mapping from relation kind to the event that names its ending is a table in one place:

| kind | named by |
|---|---|
| `Trade` | `ECONOMY.TRADE_COLLAPSE` |
| `Alliance` | `DIPLO.ALLIANCE_BROKEN` |
| `AtWar` | `DIPLO.PEACE_SIGNED` (already; not routed through the helper — see below) |
| everything else | nothing yet — a kind that acquires a termination must name it here first |

A kind with no entry cannot be terminated. That is the point: the way this defect returns is a
rule that deletes an edge inline because no event existed for it, which is exactly how
`RelDel` on the war declaration came to be the only record of fifteen broken alliances.

### What the termination records

Parties, kind, cause tag, the year the tie was made, its value at death, and the event that
justifies it. The created-year and the dying value come off `Relation`, which is why the helper
reads the edge before the reducer removes it — after the fold they are gone and unrecoverable.

### Is termination distinct from never-having-existed?

**Yes, and the distinction lives in the log, not in `RelationGraph`.** Decided deliberately,
against the alternative, and this is the half of §2 most likely to be wrong.

The alternative — `RelationGraph` keeps a tombstone, so `EverHeld(a, b, kind)` answers in O(1) —
is genuinely attractive and is rejected for a specific reason. There are about forty call sites of
`Has`, `ValueOf`, `From`, `To` and `IncomingTotal`. A tombstone makes every one of them ambiguous
between "is there a live edge" and "is there an edge", and the audit that would have to follow is
the same audit the absent-versus-unknown card (`https://trello.com/c/QiADoVAB`) describes, arriving
through a different door: a live-versus-dead conflation replacing an absent-versus-unknown one, in
the subsystem that was supposed to be the clean one.

What makes the log a sufficient answer is a property this step must actually hold:

> **Every relation that ends does so inside an event that names its ending.**

If that holds, "were these two ever allied" is answerable from the record with no inference, and
`WorldState` keeps meaning what it means — the world *now*, folded from the record. If it does not
hold, a tombstone would not have saved anything either; it would have recorded the same silent
deletions under a different name.

Tested, not asserted in prose: `RelationTerminationTests` walks every panel log, folds the relation
graph, and requires that no edge disappears except inside an event whose kind is the one the table
above names for it.

**The cost, stated plainly.** No rule can cheaply ask "did these two once trade" at decision time.
Nothing needs to today. A rule that needs it later will need a log scan, which is what `Recent`
already does for the same class of question — so the precedent exists and the cost is a scan, not
a redesign.

### The alliance deletion stays on the war declaration

`DeclareWar` carries `RelDel` for the alliance, and step one deliberately left it there so the
break could be proved additive. Moving it onto `DIPLO.ALLIANCE_BROKEN` would make the invariant
above literally true of alliances as well as of trade, and it is **not being done**, because it
would change the war declaration's payload — and the war precedes the break it causes, so §5's
first-divergence check would fire *before* the first termination on every seed with a war. That
check is the only mechanical guard this step has, and spending it to relocate a payload key
between two adjacent events in the same year is a bad trade.

So the invariant is enforced in the form that is true and useful: **the ending is named by an
event in the same year, by the event that caused it or the event that reports it.** Which of the
two physically carries the `relDel` key is a detail of the fold. Recorded as owed, not as done.

---

## §3 — what a collapse emits

**One event, and it is `POLITY.COLLAPSE` itself.** Not per-relation events, and not a new
cleanup event beside the collapse.

Against per-relation: a house dying with twelve edges would emit twelve events, three of them
`ECONOMY.TRADE_COLLAPSE` between a dead house and somebody who did not decide anything. Those read
as news and are bookkeeping, they would dominate the §4 cause distribution with the one cause that
carries no decision in it, and a collapse year would go from one readable line to thirteen.

Against a separate cleanup event: `POLITY.COLLAPSE` is already the event that says this house is
finished, already disposes of its ground and its people in its own payload, and a second event
beside it saying "and also its ties" is a bookkeeping row wearing a history event's clothes.

So the collapse carries the deletions, and — per §3's requirement that a bare count is an
unlabelled figure — it carries the count **and** the kinds:

```
tiesEnded=7  tiesEndedKinds=Alliance:2,Trade:3,AtWar:2
```

plus one `relDel:` key per edge, so the record names every individual tie and not only the total.
The renderer says the number and the kinds.

### Which relations a collapse ends

**The obligations between houses, not the memory of what they did.**

| ended | left standing |
|---|---|
| `Alliance` — an undertaking, and there is nobody left to keep it | `Grievance` — memory, and the engine's whole reason for having one |
| `Trade` — an arrangement to move goods, and one end of it is gone | `Kin`, `Marriage` — facts about people, who outlive their house |
| `Vassal` — a subordination to a house that no longer exists | `Fealty` — actor to actor; both actors are still alive |
| `AtWar` — you cannot be at war with nobody | `Rivalry` — no rule reads or writes it (see the §6 sweep) |

The line is: an obligation needs two parties to hold it, and a fact does not stop being a fact.
A grievance against a house that is gone is exactly the sort of thing this world is supposed to
remember.

---

## §4 — trade termination

### The four candidates

**1. War declaration between partners — IN, and it is definitional rather than probabilistic.**

A trade tie in this engine is an arrangement to move grain and silver across a border between two
houses. A declaration of war closes that border. There is no roll and no threshold, because there
is no question being asked: the two states of "at war" and "trading" are not simultaneously
available, and a world that renders both is describing something that is not happening.

This is §4's sharp edge and it is taken with eyes open. Twenty-one of the panel's twenty-four
declarations occur between factions with a live tie, so this cause alone will end most of the
ties that exist at the moment a war opens.

**2. Partner collapse — IN, and it falls out of §3 rather than being decided here.**

**3. Distance — OUT.**

Trade formation does not consult geography. `BuyGrain` and `TradePact` are the only two rules that
write a `Trade` edge and neither asks `Geo` anything; the four distance-consuming mechanics are
raid targeting, war declaration, conquest and pairing. Terminating on distance while forming
regardless of it makes the mechanic asymmetric in the one direction that reads as a bug: a tie
that the rules were happy to form is killed by a fact that was already true when they formed it.
Worse, places do not move, so the first evaluation would cull every too-far tie at once and
nothing after that — a one-time sweep wearing a mechanic's clothes.

If distance should gate trade, it should gate **formation**, and that is a different change with
its own baseline cost. Recorded as a candidate, not taken.

**4. Disuse decay — IN, and it carries the only constant in this step.**

A `Trade` edge is a *value*, not a flag: a pact writes 25, a grain purchase writes 8. Nothing has
ever read that value as anything but a number to add to, and nothing has ever removed from it.
That is the monotonic defect stated in its most literal form.

Commerce is a flow. A flow that stops is a relationship that stops, and the value is the natural
place to say so.

### The constant, and the argument for it

**A trade tie loses one point per year, and ends when it reaches zero.**

Two decisions, and the second is the one that matters.

**Linear, not proportional.** Grievance decays at 96% retention per year, and the consequence is
recorded in `phase-relation-termination.md` §0: *260 grievances cross 40 and none ever clear.*
Proportional decay is asymptotic; it cannot reach zero, so a termination rule built on it would
never fire and would be a mechanic that cannot happen — this project's most familiar shape, and
the one §0 of the parent phase exists to repair. Choosing proportional decay here would repair
the trade ratchet by installing the grievance ratchet in its place.

**One point per year, because the unit is the year.** The constant is 1 so that the number on the
edge means *years of commerce still in hand*, which is a legible unit rather than a tuning dial:

- a negotiated pact (25) buys twenty-five years;
- a single grain purchase (8) buys eight;
- the recency guard already forbids the same pair repeating a pact inside five years, so a live
  relationship refreshes at roughly +8 per five years against −1 per year and survives
  indefinitely, while a relationship nobody has used dies on its own schedule.

The bar this has to clear is §4's own: *a constant that cannot be argued from what the mechanic
represents is a halt condition.* One point per year is argued from the unit, not from an outcome.
No figure below was consulted in choosing it, and the sentence above was written before the first
ruleset-6 world existed.

**What would falsify it.** If the panel comes back with final ties near zero or near peak, the
degeneracy guard fires and the fix is the rule, not the number — per §4 and §10. Specifically:
near zero means the flow model is wrong because refreshment is rarer than assumed, and the answer
is that trade should be refreshed by ordinary commerce rather than only by pacts; near peak means
nothing decays because everything is refreshed, and the answer is that the value is not a flow at
all. Neither answer is "try 2 points per year", and if that is what the number wants to be, this
document says so before the temptation exists.

### The terminating cause as a rendered field

Three causes reach the field: `war`, `disuse`, `partner-collapse` — except that a collapse ends
its ties inside `POLITY.COLLAPSE` per §3, so only two reach `ECONOMY.TRADE_COLLAPSE`. If the panel
shows one of `war` or `disuse` at zero, the standing rule from step one applies — **a field with
one reachable value across the panel does not get rendered until it has two** — and the cause is
recorded in the payload and left out of the sentence.

Decided after measurement, because it is a measurement. Not decided by whether the sentence reads
better with it.

### What cites `TRADE_COLLAPSE`

Nothing is being written to cite it, and §7 forbids inventing a consumer to strengthen the
control. The honest prediction, recorded before the run: **nothing cites it**, so its causal edges
are inbound only, it terminates chains rather than extending them, and the §5 negative control is
the same weak one step one had. That is the answer §11 asks for, and it is being written down
rather than fixed.

The consequence to watch is arithmetic rather than narrative. `TRADE_COLLAPSE` is an `ECONOMY`
event citing a `DIPLO` one, so every emission adds to the cross-domain count and to the total edge
count while adding nothing to the ECONOMY→non-ECONOMY numerator. The share falls mechanically.
Measured before and after against the ≥10% Layer 1 invariant; at ruleset 5 the panel sits at
15.9%, 16.4%, 17.7%, 15.5% and 21.0%.

---

## Ruleset 6

Mechanics change. Worlds change. The additive-only property does not apply and is replaced by §5's
first-divergence check. Baselines are re-cut as part of this step, both halves, per §9.
