using System.Globalization;
using System.Text;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Every component of <see cref="WorldState"/>, fingerprinted one field at a time.
///
/// <b>Why field-at-a-time rather than one string.</b> <c>ReplayTests</c> has asserted "state is a
/// fold over the log" since v1 against a single concatenated fingerprint that enumerates actors,
/// places, factions, arcs and relations — and neither goals, nor traits, nor yields, nor a
/// relation's provenance. It cannot fail on anything it does not read, which is the eighth
/// appearance of the family the project already names: <i>a verifier that reads a field name the
/// engine doesn't write cannot fail.</i> Here the field list is exhaustive and the comparison
/// reports which field moved, so a gap shows up as a named row rather than as a silence.
///
/// <b>The board is deliberately not here.</b> It is the one piece of a world that is not folded
/// from the log and says so in <see cref="WorldState.Board"/>: a stored artefact carried beside the
/// record, hashed into the genesis event. A fold cannot reproduce it and is not supposed to.
/// </summary>
public static class WorldFingerprint
{
    /// <summary>Component name to a fingerprint of that component alone.</summary>
    public static SortedDictionary<string, string> Of(WorldState state)
    {
        SortedDictionary<string, string> rows = new(StringComparer.Ordinal);

        rows["world.year"] = N(state.Year);

        Each(rows, "actors.identity", state.Actors, a => $"{a.Id}|{a.Name}|{N(a.BirthYear)}");
        Each(rows, "actors.death", state.Actors, a => $"{a.Id}|{a.DeathYear?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        Each(rows, "actors.title", state.Actors, a => $"{a.Id}|{a.Title}");
        Each(rows, "actors.faction", state.Actors, a => $"{a.Id}|{a.Faction}");
        Each(rows, "actors.place", state.Actors, a => $"{a.Id}|{a.Place}");
        Each(rows, "actors.traits", state.Actors,
            a => $"{a.Id}|{N(a.Traits.Ambition)},{N(a.Traits.Guile)},{N(a.Traits.Martial)},{N(a.Traits.Loyalty)}");

        Each(rows, "places.identity", state.Places, p => $"{p.Id}|{p.Name}|{p.Kind}|{p.Parent}");
        Each(rows, "places.cell", state.Places, p => $"{p.Id}|{N(p.Cell)}");
        Each(rows, "places.yield", state.Places,
            p => $"{p.Id}|{N(p.Yield[0])},{N(p.Yield[1])},{N(p.Yield[2])}");
        Each(rows, "places.population", state.Places, p => $"{p.Id}|{N(p.Population)}");
        Each(rows, "places.stockpile", state.Places,
            p => $"{p.Id}|{N(p.Stockpile[0])},{N(p.Stockpile[1])},{N(p.Stockpile[2])}");
        Each(rows, "places.controller", state.Places, p => $"{p.Id}|{p.Controller}");

        Each(rows, "factions.identity", state.Factions, f => $"{f.Id}|{f.Name}|{f.Succession}|{f.Seat}");
        Each(rows, "factions.leader", state.Factions, f => $"{f.Id}|{f.Leader}");
        Each(rows, "factions.legitimacy", state.Factions, f => $"{f.Id}|{N(f.Legitimacy)}");
        Each(rows, "factions.treasury", state.Factions, f => $"{f.Id}|{N(f.Treasury)}");

        Each(rows, "arcs.identity", state.Arcs, a => $"{a.Id}|{a.Kind}|{a.Name}|{N(a.StartYear)}|{a.Origin}");
        Each(rows, "arcs.end", state.Arcs, a => $"{a.Id}|{a.EndYear?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
        Each(rows, "arcs.sides", state.Arcs, a => $"{a.Id}|{string.Join(",", a.Sides)}");

        Each(rows, "relations.edges", state.Relations.All, r => $"{r.Key.From}->{r.Key.To}:{r.Key.Kind}");
        Each(rows, "relations.value", state.Relations.All, r => $"{Key(r)}|{N(r.Value)}");
        Each(rows, "relations.origin", state.Relations.All, r => $"{Key(r)}|{N(r.CreatedYear)}|{r.Cause}");
        Each(rows, "relations.lastChange", state.Relations.All,
            r => $"{Key(r)}|{N(r.LastChangedYear)}|{r.LastCause}");

        // Goals, on exactly the same footing as everything else. The point of the file is that
        // this row is not special-cased into invisibility.
        List<Goal> goals = state.Goals.Snapshot();
        Each(rows, "goals.identity", goals,
            g => $"{N(g.Id)}|{g.Owner}|{g.Kind}|{g.Target}|{N(g.CreatedYear)}|{N(g.ExpiresYear)}|{g.Cause}");
        Each(rows, "goals.progress", goals, g => $"{N(g.Id)}|{N(g.Progress)}");
        Each(rows, "goals.arc", goals, g => $"{N(g.Id)}|{g.Arc}");

        return rows;
    }

    /// <summary>The components on which two worlds disagree, in name order.</summary>
    public static List<string> Differences(WorldState a, WorldState b)
    {
        SortedDictionary<string, string> left = Of(a);
        SortedDictionary<string, string> right = Of(b);

        List<string> differ = [];
        foreach ((string name, string value) in left)
        {
            if (!right.TryGetValue(name, out string? other) || !string.Equals(value, other, StringComparison.Ordinal))
                differ.Add(name);
        }

        foreach (string name in right.Keys)
            if (!left.ContainsKey(name)) differ.Add(name);

        return differ;
    }

    /// <summary>Every component name this fingerprint covers, for a report that has to say what it checked.</summary>
    public static List<string> Components(WorldState state) => [.. Of(state).Keys];

    private static string Key(Relation r) => $"{r.Key.From}->{r.Key.To}:{r.Key.Kind}";

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void Each<T>(
        SortedDictionary<string, string> rows, string name, IEnumerable<T> items, Func<T, string> line)
    {
        StringBuilder sb = new();
        foreach (T item in items) sb.Append(line(item)).Append('\n');
        rows[name] = sb.ToString();
    }
}
