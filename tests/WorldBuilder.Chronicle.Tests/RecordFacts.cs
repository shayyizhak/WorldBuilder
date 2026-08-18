using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Chronicle.Tests;

/// <summary>One person's hold on one seat, and how it ended.</summary>
public sealed record Held(EntityId Ruler, EntityId Faction, int From, int To, string Ended, EntityId EndedBy);

/// <summary>Raids as the record has them, split the three ways a reader distinguishes.</summary>
public sealed record RaidTally(int BeatenOff, int TookAHaul, int TookNothing)
{
    public int Total => BeatenOff + TookAHaul + TookNothing;
}

/// <summary>
/// What the record says about a window, computed from the event log alone.
///
/// <b>This deliberately duplicates the checker.</b> The checker decides what enters canon; this
/// decides whether the checker works, and a checker that has silently stopped firing is invisible
/// to anything that shares its implementation. Nothing here calls into WorldBuilder.Inference —
/// it cannot, because this assembly does not reference it — and every figure below is derived
/// from <see cref="EventLog"/> a second time, on purpose.
///
/// Read from the record, never from the readable view. The <c>.log</c> hides the yearly accounts,
/// which is where most of the economy's influence lives and where three separate reviews went
/// wrong.
/// </summary>
public static class RecordFacts
{
    /// <summary>
    /// Everyone who held a seat, in order, with how their hold ended.
    ///
    /// Three sources, and the third is the one that was missed until round 8: a secession names
    /// the founding holder of the house it creates, and a ruler list built from successions alone
    /// leaves every founder invisible.
    /// </summary>
    public static List<Held> SeatHistory(WorldView view, EntityId faction)
    {
        List<(int Year, EntityId Ruler)> took = [];

        foreach (Event e in view.Log.Events)
        {
            switch (e.Kind)
            {
                case EventKind.PolitySuccession when e.Faction == faction:
                // A challenge the challenger won moves the seat; one they lost does not.
                case EventKind.PolityChallenge when e.Faction == faction && e.Outcome == Outcome.Succeeded:
                    if (!e.Subject.IsNone) took.Add((e.Year, e.Subject));
                    break;

                // The founding holder of a house that broke away.
                //
                // A secession names the parent as its Faction and the new house as a bystander,
                // which is the opposite of the obvious reading and the reason this source was
                // missed until round 8. Taking Faction here would credit the founder of every
                // breakaway to the house it left.
                case EventKind.PolitySecession when NewHouse(e) == faction:
                    if (!e.Subject.IsNone) took.Add((e.Year, e.Subject));
                    break;
            }
        }

        took.Sort(static (a, b) => a.Year.CompareTo(b.Year));

        // One hold, however many records moved the seat.
        //
        // A contested transfer emits two: the challenge that decided it, and a POLITY.SUCCESSION
        // beside it carrying the state change. Reading both as separate holds put the same man on
        // the same seat twice in the same year — "Pouldrir Ho 15–15, Pouldrir Ho 15–20" — which is
        // not a ruler list, and §7 says this layer verifies ruler lists. It did not break any
        // assertion here, because every assertion was about the partition rather than about the
        // list, which is its own small lesson about what "verified" was covering.
        //
        // The year decides, not adjacency. Collapsing any two neighbouring appearances by one
        // person, whatever their years, also deletes a genuine second tenure — the same man back
        // on the same seat with nobody recorded between — and "no duplicate in the list" is
        // satisfied by both the correct collapse and that deletion. Derived here a second time
        // rather than shared with the engine's copy, like everything else in this file.
        List<(int Year, EntityId Ruler)> distinct = [];
        foreach ((int year, EntityId ruler) in took)
            if (distinct.Count == 0 || distinct[^1].Ruler != ruler || distinct[^1].Year != year)
                distinct.Add((year, ruler));

        List<Held> spells = [];

        for (int i = 0; i < distinct.Count; i++)
        {
            int from = distinct[i].Year;
            int to = i + 1 < distinct.Count ? distinct[i + 1].Year : view.LastYear;

            (string ended, EntityId by) = HowItEnded(view, faction, distinct[i].Ruler, from, to,
                last: i + 1 == distinct.Count);

            spells.Add(new Held(distinct[i].Ruler, faction, from, to, ended, by));
        }

        return spells;
    }

    /// <summary>
    /// How a hold ended: killed, cast out, died naturally, or still holding.
    ///
    /// The partition must be exhaustive. A departure that falls through every branch would read
    /// as a natural death, which is the quietest possible way to be wrong about a murder.
    /// </summary>
    private static (string, EntityId) HowItEnded(
        WorldView view, EntityId faction, EntityId ruler, int from, int to, bool last)
    {
        foreach (Event e in view.Log.Events)
        {
            if (e.Year < from || e.Year > to) continue;

            if (e.Kind == EventKind.LifeDeathViolent && e.Subject == ruler)
                return ("killed", e.Object);

            if (e.Kind == EventKind.PolityExile && e.Subject == ruler && e.Faction == faction)
                return ("cast out", EntityId.None);

            if (e.Kind == EventKind.LifeDeathNatural && e.Subject == ruler)
                return ("died", EntityId.None);
        }

        return last ? ("still holding", EntityId.None) : ("replaced", EntityId.None);
    }

    /// <summary>Raids this faction sent inside the window, split three ways.</summary>
    public static RaidTally RaidsSent(WorldView view, EntityId faction, int from, int to)
    {
        int beaten = 0, haul = 0, empty = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid || e.Faction != faction) continue;
            if (e.Year < from || e.Year > to) continue;

            if (e.Outcome != Outcome.Succeeded) { beaten++; continue; }
            (Took(e) > 0 ? ref haul : ref empty)++;
        }

        return new RaidTally(beaten, haul, empty);
    }

    /// <summary>
    /// Raids this faction suffered, with ownership resolved at the time of the event.
    ///
    /// Resolving it from the final world is the error this exists to avoid: a town changes hands,
    /// and every raid on it through the whole century is then attributed to whoever holds it at
    /// the end.
    /// </summary>
    public static RaidTally RaidsSuffered(WorldView view, EntityId faction, int from, int to)
    {
        int beaten = 0, haul = 0, empty = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictRaid) continue;
            if (e.Year < from || e.Year > to) continue;

            bool aimedHere = e.Object == faction
                || (!e.Where.IsNone && HeldBy(view, e.Where, e.Year) == faction);

            if (!aimedHere) continue;

            if (e.Outcome != Outcome.Succeeded) { beaten++; continue; }
            (Took(e) > 0 ? ref haul : ref empty)++;
        }

        return new RaidTally(beaten, haul, empty);
    }

    /// <summary>Who held a place in a given year, by replaying conquests up to that year.</summary>
    public static EntityId HeldBy(WorldView view, EntityId place, int year)
    {
        EntityId owner = EntityId.None;

        foreach (Event e in view.Log.Events)
        {
            if (e.Year > year) break;
            if (e.Where != place) continue;

            if (e.Kind is EventKind.ConflictConquest or EventKind.PolitySecession && !e.Faction.IsNone)
                owner = e.Faction;
        }

        return owner.IsNone ? view.State.PlaceOf(place).Controller : owner;
    }

    /// <summary>Battles in the window involving this faction, won and lost.</summary>
    public static (int Won, int Lost) Battles(WorldView view, EntityId faction, int from, int to)
    {
        int won = 0, lost = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictBattle) continue;
            if (e.Year < from || e.Year > to) continue;

            // Winner is the Faction, loser the bystanding house. Object names the losing
            // commander, who is a person — reading the loser from there counts no battles at all
            // and reports a panel in which every battle was won by somebody and lost by nobody.
            if (e.Faction == faction) { won++; continue; }
            if (NewHouse(e) == faction) lost++;
        }

        return (won, lost);
    }

    /// <summary>The bystanding faction on an event: the new house on a secession, the loser on a battle.</summary>
    public static EntityId NewHouse(Event e)
    {
        foreach (Participant p in e.Participants)
            if (p.Role == Role.Bystander && p.Id.Kind == EntityKind.Faction) return p.Id;

        return EntityId.None;
    }

    /// <summary>
    /// Killings in the window, split by whether killer and victim shared a house at the time.
    ///
    /// Classified at the time of the event and not from the final world, for the same reason
    /// raids are.
    /// </summary>
    public static (int Internal, int External) Killings(WorldView view, EntityId faction, int from, int to)
    {
        int inside = 0, outside = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.LifeDeathViolent) continue;
            if (e.Year < from || e.Year > to) continue;
            if (e.Faction != faction && !Serves(view, e.Subject, faction, e.Year)) continue;

            if (Serves(view, e.Object, faction, e.Year)) inside++;
            else outside++;
        }

        return (inside, outside);
    }

    /// <summary>Marriages in the window counted against the first-named party, stated as the convention.</summary>
    public static int Marriages(WorldView view, EntityId faction, int from, int to)
    {
        int count = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.LifeMarriage) continue;
            if (e.Year < from || e.Year > to) continue;
            if (e.Faction == faction || Serves(view, e.Subject, faction, e.Year)) count++;
        }

        return count;
    }

    /// <summary>Every year the record attaches to an event naming this faction, inside the window.</summary>
    public static HashSet<int> YearsNamed(WorldView view, EntityId faction, int from, int to)
    {
        HashSet<int> years = [];

        foreach (Event e in view.Log.Events)
        {
            if (e.Year < from || e.Year > to) continue;
            foreach (Participant p in e.Participants)
                if (p.Id == faction) years.Add(e.Year);
        }

        return years;
    }

    /// <summary>Every proper name the world contains, lower-cased, as single words.</summary>
    public static HashSet<string> AllNameWords(WorldView view)
    {
        HashSet<string> words = new(StringComparer.OrdinalIgnoreCase);

        void Add(string name)
        {
            foreach (string word in name.Split([' ', '\'', '-'], StringSplitOptions.RemoveEmptyEntries))
                words.Add(word.Trim('.', ',', ';', ':').ToLowerInvariant());
        }

        foreach (Actor a in view.State.Actors) Add(a.Name);
        foreach (Place p in view.State.Places) Add(p.Name);
        foreach (Faction f in view.State.Factions) Add(f.Name);
        foreach (Arc a in view.State.Arcs) Add(a.Name);

        return words;
    }

    private static bool Serves(WorldView view, EntityId actor, EntityId faction, int year)
    {
        if (actor.IsNone || actor.Kind != EntityKind.Actor) return false;

        EntityId serving = EntityId.None;

        foreach (Event e in view.Log.Events)
        {
            if (e.Year > year) break;
            if (e.Subject != actor) continue;

            if (e.Kind is EventKind.PolitySuccession or EventKind.PolitySecession
                or EventKind.PolityExileReturn or EventKind.PolityAppointment && !e.Faction.IsNone)
            {
                serving = e.Faction;
            }

            if (e.Kind == EventKind.PolityExile && e.Faction == serving) serving = EntityId.None;
        }

        return serving == faction;
    }

    /// <summary>
    /// What a raid carried off, from the key the engine actually writes.
    ///
    /// <b>The silent-path family, inside the layer that exists to catch it.</b> This read
    /// <c>took</c>, <c>haul</c> and <c>plunder</c>; the engine has only ever written <c>loot</c>.
    /// So every successful raid came back as zero and Layer 4's three-way raid split has been a
    /// two-way one since it was written — the partition still summed, the totals still matched the
    /// record, and nothing failed. A correct rule whose input never arrives, presenting as a pass,
    /// for the eighth recorded time.
    ///
    /// Read against the key list rather than one string, because the list was the defect: three
    /// plausible names and not the real one is what a lexicon assembled from memory looks like. It
    /// is the record that decides, so <c>loot</c> is asserted to be present and non-zero somewhere
    /// by <c>RaidTalliesPartitionTheRaids</c> — absence of failure is not extraction.
    /// </summary>
    private static int Took(Event e) => e.GetInt("loot");
}
