using System.Globalization;

namespace WorldBuilder.Core;

/// <summary>
/// The only thing in the engine that mutates <see cref="WorldState"/>. Rules read state and
/// emit events; this folds events into state. Keeping the write path to one function is what
/// makes replay, time travel and (later) retconning the past mechanically sound.
///
/// Most numeric change travels as declarative deltas in the event payload rather than as
/// hand-written cases here — see <see cref="ApplyDeltas"/>. That keeps the switch to genuinely
/// structural changes (an entity appears, a title moves, a place changes hands) and makes the
/// JSONL log self-describing: every scalar the world moved is visible in the event that moved it.
/// </summary>
public static class EventReducer
{
    public static void Apply(WorldState state, Event e)
    {
        switch (e.Kind)
        {
            case EventKind.GenesisWorld:
                state.Year = e.Year;
                break;

            case EventKind.GenesisPlace:
                ApplyGenesisPlace(state, e);
                break;

            case EventKind.GenesisFaction:
                ApplyGenesisFaction(state, e);
                break;

            case EventKind.GenesisActor:
            case EventKind.LifeBirth:
                ApplyActorAppears(state, e);
                break;

            case EventKind.LifeComingOfAge:
                state.ActorOf(e.Subject).Title = ParseTitle(e.GetString("title"), Title.Retainer);
                break;

            case EventKind.LifeDeathNatural:
            case EventKind.LifeDeathViolent:
                ApplyDeath(state, e);
                break;

            case EventKind.PolitySuccession:
                ApplySuccession(state, e);
                break;

            case EventKind.PolityAppointment when !e.Subject.IsNone:
                state.ActorOf(e.Subject).Title = ParseTitle(e.GetString("title"), Title.Retainer);
                break;

            case EventKind.PolityExile:
                ApplyExile(state, e);
                break;

            case EventKind.PolityExileReturn:
                ApplyExileReturn(state, e);
                break;

            case EventKind.IntrigueBetrayal:
                ApplyDefection(state, e);
                break;

            case EventKind.PolitySecession:
                ApplySecession(state, e);
                break;

            case EventKind.PolityPartition:
                ApplyPartition(state, e);
                break;

            case EventKind.ConflictConquest:
                ApplyConquest(state, e);
                break;

            case EventKind.PolityCollapse:
                state.FactionOf(e.Faction).Leader = EntityId.None;
                break;

            case EventKind.DiploWarDeclared:
                ApplyWarDeclared(state, e);
                break;

            case EventKind.DiploPeaceSigned:
                ApplyPeace(state, e);
                break;

            case EventKind.PolityCoupPlotted:
            case EventKind.PolitySuccessionDisputed:
                ApplyArcOpened(state, e);
                break;

            case EventKind.PolityCoupResolved:
            case EventKind.PolityPlotDiesWithPlotter:
            case EventKind.PolityPlotLapses:
                CloseArc(state, e);
                break;

            case EventKind.EconomyFamine:
            case EventKind.EconomyPlague:
                ApplyArcOpened(state, e);
                break;

            default:
                // Everything else changes only scalars and edges, handled uniformly below.
                break;
        }

        ApplyDeltas(state, e);
    }

    // ---- structural cases -------------------------------------------------

    private static void ApplyGenesisPlace(WorldState state, Event e)
    {
        EntityId id = state.NextPlaceId;
        if (id != e.Subject)
            throw new InvalidOperationException($"Place genesis out of order: event says {e.Subject}, state expects {id}.");

        Place place = new()
        {
            Id = id,
            Name = e.GetString("name") ?? id.ToString(),
            Kind = Enum.Parse<PlaceKind>(e.GetString("placeKind") ?? nameof(PlaceKind.Settlement)),
            Parent = e.GetEntity("parent"),
            Population = e.GetInt("population"),
            Yield =
            [
                e.GetInt("yieldGrain"),
                e.GetInt("yieldOre"),
                e.GetInt("yieldSilver"),
            ],
            Controller = e.GetEntity("controller"),
        };
        place.Stockpile[(int)Resource.Grain] = e.GetInt("stockGrain");
        place.Stockpile[(int)Resource.Ore] = e.GetInt("stockOre");
        place.Stockpile[(int)Resource.Silver] = e.GetInt("stockSilver");

        state.AddPlace(place);
    }

    private static void ApplyGenesisFaction(WorldState state, Event e)
    {
        EntityId id = state.NextFactionId;
        if (id != e.Subject)
            throw new InvalidOperationException($"Faction genesis out of order: event says {e.Subject}, state expects {id}.");

        state.AddFaction(new Faction
        {
            Id = id,
            Name = e.GetString("name") ?? id.ToString(),
            Succession = Enum.Parse<SuccessionRule>(e.GetString("succession") ?? nameof(SuccessionRule.Primogeniture)),
            Seat = e.GetEntity("seat"),
            Legitimacy = e.GetInt("legitimacy", 60),
            Treasury = e.GetInt("treasury"),
        });
    }

    private static void ApplyActorAppears(WorldState state, Event e)
    {
        EntityId id = state.NextActorId;
        if (id != e.Subject)
            throw new InvalidOperationException($"Actor genesis out of order: event says {e.Subject}, state expects {id}.");

        state.AddActor(new Actor
        {
            Id = id,
            Name = e.GetString("name") ?? id.ToString(),
            BirthYear = e.GetInt("birthYear", e.Year),
            Traits = new Traits(
                e.GetInt("ambition"),
                e.GetInt("guile"),
                e.GetInt("martial"),
                e.GetInt("loyalty")),
            Place = e.GetEntity("place"),
            Faction = e.GetEntity("faction"),
            Title = ParseTitle(e.GetString("title"), Title.Commoner),
        });
    }

    private static void ApplyDeath(WorldState state, Event e)
    {
        Actor actor = state.ActorOf(e.Subject);
        actor.DeathYear = e.Year;

        // The title is deliberately left alone. The dead keep the rank they died holding,
        // which is both truer and what lets the log say "Ruler of the Ironmark, dies at 63"
        // when it renders this event during replay.
        state.Goals.RemoveAllFor(actor.Id);

        if (!actor.Faction.IsNone)
        {
            Faction f = state.FactionOf(actor.Faction);
            if (f.Leader == actor.Id) f.Leader = EntityId.None;
        }
    }

    private static void ApplySuccession(WorldState state, Event e)
    {
        Faction f = state.FactionOf(e.Faction);
        Actor heir = state.ActorOf(e.Subject);

        f.Leader = heir.Id;
        heir.Faction = f.Id;
        heir.Title = Title.Ruler;
    }

    /// <summary>
    /// Expulsion, or an outlawing in absentia.
    ///
    /// A house can pronounce against someone who has already left — a conspirator uncovered
    /// three years after he fled is still condemned — but pronouncing is not the same as
    /// throwing out, and the old code treated them identically. That tore a man out of the
    /// house he had joined the same year, because a different house had sentenced him: he was
    /// left stateless by a body with no hold over him, and the log then had him "returning
    /// from exile" he was never in. The flag is set at emit time, where membership is known.
    /// </summary>
    private static void ApplyExile(WorldState state, Event e)
    {
        Actor actor = state.ActorOf(e.Subject);
        EntityId from = e.Faction;

        if (e.GetInt("outlaw") == 1) return;

        actor.Title = Title.Exile;
        actor.Faction = EntityId.None;
        state.Goals.RemoveAllFor(actor.Id);

        if (!from.IsNone && state.FactionOf(from).Leader == actor.Id)
            state.FactionOf(from).Leader = EntityId.None;
    }

    private static void ApplyExileReturn(WorldState state, Event e)
    {
        Actor actor = state.ActorOf(e.Subject);
        actor.Faction = e.Faction;
        actor.Title = ParseTitle(e.GetString("title"), Title.Retainer);
        if (!e.Where.IsNone) actor.Place = e.Where;
    }

    private static void ApplyDefection(WorldState state, Event e)
    {
        Actor actor = state.ActorOf(e.Subject);
        EntityId from = actor.Faction;

        actor.Faction = e.GetEntity("toFaction");
        actor.Title = Title.Retainer;
        state.Goals.RemoveAllFor(actor.Id);

        if (!from.IsNone && state.FactionOf(from).Leader == actor.Id)
            state.FactionOf(from).Leader = EntityId.None;
    }

    private static void ApplySecession(WorldState state, Event e)
    {
        // A place breaks away, and — unlike a conquest — a new polity comes into being.
        // New factions appearing mid-history is a deliberate readability lever: the map
        // gains names over time instead of only losing them.
        EntityId newFaction = state.NextFactionId;
        state.AddFaction(new Faction
        {
            Id = newFaction,
            Name = e.GetString("name") ?? newFaction.ToString(),
            Succession = Enum.Parse<SuccessionRule>(e.GetString("succession") ?? nameof(SuccessionRule.Election)),
            Seat = e.Where,
            Legitimacy = e.GetInt("legitimacy", 45),
            Treasury = e.GetInt("treasury"),
        });

        Place place = state.PlaceOf(e.Where);
        place.Controller = newFaction;

        if (!e.Subject.IsNone)
        {
            Actor leader = state.ActorOf(e.Subject);
            leader.Faction = newFaction;
            leader.Title = Title.Ruler;
            state.FactionOf(newFaction).Leader = leader.Id;
            state.Goals.RemoveAllFor(leader.Id);
        }

        // No arc: a secession is a moment, not a storyline. Opening one here left an arc that
        // nothing ever closed, and they piled up until the world had forty running at once.
    }

    /// <summary>
    /// A realm splits between two claimants. Unlike a secession this is not one town walking
    /// away — it is the empire itself dividing, which is how large realms have historically
    /// come apart and the only force in this engine strong enough to undo a conquest run.
    /// The places themselves move via the generic <c>ctrl:</c> deltas on the same event.
    /// </summary>
    private static void ApplyPartition(WorldState state, Event e)
    {
        EntityId newFaction = state.NextFactionId;

        state.AddFaction(new Faction
        {
            Id = newFaction,
            Name = e.GetString("name") ?? newFaction.ToString(),
            Succession = Enum.Parse<SuccessionRule>(e.GetString("succession") ?? nameof(SuccessionRule.Primogeniture)),
            Seat = e.Where,
            Legitimacy = e.GetInt("legitimacy", 50),
            Treasury = e.GetInt("treasury"),
        });

        Actor claimant = state.ActorOf(e.Subject);
        claimant.Faction = newFaction;
        claimant.Title = Title.Ruler;
        claimant.Place = e.Where;
        state.FactionOf(newFaction).Leader = claimant.Id;
        state.Goals.RemoveAllFor(claimant.Id);
    }

    private static void ApplyConquest(WorldState state, Event e)
    {
        Place place = state.PlaceOf(e.Where);
        place.Controller = e.Faction;
    }

    private static void ApplyWarDeclared(WorldState state, Event e)
    {
        EntityId aggressor = e.Faction;
        EntityId defender = e.GetEntity("against");

        Arc arc = OpenArc(state, e, ArcKind.War);
        arc.Sides.Add(aggressor);
        arc.Sides.Add(defender);

        state.Relations.Adjust(aggressor, defender, RelationKind.AtWar, 1, e.Year, e.Id);
        state.Relations.Adjust(defender, aggressor, RelationKind.AtWar, 1, e.Year, e.Id);
    }

    private static void ApplyPeace(WorldState state, Event e)
    {
        EntityId a = e.Faction;
        EntityId b = e.GetEntity("with");

        state.Relations.Remove(a, b, RelationKind.AtWar);
        state.Relations.Remove(b, a, RelationKind.AtWar);

        CloseArc(state, e);
    }

    private static void ApplyArcOpened(WorldState state, Event e)
    {
        ArcKind kind = e.Kind switch
        {
            EventKind.PolityCoupPlotted => ArcKind.Plot,
            EventKind.PolitySuccessionDisputed => ArcKind.Succession,
            EventKind.EconomyFamine => ArcKind.Famine,
            EventKind.EconomyPlague => ArcKind.Plague,
            _ => ArcKind.Feud,
        };

        Arc arc = OpenArc(state, e, kind);
        if (arc.Sides.Count > 0) return;

        // An arc's "sides" are whatever it is fundamentally about: a famine is about a place,
        // a plot or a disputed succession is about a faction. Recorded so later events can
        // find the arc they belong to instead of starting a new one every year.
        EntityId subject = kind is ArcKind.Famine or ArcKind.Plague ? e.Where : e.Faction;
        if (!subject.IsNone) arc.Sides.Add(subject);
    }

    private static Arc OpenArc(WorldState state, Event e, ArcKind kind)
    {
        // The rules layer pre-allocates the arc id when it builds the event, so the event's
        // Arc field and the state's next id must agree; if the arc already exists this is a
        // later event joining an open arc, not a new one.
        if (!e.Arc.IsNone && e.Arc.Index <= state.Arcs.Count) return state.ArcOf(e.Arc);

        EntityId id = state.NextArcId;

        // Two wars fought over the same ground get the same generated name, and a chronicle
        // with two chapters both called "the War for Threi Cut" is unreadable. The later one
        // takes its year, as a chronicler would distinguish them.
        string name = e.GetString("arcName") ?? $"{kind} of {e.Year}";
        foreach (Arc existing in state.Arcs)
        {
            if (!string.Equals(existing.Name, name, StringComparison.Ordinal)) continue;
            name = $"{name} of {e.Year.ToString(CultureInfo.InvariantCulture)}";
            break;
        }

        Arc arc = new()
        {
            Id = id,
            Kind = kind,
            Name = name,
            StartYear = e.Year,
            Origin = e.Id,
        };
        state.AddArc(arc);
        return arc;
    }

    private static void CloseArc(WorldState state, Event e)
    {
        if (e.Arc.IsNone || e.Arc.Index > state.Arcs.Count) return;
        Arc arc = state.ArcOf(e.Arc);
        arc.EndYear ??= e.Year;
    }

    // ---- declarative deltas ----------------------------------------------

    /// <summary>
    /// Applies the generic payload keys every event may carry:
    /// <c>pop:&lt;place&gt;</c>, <c>stock:&lt;place&gt;:&lt;resource&gt;</c>,
    /// <c>leg:&lt;faction&gt;</c>, <c>treas:&lt;faction&gt;</c>,
    /// <c>rel:&lt;from&gt;:&lt;to&gt;:&lt;kind&gt;</c> and <c>relDel:&lt;from&gt;:&lt;to&gt;:&lt;kind&gt;</c>.
    /// </summary>
    private static void ApplyDeltas(WorldState state, Event e)
    {
        foreach (KeyValuePair<string, string> kv in e.Data)
        {
            string[] parts = kv.Key.Split(':');
            switch (parts[0])
            {
                case "pop" when parts.Length == 3:
                {
                    Place p = state.PlaceOf(Entity(parts, 1));
                    p.Population = Math.Max(0, p.Population + Int(kv.Value));
                    break;
                }

                case "stock" when parts.Length == 4:
                {
                    Place p = state.PlaceOf(Entity(parts, 1));
                    int r = (int)Enum.Parse<Resource>(parts[3], ignoreCase: true);
                    p.Stockpile[r] = Math.Max(0, p.Stockpile[r] + Int(kv.Value));
                    break;
                }

                case "leg" when parts.Length == 3:
                {
                    Faction f = state.FactionOf(Entity(parts, 1));
                    f.Legitimacy = Math.Clamp(f.Legitimacy + Int(kv.Value), 0, 100);
                    break;
                }

                case "treas" when parts.Length == 3:
                {
                    Faction f = state.FactionOf(Entity(parts, 1));
                    f.Treasury = Math.Max(0, f.Treasury + Int(kv.Value));
                    break;
                }

                case "rel" when parts.Length == 6:
                {
                    EntityId from = Entity(parts, 1);
                    EntityId to = Entity(parts, 3);
                    RelationKind kind = Enum.Parse<RelationKind>(parts[5], ignoreCase: true);
                    state.Relations.Adjust(from, to, kind, Int(kv.Value), e.Year, e.Id);
                    break;
                }

                case "ctrl" when parts.Length == 3:
                {
                    Place p = state.PlaceOf(Entity(parts, 1));
                    p.Controller = EntityId.TryParse(kv.Value, out EntityId owner) ? owner : EntityId.None;
                    break;
                }

                case "arcEnd" when parts.Length == 3:
                {
                    EntityId id = Entity(parts, 1);
                    if (id.Index <= state.Arcs.Count) state.ArcOf(id).EndYear ??= e.Year;
                    break;
                }

                case "join" when parts.Length == 3:
                {
                    Actor a = state.ActorOf(Entity(parts, 1));
                    a.Faction = EntityId.TryParse(kv.Value, out EntityId f) ? f : EntityId.None;
                    break;
                }

                case "disown" when parts.Length == 3:
                {
                    Actor a = state.ActorOf(Entity(parts, 1));
                    a.Faction = EntityId.None;
                    if (a.Title != Title.Exile) a.Title = Title.Commoner;
                    state.Goals.RemoveAllFor(a.Id);
                    break;
                }

                case "relDel" when parts.Length == 6:
                {
                    EntityId from = Entity(parts, 1);
                    EntityId to = Entity(parts, 3);
                    RelationKind kind = Enum.Parse<RelationKind>(parts[5], ignoreCase: true);
                    state.Relations.Remove(from, to, kind);
                    break;
                }
            }
        }
    }

    /// <summary>Entity ids are two colon-separated tokens (<c>p</c>, <c>14</c>) once the key is split.</summary>
    private static EntityId Entity(string[] parts, int at)
    {
        if (!EntityId.TryParse($"{parts[at]}:{parts[at + 1]}", out EntityId id))
            throw new FormatException($"Bad entity reference '{parts[at]}:{parts[at + 1]}' in event payload.");
        return id;
    }

    private static int Int(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static Title ParseTitle(string? value, Title fallback) =>
        value is not null && Enum.TryParse(value, ignoreCase: true, out Title t) ? t : fallback;
}
