using System.Globalization;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Rendering;

namespace WorldBuilder.Inference;

/// <summary>
/// What the bookkeeping rows establish, stated as the condition they left behind rather than as
/// the rows themselves.
///
/// Retrieval reads the record and finds, behind Hadale's secession, two yearly accounting rows
/// recording that the Kebarrow Compact's standing had quietly eroded. Those rows explain the
/// secession and the chronicle is right never to print them: a passage opening on "a harvest
/// count at Meigate revealed a grain shortage" is narrating a spreadsheet, and that sentence
/// reached canon once already.
///
/// The resolution is not to pick one of the two. A bookkeeping row is a measurement, not an
/// event, and what a question needs from it is the quantity it measured — "the Compact's standing
/// had fallen to nothing by 26" — which explains without offering anything to narrate. So the
/// deltas are read, the world is folded to the end of that year, and the resulting <em>state</em>
/// is what the model is handed. There is no event here for it to tell.
/// </summary>
public static class PackCauses
{
    /// <summary>
    /// Above this the section stops being an explanation and becomes a second event list. A
    /// causal trace is capped at sixteen records and only a minority are bookkeeping, so this is
    /// slack rather than a constraint in practice.
    /// </summary>
    private const int MaxNotes = 12;

    /// <summary>
    /// State statements for the rows a pack carries as causes, oldest first.
    ///
    /// <paramref name="about"/> is what the pack is about — its cast and its subject. The yearly
    /// accounts touch every place and every power in the world, so an unfiltered reading would
    /// bury the one line that matters under thirty that do not.
    /// </summary>
    public static List<string> Notes(
        WorldView view, IReadOnlyList<EventId> rows, IReadOnlyCollection<EntityId> about)
    {
        if (rows.Count == 0) return [];

        HashSet<EntityId> relevant = [.. about];
        HashSet<string> seen = new(StringComparer.Ordinal);
        Dictionary<int, WorldState> byYear = [];

        // Gathered by kind and emitted in that order, because the cap is reached and what it
        // cuts matters. A single drift row adjusts a dozen grudges and one legitimacy, and
        // taking them in payload order filled the section with quarrels between people the
        // question never mentioned while dropping the standing that explains the secession —
        // the right rows retrieved, carried through the pack, and crowded out at the last step.
        List<string> standing = [], holdings = [], grudges = [];

        foreach (EventId id in rows)
        {
            Event e = view.Log.Get(id);
            WorldState at = AtEndOf(view, byYear, e.Year);

            foreach ((Sort sort, string note) in Reads(view, at, e, relevant))
            {
                if (!seen.Add(note)) continue;

                switch (sort)
                {
                    case Sort.Standing: standing.Add(note); break;
                    case Sort.Holding: holdings.Add(note); break;
                    default: grudges.Add(note); break;
                }
            }
        }

        List<string> notes = [.. standing, .. holdings, .. grudges];
        return notes.Count <= MaxNotes ? notes : notes[..MaxNotes];
    }

    /// <summary>What a statement is about, in the order the section wants them.</summary>
    private enum Sort
    {
        Standing,
        Holding,
        Grudge,
    }

    /// <summary>
    /// The quantities one row moved, as they stood once the year was over.
    ///
    /// Read off the event's own deltas rather than off its sentence. The sentence for a drift row
    /// is "old grudges cool and standing quietly erodes", which names neither the power whose
    /// standing moved nor what it moved to — everything that makes the row an explanation is in
    /// the payload the reducer applies.
    /// </summary>
    private static IEnumerable<(Sort Sort, string Note)> Reads(
        WorldView view, WorldState at, Event e, HashSet<EntityId> relevant)
    {
        foreach (KeyValuePair<string, string> kv in e.Data)
        {
            string[] parts = kv.Key.Split(':');
            (Sort sort, string? note) = parts[0] switch
            {
                "leg" when parts.Length == 3 =>
                    (Sort.Standing, Standing(view, at, Entity(parts, 1), relevant, e.Year)),
                "rel" when parts.Length == 6 =>
                    (Sort.Grudge, Grievance(view, at, parts, relevant, e.Year)),
                "stock" when parts.Length == 4 && parts[3] == "Grain" =>
                    (Sort.Holding, Grain(view, at, Entity(parts, 1), relevant, e.Year)),
                "pop" when parts.Length == 3 =>
                    (Sort.Holding, People(view, at, Entity(parts, 1), relevant, e.Year)),
                _ => (Sort.Grudge, null),
            };

            if (note is not null) yield return (sort, note);
        }
    }

    private static string? Standing(
        WorldView view, WorldState at, EntityId faction, HashSet<EntityId> relevant, int year)
    {
        if (faction.Kind != EntityKind.Faction || !relevant.Contains(faction)) return null;
        if (faction.Index > at.Factions.Count) return null;

        int standing = at.FactionOf(faction).Legitimacy;

        // "Nothing" rather than "0" where it bottomed out, because that is the wording the
        // engine's own revolt sentence uses — "whose standing had fallen to nothing" — and two
        // ways of saying one state is how a document ends up disagreeing with itself.
        return $"by {N(year)}, the standing of {view.State.NameOf(faction)} " +
               (standing == 0 ? "had fallen to nothing" : $"stood at {N(standing)} out of 100");
    }

    private static string? Grievance(
        WorldView view, WorldState at, string[] parts, HashSet<EntityId> relevant, int year)
    {
        if (!Enum.TryParse(parts[5], ignoreCase: true, out RelationKind kind)) return null;
        if (kind != RelationKind.Grievance) return null;

        EntityId from = Entity(parts, 1);
        EntityId to = Entity(parts, 3);
        if (from.IsNone || to.IsNone) return null;

        // Both ends, not either. A power the question is about is one end of every quarrel it
        // was ever in, so the looser test admitted a dozen grudges held by people the question
        // never named — noise that is worse than absence, because it reads as relevance.
        if (!relevant.Contains(from) || !relevant.Contains(to)) return null;

        int held = at.Relations.ValueOf(from, to, RelationKind.Grievance);
        if (held <= 0)
        {
            return $"by {N(year)}, {view.State.NameOf(from)} held nothing further against " +
                   $"{view.State.NameOf(to)}";
        }

        return $"by {N(year)}, the grievance {view.State.NameOf(from)} held against " +
               $"{view.State.NameOf(to)} stood at {N(held)}";
    }

    private static string? Grain(
        WorldView view, WorldState at, EntityId place, HashSet<EntityId> relevant, int year)
    {
        if (place.Kind != EntityKind.Place || !relevant.Contains(place)) return null;
        if (place.Index > at.Places.Count) return null;

        return $"by {N(year)}, the grain stored at {view.State.NameOf(place)} stood at " +
               $"{N(at.PlaceOf(place).Stockpile[(int)Resource.Grain])}";
    }

    private static string? People(
        WorldView view, WorldState at, EntityId place, HashSet<EntityId> relevant, int year)
    {
        if (place.Kind != EntityKind.Place || !relevant.Contains(place)) return null;
        if (place.Index > at.Places.Count) return null;

        return $"by {N(year)}, {view.State.NameOf(place)} held {N(at.PlaceOf(place).Population)} people";
    }

    /// <summary>
    /// The world as it stood at the end of a year, folded once per year and kept.
    ///
    /// The fold is the same reducer the simulation runs, so this cannot drift from the state the
    /// row actually produced — the alternative, reconstructing a quantity by summing deltas out
    /// of the payload, is a second implementation of the reducer and would be wrong the first
    /// time a rule clamped a value.
    /// </summary>
    private static WorldState AtEndOf(WorldView view, Dictionary<int, WorldState> cache, int year)
    {
        if (cache.TryGetValue(year, out WorldState? state)) return state;
        return cache[year] = Replay.Fold(view.Log, view.Seed, year);
    }

    private static EntityId Entity(string[] parts, int at) =>
        EntityId.TryParse($"{parts[at]}:{parts[at + 1]}", out EntityId id) ? id : EntityId.None;

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
}
