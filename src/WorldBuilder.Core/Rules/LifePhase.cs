namespace WorldBuilder.Core.Rules;

/// <summary>
/// Phase 1 — mortality, coming of age, marriage, birth.
///
/// Mortality is doing more narrative work here than its size suggests: it is the only rule
/// that reliably ends arcs and forces the succession machinery to run, and succession is where
/// institutional rules collide with individual ambition. A world where nobody dies has no plot.
/// </summary>
public static class LifePhase
{
    public static void Run(Tick tick)
    {
        WorldState state = tick.State;

        // Snapshot: births during the phase must not be aged or killed in the same year.
        List<Actor> living = [.. state.LivingActors()];

        foreach (Actor actor in living) Age(tick, actor);

        // The marriage market is built once and shared, rather than each actor scanning every
        // other actor. The pairwise scan was quadratic in the living cast every single year.
        List<Actor> single = [];
        foreach (Actor a in living)
        {
            if (!a.IsAlive || a.Title == Title.Exile) continue;
            if (!Marriageable(tick, a)) continue;
            if (state.Relations.From(a.Id, RelationKind.Marriage).Count > 0) continue;
            single.Add(a);
        }

        foreach (Actor actor in single) Marry(tick, actor, single);
        foreach (Actor actor in living) Beget(tick, actor, living.Count);
    }

    private static void Age(Tick tick, Actor actor)
    {
        SimConfig config = tick.Config;
        int age = actor.AgeAt(tick.Year);

        if (age == config.AdultAge && actor.Title == Title.Commoner && !actor.Faction.IsNone)
        {
            tick.Emit(new EventDraft(EventKind.LifeComingOfAge)
                .Subject(actor.Id)
                .By(actor.Faction)
                .At(actor.Place)
                .Set("title", Title.Retainer)
                .Weight(Significance.Bookkeeping));
            return;
        }

        if (age < config.AdultAge) return;

        int chance = config.BaseMortalityBp + Math.Max(0, age - config.OldAge) * config.MortalityPerYearOverBp;
        Rng rng = tick.Rng(actor.Id, RngPurpose.Mortality);
        if (!rng.ChanceBp(chance)) return;

        bool wasLeader = !actor.Faction.IsNone && tick.State.FactionOf(actor.Faction).Leader == actor.Id;

        Event death = tick.Emit(new EventDraft(EventKind.LifeDeathNatural)
            .Subject(actor.Id)
            .By(actor.Faction)
            .At(actor.Place)
            .Set("age", age)
            .Set("wasLeader", wasLeader ? 1 : 0)
            // No cause. Dying of old age is not brought about by any prior event, and citing
            // the actor's own birth — as this did — was the single largest manufacturer of
            // deep "causal" chains that turned out to be one person's biography.
            .Weight(wasLeader || actor.Title == Title.Ruler ? Significance.Major : Significance.Minor));

        if (wasLeader) tick.PendingSuccessions.Add((actor.Faction, death.Id));
    }

    private static void Marry(Tick tick, Actor actor, List<Actor> single)
    {
        // Commoners marry too. They are the pool the titled cast is replenished from, and
        // excluding them was what starved the world of people in the first place.
        if (tick.State.Relations.From(actor.Id, RelationKind.Marriage).Count > 0) return;

        Rng rng = tick.Rng(actor.Id, RngPurpose.Marriage);
        if (!rng.ChanceBp(tick.Config.MarriageChanceBp)) return;

        Actor? match = ChooseSpouse(tick, actor, single, ref rng);
        if (match is null) return;

        // Marrying across factions is the cheapest way to make a later succession messy:
        // it plants kin claims inside a rival house decades before they matter.
        bool crossFaction = !actor.Faction.IsNone && !match.Faction.IsNone && actor.Faction != match.Faction;

        EventDraft draft = new EventDraft(EventKind.LifeMarriage)
            .Subject(actor.Id)
            .Object(match.Id)
            .By(actor.Faction)
            .At(actor.Place)
            .RelBoth(actor.Id, match.Id, RelationKind.Marriage, 1)
            .RelBoth(actor.Id, match.Id, RelationKind.Kin, 1)
            // Being born is a precondition of marrying, not a cause of it. A cross-faction
            // match does have a cause worth recording — the alliance it is meant to serve.
            .Weight(crossFaction ? Significance.Major : Significance.Minor);

        if (crossFaction)
        {
            // A match may be cited to the alliance it seals, but only where that alliance was
            // actually negotiated. Citing the edge's most recent cause chained marriage to
            // marriage — each one bumps the alliance, so the next cited the last, producing
            // long causal runs between couples with no person in common.
            Relation? pact = tick.State.Relations.Find(actor.Faction, match.Faction, RelationKind.Alliance);
            if (pact is not null
                && tick.Log.TryGet(pact.Cause, out Event origin)
                && origin.Kind == EventKind.DiploAllianceFormed)
            {
                draft.Because(pact.Cause);
            }

            draft.RelBoth(actor.Faction, match.Faction, RelationKind.Alliance, 12)
                 .Set("crossFaction", 1);
        }

        tick.Emit(draft);
    }

    private static Actor? ChooseSpouse(Tick tick, Actor actor, List<Actor> single, ref Rng rng)
    {
        WorldState state = tick.State;
        List<Actor> candidates = [];
        List<int> weights = [];
        List<int> flatWeights = [];
        int nearest = int.MaxValue, furthest = int.MinValue;

        foreach (Actor other in single)
        {
            if (other.Id == actor.Id) continue;
            if (!other.IsAlive) continue;
            if (state.Relations.From(other.Id, RelationKind.Marriage).Count > 0) continue;
            if (state.Relations.Has(actor.Id, other.Id, RelationKind.Kin)) continue;
            if (other.Id < actor.Id) continue; // pair each couple once

            int weight = 10;
            if (other.Faction != actor.Faction) weight += 14;
            if (state.Relations.ValueOf(actor.Faction, other.Faction, RelationKind.Grievance) > 20) weight -= 8;
            if (state.Relations.Has(actor.Faction, other.Faction, RelationKind.Alliance)) weight += 10;

            // People marry people they have met.
            //
            // The market was the whole living cast, so a steward in one town was as likely to
            // marry across the map as across the square — and since a cross-house match carries an
            // alliance edge, the pairing rule was quietly manufacturing ties between realms with
            // no other connection at all. Proximity reads 100 at a typical separation between this
            // world's places, so a match at ordinary distance weighs what it always weighed.
            //
            // The existing Math.Max(1, …) floor is left alone and does the work it always did: a
            // distant match becomes unlikely, never impossible, which keeps the occasional
            // dynastic marriage across the world available as the remarkable thing it should be.
            int near = state.Geo?.BetweenActors(actor.Id, other.Id) ?? Geography.Geography.Neutral;
            nearest = Math.Min(nearest, near);
            furthest = Math.Max(furthest, near);

            flatWeights.Add(Math.Max(1, weight));
            weight = weight * near / 100;

            candidates.Add(other);
            weights.Add(Math.Max(1, weight));
        }

        if (candidates.Count == 0) return null;

        int index = rng.PickIndexWeighted(
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan(weights), out long roll, out long total);

        if (tick.Probe is not null)
        {
            int flat = Rng.WouldPick(
                System.Runtime.InteropServices.CollectionsMarshal.AsSpan(flatWeights), roll, total);
            tick.Probe.Ranked("marriage", candidates.Count, furthest - nearest, flat != index);
        }

        return index < 0 ? null : candidates[index];
    }

    /// <summary>Whoever holds the ground you were born on, failing that your parent's house.</summary>
    private static EntityId Allegiance(Tick tick, Actor parent)
    {
        if (parent.Place.IsNone) return parent.Faction;
        EntityId holder = tick.State.PlaceOf(parent.Place).Controller;
        return holder.IsNone ? parent.Faction : holder;
    }

    private static bool Marriageable(Tick tick, Actor actor)
    {
        int age = actor.AgeAt(tick.Year);
        return age >= tick.Config.MarriageMinAge && age <= tick.Config.MarriageMaxAge;
    }

    private static void Beget(Tick tick, Actor actor, int livingCount)
    {
        if (actor.Title == Title.Exile) return;
        if (!Marriageable(tick, actor)) return;

        List<Relation> marriage = tick.State.Relations.From(actor.Id, RelationKind.Marriage);
        if (marriage.Count == 0) return;

        EntityId spouse = marriage[0].Key.To;
        if (spouse < actor.Id) return; // only the lower-id partner rolls, so each couple rolls once

        // Only notable lines produce named children. Everyone else's children are part of the
        // town's population, which the economy already tracks. Once the cast is at its ceiling
        // even notables stop, apart from ruling houses — a throne must always have an heir.
        bool royal = actor.Title is Title.Ruler or Title.Heir
                     || tick.State.ActorOf(spouse).Title is Title.Ruler or Title.Heir;
        bool notable = royal || actor.Title != Title.Commoner
                       || tick.State.ActorOf(spouse).Title != Title.Commoner;

        if (!notable) return;
        if (livingCount >= tick.Config.NamedCastCap && !royal) return;

        Rng rng = tick.Rng(actor.Id, RngPurpose.Birth);
        if (!rng.ChanceBp(tick.Config.BirthChanceBp)) return;

        EntityId childId = tick.Chronicle.ReserveActor();

        tick.Emit(new EventDraft(EventKind.LifeBirth)
            .Subject(childId)
            .Object(actor.Id)
            .By(actor.Faction)
            .At(actor.Place)
            .Set("name", tick.Forge.PersonName(20_000 + childId.Index))
            .Set("birthYear", tick.Year)
            .Set("ambition", rng.Range(10, 95))
            .Set("guile", rng.Range(10, 95))
            .Set("martial", rng.Range(10, 95))
            .Set("loyalty", rng.Range(15, 90))
            .Set("place", actor.Place)
            // Allegiance follows the ground, not the parents. Inheriting the mother's faction
            // meant that when a house was dissolved its descendants stayed stateless forever,
            // so the realm that had conquered them could never find a claimant of its own.
            .Set("faction", Allegiance(tick, actor))
            .Set("title", Title.Commoner)
            .Set("mother", actor.Id)
            .Set("father", spouse)
            .RelBoth(childId, actor.Id, RelationKind.Kin, 1)
            .RelBoth(childId, spouse, RelationKind.Kin, 1)
            .Because(marriage[0].Cause)
            .Weight(royal ? Significance.Minor : Significance.Bookkeeping));
    }
}
