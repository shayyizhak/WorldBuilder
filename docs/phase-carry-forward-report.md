# Phase report — carry-forward

Run as a loop-prompt against `docs/phase-carry-forward.md`. **No abort triggered.** All seven halt
conditions hold. Two steps produced findings the brief did not anticipate, and one of the brief's own
premises turned out to be half wrong in a way that strengthens rather than weakens its conclusion.

**Entry state:** ruleset 4, 515 tests green, tree clean.
**Exit state:** ruleset 4, **541 tests green**, no threshold moved, no mechanic changed, no checker
rule added.

---

## Halt conditions

| # | condition | state |
|---|---|---|
| 1 | Layer 3 asserted world-independent against ruleset 4, or the exceptions reported | **held, with exceptions reported** — see step 1 |
| 2 | Layer 4 regenerated and passing on ruleset-4 seed 42 | **held** — 22 tests, both baselines |
| 3 | Reference-set materials staged, marked unverified, nothing entered as ground truth | **held** |
| 4 | Dispersion figures self-identifying at emission, with a test asserting it | **held** — 6 tests |
| 5 | Concurrency wedge recorded against Stage 15; concurrency bounded; failure loud | **held** — 5 tests |
| 6 | `flat − geography` registered, not run | **held** |
| 7 | Budget intact | **held** — no `SimConfig` threshold moved, ruleset stays 4, no new mechanics or checker rules |

### The budget, checked rather than asserted

`SimConfig.cs` is untouched, and so are all five files the checker fingerprint is computed over
(`Claims.cs`, `Coverage.cs`, `FabricationCheck.cs`, `RuleNames.cs`, `SelfConsistency.cs`) — so the
fingerprint `60f5b325…` in every baseline manifest still describes the checker in the tree. Sixteen
rules, unchanged.

**Two engine rule files were touched, and the only thing that can prove that safe is a hash.**
`ActionPhase.cs` and `LifePhase.cs` changed at three `GeographyProbe.Ranked` call sites, which pass a
range's two ends where they used to pass its width. Instrumentation only, no draw involved — but *a
refactor at a short-circuiting site is a behavioural change until a hash says otherwise*, and one of
those three sites sits beside a short-circuiting guard. Re-simulating seed 42 for 50 years reproduces
the sealed ruleset-4 baseline's event lines **byte for byte**, sha256 `b3025dfb…` on both sides.
`InstrumentationInvarianceTests`, `DeterminismTests`, `ReplayTests` and `RngStreamTests` all pass.

**Escalations for Shay** are collected at the end. Two of them are the ones the brief predicted; two
are new.

---

## Step 1 — the block lifted, and the framing corrected

The brief's correction was right and did not go far enough. It said the reference set is *rebuilt,
not refreshed*, which is true. It also said Layer 3 is **world-independent** and *needs nothing*. That
is half true, and the half that is not is the useful half.

### Layer 3 is not world-independent. It is world-*pinned*, and that is fine.

A corpus row with no `scope` is checked by Tier 1 alone and reads no world at all. A row *with* a
scope is checked against a context pack rebuilt from a particular log, so it asks its question of
whatever world it is handed.

Every row was run against `baselines/ruleset-4/seed-42` and classified. Asserted in
`CorpusWorldIndependenceTests`:

- **6 rows have no scope.** Byte-identical verdicts on both worlds, asserted including the detail
  string. These are genuinely world-independent.
- **8 of the 28 scoped rows still catch their fabrication** in a world nobody wrote them about:
  `r07-a-raid-that-does-not-exist`, `r09-threi-cut-revolt-in-the-wrong-year`,
  `r10-three-holders-one-unnamed`, `r10-one-killing-told-twice`, `r09-r11-killing-149-men`,
  `v12-kebarrow-2-21-action-extracts-nothing`, `v12-raid-target-over-read`,
  `v12-raid-indexed-by-place-only`. That is a stronger statement about the checker than the corpus
  was ever asked for.
- **20 do not**, and the reasons are all of one kind. **Twelve** fail because the passage names people
  and events the new history does not contain — the extracted assertion resolves to nobody, so the
  rule reports `name` rather than firing. **Eight** fail because the *scope itself* is gone: no reign
  of Heth Fal over the Sworn Men of Laehiford 39–51, no Sworn Men of Meigate 19–51. One of the twelve
  (`r09-r11-held-the-seat-since-year-one`) is worth naming — it fires a real `wrong-fate` on its
  *corrected* half, because in the new world Pouldrir Ho was cast out in 9 rather than killed in 20.
  The correction is true of v1 and false here, which is the world-dependence of a corpus row shown
  from the other side.

**No abort.** The brief's abort trigger is a row world-dependent *"in a way that changes what the
corpus means"*. Nothing here does. The rows are pinned to the sealed v1 record deliberately — that
policy was learned at ruleset 2, when re-simulating to obtain the world made 56 fixtures assertions
about whatever the current rules produced and they failed together. This measures how much of the
corpus that pinning is load-bearing for. The answer is 20 rows' worth, permanently, and since the v1
baseline is create-only and sealed, the cost is already paid. **Layer 3 still needs nothing** — for a
different reason than the brief gave.

### Layer 4, and two defects that only a second world could find

Layer 4 now runs as a theory over both sealed baselines. 22 tests, all passing. Two figures pinned per
baseline (headings declared, scopes carrying prose); everything else derived from that world's own
record, because a hard figure would be asserting v1's shape of a different world.

Running it against a second world found two things, **neither of which broke any existing assertion**:

**1. The independent verifier read a data key the engine has never written.** `RecordFacts.Took`
looked for `took`, `haul` or `plunder`; the engine writes `loot`. So every successful raid came back
as zero and Layer 4's three-way raid split has been a two-way one since the layer was written. The
partition summed, the totals matched the record, nothing failed — because every assertion was about
the *accounting* and none about the cells. This is the silent-path family reaching the layer that
exists to catch it, and three plausible key names and not the real one is precisely what instances 1
and 2 of that family looked like. Fixed, and each branch is now asserted non-empty: **assert
extraction, not just absence of failure.**

**2. The ruler list was a record list.** A contested transfer emits both the challenge or coup that
decided it *and* a `POLITY.SUCCESSION` row carrying the state change — deliberately, since one
readable line described the act twice. Reading both put the same person on the same seat twice in the
same year (`Pouldrir Ho 15–15, Pouldrir Ho 15–20`). §7 says Layer 4 verifies ruler lists; it was
verifying a partition over them. Collapsed, and asserted: no ruler appears twice consecutively.

### What is staged, and what it is not

`out/carry-forward/reference-set/`, written by a new `wb reference` command. Zero inference — no
client is constructed. Every page carries the banner as its first line.

- **`candidate-facts.md`** — the shape of §9 for the new world.
- **`candidate-query-suite.md`** — **16 slots**, in the shape of the v1.2 suite, each with the records
  its machine answer came from. Both of v1's traps reproduced.
- **`withheld-not-absent-candidates.md`** — **5 ranked candidates**, each checked against all three
  properties. 41 records in this world satisfy all three.
- **`README.md`** — what it is, and where the sealed source artefacts are. The chronicle and log are
  **pointed at, not copied**: a copy of a sealed artefact is a thing that can drift from it. The seal
  was verified at staging time and every artefact matches its manifest hash.

**Nothing is marked verified. Nothing entered the suite.** Halting here.

`out/` is gitignored, so the three sheets are on disk and not in the repository — which is correct
rather than a gap: they are a pure function of the sealed log and the deriving code, at zero inference
cost, so `wb reference` reproduces them exactly whenever they are wanted. **Verified and derived are
different, and only one of them is precious.** What would be precious is Shay's reading of them, and
that has an obvious home in the suite once it exists.

Three things went wrong while deriving these, all caught by reading the output, all fixed, and all
worth naming because each would have wasted the reading rather than helped it:

- The ruler lists carried the duplicate-spell defect above, which is how it was found at all.
- A false-premise candidate asserted "the Kebarrow Compact never took Threi Cut" having only checked
  that it was not the house on *that* record. A **false false premise** — a question whose premise is
  true is one the engine is right to answer, and it would have scored as a suite failure. Now
  verified against every conquest and secession of that place, and the sheet lists every house that
  ever held it.
- The causal slot staged a war whose recorded cause was `e:9 Threi Cut exists — site, 276 souls`. A
  true edge and a useless answer: the slot tests whether the query layer can walk a causal chain, and
  a chain one step long into the world's creation tests nothing. Genesis rows are now excluded and the
  candidate must have a cause that is itself something that happened.

---

## Step 2 — dispersion figures self-identify

Third instance of a verdict reported under an ambiguity in an engine figure. Three is a family, and
the family is now closed in the emitter rather than in anyone's discipline.

`WorldBuilder.Core.Analysis.Dispersion` carries its kind and cannot render without it:

```
sd=14.17            cv=35.3%            var=251.86
range=[38, 76] width=38                 ci95=[-2.70, +1.65]
```

Four decisions worth stating:

- **An interval prints both ends *and* its width.** That is the specific ambiguity that happened: a
  width of 38 alone reads exactly like a standard deviation of 38, and both readings were
  arithmetically sensible. `range=[38, 76] width=38` cannot be misread.
- **It is a reference type, so an absent one is null and loud.** A struct would default to a
  valid-looking `sd=0.00`, which is the absent-versus-zero conflation this project has already met in
  the checker, the query layer, the plot ledger and the ratio metrics.
- **"Spread" is retired from every emitted figure.** It meant three things: a coefficient of
  variation in `SeparationProfile`, max − min in a report, and the share of the commonest branch in
  `OutcomeSpread`. The third is now `OutcomeSkew` / `skew`, and the invariant names read
  `raid outcome skew`, `tribute outcome skew`, `heir claim outcome skew`.
- **No `iqr` member**, although the brief listed one. Nothing in this engine computes an interquartile
  range — `GeographyAudit` says why it reports a coefficient of variation instead — and §4's lesson is
  that a label with no emitter is worse than a dead branch. It goes in beside the first thing that
  emits one.

Converted at every emission site: `Contrast` (sd, ci95), `SeparationProfile` (range, cv),
`GeographyAudit.ProximityRange`, `DistanceDecision.Range`, the `distance can vary` invariant, and
every figure `wb panel` and `wb geometry` print. The panel's private `Sd()` helper is gone.

### The test, in three parts

`DispersionTests`, six tests, because one assertion would not cover it:

1. **Exhaustive over the enum.** Every kind has a distinct non-empty label and renders `label=`. A
   kind added without a label fails here rather than emitting a bare number in a report. The
   construction switch is exhaustive too, so adding a kind without teaching the test about it fails.
2. **Structural.** No public member of the analysis or checker assemblies exposes a dispersion as a
   bare number. A property called `SpreadPct` returning an int is an invitation to interpolate it
   beside a mean, which is how the figure lost its kind the first time. Two-letter abbreviations match
   a whole camel-case word rather than a substring — `CrossDomainEdges` and `WarsDeclared` both
   contain "sd", and a lexicon that flagged them would be switched off within a round.
3. **Behavioural, entered where production enters.** `Contrast.Line()` must contain `sd=` and
   `ci95=`; the `distance can vary` invariant is read out of `Invariants.Check` on a real world and
   must start `range=[`; and no invariant on any reference seed may report two digits either side of
   an en dash, which is the form a range used to be written in.

**And the good half, recorded.** All three ambiguities were caught, each by a different route, and
every catch came from **re-deriving rather than re-reading**. The figure that gets checked is the one
somebody computes a second time, not the one somebody reads a second time. That is the working method
this project actually has, and it is now written down as one.

---

## Step 3 — the concurrency wedge

**Recorded** in `docs/WORLDBUILDER-PROJECT.md` §5 under Stage 15, with the conditions that produced
it: `wb book` against local Ollama, concurrently with `wb panel --count 207` — four arms plus a
shared-board arm per seed, saturating every core. Twice, on the reference machine. Both times the
symptom was a request that never returned rather than one that failed.

**Mitigated cheaply.** `LlmOptions.MaxConcurrentCalls` is 1, `ConcurrencyWaitSeconds` is 300, and
`OllamaClient` gates every call through `CompleteAsync`. Exceeding the bound throws
`LlmUnavailableException` naming the bound, the wait and the endpoint — because the actual cause both
times was something else saturating the machine and nothing in the output said so. Waiting forever is
what the wedge looks like from outside, so a bound whose failure mode is the thing it prevents is not
a bound.

Nothing in the engine issues concurrent calls today; rendering a book is a loop. This is the bound the
first thing that parallelises rendering will meet.

**A second defect fell out, and it is the same family as step 2.** `OllamaClient` built its own
`HttpClient`, whose default timeout is 100 seconds, so a call configured to wait 900 was cancelled at
100 and then reported *"did not answer within 900s"*. A wrong figure in an error message is an
unlabelled figure by another route — nothing questions it — and this one fires routinely, because a
2,000-token pack costs about 80 seconds in prompt evaluation before a word is generated. One deadline
now, and it is the configured one. The response body's read shares it too; on the outer token alone, a
response whose headers arrived and whose body stalled would have waited without limit — the wedge
wearing a 200.

**Not investigated further.** Recorded, not scheduled.

`GenerationConcurrencyTests`, five tests, all entered through `CompleteAsync` because that is the only
method any caller uses: the default is 1; a second concurrent call refuses loudly with the bound
named; a slot is released after a call that *failed*, so one bad call cannot become a wedge of its
own; a bound of zero is refused at construction; and the client carries no second, shorter deadline.

---

## Step 4 — `flat − geography` registered, not run

Registered in `docs/panel-prereg.md` §7 with **MDE 5 points**, the same minimum effect §3 set and for
the same reason. Both readings are stated as live: distance genuinely constrains, or the gap is within
noise, which at these intervals it plainly is. Decision rules fixed in advance, including that a
*negative* result is to be reported plainly — geography's design rationale never rested on a variety
delta in either direction.

**Registered as its own family of one at α = 0.05, not as a fourth member of the three-contrast
family.** This is a decision, and the reason to state it: adding it to §6's family would raise every
Holm threshold in it and change the verdicts on three contrasts *that have already been reported*.
That is re-analysing a settled result by enlarging its family after the fact — the same move as
re-analysing seen data with a newly chosen variance, which §2 forbids in the other direction. If Shay
would rather it join the family and the three be re-reported under the new thresholds, that is a
different and defensible choice; it is not one this phase should make quietly.

`wb panel` computes and prints it under its own heading. **The panel was not run.** The registered
measurement is N=207 and nothing at that N was executed. What was executed is a 3-seed, 8-year smoke
run to prove the code path is not a silent one — an unexercised branch is exactly the family this
phase spent its time on — and its figures are not results and are not recorded here.

---

## The record

**The best-executed phase in the record produced no positive result.** The null is precise — the
headline interval excludes the MDE in both directions — and realised σ was 15.87 against 16.48
predicted, so the sizing held. That is the outcome, not a disappointment.

**Geography's design rationale is untouched.** Distance gates conflict, trade, alliance and later
rumour. The variety-delta claim was volunteered, never required, and removing it improves the record.

**What three phases of chasing a dead claim actually bought:** a probe that catches ordering bugs
reading would not find; instrumentation invariance as a standing rule; RNG draw order as a Stage 3
determinism constraint; the measurement panel decoupled from the reference panel; `BaselineArchive`
verifying the archived board against genesis rather than merely requiring one; and a discipline that
killed its own headline twice.

**Seed 42 at ruleset 4 is a different world, not a stale one.** The reference set is rebuilt, not
refreshed. Any future ruleset change carries the same cost, and that cost is the argument for keeping
the hand-verified set as small as the suite genuinely requires. This phase measured how small that is:
Layers 3, 4 and 5 need no human at all, and what needs one is the query suite and one epistemic case.

**A new one, from this phase.** *A verifier is only verified against a second subject.* Layer 4 read a
key the engine never writes, and reported a three-way split as two-way, for as long as it had exactly
one world to read. Nothing about the defect required ruleset 4 to find — it was findable from day one
— but nothing *found* it, because every assertion was about the accounting and the accounting was
sound. Duplicated verification buys independence; it does not buy coverage. The second world did.

---

## Escalate — for Shay

1. **The 16 query questions.** Candidates staged with their supporting records. Both the questions and
   the answers need establishing; the machine answers are prompts for the reading, not answers.
2. **The canonical withheld-not-absent case.** Five ranked candidates, 41 qualifying records. This one
   case carries the v3 epistemic layer's premise, so it is worth choosing deliberately.
3. **The ruleset-4 chronicle holds out 6 of its 13 scopes.** v1 held out 3 of 15. Whether six
   exclusions on thirteen is the checker working or the checker over-firing is a question about what
   the excluded passages *say*, which is prose judgement. The figure is pinned in
   `SealedBaselines.Ruleset4` so it cannot drift while nobody is looking at it. `chronicle-42.unverified.md`
   in that baseline holds the passages and their findings.
4. **The Trello Stage 15 card.** The finding is recorded in `docs/WORLDBUILDER-PROJECT.md` §5, which is
   the durable record. The board at `trello.com/b/Ovwt583e/world-builder` is not reachable from this
   machine, so the card needs the same line added by hand.
5. **`flat − geography`'s Holm family.** Registered as its own family of one, for the reason in step 4.
   Overrule if you would rather it join the three and have those verdicts re-reported.

---

## Not in this phase

**Next is the workbench (Stage 5), and it needs its own scoping rather than a loop-prompt derived from
this one.** The case for it is stronger than when it was deferred: every decisive moment across the
last three phases was an inspection problem — a metric catching a miscalibration, a probe catching an
ordering bug, a guard catching an underpowered comparison, and now a second world catching a verifier
reading a key that was never written. Instrumentation for the builder is not polish. The economy half
of Stage 6 waits behind it.
