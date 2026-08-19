using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>What one faction's goal book held at the year a runaway formed.</summary>
public sealed record BookAtRunaway(EntityId Faction, string Name, bool IsRunaway, IReadOnlyList<GoalKind> Held);

/// <summary>One world's answer.</summary>
/// <param name="RunawayYear">The first year one power held 70% of the settled population, or 0.</param>
public sealed record BookPressureSeed(
    ulong Seed, int RunawayYear, IReadOnlyList<BookAtRunaway> Books);

/// <summary>
/// §5 of the phase-2 brief: at the year a runaway forms, how many factions held a full book, and what
/// was in it?
///
/// <b>A measurement, not a mechanism, and the brief is explicit about why.</b> 441 creations were
/// refused across the panel against 505 admitted, almost all for space at <c>MaxPerOwner = 2</c>, with
/// <c>SeizeLeadership</c> the largest kind at 157 against <c>FormAlliance</c>'s 13. The hypothesis is
/// that internal ambition crowds out the external goals that would balance a leader who is running
/// away. If books are full of <c>SeizeLeadership</c> and <c>Avenge</c> while alliances go unformed, a
/// candidate brake already sits inside existing machinery; if they are full of unrelated things, there
/// is nothing here.
///
/// <b>Reported, not acted on.</b> It feeds a decision nobody has made, and this project has already
/// proposed a mechanism from a variable that turned out not to move.
///
/// <b>Read at one year, and that is a real limitation.</b> The runaway year is when the concentration
/// threshold is first crossed, which is late in the process it names — the crowding out, if it happened,
/// happened in the decade before. A single-year snapshot can only say what the books looked like when
/// the outcome was already arriving. Widening it is a decision for whoever takes the brake question up.
/// </summary>
public static class BookPressure
{
    /// <summary>Share of the settled population one power must hold to count as a runaway.</summary>
    public const int RunawaySharePercent = 70;

    public static BookPressureSeed Run(ulong seed, int years)
    {
        Simulation sim = new(seed);

        int runawayYear = 0;
        EntityId runaway = EntityId.None;
        List<BookAtRunaway> books = [];

        for (int i = 1; i <= years; i++)
        {
            sim.Step(sim.StartYear + i);
            if (runawayYear != 0) continue;

            (EntityId biggest, int share) = Concentration(sim.State);
            if (share < RunawaySharePercent) continue;

            runawayYear = sim.StartYear + i;
            runaway = biggest;

            // Read inside the loop, because the book is a live object and the question is about the
            // year the threshold was crossed rather than about the end of the run.
            foreach (Faction f in sim.State.Factions)
            {
                if (sim.State.IsDefunct(f.Id)) continue;

                List<GoalKind> held = [];
                foreach (Goal g in sim.State.Goals.For(f.Id)) held.Add(g.Kind);
                books.Add(new BookAtRunaway(f.Id, f.Name, f.Id == runaway, held));
            }
        }

        return new BookPressureSeed(seed, runawayYear, books);
    }

    private static (EntityId Biggest, int Share) Concentration(WorldState state)
    {
        int total = 0, best = 0;
        EntityId biggest = EntityId.None;

        foreach (Faction f in state.Factions)
        {
            int held = state.PopulationOf(f.Id);
            total += held;
            if (held > best) { best = held; biggest = f.Id; }
        }

        return (biggest, total == 0 ? 0 : best * 100 / total);
    }

    public static IReadOnlyList<string> Render(IReadOnlyList<BookPressureSeed> panel)
    {
        List<string> lines =
        [
            "## Goal books at the year a runaway formed",
            "",
            $"A runaway is one power holding {N(RunawaySharePercent)}% of the settled population. " +
            $"`MaxPerOwner` is {N(GoalBook.MaxPerOwner)}, so a book of that size is full.",
            "",
            "| seed | runaway year | standing factions | books full | what they held |",
            "|---|---|---|---|---|",
        ];

        int seedsWithRunaway = 0, factionsSeen = 0, booksFull = 0;
        SortedDictionary<GoalKind, int> heldTally = [];

        foreach (BookPressureSeed s in panel)
        {
            if (s.RunawayYear == 0)
            {
                lines.Add($"| {N((int)s.Seed)} | none | — | — | — |");
                continue;
            }

            seedsWithRunaway++;
            int full = 0;
            List<string> contents = [];

            foreach (BookAtRunaway b in s.Books)
            {
                factionsSeen++;
                if (b.Held.Count >= GoalBook.MaxPerOwner) { full++; booksFull++; }
                foreach (GoalKind k in b.Held) heldTally[k] = heldTally.GetValueOrDefault(k) + 1;

                contents.Add(b.Held.Count == 0
                    ? $"{b.Name}{(b.IsRunaway ? "*" : "")}: empty"
                    : $"{b.Name}{(b.IsRunaway ? "*" : "")}: {string.Join("+", b.Held)}");
            }

            lines.Add($"| {N((int)s.Seed)} | {N(s.RunawayYear)} | {N(s.Books.Count)} | {N(full)} " +
                      $"| {string.Join("; ", contents)} |");
        }

        lines.Add("");
        lines.Add($"`*` marks the runaway itself. {N(seedsWithRunaway)} of " +
                  $"{N(panel.Count)} world(s) produced one; across those, " +
                  $"**{N(booksFull)} of {N(factionsSeen)} standing factions held a full book**.");
        lines.Add("");

        if (heldTally.Count > 0)
        {
            lines.Add("What was in them, pooled:");
            lines.Add("");
            lines.Add("| goal kind | held at the runaway year |");
            lines.Add("|---|---|");
            foreach ((GoalKind kind, int n) in heldTally) lines.Add($"| `{kind}` | {N(n)} |");
            lines.Add("");
        }

        lines.Add("**Not acted on.** The figure feeds the brake decision and does not make it.");
        lines.Add("");

        // The caveat is emitted beside the number rather than left for a reader to notice, because an
        // unlabelled figure is a fabrication vector regardless of who reads it next — and this one
        // reads as support for the hypothesis until you count the denominator.
        int mostStanding = 0;
        foreach (BookPressureSeed s in panel)
            if (s.Books.Count > mostStanding) mostStanding = s.Books.Count;

        if (seedsWithRunaway > 0 && mostStanding <= 2)
        {
            lines.Add($"**The denominator is the finding, and it undercuts the hypothesis.** At most " +
                      $"{N(mostStanding)} faction(s) were standing in any world at its runaway year, so " +
                      $"the {N(booksFull)}-of-{N(factionsSeen)} figure is two factions per seed and not " +
                      "a population. Worse for the crowding-out story: with two powers left, the only " +
                      "available ally *is* the hegemon, and `FindAllyCandidate` excludes the threat by " +
                      "design. So `FormAlliance` is absent here because there is nobody to form one " +
                      "with, which is structural and not a book-space effect.");
            lines.Add("");
            lines.Add("**What this means for the brake question.** Measured at the runaway year, the " +
                      "question cannot be answered either way: the field is too small for the " +
                      "mechanism to be visible, and any figure taken here is consistent with both " +
                      "explanations. Whoever takes the question up should measure over the decade " +
                      "*before* the threshold, while there are still powers to ally with — that is " +
                      "where crowding out would have to happen if it happens at all.");
        }
        else
        {
            lines.Add("A full book of internal goals beside no alliance is consistent with crowding " +
                      "out and also with a faction that has nobody worth allying with, and this " +
                      "measurement cannot separate them.");
        }

        return lines;
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
