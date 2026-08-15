namespace WorldBuilder.Core.Rules;

/// <summary>
/// Phase 3 — factions and actors notice their situation and form goals.
///
/// This phase emits no events at all, which is the point. A goal carries the id of the event
/// that produced it, so every action taken later cites that event as its cause: the causal
/// chain survives without paying for it in log noise. Goals are also what stop the history
/// reading as event soup — an actor who wants something for twelve years produces an arc,
/// whereas an actor who rolls dice every year produces a list.
/// </summary>
public static class PerceptionPhase
{
    public static void Run(Tick tick)
    {
        foreach (Faction faction in tick.State.Factions) FactionGoals(tick, faction);
        foreach (Actor actor in tick.State.LivingActors()) ActorGoals(tick, actor);
    }

    private static void FactionGoals(Tick tick, Faction faction)
    {
        WorldState state = tick.State;
        SimConfig config = tick.Config;
        GoalBook goals = state.Goals;

        List<Place> holdings = state.HoldingsOf(faction.Id);
        if (holdings.Count == 0) return;

        // Hunger first: a faction that cannot feed itself does not care about the ore.
        int grain = state.StockOf(faction.Id, Resource.Grain);
        int eaten = state.PopulationOf(faction.Id) / config.ConsumptionDivisor;
        if (grain < eaten && !goals.Has(faction.Id, GoalKind.SecureGrain))
        {
            // The famine if there has been one, otherwise the harvest that left them short.
            EventId cause = LatestCauseFor(tick, faction.Id, EventKind.EconomyFamine);
            if (cause.IsNone) cause = tick.YieldEvent;
            goals.Add(faction.Id, GoalKind.SecureGrain, EntityId.None, tick.Year, 6, cause);
        }

        if (faction.Legitimacy < config.LegitimacyCrisisThreshold && !goals.Has(faction.Id, GoalKind.RestoreLegitimacy))
        {
            // Whatever most recently cost them standing — that is what they are answering.
            EventId cause = LastLegitimacyBlow(tick, faction.Id);
            if (cause.IsNone) cause = LatestCauseFor(tick, faction.Id, EventKind.PolityLegitimacyCrisis);
            goals.Add(faction.Id, GoalKind.RestoreLegitimacy, EntityId.None, tick.Year, 8, cause);
        }

        // Revenge, sourced. The grievance edge remembers which event created it, so a war
        // declared in year 38 can point at the assassination in year 12 that caused it.
        Relation? worst = null;
        foreach (Relation r in state.Relations.From(faction.Id, RelationKind.Grievance))
        {
            if (r.Key.To.Kind != EntityKind.Faction) continue;
            if (r.Value < config.GrievanceGoalThreshold) continue;
            if (state.IsDefunct(r.Key.To)) continue; // no revenge on a house that is already gone
            if (worst is null || r.Value > worst.Value) worst = r;
        }

        if (worst is not null && !goals.Has(faction.Id, GoalKind.Avenge))
            goals.Add(faction.Id, GoalKind.Avenge, worst.Key.To, tick.Year, config.GoalLifespan, worst.LastCause);

        // The unclaimed mine. Every faction can see it, so somebody always moves.
        if (!goals.Has(faction.Id, GoalKind.ControlOre))
        {
            Place? prize = FindOrePrize(tick, faction);
            if (prize is not null)
            {
                // Cause is the mine's own genesis: the honest answer to "why does this faction
                // want that place" is "because that place is there and it has ore in it".
                goals.Add(faction.Id, GoalKind.ControlOre, prize.Id, tick.Year,
                    config.GoalLifespan, tick.Log.OriginOf(prize.Id));
            }
        }

        // Shared threat, not just being at war. A faction looks for friends when it is losing a
        // war, or when somebody else on the map has grown large enough to frighten everyone.
        // Requiring an existing war *and* a shared enemy was so narrow that across five
        // fifty-year runs it produced no alliances whatsoever.
        if (!goals.Has(faction.Id, GoalKind.FormAlliance))
        {
            bool losing = state.AtWar(faction.Id) && IsLosing(state, faction.Id);
            EntityId hegemon = Hegemon(state, faction.Id);
            bool overshadowed = !hegemon.IsNone;

            if (losing || overshadowed)
            {
                EntityId friend = FindAllyCandidate(tick, faction, hegemon);
                if (!friend.IsNone)
                {
                    EventId cause = losing
                        ? LatestCauseFor(tick, faction.Id, EventKind.DiploWarDeclared)
                        : LatestCauseFor(tick, hegemon, EventKind.ConflictConquest);

                    goals.Add(faction.Id, GoalKind.FormAlliance, friend, tick.Year, 6, cause);
                }
            }
        }
    }

    private static Place? FindOrePrize(Tick tick, Faction faction)
    {
        WorldState state = tick.State;
        Place? best = null;
        int bestScore = 0;

        foreach (Place place in state.Places)
        {
            if (place.Kind != PlaceKind.Site) continue;
            if (place.Controller == faction.Id) continue;

            int score = place.YieldOf(Resource.Ore);
            if (place.Controller.IsNone) score *= 3;                                  // unowned: just walk in
            else if (state.PowerOf(place.Controller) < state.PowerOf(faction.Id)) score *= 2;
            else score /= 2;

            if (score > bestScore) { bestScore = score; best = place; }
        }

        return bestScore >= 30 ? best : null;
    }

    /// <summary>Is this faction plainly behind in a war it is fighting?</summary>
    private static bool IsLosing(WorldState state, EntityId faction)
    {
        foreach (Relation war in state.Relations.From(faction, RelationKind.AtWar))
            if (state.PowerOf(faction) * 100 < state.PowerOf(war.Key.To) * 80) return true;
        return false;
    }

    /// <summary>
    /// A power large enough that everyone else has cause to worry, if it is not this one.
    /// Fear of the biggest is the engine of coalitions, and without it a strong faction is only
    /// ever opposed one at a time.
    /// </summary>
    private static EntityId Hegemon(WorldState state, EntityId self)
    {
        int total = 0;
        int best = 0;
        EntityId biggest = EntityId.None;

        foreach (Faction f in state.ActiveFactions())
        {
            int held = state.PopulationOf(f.Id);
            total += held;
            if (held > best) { best = held; biggest = f.Id; }
        }

        if (biggest == self || total == 0) return EntityId.None;
        return best * 100 / total >= 40 ? biggest : EntityId.None;
    }

    /// <summary>
    /// Somebody to stand with. Preference goes to a faction that shares the same fear, but any
    /// power not already hostile will do — the requirement for a formally shared enemy was what
    /// made coalitions impossible.
    /// </summary>
    private static EntityId FindAllyCandidate(Tick tick, Faction faction, EntityId threat)
    {
        WorldState state = tick.State;
        EntityId fallback = EntityId.None;

        foreach (Faction other in state.ActiveFactions())
        {
            if (other.Id == faction.Id || other.Id == threat) continue;
            if (state.Relations.Has(faction.Id, other.Id, RelationKind.AtWar)) continue;
            if (state.Relations.Has(faction.Id, other.Id, RelationKind.Alliance)) continue;
            if (state.Relations.ValueOf(faction.Id, other.Id, RelationKind.Grievance) > 45) continue;

            // Someone who fears the same power, or is already fighting my enemy, first.
            if (!threat.IsNone && state.Relations.ValueOf(other.Id, threat, RelationKind.Grievance) > 10)
                return other.Id;

            foreach (Relation war in state.Relations.From(faction.Id, RelationKind.AtWar))
                if (state.Relations.Has(other.Id, war.Key.To, RelationKind.Grievance)) return other.Id;

            if (fallback.IsNone) fallback = other.Id;
        }

        return fallback;
    }

    private static void ActorGoals(Tick tick, Actor actor)
    {
        WorldState state = tick.State;
        GoalBook goals = state.Goals;

        if (actor.AgeAt(tick.Year) < tick.Config.AdultAge) return;

        if (actor.Title == Title.Exile)
        {
            if (!goals.Has(actor.Id, GoalKind.ReturnFromExile))
            {
                EventId cause = LatestCauseFor(tick, actor.Id, EventKind.PolityExile);
                goals.Add(actor.Id, GoalKind.ReturnFromExile, EntityId.None, tick.Year, 25, cause);
            }
            return;
        }

        if (actor.Faction.IsNone) return;

        Faction faction = state.FactionOf(actor.Faction);
        if (faction.Leader == actor.Id) return;

        // A man newly back from exile is a live threat, and his return is why. Citing it makes
        // the homecoming a cause of whatever he does next instead of a closing flourish.
        EventId homecoming = LatestCauseFor(tick, actor.Id, EventKind.PolityExileReturn);

        // Ambition needs an opening as well as an appetite: a secure ruler is not worth
        // plotting against, a weak one is. Low loyalty and low legitimacy both widen the door.
        if (!goals.Has(actor.Id, GoalKind.SeizeLeadership))
        {
            int spark = tick.Config.AmbitionSparkBp
                * actor.Traits.Ambition / 100
                * (140 - actor.Traits.Loyalty) / 100
                * (130 - faction.Legitimacy) / 100;

            if (actor.Title is Title.Heir or Title.Captain or Title.Steward) spark = spark * 3 / 2;

            Rng rng = tick.Rng(actor.Id, RngPurpose.GoalFormation);
            if (rng.ChanceBp(Math.Min(6000, spark)))
            {
                // What makes a seat worth taking is the weakness of whoever holds it, or
                // failing that the accession that put them there. Citing the plotter's own
                // birth — as this did — is not a cause, it is a biography. A recent return from
                // exile takes precedence: that is the specific thing that put him in the room.
                EventId cause = homecoming.IsNone ? EventId.None
                    : tick.Year - tick.Log.Get(homecoming).Year <= 6 ? homecoming : EventId.None;

                if (cause.IsNone) cause = LastLegitimacyBlow(tick, faction.Id);
                if (cause.IsNone) cause = LatestCauseFor(tick, faction.Id, EventKind.PolitySuccession);

                // A polity founded this year — by secession or partition — has neither yet.
                // Its founding is then the honest answer: there is a seat because that happened.
                if (cause.IsNone) cause = tick.Log.OriginOf(faction.Id);

                goals.Add(actor.Id, GoalKind.SeizeLeadership, faction.Id, tick.Year, tick.Config.GoalLifespan, cause);
            }
        }

        // Personal grudges, held against people rather than polities.
        Relation? worst = null;
        foreach (Relation r in state.Relations.From(actor.Id, RelationKind.Grievance))
        {
            if (r.Key.To.Kind != EntityKind.Actor) continue;
            if (r.Value < tick.Config.GrievanceGoalThreshold) continue;
            if (!state.ActorOf(r.Key.To).IsAlive) continue;
            if (worst is null || r.Value > worst.Value) worst = r;
        }

        if (worst is not null && !goals.Has(actor.Id, GoalKind.Avenge))
            goals.Add(actor.Id, GoalKind.Avenge, worst.Key.To, tick.Year, tick.Config.GoalLifespan, worst.LastCause);
    }

    /// <summary>
    /// Most recent event of a kind touching this entity, or nothing.
    ///
    /// This used to fall back to "the last event that mentioned this faction", which is where
    /// most of the log's false causality came from: births and marriages carry the faction as a
    /// participant, so a goal formed in a quiet year would cite somebody's wedding, and then
    /// every raid, war and coup pursuing that goal inherited the wedding as its ancestor. It
    /// manufactured exactly the long lifecycle-shaped chains that made the depth look real.
    /// An empty cause is honest; an invented one corrupts every traversal that touches it.
    /// </summary>
    private static EventId LatestCauseFor(Tick tick, EntityId entity, EventKind kind)
    {
        IReadOnlyList<EventId> history = tick.Log.ForEntity(entity);
        for (int i = history.Count - 1; i >= 0; i--)
            if (tick.Log.Get(history[i]).Kind == kind) return history[i];

        return EventId.None;
    }

    /// <summary>
    /// The most recent event that actually cost this faction legitimacy. When a polity acts out
    /// of weakness rather than in answer to one incident, this is the honest parent: it is the
    /// thing that materially produced the state the decision responds to.
    /// </summary>
    internal static EventId LastLegitimacyBlow(Tick tick, EntityId faction)
    {
        string key = $"leg:{faction}";
        IReadOnlyList<EventId> history = tick.Log.ForEntity(faction);

        for (int i = history.Count - 1; i >= 0; i--)
        {
            Event e = tick.Log.Get(history[i]);
            if (e.GetLong(key) < 0) return history[i];
        }
        return EventId.None;
    }
}
