using System.Globalization;

namespace WorldBuilder.Core.Analysis;

/// <summary>
/// Per-entity views: an actor's life as a timeline, a faction's rise and fall as a ledger.
/// These exist because a flat chronological log hides the thing the gate is actually testing —
/// whether any individual accumulated a story worth following.
/// </summary>
public static class Dossier
{
    public static IReadOnlyList<string> Actor(WorldView view, EntityId id)
    {
        WorldState state = view.State;
        Actor actor = state.ActorOf(id);
        List<string> lines = [];

        string life = actor.IsAlive
            ? $"born {actor.BirthYear}, living (age {view.LastYear - actor.BirthYear})"
            : $"{actor.BirthYear}–{actor.DeathYear}";

        lines.Add($"{actor.Name} ({id})  {life}");
        lines.Add($"  house      {(actor.Faction.IsNone ? "none" : state.Label(actor.Faction))}");
        lines.Add($"  rank       {actor.Title}");
        lines.Add($"  at         {(actor.Place.IsNone ? "nowhere" : state.Label(actor.Place))}");
        lines.Add($"  traits     ambition {actor.Traits.Ambition}, guile {actor.Traits.Guile}, " +
                  $"martial {actor.Traits.Martial}, loyalty {actor.Traits.Loyalty}");

        List<Relation> edges = state.Relations.Touching(id);
        if (edges.Count > 0)
        {
            lines.Add("");
            lines.Add("  ties");
            foreach (Relation r in edges)
            {
                if (r.Value == 0) continue;
                EntityId other = r.Key.From == id ? r.Key.To : r.Key.From;
                string direction = r.Key.From == id ? "->" : "<-";
                lines.Add($"    {direction} {r.Key.Kind,-10} {r.Value,4}  {state.Label(other)}" +
                          $"   (since {r.CreatedYear}, {r.Cause})");
            }
        }

        lines.Add("");
        lines.Add("  life");
        foreach (EventId e in view.Log.ForEntity(id))
        {
            if (view.Log.Get(e).Significance < Significance.Minor) continue;
            lines.Add($"    {view.Summarise(e)}");
        }

        return lines;
    }

    public static IReadOnlyList<string> Faction(WorldView view, EntityId id)
    {
        WorldState state = view.State;
        Faction faction = state.FactionOf(id);
        List<string> lines =
        [
            $"{faction.Name} ({id})   seat {state.Label(faction.Seat)}, succession by {faction.Succession}",
            $"  leader     {(faction.Leader.IsNone ? "vacant" : state.Label(faction.Leader))}",
            $"  legitimacy {faction.Legitimacy}",
            $"  treasury   {faction.Treasury}",
            $"  power      {state.PowerOf(id)}",
        ];

        List<Place> holdings = state.HoldingsOf(id);
        lines.Add($"  holds      {holdings.Count} place(s), {state.PopulationOf(id)} souls");
        foreach (Place p in holdings)
        {
            lines.Add($"    {p.Id,-6} {p.Name,-18} pop {p.Population,6}   " +
                      $"grain {p.Stockpile[(int)Resource.Grain],5}  ore {p.Stockpile[(int)Resource.Ore],5}");
        }

        lines.Add("");
        lines.Add("  standing with others");
        foreach (Core.Faction other in state.Factions)
        {
            if (other.Id == id) continue;
            int grievance = state.Relations.ValueOf(id, other.Id, RelationKind.Grievance);
            int trade = state.Relations.ValueOf(id, other.Id, RelationKind.Trade);
            bool allied = state.Relations.Has(id, other.Id, RelationKind.Alliance);
            bool war = state.Relations.Has(id, other.Id, RelationKind.AtWar);
            if (grievance == 0 && trade == 0 && !allied && !war) continue;

            string flags = (war ? " AT-WAR" : "") + (allied ? " ALLIED" : "");
            lines.Add($"    {state.Label(other.Id),-30} grievance {grievance,4}  trade {trade,4}{flags}");
        }

        lines.Add("");
        lines.Add("  history");
        foreach (EventId e in view.Log.ForEntity(id))
        {
            if (view.Log.Get(e).Significance < Significance.Major) continue;
            lines.Add($"    {view.Summarise(e)}");
        }

        return lines;
    }

    /// <summary>The map as it stands, so the reader can orient before reading 50 years of log.</summary>
    public static IReadOnlyList<string> Atlas(WorldView view)
    {
        WorldState state = view.State;
        List<string> lines = [$"{state.Places.Count} places, {state.Factions.Count} factions, " +
                              $"{CountLiving(state)} living of {state.Actors.Count} ever"];

        lines.Add("");
        foreach (Core.Faction f in state.Factions)
        {
            List<Place> holdings = state.HoldingsOf(f.Id);
            string leader = f.Leader.IsNone ? "vacant" : state.NameOf(f.Leader);
            lines.Add($"  {f.Id,-5} {f.Name,-28} leg {f.Legitimacy,3}  power {state.PowerOf(f.Id),4}  " +
                      $"{holdings.Count} place(s)  led by {leader}");
        }

        lines.Add("");
        foreach (Place p in state.Places)
        {
            if (p.Kind == PlaceKind.Region) continue;
            string holder = p.Controller.IsNone ? "unclaimed" : state.NameOf(p.Controller);
            lines.Add($"  {p.Id,-5} {p.Name,-18} {p.Kind,-11} pop {p.Population,6}   {holder}");
        }

        return lines;
    }

    private static int CountLiving(WorldState state)
    {
        int n = 0;
        foreach (Core.Actor a in state.LivingActors()) n++;
        return n;
    }

    public static string Percent(int part, int whole) =>
        whole == 0 ? "0%" : ((part * 100 / whole).ToString(CultureInfo.InvariantCulture) + "%");
}
