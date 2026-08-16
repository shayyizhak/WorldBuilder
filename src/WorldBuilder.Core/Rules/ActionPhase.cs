namespace WorldBuilder.Core.Rules;

/// <summary>
/// Phase 4 — everyone with a goal takes at most one action towards it.
///
/// Actions are chosen by weighted roll over the actor's traits and the situation, never
/// uniformly: a guileful coward plots, a martial one challenges in the open, and the same
/// goal therefore produces different history depending on who holds it. Every event emitted
/// here cites the goal's originating event, so the log's causal edges reach all the way back
/// to whatever grievance or famine started the whole business.
/// </summary>
public static class ActionPhase
{
    public static void Run(Tick tick)
    {
        foreach (Goal goal in tick.State.Goals.Snapshot())
        {
            if (goal.Owner.Kind == EntityKind.Faction) ActOnBehalfOfFaction(tick, goal);
            else if (goal.Owner.Kind == EntityKind.Actor) ActAsPerson(tick, goal);
        }
    }

    // ---- factions ---------------------------------------------------------

    private static void ActOnBehalfOfFaction(Tick tick, Goal goal)
    {
        WorldState state = tick.State;
        Faction faction = state.FactionOf(goal.Owner);
        if (faction.Leader.IsNone) return;

        Actor leader = state.ActorOf(faction.Leader);
        if (!leader.IsAlive) return;

        switch (goal.Kind)
        {
            case GoalKind.SecureGrain: SecureGrain(tick, goal, faction, leader); break;
            case GoalKind.RestoreLegitimacy: RestoreLegitimacy(tick, goal, faction, leader); break;
            case GoalKind.Avenge: FactionRevenge(tick, goal, faction, leader); break;
            case GoalKind.ControlOre:
            case GoalKind.TakePlace: PursuePlace(tick, goal, faction, leader); break;
            case GoalKind.FormAlliance: ProposeAlliance(tick, goal, faction, leader); break;
            default: break;
        }
    }

    private static void SecureGrain(Tick tick, Goal goal, Faction faction, Actor leader)
    {
        WorldState state = tick.State;
        Faction? surplus = RichestInGrain(state, faction.Id);
        if (surplus is null) return;

        bool hostile = state.Relations.ValueOf(faction.Id, surplus.Id, RelationKind.Grievance) > 20
                       || state.Relations.Has(faction.Id, surplus.Id, RelationKind.AtWar);

        Span<int> weights =
        [
            hostile ? 5 : 40 + faction.Treasury / 10,          // buy it
            hostile ? 45 : 12 + leader.Traits.Martial / 3,     // take it
            hostile ? 8 : 30 + leader.Traits.Guile / 4,        // pact
        ];

        Rng rng = tick.Rng(faction.Id, RngPurpose.ActionChoice);
        switch (rng.PickIndexWeighted(weights))
        {
            case 0: BuyGrain(tick, goal, faction, surplus); break;
            case 1: Raid(tick, goal, faction, surplus, leader, Resource.Grain); break;
            case 2: TradePact(tick, goal, faction, surplus); break;
            default: break;
        }
    }

    private static void BuyGrain(Tick tick, Goal goal, Faction buyer, Faction seller)
    {
        if (Recent.Did(tick, buyer.Id, EventKind.EconomyTradePact, seller.Id, 5)) return;

        WorldState state = tick.State;
        int silver = Math.Min(buyer.Treasury, 60);
        if (silver < 15) return;

        List<Place> theirs = state.HoldingsOf(seller.Id);
        List<Place> ours = state.HoldingsOf(buyer.Id);
        if (theirs.Count == 0 || ours.Count == 0) return;

        Place from = Neediest(theirs, Resource.Grain, most: true);
        Place to = Neediest(ours, Resource.Grain, most: false);
        int grain = Math.Min(from.Stockpile[(int)Resource.Grain] / 2, silver / 2);
        if (grain <= 0) return;

        tick.Emit(new EventDraft(EventKind.EconomyTradePact)
            .By(buyer.Id)
            .Object(seller.Id)
            .At(to.Id)
            .Set("grain", grain)
            .Set("silver", silver)
            .Set("mode", "purchase")
            .Treas(buyer.Id, -silver)
            .Treas(seller.Id, silver)
            .Stock(from.Id, Resource.Grain, -grain)
            .Stock(to.Id, Resource.Grain, grain)
            .RelBoth(buyer.Id, seller.Id, RelationKind.Trade, 8)
            .Because(goal.Cause)
            // Somebody had a surplus to sell, and this is where it came from; and if these two
            // already had terms, that is why the grain could be bought rather than taken.
            .Because(Recent.LastOfKind(tick, from.Id, EventKind.EconomyBumperHarvest))
            .Because(state.Relations.Find(buyer.Id, seller.Id, RelationKind.Trade)?.LastCause ?? EventId.None)
            .Weight(Significance.Minor));

        goal.Progress += 60;
    }

    private static void TradePact(Tick tick, Goal goal, Faction a, Faction b)
    {
        if (tick.State.Relations.ValueOf(a.Id, b.Id, RelationKind.Trade) > 40) return;

        tick.Emit(new EventDraft(EventKind.EconomyTradePact)
            .By(a.Id)
            .Object(b.Id)
            .At(tick.State.FactionOf(b.Id).Seat)
            .Set("mode", "pact")
            .RelBoth(a.Id, b.Id, RelationKind.Trade, 25)
            .Leg(a.Id, 2)
            .Because(goal.Cause)
            .Weight(Significance.Minor));

        goal.Progress += 40;
    }

    private static void RestoreLegitimacy(Tick tick, Goal goal, Faction faction, Actor leader)
    {
        WorldState state = tick.State;

        Span<int> weights =
        [
            // A hard gate, not a discouragement: as a mere weight this still fired every other
            // year whenever no favourite was available to elevate instead.
            faction.Treasury > 40 && !Recent.Happened(tick, faction.Id, EventKind.PolityLegitimacyCrisis, 8)
                ? 50 : 0,                               // largesse
            30 + leader.Traits.Guile / 3,               // elevate a favourite
        ];

        Rng rng = tick.Rng(faction.Id, RngPurpose.ActionChoice).Branch(1);
        int choice = rng.PickIndexWeighted(weights);

        if (choice == 0)
        {
            // Buying goodwill gets dearer and less convincing every time a house does it.
            // A flat fifty silver that always worked was a no-op dressed as an event: the
            // same line, the same number, the same result, as often as the treasury allowed.
            int priorAttempts = Recent.CountEver(tick, faction.Id, EventKind.PolityLegitimacyCrisis, "response", "largesse");
            int asking = 50 + priorAttempts * 45;
            int spend = Math.Min(faction.Treasury, asking);

            Rng roll = tick.Rng(faction.Id, RngPurpose.Diplomacy).Branch(21);
            bool convinced = spend >= asking && roll.Chance(Math.Max(15, 85 - priorAttempts * 22));

            tick.Emit(new EventDraft(EventKind.PolityLegitimacyCrisis)
                .Subject(leader.Id)
                .By(faction.Id)
                .At(faction.Seat)
                .Set("response", "largesse")
                .Set("spent", spend)
                .Set("asking", asking)
                .Set("attempt", priorAttempts + 1)
                .Resolved(convinced ? Outcome.Succeeded : Outcome.Failed)
                .Treas(faction.Id, -spend)
                .Leg(faction.Id, convinced ? spend / 5 : -3)
                .Because(goal.Cause)
                .Weight(Significance.Minor));

            goal.Progress += convinced ? 45 : 10;
            return;
        }

        List<Actor> members = state.MembersOf(faction.Id);
        Actor? favourite = null;
        foreach (Actor a in members)
        {
            if (a.Id == leader.Id || a.Title is Title.Ruler or Title.Commoner) continue;
            if (Recent.Did(tick, a.Id, EventKind.PolityAppointment, EntityId.None, 12)) continue;
            if (favourite is null || a.Traits.Loyalty > favourite.Traits.Loyalty) favourite = a;
        }
        if (favourite is null) return;

        EventDraft appointment = new EventDraft(EventKind.PolityAppointment)
            .Subject(favourite.Id)
            .Object(leader.Id)
            .By(faction.Id)
            .At(faction.Seat)
            .Set("title", Title.Steward)
            .Leg(faction.Id, 4)
            .Rel(favourite.Id, leader.Id, RelationKind.Fealty, 20)
            .Because(goal.Cause)
            .Weight(Significance.Minor);

        // Elevating one person slights the others. The passed-over take it personally, and the
        // grievance edge carries this event as its cause — so when one of them moves against
        // the favourite years later, the log can say what it was about. An appointment that
        // changed nobody's mind about anything was simply an announcement.
        foreach (Actor other in members)
        {
            if (other.Id == favourite.Id || other.Id == leader.Id) continue;
            if (other.Title is Title.Commoner or Title.Exile) continue;
            if (other.Traits.Ambition < 45) continue;
            appointment.Rel(other.Id, favourite.Id, RelationKind.Grievance, 8 + other.Traits.Ambition / 10);
        }

        tick.Emit(appointment);
        goal.Progress += 35;
    }

    private static void FactionRevenge(Tick tick, Goal goal, Faction faction, Actor leader)
    {
        WorldState state = tick.State;
        if (goal.Target.Kind != EntityKind.Faction) return;

        // The enemy may have died since the grudge was formed. You cannot be avenged on a
        // house that no longer holds anything, so the goal lapses.
        if (state.IsDefunct(goal.Target)) { state.Goals.Remove(goal); return; }

        Faction target = state.FactionOf(goal.Target);
        int grievance = state.Relations.ValueOf(faction.Id, target.Id, RelationKind.Grievance);
        bool atWar = state.Relations.Has(faction.Id, target.Id, RelationKind.AtWar);

        // Opportunity, not just appetite: nobody declares war on someone stronger and
        // undistracted, so wars cluster around weakness the way they do in real histories.
        int ours = state.PowerOf(faction.Id);
        int theirs = state.PowerOf(target.Id);
        int opportunity = Math.Clamp(ours * 100 / Math.Max(1, theirs), 20, 300);
        if (state.AtWar(target.Id)) opportunity = opportunity * 3 / 2;

        int warScore = grievance * opportunity / 100;
        bool canDeclare = !atWar && warScore >= tick.Config.WarDeclarationThreshold && !state.AtWar(faction.Id);

        // Nobody repeats the same gesture at the same rival year after year. Once an insult
        // or a demand has been made, the next move up the ladder is the interesting one.
        bool insultedLately = Recent.Did(tick, faction.Id, EventKind.DiploInsult, target.Id, 6);
        bool demandedLately = Recent.Did(tick, faction.Id, EventKind.DiploTributeDemanded, target.Id, 8);
        bool triedMurderLately = Recent.Did(tick, faction.Id, EventKind.ConflictAssassination, target.Id, 6);

        Span<int> weights =
        [
            atWar || insultedLately ? 0 : 25,                                // insult
            atWar ? 0 : 15 + leader.Traits.Martial / 3,                      // raid
            atWar || demandedLately ? 0 : 10 + leader.Traits.Guile / 3,      // demand tribute
            canDeclare ? 40 + grievance / 2 : 0,                             // declare war
            triedMurderLately ? 0 : 5 + leader.Traits.Guile / 5,             // send an assassin
        ];

        Rng rng = tick.Rng(faction.Id, RngPurpose.ActionChoice).Branch(2);
        switch (rng.PickIndexWeighted(weights))
        {
            case 0: Insult(tick, goal, faction, target); break;
            case 1: Raid(tick, goal, faction, target, leader, Resource.Ore); break;
            case 2: DemandTribute(tick, goal, faction, target, leader); break;
            case 3: DeclareWar(tick, goal, faction, target, EntityId.None); break;
            case 4: SendAssassin(tick, goal, faction, target, leader); break;
            default: break;
        }
    }

    private static void Insult(Tick tick, Goal goal, Faction from, Faction to)
    {
        // Guarded here rather than at the call sites: two different goals can reach this, and
        // the second path was bypassing the cooldown and re-emitting the same line every year.
        // The gap widens each time — a house that has slighted the same rival three times over
        // and never gone to war has made its point, and saying it again is not escalation.
        int priorSlights = Recent.CountEver(tick, from.Id, EventKind.DiploInsult, "at", to.Id.ToString());
        if (Recent.Did(tick, from.Id, EventKind.DiploInsult, to.Id, 6 + priorSlights * 9)) return;

        // An insult does not settle the insulter's own grudge — an earlier version credited
        // the aggressor for venting, which quietly capped every grievance below the war
        // threshold and meant the engine produced almost no wars at all.
        tick.Emit(new EventDraft(EventKind.DiploInsult)
            .By(from.Id)
            .Object(to.Id)
            .At(to.Seat)
            .Set("at", to.Id)
            .Rel(to.Id, from.Id, RelationKind.Grievance, 10)
            .Leg(from.Id, 1)
            .Because(goal.Cause)
            .Because(Spark(tick, from.Id, to.Id))
            .Weight(Significance.Minor));

        goal.Progress += 10;
    }

    private static void DemandTribute(Tick tick, Goal goal, Faction from, Faction to, Actor leader)
    {
        int priorDemands = Recent.CountEver(tick, from.Id, EventKind.DiploTributeDemanded, "at", to.Id.ToString());
        if (Recent.Did(tick, from.Id, EventKind.DiploTributeDemanded, to.Id, 8 + priorDemands * 10)) return;

        WorldState state = tick.State;
        int demand = Math.Min(to.Treasury, 30 + leader.Traits.Guile / 2);

        Rng rng = tick.Rng(from.Id, RngPurpose.Diplomacy);
        bool complies = state.PowerOf(to.Id) * 100 < state.PowerOf(from.Id) * 75 && demand > 0 && rng.Chance(70);

        EventDraft draft = new EventDraft(EventKind.DiploTributeDemanded)
            .By(from.Id)
            .Object(to.Id)
            .At(to.Seat)
            .Set("at", to.Id)
            .Set("demand", demand)
            .Resolved(complies ? Outcome.Succeeded : Outcome.Failed)
            .Rel(to.Id, from.Id, RelationKind.Grievance, complies ? 14 : 6)
            .Because(goal.Cause)
            .Because(Spark(tick, from.Id, to.Id))
            .Weight(Significance.Minor);

        if (complies)
            draft.Treas(to.Id, -demand).Treas(from.Id, demand).Leg(to.Id, -5).Leg(from.Id, 3);
        else
            draft.Leg(from.Id, -2);

        tick.Emit(draft);
        goal.Progress += complies ? 35 : 12;
    }

    /// <summary>
    /// What this raider has already tried at this place, split by how it went.
    ///
    /// Read from the log rather than carried on state, so it survives replay and costs nothing
    /// when no raid is being considered.
    /// </summary>
    private static (int Failures, int Successes) RaidHistory(Tick tick, EntityId raider, EntityId place)
    {
        int failures = 0, successes = 0;

        foreach (EventId id in tick.Log.ForEntity(raider))
        {
            Event e = tick.Log.Get(id);
            if (e.Kind != EventKind.ConflictRaid || e.Faction != raider || e.Where != place) continue;

            if (e.Outcome == Outcome.Succeeded) successes++;
            else failures++;
        }

        return (failures, successes);
    }

    private static void Raid(Tick tick, Goal goal, Faction raider, Faction victim, Actor leader, Resource after)
    {
        WorldState state = tick.State;
        List<Place> targets = state.HoldingsOf(victim.Id);
        if (targets.Count == 0) return;

        // Target selection is deterministic, so without a cooldown the same warband hit the
        // same mine every other year. The cooldown also lengthens with each previous attempt:
        // a place that has beaten you off twice is a place you stop riding out to, which is
        // both sensible and what stops "the raid is beaten off" becoming the log's refrain.
        // The cooldown counts what happened, not merely that something did.
        //
        // It counted attempts, so a place that had repelled you three times and a place you had
        // looted three times were the same place to it — while the comment above says the
        // opposite, and says it as the reason the mechanic exists. Fifty of the panel's
        // fifty-eight repeat targets were returns to a house that had already beaten the raider
        // off. The intent was outcome-sensitive and the implementation was outcome-blind.
        //
        // Failures weigh roughly three times a success: being beaten off is a bloody nose and a
        // reason to ride somewhere else, while a raid that carried something off only means the
        // stockpile needs time to refill.
        List<Place> fresh = [];
        foreach (Place p in targets)
        {
            (int failures, int successes) = RaidHistory(tick, raider.Id, p.Id);
            int cooldown = 5 + failures * 10 + successes * 3;
            if (!Recent.Did(tick, raider.Id, EventKind.ConflictRaid, p.Id, cooldown)) fresh.Add(p);
        }

        if (fresh.Count == 0) return;

        Place place = Neediest(fresh, after, most: true);
        Rng rng = tick.Rng(raider.Id, RngPurpose.Raid);

        // A raid is one house taking from another, so both sides bring a house.
        //
        // This set one man's Martial trait against an entire faction's power, added a flat 25 to
        // the defender that nothing anywhere documents, and gave the attacker a wider die than
        // the defender. An institution was being defended against an individual, and the result
        // was a mechanic that failed four times in five and whose events were cited by nothing
        // two thirds of the time — a thing that happened constantly and changed nothing.
        //
        // The leader is now a modifier rather than the attacking force: half his Martial, so a
        // good commander helps and does not substitute for a house. The dice are symmetric
        // because neither side should get a wider spread for reasons nobody chose. The 25 is
        // gone; a constant nobody can justify is not a defensible constant.
        int strength = state.PowerOf(raider.Id) + leader.Traits.Martial / 2 + rng.Next(40);
        int defence = state.PowerOf(victim.Id) + rng.Next(40);
        bool success = strength > defence;

        int loot = success ? Math.Min(place.Stockpile[(int)after], 20 + rng.Next(40)) : 0;
        int deaths = success ? place.Population / 40 : place.Population / 200;

        List<Place> ours = state.HoldingsOf(raider.Id);
        EntityId home = ours.Count > 0 ? ours[0].Id : EntityId.None;

        EventDraft draft = new EventDraft(EventKind.ConflictRaid)
            .Subject(leader.Id)
            .By(raider.Id)
            .Object(victim.Id)
            .At(place.Id)
            .Set("target", place.Id)
            .Set("resource", after)
            .Set("loot", loot)
            .Set("deaths", deaths)
            .Resolved(success ? Outcome.Succeeded : Outcome.Failed)
            .Pop(place.Id, -deaths)
            .Rel(victim.Id, raider.Id, RelationKind.Grievance, success ? 22 : 12)
            .Because(goal.Cause)
            .Because(Spark(tick, raider.Id, victim.Id))
            .Weight(Significance.Major);

        if (loot > 0 && !home.IsNone)
            draft.Stock(place.Id, after, -loot).Stock(home, after, loot);

        if (!success) draft.Leg(raider.Id, -3);

        tick.Emit(draft);
        goal.Progress += success ? 30 : 8;
    }

    private static void SendAssassin(Tick tick, Goal goal, Faction from, Faction to, Actor leader)
    {
        if (to.Leader.IsNone) return;
        Actor victim = tick.State.ActorOf(to.Leader);
        if (!victim.IsAlive) return;

        Attempt(tick, goal, killer: leader, victim: victim, sponsor: from.Id, blamed: from.Id);
    }

    private static void PursuePlace(Tick tick, Goal goal, Faction faction, Actor leader)
    {
        WorldState state = tick.State;
        if (goal.Target.Kind != EntityKind.Place) return;

        Place place = state.PlaceOf(goal.Target);
        if (place.Controller == faction.Id) { state.Goals.Remove(goal); return; }

        // Unclaimed ground needs no war, only the nerve to walk onto it.
        if (place.Controller.IsNone)
        {
            tick.Emit(new EventDraft(EventKind.ConflictConquest)
                .Subject(leader.Id)
                .By(faction.Id)
                .At(place.Id)
                .Set("mode", "occupation")
                .Leg(faction.Id, 4)
                .Because(goal.Cause)
                // Land is only lying open because somebody let go of it. Naming that event
                // makes a collapse or a partition the reason the map was redrawn, rather than
                // an ending that nothing followed from.
                .Because(WhatFreedThePlace(tick, place.Id))
                .Weight(Significance.Major));

            state.Goals.Remove(goal);
            return;
        }

        Faction holder = state.FactionOf(place.Controller);
        if (state.Relations.Has(faction.Id, holder.Id, RelationKind.AtWar)) return; // resolution phase handles it

        int ours = state.PowerOf(faction.Id);
        int theirs = state.PowerOf(holder.Id);

        Span<int> weights =
        [
            ours * 100 / Math.Max(1, theirs) > 110 && !state.AtWar(faction.Id) ? 45 : 0, // war
            20 + leader.Traits.Guile / 3,                                                 // tribute
            18,                                                                           // insult
        ];

        Rng rng = tick.Rng(faction.Id, RngPurpose.ActionChoice).Branch(3);
        switch (rng.PickIndexWeighted(weights))
        {
            case 0: DeclareWar(tick, goal, faction, holder, place.Id); break;
            case 1: DemandTribute(tick, goal, faction, holder, leader); break;
            case 2: Insult(tick, goal, faction, holder); break;
            default: break;
        }
    }

    private static void DeclareWar(Tick tick, Goal goal, Faction aggressor, Faction defender, EntityId over)
    {
        WorldState state = tick.State;
        if (state.OpenWarBetween(aggressor.Id, defender.Id) is not null) return;
        if (state.IsDefunct(defender.Id) || state.IsDefunct(aggressor.Id)) return;

        // Peace has to mean something for a few years. Without this the same two factions
        // signed and re-declared on a two-year cycle, which reads as a stutter rather than
        // as a rivalry.
        if (Recent.Did(tick, aggressor.Id, EventKind.DiploPeaceSigned, defender.Id, 7)) return;
        if (Recent.Did(tick, aggressor.Id, EventKind.DiploWarDeclared, defender.Id, 7)) return;

        EntityId arc = tick.Chronicle.ReserveArc();
        Rng rng = tick.Rng(aggressor.Id, RngPurpose.Diplomacy).Branch(9);

        string overName = over.IsNone
            ? state.PlaceOf(state.FactionOf(defender.Id).Seat).Name
            : state.PlaceOf(over).Name;

        EventDraft draft = new EventDraft(EventKind.DiploWarDeclared)
            .Subject(aggressor.Leader)
            .By(aggressor.Id)
            .Object(defender.Id)
            .At(defender.Seat)
            .Set("against", defender.Id)
            .Set("arcName", ArcNames.War(ref rng, overName, aggressor.Name, aggressor.Name))
            .Set("grievance", state.Relations.ValueOf(aggressor.Id, defender.Id, RelationKind.Grievance))
            .Rel(defender.Id, aggressor.Id, RelationKind.Grievance, 15)
            .RelDel(aggressor.Id, defender.Id, RelationKind.Alliance)
            .RelDel(defender.Id, aggressor.Id, RelationKind.Alliance)
            .InArc(arc)
            .Because(goal.Cause)
            .Weight(Significance.Major);

        if (!over.IsNone) draft.Set("over", over);

        // Also cite whatever most recently deepened the grudge. The goal caches its cause when
        // it forms and can then sit for a decade, so every insult and refused demand in between
        // was left with no consequence — which is precisely why they repeated without escalating.
        Relation? grudge = state.Relations.Find(aggressor.Id, defender.Id, RelationKind.Grievance);
        if (grudge is not null) draft.Because(grudge.LastCause);

        tick.Emit(draft);
        goal.Arc = arc;
        goal.Progress += 25;
    }

    private static void ProposeAlliance(Tick tick, Goal goal, Faction faction, Actor leader)
    {
        WorldState state = tick.State;
        if (goal.Target.Kind != EntityKind.Faction) return;
        if (state.Relations.Has(faction.Id, goal.Target, RelationKind.Alliance)) { state.Goals.Remove(goal); return; }
        if (state.IsDefunct(goal.Target)) { state.Goals.Remove(goal); return; }
        if (Recent.Did(tick, faction.Id, EventKind.DiploAllianceFormed, goal.Target, 6)) return;

        Faction other = state.FactionOf(goal.Target);
        Rng rng = tick.Rng(faction.Id, RngPurpose.Diplomacy).Branch(4);

        int appeal = 30 + leader.Traits.Guile / 3
                     + state.Relations.ValueOf(faction.Id, other.Id, RelationKind.Trade) / 2
                     - state.Relations.ValueOf(other.Id, faction.Id, RelationKind.Grievance);

        // Fear carries a proposal further than charm does. Somebody who is themselves at war,
        // or who resents the same rising power, has a reason to say yes — without this, pacts
        // were offered at long odds and almost none were ever concluded.
        if (state.AtWar(other.Id)) appeal += 25;
        foreach (Relation war in state.Relations.From(faction.Id, RelationKind.AtWar))
            appeal += state.Relations.ValueOf(other.Id, war.Key.To, RelationKind.Grievance) / 2;

        bool accepted = rng.Next(100) < Math.Clamp(appeal, 5, 90);

        // Alliances grow out of things that already happened between these two: goods traded,
        // or a war they agreed to stop. Citing them turns pacts and peace treaties from
        // terminal announcements into the groundwork for what follows.
        Relation? commerce = state.Relations.Find(faction.Id, other.Id, RelationKind.Trade);

        tick.Emit(new EventDraft(EventKind.DiploAllianceFormed)
            .Subject(leader.Id)
            .By(faction.Id)
            .Object(other.Id)
            .At(other.Seat)
            .Resolved(accepted ? Outcome.Succeeded : Outcome.Failed)
            .RelBoth(faction.Id, other.Id, RelationKind.Alliance, accepted ? 30 : 0)
            .Because(goal.Cause)
            .Because(commerce?.LastCause ?? EventId.None)
            .Because(Recent.LastOfKind(tick, faction.Id, EventKind.DiploPeaceSigned))
            .Weight(accepted ? Significance.Major : Significance.Minor));

        if (accepted) state.Goals.Remove(goal);
        else goal.Progress += 20;
    }

    // ---- people -----------------------------------------------------------

    private static void ActAsPerson(Tick tick, Goal goal)
    {
        WorldState state = tick.State;
        Actor actor = state.ActorOf(goal.Owner);
        if (!actor.IsAlive) return;

        switch (goal.Kind)
        {
            case GoalKind.SeizeLeadership: SeizeLeadership(tick, goal, actor); break;
            case GoalKind.Avenge: PersonalRevenge(tick, goal, actor); break;
            case GoalKind.ReturnFromExile: ReturnFromExile(tick, goal, actor); break;
            default: break;
        }
    }

    private static void SeizeLeadership(Tick tick, Goal goal, Actor actor)
    {
        WorldState state = tick.State;
        if (actor.Faction.IsNone) { state.Goals.Remove(goal); return; }

        Faction faction = state.FactionOf(actor.Faction);
        if (faction.Leader == actor.Id) { state.Goals.Remove(goal); return; }
        if (faction.Leader.IsNone) return; // the succession machinery will settle it

        // One upheaval per seat per year. Without this a faction could take a coup, a counter-
        // challenge, an assassination and a disputed succession all in the same twelve months,
        // which reads as farce rather than as a crisis.
        if (Recent.Happened(tick, faction.Id, EventKind.PolitySuccession, 0)) return;

        Actor ruler = state.ActorOf(faction.Leader);
        Traits t = actor.Traits;

        // Nobody challenges bare-handed. A rising has to be prepared first, so courting the
        // household is the common opening move and the open challenge only becomes available
        // once there is a following behind it — fewer attempts, each one meaning something.
        int backing = Support(state, actor.Id);
        bool readyToRise = backing >= Support(state, ruler.Id) / 2;

        Span<int> weights =
        [
            22 + (100 - t.Guile) / 4,                                        // court support openly
            goal.Arc.IsNone ? 12 + t.Guile / 3 : 0,                          // form a plot
            goal.Arc.IsNone ? 0 : 16 + t.Guile / 3,                          // strike from the plot
            readyToRise && t.Martial > 45 ? 20 + t.Martial / 3 : 0,          // challenge in the open
        ];

        Rng rng = tick.Rng(actor.Id, RngPurpose.ActionChoice).Branch(5);
        switch (rng.PickIndexWeighted(weights))
        {
            case 0: CourtSupport(tick, goal, actor, faction, ruler); break;
            case 1: FormPlot(tick, goal, actor, faction, ruler); break;
            case 2: Attempt(tick, goal, actor, ruler, actor.Faction, actor.Id); break;
            case 3: ChallengeOpenly(tick, goal, actor, faction, ruler); break;
            default: break;
        }
    }

    private static void CourtSupport(Tick tick, Goal goal, Actor actor, Faction faction, Actor ruler)
    {
        WorldState state = tick.State;
        List<Actor> members = state.MembersOf(faction.Id);

        Actor? won = null;
        Rng rng = tick.Rng(actor.Id, RngPurpose.ActionTarget);
        int best = 0;

        foreach (Actor other in members)
        {
            if (other.Id == actor.Id || other.Id == ruler.Id) continue;
            if (Recent.Did(tick, actor.Id, EventKind.PolityCourtsSupport, other.Id, 10)) continue;

            int appeal = actor.Traits.Guile + (100 - other.Traits.Loyalty) + rng.Next(40)
                         - state.Relations.ValueOf(other.Id, ruler.Id, RelationKind.Fealty);

            // Reincorporation: people the reader has already seen this actor deal with are
            // preferred, so the cast stays small enough to become familiar.
            if (state.Relations.Has(actor.Id, other.Id, RelationKind.Fealty))
                appeal = appeal * tick.Config.ReincorporationBonusPct / 100;

            if (appeal > best) { best = appeal; won = other; }
        }

        if (won is null || best < 110) return;

        tick.Emit(new EventDraft(EventKind.PolityCourtsSupport)
            .Subject(actor.Id)
            .Object(won.Id)
            .By(faction.Id)
            .At(actor.Place)
            .Hidden(Visibility.FactionInternal)
            .Rel(won.Id, actor.Id, RelationKind.Fealty, 18)
            .Rel(won.Id, ruler.Id, RelationKind.Fealty, -12)
            .Leg(faction.Id, -2)
            .Because(goal.Cause)
            .Weight(Significance.Minor));

        goal.Progress += 20;
    }

    private static void FormPlot(Tick tick, Goal goal, Actor actor, Faction faction, Actor ruler)
    {
        EntityId arc = tick.Chronicle.ReserveArc();
        Rng rng = tick.Rng(actor.Id, RngPurpose.Coup);

        tick.Emit(new EventDraft(EventKind.PolityCoupPlotted)
            .Subject(actor.Id)
            .Object(ruler.Id)
            .By(faction.Id)
            .At(actor.Place)
            .Hidden(Visibility.Secret)
            .Set("arcName", ArcNames.Plot(ref rng, actor.Name, ruler.Name))
            .InArc(arc)
            .Because(goal.Cause)
            .Weight(Significance.Major));

        goal.Arc = arc;
        goal.Progress += 30;

        tick.Ledger?.Opened(arc, tick.Year);
    }

    private static void ChallengeOpenly(Tick tick, Goal goal, Actor actor, Faction faction, Actor ruler)
    {
        WorldState state = tick.State;
        Rng rng = tick.Rng(actor.Id, RngPurpose.Coup).Branch(2);

        // Support decides this, not the dice. Courting the household is the one action that
        // builds it, and it was previously divided into near-irrelevance — a challenger who
        // had spent a decade winning people over was barely better off than one who walked in
        // off the street, which is why nine coups in ten came to nothing.
        int backing = Support(state, actor.Id);
        int challenger = actor.Traits.Martial / 2 + backing + rng.Next(30);
        int incumbent = ruler.Traits.Martial / 2 + Support(state, ruler.Id) + faction.Legitimacy / 2 + rng.Next(30);
        bool won = challenger > incumbent;

        // Its own event type, and no arc. Sharing POLITY.COUP_RESOLVED with plot outcomes made
        // open bids look like resolved conspiracies — a plot appeared settled by an event that
        // never referred to it, so "what became of the plot against Y" had no answer at all.
        Event resolved = tick.Emit(new EventDraft(EventKind.PolityChallenge)
            .Subject(actor.Id)
            .Object(ruler.Id)
            .By(faction.Id)
            .At(faction.Seat)
            .Set("backing", backing)
            .Resolved(won ? Outcome.Succeeded : Outcome.Failed)
            .Leg(faction.Id, won ? -12 : -4)
            .Because(goal.Cause)
            .Weight(Significance.Major));

        SettleCoup(tick, faction, winner: won ? actor : ruler, loser: won ? ruler : actor, resolved);
        state.Goals.Remove(goal);
    }

    private static void PersonalRevenge(Tick tick, Goal goal, Actor actor)
    {
        WorldState state = tick.State;
        if (goal.Target.Kind != EntityKind.Actor) { state.Goals.Remove(goal); return; }

        Actor target = state.ActorOf(goal.Target);
        if (!target.IsAlive) { state.Goals.Remove(goal); return; }

        Span<int> weights =
        [
            5 + actor.Traits.Guile / 4,                                                     // kill them
            actor.Faction != target.Faction || actor.Faction.IsNone ? 0 : 15 + (100 - actor.Traits.Loyalty) / 3, // defect
        ];

        Rng rng = tick.Rng(actor.Id, RngPurpose.ActionChoice).Branch(6);
        switch (rng.PickIndexWeighted(weights))
        {
            case 0: Attempt(tick, goal, actor, target, actor.Faction, actor.Id); break;
            case 1: Defect(tick, goal, actor, target); break;
            default: break;
        }
    }

    private static void Defect(Tick tick, Goal goal, Actor actor, Actor because)
    {
        WorldState state = tick.State;
        EntityId from = actor.Faction;

        // Defectors go where the grudge points: to whoever their own house has wronged.
        // Living polities only — nobody takes service with a house that no longer exists.
        EntityId to = EntityId.None;
        int best = 0;
        foreach (Faction f in state.ActiveFactions())
        {
            if (f.Id == from) continue;
            int appeal = state.Relations.ValueOf(f.Id, from, RelationKind.Grievance) + f.Legitimacy / 3;
            if (appeal > best) { best = appeal; to = f.Id; }
        }
        if (to.IsNone) return;

        tick.Emit(new EventDraft(EventKind.IntrigueBetrayal)
            .Subject(actor.Id)
            .Object(because.Id)
            .By(from)
            .At(state.FactionOf(to).Seat)
            .Set("toFaction", to)
            .Rel(from, to, RelationKind.Grievance, 12)
            .Leg(from, -5)
            .Because(goal.Cause)
            .Weight(Significance.Major));

        state.Goals.Remove(goal);
    }

    private static void ReturnFromExile(Tick tick, Goal goal, Actor actor)
    {
        WorldState state = tick.State;
        Rng rng = tick.Rng(actor.Id, RngPurpose.ActionChoice).Branch(7);

        // An exile with a grudge and somewhere to take it is the engine's recurring
        // antagonist: the same name comes back twenty years later on the other side.
        EntityId host = EntityId.None;
        int best = 0;
        foreach (Faction f in state.ActiveFactions())
        {
            int appeal = f.Legitimacy / 4 + rng.Next(20);
            foreach (Relation grudge in state.Relations.From(actor.Id, RelationKind.Grievance))
                if (grudge.Key.To == f.Id) appeal = 0;
            if (appeal > best) { best = appeal; host = f.Id; }
        }

        if (host.IsNone || rng.Chance(55)) return;

        tick.Emit(new EventDraft(EventKind.PolityExileReturn)
            .Subject(actor.Id)
            .By(host)
            .At(state.FactionOf(host).Seat)
            .Set("title", Title.Captain)
            .Rel(actor.Id, host, RelationKind.Fealty, 25)
            .Because(goal.Cause)
            .Weight(Significance.Major));

        state.Goals.Remove(goal);
    }

    // ---- shared -----------------------------------------------------------

    /// <summary>An assassination attempt, resolved immediately. Failure is the interesting branch.</summary>
    internal static void Attempt(Tick tick, Goal? goal, Actor killer, Actor victim, EntityId sponsor, EntityId blamed)
    {
        // Guarded inside the action, not at the call sites: three different goals can order a
        // killing, and only one of them was checking. Nobody tries the same knife twice in a row.
        if (Recent.Did(tick, killer.Id, EventKind.ConflictAssassination, victim.Id, 7)) return;

        WorldState state = tick.State;
        Rng rng = tick.Rng(killer.Id, RngPurpose.Assassination);

        // Rarer, and far likelier to work when it does happen. Two thirds of attempts used to
        // fail, which made murder the cheapest and least consequential thing an ambitious
        // actor could do; the frequency is cut at the decision sites instead.
        int attack = killer.Traits.Guile + 18 + rng.Next(50);
        int guard = 25 + victim.Traits.Guile / 2 + victim.Traits.Martial / 3 + rng.Next(50);
        bool killed = attack > guard;
        bool exposed = !killed && rng.Chance(75);

        EventDraft draft = new EventDraft(EventKind.ConflictAssassination)
            .Subject(killer.Id)
            .Object(victim.Id)
            .By(sponsor)
            .At(victim.Place)
            .Hidden(killed || exposed ? Visibility.Public : Visibility.Secret)
            .Resolved(killed ? Outcome.Succeeded : Outcome.Failed)
            .Set("exposed", exposed ? 1 : 0)
            .Weight(Significance.Major);

        if (goal is not null) draft.Because(goal.Cause);
        if (!goal?.Arc.IsNone ?? false) draft.InArc(goal!.Arc);

        if (killed || exposed)
        {
            if (!victim.Faction.IsNone && !blamed.IsNone && victim.Faction != blamed)
                draft.Rel(victim.Faction, blamed, RelationKind.Grievance, killed ? 40 : 22);
            draft.Rel(victim.Id, killer.Id, RelationKind.Grievance, killed ? 0 : 35);
        }

        Event attempt = tick.Emit(draft);

        if (killed)
        {
            bool wasLeader = !victim.Faction.IsNone && state.FactionOf(victim.Faction).Leader == victim.Id;

            Event death = tick.Emit(new EventDraft(EventKind.LifeDeathViolent)
                .Subject(victim.Id)
                .Object(killer.Id)
                .By(victim.Faction)
                .At(victim.Place)
                .Set("age", victim.AgeAt(tick.Year))
                .Set("wasLeader", wasLeader ? 1 : 0)
                .Leg(victim.Faction, -8)
                .Because(attempt.Id)
                .Weight(Significance.Major));

            if (wasLeader) tick.PendingSuccessions.Add((victim.Faction, death.Id));
            if (goal is not null) state.Goals.Remove(goal);
            return;
        }

        if (exposed) Exile(tick, killer, killer.Faction, attempt.Id, "attempted murder");
    }

    /// <summary>
    /// Exile rather than execution wherever it is plausible. A dead enemy is an ending; a
    /// living one with a grudge and nowhere to go is a thread the log can pick up later.
    /// </summary>
    internal static void Exile(Tick tick, Actor actor, EntityId from, EventId cause, string reason)
    {
        // A house can only cast out its own. Where the man has already gone — fled, defected,
        // or taken service elsewhere in the same year the sentence lands — what it can do is
        // declare him outlaw, which is a judgement rather than a change of allegiance. Deciding
        // this here is the point: membership is live at emit time and reconstructing it later
        // from the log is exactly the guesswork the engine is built to avoid.
        bool outlaw = actor.Faction != from;

        EventDraft draft = new EventDraft(EventKind.PolityExile)
            .Subject(actor.Id)
            .By(from)
            .At(actor.Place)
            .Set("reason", reason)
            .Because(cause)
            .Weight(Significance.Major);

        if (outlaw) draft.Set("outlaw", 1);

        if (!from.IsNone)
            draft.Rel(actor.Id, from, RelationKind.Grievance, 45).Leg(from, -3);

        tick.Emit(draft);
    }

    /// <summary>Applies the aftermath of a decided coup: the winner takes the seat, the loser leaves.</summary>
    internal static void SettleCoup(Tick tick, Faction faction, Actor winner, Actor loser, Event resolved)
    {
        WorldState state = tick.State;

        if (state.FactionOf(faction.Id).Leader != winner.Id)
        {
            // Bookkeeping, because the challenge event already said who took the seat. Emitted
            // as a second readable line it described one act twice — "challenged openly and
            // took the seat, then executed a coup to take the seat again" — and labelled an
            // open challenge a coup while doing it. The state change still happens here.
            tick.Emit(new EventDraft(EventKind.PolitySuccession)
                .Subject(winner.Id)
                .By(faction.Id)
                .At(faction.Seat)
                .Set("reason", resolved.Kind == EventKind.PolityChallenge ? "open challenge" : "coup")
                .Leg(faction.Id, -6)
                .Because(resolved.Id)
                .InArc(resolved.Arc)
                .Weight(resolved.Kind == EventKind.PolityChallenge
                    ? Significance.Bookkeeping : Significance.Major));
        }

        Rng rng = tick.Rng(loser.Id, RngPurpose.Coup).Branch(11);
        if (rng.Chance(35))
        {
            tick.Emit(new EventDraft(EventKind.LifeDeathViolent)
                .Subject(loser.Id)
                .Object(winner.Id)
                .By(faction.Id)
                .At(faction.Seat)
                .Set("age", loser.AgeAt(tick.Year))
                .Because(resolved.Id)
                .InArc(resolved.Arc)
                .Weight(Significance.Major));
        }
        else
        {
            // The reason names what actually happened. One string served both paths, so a man
            // who lost an open challenge was recorded as "the losing side of a coup" — and the
            // prose duly reported a coup, correctly quoting a record that was itself wrong.
            // Ambiguous labels are the render layer's most persistent source of invention, and
            // this one had the engine putting the invention into the model's mouth.
            Exile(tick, loser, faction.Id, resolved.Id,
                resolved.Kind == EventKind.PolityChallenge
                    ? "the losing side of an open challenge"
                    : "the losing side of a coup");
        }
    }

    /// <summary>
    /// The most recent thing the target did to deserve this. Grievance edges remember which
    /// event last moved them, and citing that is what makes a slight or a refused demand the
    /// recorded reason for the reprisal that follows rather than an isolated gesture.
    ///
    /// Note the direction: the aggressor's grudge is moved by the *victim's* actions, so this
    /// cannot chain an insult to a previous insult by the same party.
    /// </summary>
    /// <summary>The event that last left this place in nobody's hands.</summary>
    private static EventId WhatFreedThePlace(Tick tick, EntityId place)
    {
        IReadOnlyList<EventId> history = tick.Log.ForEntity(place);
        for (int i = history.Count - 1; i >= 0; i--)
        {
            Event e = tick.Log.Get(history[i]);
            if (e.Kind is EventKind.PolityCollapse or EventKind.PolitySecession
                or EventKind.PolityPartition or EventKind.PolityRevolt)
            {
                return history[i];
            }
        }
        return EventId.None;
    }

    private static EventId Spark(Tick tick, EntityId from, EntityId to) =>
        tick.State.Relations.Find(from, to, RelationKind.Grievance)?.LastCause ?? EventId.None;

    /// <summary>Sworn support, in the same units the challenge is scored in.</summary>
    /// <summary>
    /// A person's following, as the fealty sworn to them. Shared with the covert path so an
    /// incumbent's standing counts the same against a conspiracy as it does against an open
    /// challenge — two ways of taking a seat should not weigh the same man differently.
    /// </summary>
    internal static int Support(WorldState state, EntityId actor) =>
        state.Relations.IncomingTotal(actor, RelationKind.Fealty) / 3;

    private static Faction? RichestInGrain(WorldState state, EntityId excluding)
    {
        Faction? best = null;
        int bestStock = 0;
        foreach (Faction f in state.ActiveFactions())
        {
            if (f.Id == excluding) continue;
            int stock = state.StockOf(f.Id, Resource.Grain);
            if (stock > bestStock) { bestStock = stock; best = f; }
        }
        return bestStock > 30 ? best : null;
    }

    private static Place Neediest(List<Place> places, Resource r, bool most)
    {
        Place chosen = places[0];
        foreach (Place p in places)
        {
            bool better = most
                ? p.Stockpile[(int)r] > chosen.Stockpile[(int)r]
                : p.Stockpile[(int)r] < chosen.Stockpile[(int)r];
            if (better) chosen = p;
        }
        return chosen;
    }
}
