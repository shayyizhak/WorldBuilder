using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Rendering;

namespace WorldBuilder.Inference;

/// <summary>
/// Runs every consumer that reads an event payload, at the entry point the product calls, with a
/// read recorder attached.
///
/// <b>One implementation, called twice.</b> The standing test asserts on what this returns and
/// <c>wb schema --reads</c> prints it. Written once because the alternative is two lists of what
/// counts as a consumer, which drift, and a consumer missing from the test's copy is a consumer
/// whose dead reads nothing checks — the same defect one level up.
///
/// <b>Layer 4 is deliberately absent.</b> It lives in an assembly that cannot reference this one,
/// and it runs the same assertion over its own reads from that side. Sweeping it from here would
/// route the independent verifier through the implementation it exists to be independent of.
/// </summary>
public static class SchemaSweep
{
    /// <summary>
    /// Every payload read the consumers make on one world.
    ///
    /// <paramref name="baselineDirectory"/> is optional and only decides whether the archive check
    /// is entered; the rest run on the world alone.
    /// </summary>
    public static EventFieldReads Run(WorldView view, string? baselineDirectory = null)
    {
        EventFieldReads reads = new();

        using (EventFieldReadLog.Record(reads))
        {
            // The fold. The one consumer that cannot be skipped: a name the reducer reads and
            // nothing writes is a state change that silently never happens.
            WorldView.Build(view.Log, view.Seed);

            // The readable view, which reads a payload for almost every kind there is.
            LogFormatter.Render(view.Log, view.Seed, Significance.Bookkeeping);

            // The chronicle pack builder, and the checker over what it produces.
            foreach (Faction f in view.State.Factions)
            {
                ContextPack pack = ContextPackBuilder.Faction(view, f.Id);
                if (pack.Events.Count == 0) continue;

                FabricationCheck.Check(pack, Prose(f.Name), wholeSection: true);

                if (f.Leader.IsNone) continue;

                ReignSpell? spell = ContextPackBuilder.Reigns(view, f.Leader).FindLast(s => s.Faction == f.Id);
                if (spell is null) continue;

                FabricationCheck.Check(ContextPackBuilder.Reign(view, spell), Prose(f.Name), wholeSection: true);
            }

            foreach (Arc arc in view.State.Arcs) ContextPackBuilder.Arc(view, arc.Id);
            for (int year = view.FirstYear; year <= view.LastYear; year++) ContextPackBuilder.Year(view, year);

            // Query retrieval. The planner is excluded on purpose — it reads a question, not a
            // record — and every shape is entered, because retrieval branches on it and a shape
            // nobody exercised is a shape whose reads nobody checked.
            QueryEngine engine = new(new NoModel(), view);

            foreach (QueryShape shape in Enum.GetValues<QueryShape>())
                foreach (Faction f in view.State.Factions)
                {
                    engine.Retrieve(engine.Ground(new QueryPlan
                    {
                        Shape = shape,
                        Subject = f.Name,
                        Question = $"What happened to {f.Name}?",
                        Topics = ["POLITY.SUCCESSION", "CONFLICT.RAID", "CONFLICT.BATTLE", "ECONOMY.PLAGUE"],
                    }));
                }

            // The reference-set derivations, which are what the staged candidate sheet is built
            // from and therefore what a hand-verified row would inherit a dead read through.
            ReferenceSet.FactsSheet(view);

            // The archive, which reads the board fingerprint off the genesis event.
            if (baselineDirectory is { Length: > 0 } dir && Directory.Exists(dir)) BaselineArchive.Check(dir);
        }

        return reads;
    }

    /// <summary>
    /// Prose with something in it for every rule to read.
    ///
    /// A rule handed an empty string extracts nothing and therefore reads nothing, so a sweep over
    /// blank text would report a checker that touches no payload at all. Counts, an enumeration, a
    /// partition, dates, departures and a plague figure, which between them reach the Tier 1 and
    /// Tier 2 families.
    /// </summary>
    private static string Prose(string subject) =>
        $"{subject} sent four raids in these years, of which two came away with grain and one was " +
        "beaten off. Three men held the seat: the first died in 24, the second was cast out in 31, " +
        "and the third still holds it. The plague of 26 killed 185 and drove 296 out. Two years " +
        "later the house made peace.";

    private sealed class NoModel : ILlmClient
    {
        public string ModelTag => "none";

        public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("the schema sweep must not call the model");
    }
}
