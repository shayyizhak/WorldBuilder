using System.Globalization;

namespace WorldBuilder.Core.Rules;

/// <summary>
/// Phase 5 — the storylines opened in earlier phases advance a year: wars fight, plots ripen
/// or are discovered, exhausted belligerents make peace.
///
/// Keeping resolution separate from action is what lets a war be a *thing that lasts* rather
/// than a single event. It is also why the log can name it: twenty battles under one arc read
/// as a war, twenty battles without one read as noise.
/// </summary>
public static class ResolutionPhase
{
    public static void Run(Tick tick)
    {
        // The plots this tick will actually consider. Taken before the loop so that the ones it
        // will not consider can be named — a plot the resolver never looked at and a plot it
        // looked at and declined to advance are otherwise indistinguishable from outside.
        AccountForUnexamined(tick);

        foreach (Arc arc in Snapshot(tick))
        {
            switch (arc.Kind)
            {
                case ArcKind.War: ProsecuteWar(tick, arc); break;
                case ArcKind.Plot: RipenPlot(tick, arc); break;
                default: break;
            }
        }
    }

    /// <summary>
    /// Every plot arc the tick will skip, with the reason it is out of reach.
    ///
    /// Diagnostic only, and it runs whether or not a ledger is attached to nothing — with no
    /// ledger it returns immediately, so an ordinary run pays a null check per year and the
    /// simulation is bit-for-bit what it was.
    /// </summary>
    private static void AccountForUnexamined(Tick tick)
    {
        if (tick.Ledger is null) return;

        foreach (Arc arc in tick.State.Arcs)
        {
            if (arc.Kind != ArcKind.Plot) continue;

            if (arc.IsOpen)
            {
                // It is open, so the loop below will reach it. Nothing to record here.
                continue;
            }

            tick.Ledger.NotExamined(arc.Id, tick.Year,
                $"the arc was closed in {arc.EndYear?.ToString(CultureInfo.InvariantCulture) ?? "?"} " +
                "and open arcs are the only ones the resolver iterates");
        }
    }

    private static List<Arc> Snapshot(Tick tick)
    {
        List<Arc> open = [];
        foreach (Arc arc in tick.State.OpenArcs()) open.Add(arc);
        return open;
    }

    // ---- war --------------------------------------------------------------

    private static void ProsecuteWar(Tick tick, Arc arc)
    {
        WorldState state = tick.State;
        if (arc.Sides.Count < 2) return;

        EntityId aggressorId = arc.Sides[0];
        EntityId defenderId = arc.Sides[1];

        Faction aggressor = state.FactionOf(aggressorId);
        Faction defender = state.FactionOf(defenderId);

        // A faction with nothing left is not at war, it is finished.
        //
        // The reason names which of the two it was. A bare "(collapse)" said only that someone
        // had collapsed, and a renderer read it as the *other* side collapsing — the side the
        // section happened to be about — losing the destruction of a founding power in the
        // process. A reason code that refers to one party has to say which.
        if (state.HoldingsOf(aggressorId).Count == 0 || state.HoldingsOf(defenderId).Count == 0)
        {
            Faction finished = state.HoldingsOf(aggressorId).Count == 0 ? aggressor : defender;
            SignPeace(tick, arc, aggressor, defender, $"after the collapse of {finished.Name}");
            return;
        }

        int years = tick.Year - arc.StartYear;
        Rng rng = tick.Rng(arc.Id, RngPurpose.Battle);

        if (years >= tick.Config.MinWarYears && WantsPeace(tick, arc, aggressor, defender, ref rng))
        {
            SignPeace(tick, arc, aggressor, defender, "both sides exhausted");
            return;
        }

        FightBattle(tick, arc, aggressor, defender, ref rng);
    }

    private static bool WantsPeace(Tick tick, Arc arc, Faction a, Faction b, ref Rng rng)
    {
        int years = tick.Year - arc.StartYear;
        int exhaustion = years * 9;

        if (a.Treasury < 30 || b.Treasury < 30) exhaustion += 25;
        if (a.Legitimacy < 30 || b.Legitimacy < 30) exhaustion += 20;

        int ratio = tick.State.PowerOf(a.Id) * 100 / Math.Max(1, tick.State.PowerOf(b.Id));
        if (ratio is > 260 or < 38) exhaustion += 30; // one side has plainly won

        return rng.Next(100) < Math.Min(85, exhaustion);
    }

    private static void FightBattle(Tick tick, Arc arc, Faction aggressor, Faction defender, ref Rng rng)
    {
        WorldState state = tick.State;

        Place field = ChooseField(tick, state, defender.Id);

        // A war that keeps fighting over the same ground has to eventually break it. Each
        // battle already fought here makes a storming likelier, so a grinding campaign ends in
        // a conquest rather than reprinting the same line for eight years.
        int foughtHere = Recent.CountEver(tick, field.Id, EventKind.ConflictBattle);
        Actor? attackLeader = LivingLeader(state, aggressor);
        Actor? defendLeader = LivingLeader(state, defender);

        // Allies turn up. This is what makes an alliance worth signing and worth citing: a
        // coalition can beat a power none of its members could face alone, which is the whole
        // point of a balance of power.
        (int attackHelp, EventId attackPact) = AlliedSupport(tick, aggressor.Id, defender.Id);
        (int defenceHelp, EventId defencePact) = AlliedSupport(tick, defender.Id, aggressor.Id);

        int attack = state.PowerOf(aggressor.Id) * 2 + attackHelp
                     + (attackLeader?.Traits.Martial ?? 20) + rng.Next(60);
        int defence = state.PowerOf(defender.Id) * 2 + defenceHelp
                      + (defendLeader?.Traits.Martial ?? 20) + 15 + rng.Next(60);

        bool attackerWon = attack > defence;
        int margin = Math.Abs(attack - defence);

        EntityId winner = attackerWon ? aggressor.Id : defender.Id;
        EntityId loser = attackerWon ? defender.Id : aggressor.Id;

        int dead = Math.Min(field.Population / 6, 40 + margin);
        int cost = Math.Min(30, 8 + margin / 6);

        EventDraft draft = new EventDraft(EventKind.ConflictBattle)
            .Subject(attackerWon ? attackLeader?.Id ?? EntityId.None : defendLeader?.Id ?? EntityId.None)
            .Object(attackerWon ? defendLeader?.Id ?? EntityId.None : attackLeader?.Id ?? EntityId.None)
            .By(winner)
            .At(field.Id)
            // The beaten side is a participant, not just a payload field.
            //
            // It was recorded only in "loserFaction", which no index reads, so a battle was
            // invisible to every lookup about the power that lost it: absent from its dossier,
            // absent from its pack, absent from its statistics. A faction's history therefore
            // contained its victories and none of its defeats, and the chronicle inherited that
            // — a war reported without the three losses that decided it, and a power destroyed
            // in two battles that its own section never mentions. Defeats are the half of a
            // history that is hardest to see and most worth keeping.
            .Bystander(loser)
            .Set("loserFaction", loser)
            .Set("margin", margin)
            .Set("dead", dead)
            .Pop(field.Id, -dead)
            .Treas(aggressor.Id, -cost)
            .Treas(defender.Id, -cost)
            .Leg(winner, 3)
            .Leg(loser, -4)
            .Rel(loser, winner, RelationKind.Grievance, 10)
            .InArc(arc.Id)
            .Because(arc.Origin)
            .Because(attackerWon ? attackPact : defencePact)
            .Weight(Significance.Major);

        if (attackHelp > 0 || defenceHelp > 0)
            draft.Set("allied", attackerWon ? attackHelp : defenceHelp);

        Event battle = tick.Emit(draft);

        // A decisive win takes ground. Anything less just kills people, which is realistic
        // and — more to the point — keeps wars from resolving the map in a single year.
        //
        // Whether the ground can be *held* is a separate question from whether it can be taken,
        // and until now nothing asked it: a decisive win converted at the same rate whether the
        // field was the next valley or the far side of the map. Distance from the winner's own
        // holdings is the whole of the difference between a conquest and a raid that stayed.
        //
        // This is the one change of the four that adds a branch rather than re-weighting one. A
        // decisive victory over a distant field now sometimes ends as a battle and nothing more,
        // where before it nearly always ended as a conquest — so the far case has an outcome it
        // did not have. Same calibration rule as the others: at a typical separation the
        // conversion chance is exactly what it was.
        int holdable = state.Geo?.FromFactionToPlace(aggressor.Id, field.Id) ?? Geography.Geography.Neutral;

        if (attackerWon && margin > 25 - foughtHere * 6
            && rng.Chance(Math.Min(90, (45 + foughtHere * 18) * holdable / 100))
            && field.Controller == defender.Id)
        {
            tick.Emit(new EventDraft(EventKind.ConflictConquest)
                .Subject(attackLeader?.Id ?? EntityId.None)
                .By(aggressor.Id)
                .Object(defender.Id)
                .At(field.Id)
                .Set("mode", "storm")
                .Leg(aggressor.Id, 5)
                .Leg(defender.Id, -8)
                .Rel(defender.Id, aggressor.Id, RelationKind.Grievance, 25)
                .InArc(arc.Id)
                .Because(battle.Id)
                .Weight(Significance.Major));
        }
    }

    private static void SignPeace(Tick tick, Arc arc, Faction a, Faction b, string reason)
    {
        WorldState state = tick.State;
        int grievanceA = state.Relations.ValueOf(a.Id, b.Id, RelationKind.Grievance);
        int grievanceB = state.Relations.ValueOf(b.Id, a.Id, RelationKind.Grievance);

        tick.Emit(new EventDraft(EventKind.DiploPeaceSigned)
            .By(a.Id)
            .Object(b.Id)
            .At(b.Seat)
            .Set("with", b.Id)
            .Set("reason", reason)
            .Set("years", tick.Year - arc.StartYear)
            // Peace settles a third of the grudge. The rest is still on the books, which is
            // why the same two names come back to blows a generation later.
            .Rel(a.Id, b.Id, RelationKind.Grievance, -grievanceA / 3)
            .Rel(b.Id, a.Id, RelationKind.Grievance, -grievanceB / 3)
            .Leg(a.Id, 2)
            .Leg(b.Id, 2)
            .InArc(arc.Id)
            .Because(arc.Origin)
            .Weight(Significance.Major));
    }

    /// <summary>
    /// Where this year's fighting happens. The richest holding is the natural objective, but
    /// once a place has been fought over twice the campaign moves on if there is anywhere else
    /// to go — armies do not besiege the same field forever, and a log that says they do reads
    /// like a stuck record rather than a war.
    /// </summary>
    private static Place ChooseField(Tick tick, WorldState state, EntityId owner)
    {
        List<Place> holdings = state.HoldingsOf(owner);
        Place best = holdings[0];
        int bestScore = int.MinValue;

        foreach (Place p in holdings)
        {
            int score = p.YieldOf(Resource.Ore) * 4 + p.Population / 50;
            score -= Recent.CountEver(tick, p.Id, EventKind.ConflictBattle) * 25;
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    /// <summary>
    /// Strength lent by allies who are not themselves friendly with the enemy, and the pact
    /// that obliged them. Returns the pact so the battle can name it: an alliance that never
    /// appears as the cause of anything is a handshake, not a treaty.
    /// </summary>
    private static (int Strength, EventId Pact) AlliedSupport(Tick tick, EntityId side, EntityId against)
    {
        WorldState state = tick.State;
        int strength = 0;
        EventId pact = EventId.None;

        foreach (Relation ally in state.Relations.From(side, RelationKind.Alliance))
        {
            EntityId friend = ally.Key.To;
            if (friend == against) continue;
            if (state.IsDefunct(friend)) continue;
            if (state.Relations.Has(friend, against, RelationKind.Alliance)) continue;

            strength += state.PowerOf(friend);
            if (pact.IsNone) pact = ally.Cause;
        }

        return (strength, pact);
    }

    private static Actor? LivingLeader(WorldState state, Faction faction) =>
        faction.Leader.IsNone || !state.ActorOf(faction.Leader).IsAlive ? null : state.ActorOf(faction.Leader);

    // ---- plots ------------------------------------------------------------

    /// <summary>
    /// A conspiracy ends because its author is dead. Distinct from a lapse on purpose: they are
    /// different endings, and folding both into one opaque "abandoned" outcome meant the log
    /// could not say which had happened — so nobody, and no later query, could ask.
    /// </summary>
    internal static void PlotDiesWithPlotter(Tick tick, Arc arc, EventId death)
    {
        Event origin = tick.Log.Get(arc.Origin);

        tick.Emit(new EventDraft(EventKind.PolityPlotDiesWithPlotter)
            .Subject(origin.Subject)
            .Object(origin.Object)
            .By(origin.Faction)
            .Set("years", tick.Year - arc.StartYear)
            .Resolved(Outcome.Failed)
            // A conspiracy that ended without ever being uncovered is still a secret. Left
            // public, the chronicle reported plots nobody in the world had heard of.
            .Hidden(origin.Scope)
            .EndArc(arc.Id)
            .InArc(arc.Id)
            .Because(arc.Origin)
            .Because(death)
            .Weight(Significance.Minor));
    }

    /// <summary>A conspiracy that ran out of time, or of anything left to conspire against.</summary>
    internal static void PlotLapses(Tick tick, Arc arc, string reason)
    {
        Event origin = tick.Log.Get(arc.Origin);

        tick.Emit(new EventDraft(EventKind.PolityPlotLapses)
            .Subject(origin.Subject)
            .Object(origin.Object)
            .By(origin.Faction)
            .Set("reason", reason)
            .Set("years", tick.Year - arc.StartYear)
            .Resolved(Outcome.Failed)
            // Inherits the plot's secrecy. Only discovery makes a conspiracy public, and a plot
            // that merely ran out of time was never discovered by anyone.
            .Hidden(origin.Scope)
            .EndArc(arc.Id)
            .InArc(arc.Id)
            .Because(arc.Origin)
            .Weight(Significance.Minor));
    }

    /// <summary>The death event of an actor, for a plot that died with them.</summary>
    private static EventId DeathOf(Tick tick, EntityId actor)
    {
        IReadOnlyList<EventId> history = tick.Log.ForEntity(actor);
        for (int i = history.Count - 1; i >= 0; i--)
        {
            Event e = tick.Log.Get(history[i]);
            if (e.Kind is EventKind.LifeDeathNatural or EventKind.LifeDeathViolent && e.Subject == actor)
                return history[i];
        }
        return EventId.None;
    }

    /// <summary>
    /// The conspiracy lands: the plotter takes the seat.
    ///
    /// The branch that did not exist. The sole emitter of <c>POLITY.COUP_RESOLVED</c> hard-coded
    /// <c>mode: exposed</c> and <c>Outcome.Failed</c>, so <c>won</c> and <c>lost</c> were
    /// unreachable and the success rate was a constant rather than a rate — while the renderer
    /// already carried a sentence for a covert win and the audit already carried a counter for
    /// one. Both were built expecting this.
    ///
    /// <b>It moves the seat.</b> A win that did not would be a cosmetic event: a log line saying
    /// power changed hands beside a world in which it did not. <see cref="ActionPhase.SettleCoup"/>
    /// is the same path an open challenge takes, so the succession, the loser's death or exile
    /// and the legitimacy cost all follow exactly as they do for a challenge won in daylight.
    /// </summary>
    private static void Seize(Tick tick, Arc arc, Actor plotter, Actor target, Faction faction, int age)
    {
        Event seized = tick.Emit(new EventDraft(EventKind.PolityCoupResolved)
            .Subject(plotter.Id)
            .Object(target.Id)
            .By(faction.Id)
            .At(faction.Seat)
            .Set("mode", "seized")
            .Set("plotYears", age)
            .Resolved(Outcome.Succeeded)
            .Leg(faction.Id, -10)
            .InArc(arc.Id)
            .Because(arc.Origin)
            .Weight(Significance.Major));

        // The goal is spent either way: the man either has the seat or has been caught trying.
        Goal? goal = tick.State.Goals.Find(plotter.Id, GoalKind.SeizeLeadership);
        if (goal is not null) tick.State.Goals.Remove(goal);

        ActionPhase.SettleCoup(tick, faction, winner: plotter, loser: target, seized);
    }

    private static void RipenPlot(Tick tick, Arc arc)
    {
        WorldState state = tick.State;
        Event origin = tick.Log.Get(arc.Origin);

        EntityId plotterId = origin.Subject;
        if (plotterId.IsNone || origin.Faction.IsNone)
        {
            tick.Ledger?.Examined(arc.Id, tick.Year, "the thread is lost", terminal: true);
            PlotLapses(tick, arc, "the thread is lost");
            return;
        }

        Actor plotter = state.ActorOf(plotterId);

        if (!plotter.IsAlive)
        {
            tick.Ledger?.Examined(arc.Id, tick.Year, "the plotter is dead", terminal: true);
            PlotDiesWithPlotter(tick, arc, DeathOf(tick, plotterId));
            return;
        }

        // Lifespan is checked before anything that can defer, and that ordering is load-bearing.
        //
        // A conspiracy is not immortal, and every gate below this one can return without ending
        // the plot — so a plot whose seat never refills, because the house it aimed at was
        // destroyed, would be examined every year forever and terminate never. The engine's own
        // "every matured plot terminates exactly once" caught that within a minute of the seat
        // rework landing. A deferral that can repeat indefinitely must sit behind a terminator.
        if (tick.Year - arc.StartYear >= tick.Config.PlotLifespan)
        {
            tick.Ledger?.Examined(arc.Id, tick.Year, "nothing came of it (lifespan reached)", terminal: true);
            PlotLapses(tick, arc, "nothing came of it");
            return;
        }

        // A conspiracy is a bid for a seat, not a vendetta against the man in it.
        //
        // Keyed on the incumbent, an unrelated murder voided the plot — and that gate alone
        // consumed 82 of 109 lapses across the panel, with "the target no longer holds the seat"
        // second for the same reason. Both were the same mistake: a plot modelled as a personal
        // grudge rather than as a play for power. Attached to the seat, the incumbent's death
        // stops being fatal to the conspiracy and becomes its opening, which is better history
        // and fits the architecture — properties, not identities. The plot targets the seat of
        // f:2, not a:50.
        Faction faction = state.FactionOf(origin.Faction);
        EntityId seatHolder = faction.Leader;

        if (seatHolder.IsNone)
        {
            // Nothing to seize this year. Not an ending: a house without a ruler will have one
            // again, and the conspiracy is still waiting for it.
            tick.Ledger?.Examined(arc.Id, tick.Year, "the seat stands empty this year");
            return;
        }

        // Succeeded by other means. It ends the plot and is emphatically not a covert win —
        // recorded under its own reason so the two can never be added together.
        if (seatHolder == plotterId)
        {
            tick.Ledger?.Examined(arc.Id, tick.Year, "the plotter took the seat by other means", terminal: true);
            PlotLapses(tick, arc, "its author took the seat by other means");
            return;
        }

        Actor target = state.ActorOf(seatHolder);

        // A plot gets at least one year to be a plot. Resolution runs in the same tick that
        // action does, so without this the log kept reporting conspiracies "uncovered after
        // 0 years" — born and buried in the same sentence.
        int age = tick.Year - arc.StartYear;
        if (age < 1)
        {
            tick.Ledger?.Examined(arc.Id, tick.Year, "too young to ripen this year");
            return;
        }

        // Expose, strike, or wait another year.
        //
        // Age fed exposure and nothing else, so for a plotter time was pure downside: every year
        // raised the chance of being caught and never raised the chance of striking. The plot's
        // only reachable fates were exposure and lapse. That is not a race the conspiracy usually
        // loses — it is a race with a finish line on one side only.
        //
        // Readiness now rises alongside exposure. The constants are reasoned rather than fitted,
        // and are deliberately left where the reasoning puts them:
        //
        //   exposure   unchanged from the ruleset-1 formula, so this step's effect can be
        //              attributed. 8 base, +6 a year, worse for a clumsy plotter, capped at 70.
        //   readiness  4 base — a conspiracy in its second year is not yet a coup — and +5 a
        //              year, slightly under exposure's +6 so that patience stays dangerous
        //              rather than free. Guile adds up to 20: the craft that hides a plot is the
        //              craft that lands it.
        //   counterweight  the incumbent's own following, and the legitimacy of the house behind
        //              him. A well-supported ruler in a stable house is hard to remove quietly,
        //              which is the same quantity the open challenge already weighs.
        Rng rng = tick.Rng(arc.Id, RngPurpose.Coup);

        int leak = Math.Min(70, 8 + age * 6 + (100 - plotter.Traits.Guile) / 4);

        int guard = (ActionPhase.Support(state, target.Id) + faction.Legitimacy / 2) / 4;
        int strike = Math.Clamp(4 + age * 5 + plotter.Traits.Guile / 5 - guard, 0, 60);

        // One draw, so the two outcomes are mutually exclusive by construction rather than by a
        // second roll that could contradict the first.
        int roll = rng.Next(100);

        if (roll < strike)
        {
            tick.Ledger?.Examined(arc.Id, tick.Year, "seized the seat", terminal: true);
            Seize(tick, arc, plotter, target, faction, age);
            return;
        }

        if (roll >= strike + leak)
        {
            tick.Ledger?.Examined(arc.Id, tick.Year, "neither ripe nor uncovered this year");
            return;
        }

        tick.Ledger?.Examined(arc.Id, tick.Year, "exposed", terminal: true);

        Event exposed = tick.Emit(new EventDraft(EventKind.PolityCoupResolved)
            .Subject(plotter.Id)
            .Object(target.Id)
            .By(faction.Id)
            .At(faction.Seat)
            .Set("mode", "exposed")
            .Set("plotYears", age)
            .Resolved(Outcome.Failed)
            .Leg(faction.Id, -5)
            .Rel(target.Id, plotter.Id, RelationKind.Grievance, 40)
            .InArc(arc.Id)
            .Because(arc.Origin)
            .Weight(Significance.Major));

        Goal? goal = state.Goals.Find(plotter.Id, GoalKind.SeizeLeadership);
        if (goal is not null) state.Goals.Remove(goal);

        // A house can only put its own to death. Where the conspirator has already gone — and
        // an uncovering routinely lands years after the flight — the reach it actually has is
        // to declare him outlaw, which is what Exile emits for a man who is no longer a member.
        // Executing him instead had a power killing people in another house's service.
        if (plotter.Faction == faction.Id && rng.Chance(30))
        {
            tick.Emit(new EventDraft(EventKind.LifeDeathViolent)
                .Subject(plotter.Id)
                .Object(target.Id)
                .By(faction.Id)
                .At(faction.Seat)
                .Set("age", plotter.AgeAt(tick.Year))
                .Set("reason", "executed for conspiracy")
                .Because(exposed.Id)
                .InArc(arc.Id)
                .Weight(Significance.Major));
        }
        else
        {
            ActionPhase.Exile(tick, plotter, faction.Id, exposed.Id, "conspiracy against the seat");
        }
    }
}
