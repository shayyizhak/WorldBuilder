# Pre-registration — phase: explain, decide, seal

Written before `wb geometry` was run once. No figure below was seen before the
rules were fixed. The phase brief pre-commits step 1's decision rule; step 2's
does not define "materially", and that gap is filled here rather than after the
numbers arrive.

---

## Measures, fixed before use

**Separation spread** is the coefficient of variation — standard deviation as a
percentage of the mean — over the pairwise travel costs between the places a
world actually has. Chosen over the interquartile range because this figure gets
compared *across boards*: an IQR is in cost units, so a board whose costs are
simply larger looks more varied, while a coefficient of variation says how
unequal the separations are rather than how big. The brief permits either and
asks for consistency; this is the one used everywhere below.

**Discriminating share** is, of the decisions distance had any room to move, the
share where holding proximity flat at 100 changes the outcome — with the random
draw held fixed. "Room to move" excludes a ranking over a single candidate and a
ranking whose candidates are all equidistant, because neither could have gone
otherwise and counting them would make the share a measure of how many foregone
conclusions a world contained. Both the raw and the open counts are reported so
the exclusion is visible rather than argued about.

---

## Step 1 — the rule, restated from the brief

The hypothesis: on seed 99 the places are sited closely enough relative to each
other that realised proximity is near-uniform, so distance rarely discriminates
and the four mechanics fall back to near pre-geography behaviour.

- **Holds** if seed 99 ranks lowest or second-lowest of five on *both*
  separation spread and discriminating share, **and** its discriminating share
  is below half the panel median.
- **Fails** if seed 99's spread and discriminating share are comparable to the
  rest of the panel. → halt, escalate, and do not go looking for a second
  explanation in the same run.
- **Mixed** — one criterion met, not the other → report both, mark partially
  explained, halt.

Seed 7 is measured into the same table and gets no decision rule. It stays in
`KnownFailing` and leaves only by holding.

---

## Step 2 — "materially", defined here because the brief does not

The brief says to halt if the attributions "move materially across boards" and
proceed if they "hold". It gives no threshold, and inventing one after seeing
the spread would be exactly the fitting this project forbids. So the rule is
comparative, in the same shape as step 1's, and needs no absolute constant.

**Do the boards differ at all?** Required before anything else is worth asking.
The boards differ if the range of separation spread *across boards* exceeds the
range of separation spread *across seeds within a board* (taken as the median of
the per-board ranges). If they do not differ, the answer to step 2 is "this test
could not be run", and it is reported as that rather than as insensitivity.

**Do the attributions move materially?** For each of the four mechanics:

- *within-board variation* = the range (max − min) of that mechanic's
  discriminating share across the five seeds on one board, taken as the median
  of that range over the boards tested.
- *between-board variation* = the range of that mechanic's pooled discriminating
  share across the boards.

**Attributions move materially if, for any one of the four mechanics,
between-board variation exceeds within-board variation.** In plain terms:
changing the map matters more than changing the seed. That is the condition
under which sealing against one board would calibrate against a distribution
that is not the one being shipped, which is what the brief is protecting.

If no mechanic meets it, attributions hold → proceed to step 4, and record the
limitation the brief demands in its own terms: **this test can demonstrate
sensitivity strongly and insensitivity only weakly**, because every board in the
sample comes from one generator and may share characteristics an Azgaar export
does not. It will not be reported as "geometry does not matter."

**Boards to be tested:** `wb map make` at generator seeds 1 (the stored board),
2, 3, 4 and 5, at the shipped 20×14. Five boards × five seeds. Fixed here so the
sample cannot be extended until it says something.

---

## What would abort

Per the brief: a fix requiring a threshold move, step 1's hypothesis failing,
the corpus fix needing a duplicated implementation, or any step needing a
ruleset change. None of these is discretionary.
