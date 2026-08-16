namespace WorldBuilder.Core.Rules;

/// <summary>
/// Phase 6 — the year's pressure comes due: thrones are filled, grudges cool a little,
/// legitimacy that has been bleeding all year finally breaks something.
///
/// Succession lives here rather than in the life phase on purpose. It runs after everything
/// that can kill a ruler has happened, so a leader assassinated in phase 4 and one who died
/// of old age in phase 1 go through exactly the same machinery.
/// </summary>
public static class ConsequencePhase
{
    public static void Run(Tick tick)
    {
        DissolveLandless(tick);
        AbsorbSubjects(tick);
        ResolveSuccessions(tick);
        DecayAndDrift(tick);
        Breakdown(tick);
        CloseFinishedArcs(tick);
        RetireGoals(tick);
    }

    /// <summary>
    /// A house that holds no ground is not a polity any more, and is dissolved.
    ///
    /// Left alone, landless factions carried on appointing stewards, courting support and
    /// disputing successions over nothing — ghost courts that produced a third of the readable
    /// log while governing not one acre. Their surviving members return to private life, where
    /// an ambitious one may still turn up later as somebody else's captain.
    /// </summary>
    private static void DissolveLandless(Tick tick)
    {
        WorldState state = tick.State;

        foreach (Faction faction in state.Factions)
        {
            if (state.HoldingCount(faction.Id) > 0) continue;

            // Terminal, and provably so: a house is announced finished exactly once, however
            // many stragglers drift back to it afterwards.
            if (Recent.Ever(tick, faction.Id, EventKind.PolityCollapse)) continue;

            List<Actor> members = state.MembersOf(faction.Id);
            if (members.Count == 0 && faction.Leader.IsNone) continue;

            EventId finishedBy = LastEventFor(tick, faction.Id);

            EventDraft draft = new EventDraft(EventKind.PolityCollapse)
                .By(faction.Id)
                .At(faction.Seat)
                .Set("places", 0)
                .Set("scattered", members.Count)
                .Leg(faction.Id, -faction.Legitimacy)
                .Because(finishedBy)
                .Weight(Significance.Major);

            // Whoever took the last of its ground is a participant in its ending.
            //
            // The event named only the house that fell, so a section about the power that
            // destroyed it never saw the collapse at all: the destruction of a founding realm
            // in the last year of a period, by the faction the section was about, was missing
            // from that section and could not have been otherwise. An ending belongs to the
            // history of both parties.
            EntityId victor = Victor(tick, finishedBy);
            if (!victor.IsNone && victor != faction.Id) draft.Bystander(victor);

            foreach (Actor m in members) draft.Set($"disown:{m.Id}", 1);

            tick.Emit(draft);
        }
    }

    /// <summary>
    /// People living under a flag eventually answer to it. Whoever holds the ground acquires
    /// the stateless adults standing on it — the survivors of dissolved houses, and anyone a
    /// conquest left behind.
    ///
    /// Without this a conquering realm inherited land but never people, so it had no candidates
    /// of its own, every succession fell to a stranger raised from the town, and the disputed
    /// successions that break large realms apart could never happen at all.
    /// </summary>
    private static void AbsorbSubjects(Tick tick)
    {
        WorldState state = tick.State;

        EventDraft draft = new EventDraft(EventKind.PolityAppointment)
            .Set("kind", "absorbed")
            .Weight(Significance.Bookkeeping);

        bool any = false;

        foreach (Actor actor in state.LivingActors())
        {
            if (!actor.Faction.IsNone || actor.Title == Title.Exile) continue;
            if (actor.AgeAt(tick.Year) < tick.Config.AdultAge) continue;
            if (actor.Place.IsNone) continue;

            EntityId holder = state.PlaceOf(actor.Place).Controller;
            if (holder.IsNone) continue;

            draft.Set($"join:{actor.Id}", holder);
            any = true;
        }

        if (any) tick.Emit(draft);
    }

    // ---- succession -------------------------------------------------------

    private static void ResolveSuccessions(Tick tick)
    {
        WorldState state = tick.State;
        List<(EntityId Faction, EventId Cause)> pending = [.. tick.PendingSuccessions];

        // Thrones emptied by exile or defection rather than by death get picked up here too.
        foreach (Faction faction in state.Factions)
        {
            if (!faction.Leader.IsNone && state.ActorOf(faction.Leader).IsAlive) continue;
            if (pending.Exists(p => p.Faction == faction.Id)) continue;

            pending.Add((faction.Id, WhatVacatedTheSeat(tick, faction.Id)));
        }

        foreach ((EntityId factionId, EventId cause) in pending)
            Succeed(tick, state.FactionOf(factionId), cause);
    }

    private static void Succeed(Tick tick, Faction faction, EventId cause)
    {
        WorldState state = tick.State;
        if (!faction.Leader.IsNone && state.ActorOf(faction.Leader).IsAlive) return;

        List<Actor> candidates = Candidates(tick, faction);
        if (candidates.Count == 0)
        {
            RaiseLocalClaimant(tick, faction, cause);
            return;
        }

        Actor heir = ChooseHeir(tick, faction, candidates);
        Actor? rival = FindRival(tick, faction, candidates, heir);

        if (rival is null)
        {
            EventDraft draft = new EventDraft(EventKind.PolitySuccession)
                .Subject(heir.Id)
                .By(faction.Id)
                .At(faction.Seat)
                .Set("reason", SuccessionLabel(faction.Succession))
                .Leg(faction.Id, 3)
                .Because(cause)
                .Weight(Significance.Major);

            // Under primogeniture, being born when you were *is* why the seat is yours. That
            // makes the birth a genuine cause rather than biography, and it is the one edge
            // that turns a dynasty into a chain the log can actually be walked along.
            if (faction.Succession == SuccessionRule.Primogeniture)
                draft.Because(tick.Log.OriginOf(heir.Id));

            // Having been raised to office is why this person was standing close enough to the
            // seat to take it. Otherwise an appointment was an announcement and nothing more.
            draft.Because(Recent.LastOfKind(tick, heir.Id, EventKind.PolityAppointment));

            tick.Emit(draft);
            return;
        }

        // Two claims and one seat. This is the single richest thing the engine does: an
        // institutional rule saying one name and an ambitious relative saying another.
        EntityId arc = tick.Chronicle.ReserveArc();
        Rng rng = tick.Rng(faction.Id, RngPurpose.Succession);

        Event dispute = tick.Emit(new EventDraft(EventKind.PolitySuccessionDisputed)
            .Subject(heir.Id)
            .Object(rival.Id)
            .By(faction.Id)
            .At(faction.Seat)
            .Set("rule", faction.Succession)
            .Set("arcName", ArcNames.Succession(ref rng, faction.Name, tick.Year))
            .Leg(faction.Id, -8)
            .Rel(rival.Id, heir.Id, RelationKind.Grievance, 30)
            .Rel(heir.Id, rival.Id, RelationKind.Grievance, 20)
            .InArc(arc)
            .Because(cause)
            .Weight(Significance.Major));

        // Being the named heir is worth something.
        //
        // The rival carried an Ambition term worth up to 33 points and the heir had no
        // counterpart to it: the one thing that made him the heir counted for nothing in the
        // comparison deciding whether he inherited. The claim was set aside in 34 of 41, and the
        // code already knew — the "decision" tag below was added because pooling hid it, and its
        // comment recorded thirteen in fifteen. Observed, tagged, never adjudicated.
        //
        // The counterweight is the legitimacy of the house behind the claim, at the same
        // magnitude as the rival's ambition. Not larger: an heir should not be guaranteed either.
        // A legitimate house passes its seat to the named heir and a house in crisis is where a
        // rival takes it, which is a story rather than a coin flip.
        int heirScore = Backing(state, heir) + heir.Traits.Martial / 2
                        + DesignationWeight(tick, heir) + rng.Next(50);
        int rivalScore = Backing(state, rival) + rival.Traits.Martial / 2
                         + rival.Traits.Ambition / 3 + rng.Next(50);

        Actor winner = heirScore >= rivalScore ? heir : rival;
        Actor loser = heirScore >= rivalScore ? rival : heir;

        tick.Emit(new EventDraft(EventKind.PolitySuccession)
            .Subject(winner.Id)
            .Object(loser.Id)
            .By(faction.Id)
            .At(faction.Seat)
            // Says *whose* claim. "(claim overturned)" did not, and the renderer read it
            // correctly once and backwards once — the expected result for an ambiguous label,
            // and a fabrication vector that has nothing to do with the model.
            .Set("reason", winner.Id == heir.Id
                ? "the named heir's claim upheld"
                : "the named heir's claim set aside")
            // Tags this as one binary decision rather than one of the several unrelated ways a
            // seat can pass. Without it the skew measure pooled "upheld or set aside" with
            // "election", "founding" and "coup" and reported a 44% commonest case, hiding the
            // real figure: the heir's claim is set aside thirteen times in fifteen.
            .Set("decision", "claim")
            .Leg(faction.Id, -4)
            .InArc(arc)
            .EndArc(arc)
            .Because(dispute.Id)
            .Weight(Significance.Major));

        if (!Partition(tick, faction, loser, dispute, arc, ref rng))
            ActionPhase.Exile(tick, loser, faction.Id, dispute.Id, "the losing claim");
    }

    /// <summary>
    /// A house can run out of names while its towns are still full of people. When that
    /// happens somebody local takes the seat rather than the whole polity evaporating.
    ///
    /// This is the single most important balance rule in the engine. Without it a faction
    /// whose named cast happened to die would dump every settlement it held onto the map as
    /// unclaimed, its neighbour would walk onto all of them unopposed, and one power ran away
    /// with the region before year twenty in every seed tested.
    /// </summary>
    private static void RaiseLocalClaimant(Tick tick, Faction faction, EventId cause)
    {
        WorldState state = tick.State;
        List<Place> holdings = state.HoldingsOf(faction.Id);
        if (holdings.Count == 0) { Collapse(tick, faction, cause); return; }

        Place home = holdings[0];
        foreach (Place p in holdings)
            if (p.Id == faction.Seat) home = p;

        if (home.Population < 100) { Collapse(tick, faction, cause); return; }

        EntityId id = RaiseNotable(tick, home, faction.Id, Title.Retainer, cause);

        tick.Emit(new EventDraft(EventKind.PolitySuccession)
            .Subject(id)
            .By(faction.Id)
            .At(home.Id)
            .Set("reason", $"raised from {home.Name}, no claimant remaining")
            .Leg(faction.Id, -10)
            .Because(cause)
            .Weight(Significance.Major));
    }

    /// <summary>
    /// Brings a named local into being out of a town's anonymous population.
    ///
    /// The engine simulates twenty-odd named people on top of settlements holding thousands,
    /// and without this a place that changed hands had no one in it the log could name — so a
    /// conquered town could never revolt, never secede, and never produce a claimant. The
    /// population was there; the cast was not.
    /// </summary>
    private static EntityId RaiseNotable(Tick tick, Place home, EntityId faction, Title title, EventId cause)
    {
        EntityId id = tick.Chronicle.ReserveActor();
        Rng rng = tick.Rng(home.Id, RngPurpose.Succession).Branch(tick.Year);

        tick.Emit(new EventDraft(EventKind.GenesisActor)
            .Subject(id)
            .By(faction)
            .At(home.Id)
            .Set("name", tick.Forge.PersonName(40_000 + id.Index))
            .Set("birthYear", tick.Year - rng.Range(24, 46))
            .Set("ambition", rng.Range(35, 95))
            .Set("guile", rng.Range(10, 90))
            .Set("martial", rng.Range(10, 90))
            .Set("loyalty", rng.Range(20, 80))
            .Set("place", home.Id)
            .Set("faction", faction)
            .Set("title", title)
            .Because(cause)
            .Weight(Significance.Bookkeeping));

        return id;
    }

    /// <summary>
    /// The power that took the last holding, read from the event that finished this house.
    /// Only a conquest has a victor; a house that simply ran out of people has none.
    /// </summary>
    private static EntityId Victor(Tick tick, EventId cause)
    {
        if (cause.IsNone || !tick.Log.TryGet(cause, out Event e)) return EntityId.None;
        return e.Kind == EventKind.ConflictConquest ? e.Faction : EntityId.None;
    }

    /// <summary>
    /// A polity holding nothing, with nobody left to claim it, stops existing. Guarded by the
    /// fact that a collapsed faction holds nothing, so it can only ever fire once.
    /// </summary>
    private static void Collapse(Tick tick, Faction faction, EventId cause)
    {
        List<Place> holdings = tick.State.HoldingsOf(faction.Id);
        if (holdings.Count == 0) return;

        EventDraft draft = new EventDraft(EventKind.PolityCollapse)
            .By(faction.Id)
            .At(faction.Seat)
            .Set("places", holdings.Count)
            .Leg(faction.Id, -faction.Legitimacy)
            .Because(cause)
            .Weight(Significance.Major);

        foreach (Place p in holdings) draft.Set($"ctrl:{p.Id}", EntityId.None);

        tick.Emit(draft);
    }

    /// <summary>
    /// The losing claimant walks off with half the realm instead of into exile.
    ///
    /// This is the counterweight to conquest. Every other brake on a winning faction — revolt,
    /// secession, coalition grievance — moves one town at a time, and over three hundred years
    /// none of them ever undid a hegemony: one power reached the whole map by year thirty and
    /// held it for the remaining two hundred and seventy. A big realm with two claimants
    /// dividing is the mechanism that actually breaks empires, and it only fires where it
    /// should — a small faction has nothing to divide.
    /// </summary>
    private static bool Partition(Tick tick, Faction faction, Actor loser, Event dispute, EntityId arc, ref Rng rng)
    {
        WorldState state = tick.State;

        List<Place> holdings = state.HoldingsOf(faction.Id);
        if (holdings.Count < 4) return false;

        // The bigger and shakier the realm, the likelier it comes apart.
        int chance = 25 + (holdings.Count - 4) * 15 + (60 - Math.Min(60, faction.Legitimacy)) / 2;
        if (!rng.Chance(Math.Min(85, chance))) return false;

        // The claimant takes the outlying half; the seat stays with the winner.
        List<Place> taken = [];
        foreach (Place p in holdings)
        {
            if (p.Id == faction.Seat) continue;
            if (taken.Count >= holdings.Count / 2) break;
            taken.Add(p);
        }

        if (taken.Count == 0) return false;

        EntityId born = tick.Chronicle.ReserveFaction();

        EventDraft draft = new EventDraft(EventKind.PolityPartition)
            .Subject(loser.Id)
            .Object(faction.Leader)
            .By(faction.Id)
            // The new polity is a participant in its own founding, so the log indexes it and
            // it has a history to cite. Without this a partitioned realm was invisible to
            // every "what happened to this faction" lookup and its events had no causes.
            .Bystander(born)
            .At(taken[0].Id)
            .Set("name", tick.Forge.SecessionName(tick.Year, taken[0].Name))
            .Set("succession", faction.Succession)
            .Set("legitimacy", Math.Max(30, faction.Legitimacy - 10))
            .Set("treasury", faction.Treasury / 2)
            .Set("places", taken.Count)
            .Treas(faction.Id, -faction.Treasury / 2)
            .Leg(faction.Id, -8)
            .Rel(loser.Id, faction.Id, RelationKind.Grievance, 40)
            .InArc(arc)
            .Because(dispute.Id)
            .Weight(Significance.Major);

        foreach (Place p in taken) draft.Set($"ctrl:{p.Id}", born);

        tick.Emit(draft);
        return true;
    }

    /// <summary>
    /// How the seat passed, in words. A bare enum name ("Strongest") tells a reader nothing
    /// about what happened; these say which rule was followed, unambiguously.
    /// </summary>
    private static string SuccessionLabel(SuccessionRule rule) => rule switch
    {
        SuccessionRule.Primogeniture => "by right of birth",
        SuccessionRule.Election => "by election",
        _ => "by the strongest claim",
    };

    private static List<Actor> Candidates(Tick tick, Faction faction)
    {
        List<Actor> candidates = [];
        foreach (Actor a in tick.State.MembersOf(faction.Id))
        {
            if (a.AgeAt(tick.Year) < tick.Config.AdultAge) continue;
            if (a.Title == Title.Exile) continue;
            candidates.Add(a);
        }

        candidates.Sort(static (x, y) => x.Id.CompareTo(y.Id));
        return candidates;
    }

    /// <summary>
    /// What a standing designation is worth to the candidate holding it.
    ///
    /// The counterweight to the rival's ambition, and the third quantity tried for the job. The
    /// first two failed for the same reason in different ways: nothing at all, and then the
    /// house's legitimacy — which collapses under exactly the conditions that produce a dispute,
    /// so the contest was decided before it opened.
    ///
    /// The age of the designation cannot collapse. It is monotone in elapsed time and derived
    /// from a recorded act, so the crisis that triggers the contest leaves it untouched. In a
    /// polity that elects, what a designated candidate brings to the vote is standing consent:
    /// the house named him and has not unmade him since.
    ///
    /// Capped at fifteen years — about half a tenure, after which further age says nothing new —
    /// and scaled to the same 0–30 the rival's ambition spans, because symmetry was the original
    /// diagnosis and overshooting would replace one asymmetry with another.
    ///
    /// <b>An heir derived by rule rather than named by an act carries nothing</b>, which is what
    /// keeps set-aside heirs common: they are load-bearing as false-premise test cases and as a
    /// grievance source, and the shape of this rule preserves them rather than a tuned constant.
    /// </summary>
    private static int DesignationWeight(Tick tick, Actor heir)
    {
        if (heir.Title != Title.Heir) return 0;

        EventId named = Recent.LastOfKind(tick, heir.Id, EventKind.PolityAppointment);
        if (named.IsNone) return 0;

        int years = tick.Year - tick.Log.Get(named).Year;
        return Math.Clamp(years, 0, 15) * 2;
    }

    private static Actor ChooseHeir(Tick tick, Faction faction, List<Actor> candidates)
    {
        WorldState state = tick.State;

        switch (faction.Succession)
        {
            case SuccessionRule.Primogeniture:
            {
                // Kin of the last ruler first, eldest of them; the throne stays in the family
                // until the family runs out, which is exactly when things get interesting.
                Actor? best = null;
                foreach (Actor a in candidates)
                {
                    bool royal = a.Title is Title.Heir or Title.Ruler
                                 || state.Relations.From(a.Id, RelationKind.Kin).Count > 0;
                    if (!royal) continue;
                    if (best is null || a.BirthYear < best.BirthYear) best = a;
                }
                return best ?? Eldest(candidates);
            }

            case SuccessionRule.Strongest:
            {
                Actor best = candidates[0];
                foreach (Actor a in candidates)
                    if (a.Traits.Martial > best.Traits.Martial) best = a;
                return best;
            }

            default:
            {
                // The house's standing candidate is the natural nominee where it elects.
                //
                // Without this the designation was real and ignored: an appointment named a
                // candidate, and the selector then picked whoever had the most backing, so "the
                // named heir" in the resulting event was routinely somebody nobody had named.
                // Any weight attached to a designation is worth nothing while the person holding
                // it is not the person the label refers to.
                //
                // Only here and under primogeniture, which already prefers the title. A house
                // whose rule is that the strongest takes the seat means it, and a designation
                // does not override it.
                Actor best = candidates[0];
                int bestScore = -1;
                foreach (Actor a in candidates)
                {
                    int score = Backing(state, a) + a.Traits.Loyalty / 2 + DesignationWeight(tick, a);
                    if (score > bestScore) { bestScore = score; best = a; }
                }
                return best;
            }
        }
    }

    private static Actor? FindRival(Tick tick, Faction faction, List<Actor> candidates, Actor heir)
    {
        Actor? rival = null;
        int best = 0;

        foreach (Actor a in candidates)
        {
            if (a.Id == heir.Id) continue;
            if (a.Traits.Ambition < 55) continue;

            int claim = a.Traits.Ambition + Backing(tick.State, a) - faction.Legitimacy / 2;
            if (a.Title is Title.Heir or Title.Captain or Title.Steward) claim += 15;
            if (claim > best) { best = claim; rival = a; }
        }

        if (rival is null) return null;

        Rng rng = tick.Rng(faction.Id, RngPurpose.Succession).Branch(rival.Id.Index);
        return rng.Next(100) < Math.Clamp(best / 2, 5, 80) ? rival : null;
    }

    private static Actor Eldest(List<Actor> candidates)
    {
        Actor best = candidates[0];
        foreach (Actor a in candidates)
            if (a.BirthYear < best.BirthYear) best = a;
        return best;
    }

    /// <summary>Sworn support, summed over incoming fealty edges. Who would actually turn out.</summary>
    private static int Backing(WorldState state, Actor actor) =>
        state.Relations.IncomingTotal(actor.Id, RelationKind.Fealty) / 6;

    // ---- slow change ------------------------------------------------------

    private static void DecayAndDrift(Tick tick)
    {
        WorldState state = tick.State;

        EventDraft draft = new EventDraft(EventKind.EconomyYield)
            .Set("kind", "drift")
            .Weight(Significance.Bookkeeping);

        bool any = false;

        // Grudges fade, but slowly. This one percentage is the dial that decides whether the
        // world has a memory: at 96% a serious grievance is still half-alive seventeen years on.
        foreach (Relation r in state.Relations.All)
        {
            if (r.Key.Kind != RelationKind.Grievance || r.Value <= 0) continue;
            int decayed = r.Value - r.Value * tick.Config.GrievanceRetentionPct / 100;
            if (decayed <= 0) continue;
            draft.Rel(r.Key.From, r.Key.To, RelationKind.Grievance, -decayed);
            any = true;
        }

        int totalPower = 0;
        foreach (Faction f in state.Factions) totalPower += state.PowerOf(f.Id);

        foreach (Faction f in state.Factions)
        {
            int holdings = state.HoldingsOf(f.Id).Count;
            if (holdings == 0) continue;

            int drift = 0;
            if (state.AtWar(f.Id)) drift -= 2;
            if (f.Leader.IsNone) drift -= 4;
            if (f.Legitimacy < 50 && !state.AtWar(f.Id) && !f.Leader.IsNone) drift += 1;

            // Overextension. Holding a lot of ground is a burden as well as a benefit, which
            // is the cheapest available brake on the snowball where one winner takes the map.
            if (holdings > 3) drift -= holdings - 3;

            if (drift != 0) { draft.Leg(f.Id, drift); any = true; }

            // Balance of power: everyone resents whoever is plainly winning. This is what
            // eventually produces a coalition war against the leader instead of a slow,
            // unopposed absorption of the whole region by whoever got the first ore site.
            int share = totalPower == 0 ? 0 : state.PowerOf(f.Id) * 100 / totalPower;
            if (share < 38) continue;

            foreach (Faction other in state.Factions)
            {
                if (other.Id == f.Id || state.HoldingsOf(other.Id).Count == 0) continue;
                draft.Rel(other.Id, f.Id, RelationKind.Grievance, 2 + (share - 38) / 6);
                any = true;
            }
        }

        if (any) tick.Emit(draft);
    }

    // ---- breakdown --------------------------------------------------------

    private static void Breakdown(Tick tick)
    {
        WorldState state = tick.State;

        // Snapshot: a secession brings a new faction into being mid-loop, and a polity born
        // this year does not also get to fall apart this year.
        List<Faction> existing = [.. state.Factions];

        foreach (Faction faction in existing)
        {
            if (faction.Legitimacy >= tick.Config.RevoltThreshold) continue;

            List<Place> holdings = state.HoldingsOf(faction.Id);
            if (holdings.Count <= 1) continue;

            Rng rng = tick.Rng(faction.Id, RngPurpose.Unrest);

            Place? worst = null;
            foreach (Place p in holdings)
            {
                if (p.Id == faction.Seat) continue;
                if (worst is null || p.Stockpile[(int)Resource.Grain] < worst.Stockpile[(int)Resource.Grain]) worst = p;
            }
            if (worst is null) continue;

            EventId cause = LastEventFor(tick, faction.Id);

            if (faction.Legitimacy < tick.Config.SecessionThreshold && rng.Chance(45))
            {
                Secede(tick, faction, worst, cause);
                continue;
            }

            if (!rng.Chance(50)) continue;

            // A town that has risen before does not rise again the following decade. The gap
            // widens with each attempt: repeated risings at the same place with the same
            // outcome are a stuck mechanic, not a restive population.
            int priorRisings = Recent.CountEver(tick, worst.Id, EventKind.PolityRevolt);
            if (Recent.Did(tick, faction.Id, EventKind.PolityRevolt, worst.Id, 9 + priorRisings * 8)) continue;

            tick.Emit(new EventDraft(EventKind.PolityRevolt)
                .By(faction.Id)
                .At(worst.Id)
                .Set("legitimacy", faction.Legitimacy)
                .Pop(worst.Id, -worst.Population / 20)
                .Leg(faction.Id, -3)
                .Because(cause)
                .Weight(Significance.Major));
        }
    }

    private static void Secede(Tick tick, Faction faction, Place place, EventId cause)
    {
        WorldState state = tick.State;
        Rng rng = tick.Rng(place.Id, RngPurpose.Unrest).Branch(3);

        // The breakaway needs a face. Whoever is standing there with the most ambition
        // becomes a head of state, which is a promotion the reader tends to remember.
        Actor? leader = null;
        int best = -1;
        foreach (Actor a in state.LivingActors())
        {
            if (a.Place != place.Id) continue;
            if (a.Title == Title.Ruler) continue;
            int score = a.Traits.Ambition + (100 - a.Traits.Loyalty) / 2 + rng.Next(20);
            if (score > best) { best = score; leader = a; }
        }

        // A town large enough to matter can find its own leader. Conquered places hold no
        // named actors, so without this the one valve that could break up an over-large
        // empire was permanently shut and a hegemon simply sat on the map to the last year.
        EntityId leaderId = leader?.Id
            ?? (place.Population >= 200 ? RaiseNotable(tick, place, faction.Id, Title.Retainer, cause) : EntityId.None);

        if (leaderId.IsNone) return;

        EventDraft draft = new EventDraft(EventKind.PolitySecession)
            .By(faction.Id)
            .Bystander(tick.Chronicle.ReserveFaction())
            .At(place.Id)
            .Set("name", tick.Forge.SecessionName(tick.Year, place.Name))
            .Set("succession", SuccessionRule.Election)
            .Set("legitimacy", 45)
            .Set("treasury", 20)
            .Leg(faction.Id, -6)
            .Because(cause)
            .Weight(Significance.Major);

        draft.Subject(leaderId);
        tick.Emit(draft);
    }

    /// <summary>
    /// Why a polity is coming apart: the last thing that actually cost it legitimacy. Formerly
    /// this returned whatever event had most recently mentioned the faction, which meant a
    /// revolt could be recorded as caused by a birth.
    /// </summary>
    private static EventId LastEventFor(Tick tick, EntityId entity) =>
        PerceptionPhase.LastLegitimacyBlow(tick, entity);

    /// <summary>
    /// What emptied the seat: a death, an exile, a defection or a collapse. Anything else that
    /// merely happened nearby is not a cause of the succession that follows.
    /// </summary>
    private static EventId WhatVacatedTheSeat(Tick tick, EntityId faction)
    {
        IReadOnlyList<EventId> history = tick.Log.ForEntity(faction);

        for (int i = history.Count - 1; i >= 0; i--)
        {
            EventKind kind = tick.Log.Get(history[i]).Kind;
            if (kind is EventKind.LifeDeathNatural or EventKind.LifeDeathViolent
                or EventKind.PolityExile or EventKind.IntrigueBetrayal or EventKind.PolityPartition)
            {
                return history[i];
            }
        }
        return EventId.None;
    }

    // ---- housekeeping -----------------------------------------------------

    private static void CloseFinishedArcs(Tick tick)
    {
        WorldState state = tick.State;

        HashSet<EntityId> touchedThisYear = [];
        foreach (Event e in tick.Log.InYear(tick.Year))
            if (!e.Arc.IsNone) touchedThisYear.Add(e.Arc);

        foreach (Arc arc in new List<Arc>(state.OpenArcs()))
        {
            if (arc.Kind is not (ArcKind.Famine or ArcKind.Plague)) continue;
            if (touchedThisYear.Contains(arc.Id)) continue;
            if (arc.Sides.Count == 0) continue;

            Place place = state.PlaceOf(arc.Sides[0]);

            // A famine is over when there is grain again. A plague is over when a year passes
            // without it — it burns out rather than being fed.
            if (arc.Kind == ArcKind.Famine && place.Stockpile[(int)Resource.Grain] <= 0) continue;

            // People come back — but only to somewhere that can actually feed them. Returning
            // them unconditionally set up a cycle: the mining sites starve, empty, recover on
            // paper, refill, and starve again, which put the same two famine lines in the log
            // seven times in one run.
            bool canFeedThem = place.YieldOf(Resource.Grain)
                               > place.Population / tick.Config.ConsumptionDivisor;

            int returning = 0;
            if (!canFeedThem) { EmitRecovery(tick, arc, place, 0); continue; }
            foreach (Place other in state.HoldingsOf(place.Controller))
            {
                if (other.Id == place.Id) continue;
                int spare = other.Population - other.Population * 9 / 10;
                if (spare > 0) returning += Math.Min(spare, place.Population / 6);
            }

            EmitRecovery(tick, arc, place, returning);
        }
    }

    /// <summary>Closes a famine or a pestilence, moving any returning people home with it.</summary>
    private static void EmitRecovery(Tick tick, Arc arc, Place place, int returning)
    {
        EventDraft draft = new EventDraft(
                arc.Kind == ArcKind.Famine ? EventKind.EconomyFamineEnds : EventKind.EconomyPlagueEnds)
            .At(place.Id)
            .By(place.Controller)
            .Set("years", tick.Year - arc.StartYear)
            .Set("returned", returning)
            .EndArc(arc.Id)
            .InArc(arc.Id)
            .Because(arc.Origin)
            .Weight(Significance.Minor);

        int left = returning;
        if (left > 0)
        {
            draft.Pop(place.Id, left);
            foreach (Place other in tick.State.HoldingsOf(place.Controller))
            {
                if (other.Id == place.Id) continue;
                int share = Math.Min(other.Population / 10, left);
                if (share > 0) { draft.Pop(other.Id, -share); left -= share; }
                if (left <= 0) break;
            }
        }

        tick.Emit(draft);
    }

    private static void RetireGoals(Tick tick)
    {
        WorldState state = tick.State;

        foreach (Goal goal in state.Goals.Snapshot())
        {
            bool ownerGone = goal.Owner.Kind == EntityKind.Actor && !state.ActorOf(goal.Owner).IsAlive;
            if (ownerGone || goal.Progress >= 100 || tick.Year > goal.ExpiresYear)
                state.Goals.Remove(goal);
        }
    }
}
