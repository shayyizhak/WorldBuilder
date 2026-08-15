using System.Globalization;

namespace WorldBuilder.Core.Rendering;

/// <summary>
/// Turns an event into one English sentence by static template.
///
/// There is no cleverness here and there is deliberately no model. If the history only reads
/// well because the prose is doing the work, the simulation has failed the one question v0
/// exists to answer — so the sentences are kept flat on purpose and every name carries its id.
/// </summary>
public static class EventTemplates
{
    public static string Describe(WorldState state, Event e)
    {
        string subject = Label(state, e.Subject);
        string obj = Label(state, e.Object);
        string place = Label(state, e.Where);
        string faction = Label(state, e.Faction);

        return e.Kind switch
        {
            EventKind.GenesisWorld =>
                $"the world begins in year {e.GetInt("startYear").ToString(CultureInfo.InvariantCulture)} (seed {e.GetString("seed")})",

            EventKind.GenesisPlace =>
                $"{subject} exists — {e.GetString("placeKind")?.ToLowerInvariant()}, {Num(e, "population")} souls",

            EventKind.GenesisFaction =>
                $"{subject} holds {place}, succession by {e.GetString("succession")?.ToLowerInvariant()}",

            EventKind.GenesisActor =>
                $"{subject} of {faction} — {e.GetString("title")?.ToLowerInvariant()}, born {Num(e, "birthYear")}",

            EventKind.LifeBirth =>
                $"{subject} is born to {obj} at {place}",

            EventKind.LifeComingOfAge =>
                $"{subject} comes of age and enters the service of {faction}",

            EventKind.LifeDeathNatural =>
                $"{subject}, {Rank(state, e.Subject, faction)}, dies at {Num(e, "age")} in {place}",

            EventKind.LifeDeathViolent when e.GetString("reason") is { } reason =>
                $"{subject} is put to death — {reason}",

            EventKind.LifeDeathViolent =>
                $"{subject}, {Rank(state, e.Subject, faction)}, is killed by {obj} at {place}",

            EventKind.LifeMarriage when e.GetInt("crossFaction") == 1 =>
                $"{subject} marries {obj}, binding {faction} to {Label(state, FactionOf(state, e.Object))}",

            EventKind.LifeMarriage =>
                $"{subject} marries {obj} at {place}",

            // Bookkeeping, so normally unseen — but these are legitimate causal roots now that
            // fabricated edges are gone, and they surface in causal traces. Worth a sentence.
            EventKind.EconomyYield when e.GetString("kind") == "drift" =>
                "old grudges cool and standing quietly erodes",

            EventKind.EconomyYield when e.GetString("kind") == "taxes" =>
                "the year's revenues are gathered in",

            EventKind.EconomyYield =>
                "the year's harvest is counted",

            EventKind.EconomyFamine when e.GetInt("left") > 0 && !e.GetEntity("refuge").IsNone =>
                $"famine at {place}, year {Num(e, "years")}: {Num(e, "deaths")} dead and " +
                $"{Num(e, "left")} leave for {Label(state, e.GetEntity("refuge"))}",

            EventKind.EconomyFamine when e.GetInt("left") > 0 =>
                $"famine at {place}, year {Num(e, "years")}: {Num(e, "deaths")} dead, " +
                $"{Num(e, "left")} abandon the place",

            EventKind.EconomyFamine =>
                $"famine at {place}: {Num(e, "deaths")} dead, grain short by {Num(e, "shortfall")}",

            EventKind.EconomyFamineEnds when e.GetInt("returned") > 0 =>
                $"the hunger at {place} passes after {Num(e, "years")} years and " +
                $"{Num(e, "returned")} return to the fields",

            EventKind.EconomyFamineEnds =>
                $"the hunger at {place} passes after {Num(e, "years")} years",

            EventKind.EconomyBumperHarvest =>
                $"a heavy harvest at {place} ({Num(e, "harvestPct")}% of usual)",

            EventKind.EconomyPlague when e.GetInt("left") > 0 =>
                $"plague at {place}, year {Num(e, "years")}: {Num(e, "deaths")} dead and " +
                $"{Num(e, "left")} flee",

            EventKind.EconomyPlague when e.GetInt("years") > 1 =>
                $"plague at {place}, year {Num(e, "years")}: {Num(e, "deaths")} more dead",

            EventKind.EconomyPlague =>
                $"plague breaks out at {place} and takes {Num(e, "deaths")}",

            EventKind.EconomyPlagueEnds =>
                $"the pestilence at {place} burns out after {Num(e, "years")} years",

            EventKind.EconomyTradePact when e.GetString("mode") == "purchase" =>
                $"{faction} buys {Num(e, "grain")} grain from {obj} for {Num(e, "silver")} silver",

            EventKind.EconomyTradePact =>
                $"{faction} and {obj} agree terms of trade",

            EventKind.EconomyTradeCollapse =>
                $"trade between {faction} and {obj} breaks down",

            EventKind.PolitySuccession when e.GetString("reason") == "founding" =>
                $"{subject} founds the line of {faction}",

            // Both parties named, in both outcomes. "the named heir's claim upheld" said what
            // happened without saying to whom, and an upheld claim is the rare case here — the
            // renderer applied the common pattern instead of reading the clause, and reported
            // the heir as the contester and the contester as the heir. The same fix the open
            // challenge got in round 6: whoever won is named, and so is whoever lost.
            EventKind.PolitySuccession when e.GetString("reason") == "the named heir's claim upheld" =>
                $"{subject}, the named heir, takes the seat of {faction}; the rival claim of {obj} is rejected",

            EventKind.PolitySuccession when e.GetString("reason") == "the named heir's claim set aside" =>
                $"{subject} takes the seat of {faction}, setting aside the claim of the named heir {obj}",

            EventKind.PolitySuccession =>
                $"{subject} takes the seat of {faction} ({e.GetString("reason")?.ToLowerInvariant()})",

            EventKind.PolitySuccessionDisputed =>
                $"{obj} contests the claim of {subject}, the named heir, to {faction} " +
                $"(decided by {e.GetString("rule")?.ToLowerInvariant()})",

            EventKind.PolityCoupPlotted =>
                $"{subject} begins conspiring against {obj} of {faction}",

            EventKind.PolityPlotDiesWithPlotter =>
                $"{subject}'s conspiracy against {obj} dies with them, {Num(e, "years")} years in",

            EventKind.PolityPlotLapses =>
                $"{subject}'s conspiracy against {obj} lapses after {Num(e, "years")} years — {e.GetString("reason")}",

            // Both outcomes name the winner. The two lines previously shared a stem and differed
            // only in a trailing clause — "and takes the seat" against "and is beaten" — and a
            // renderer duly inverted one, reporting a challenger who lost as having ended the
            // incumbent's rule. Whoever holds the seat afterwards is now stated outright, so
            // reading the sentence backwards requires ignoring a name rather than a participle.
            EventKind.PolityChallenge when e.Outcome == Outcome.Succeeded =>
                $"{subject} challenges {obj} openly for {faction}, wins, and takes the seat from {obj}",

            EventKind.PolityChallenge =>
                $"{subject} challenges {obj} openly for {faction}, loses, and {obj} keeps the seat",

            EventKind.PolityCoupResolved when e.GetString("mode") == "exposed" =>
                $"{subject}'s conspiracy against {obj} is uncovered after {Num(e, "plotYears")} years",

            EventKind.PolityCoupResolved =>
                $"{subject} challenges {obj} for {faction} and {Won(e)}",

            EventKind.PolityCourtsSupport =>
                $"{subject} wins {obj} away from the ruler of {faction}",

            EventKind.PolityExile when e.GetInt("outlaw") == 1 =>
                $"{subject}, who had already left, is declared outlaw by {faction} — {e.GetString("reason")}",

            EventKind.PolityExile =>
                $"{subject} is cast out of {faction} — {e.GetString("reason")}",

            EventKind.PolityExileReturn =>
                $"{subject} returns from exile and takes service with {faction}",

            EventKind.PolityAppointment when e.GetString("kind") == "absorbed" =>
                "stateless subjects come under the flag that holds their ground",

            EventKind.PolityAppointment =>
                $"{subject} is raised to {e.GetString("title")?.ToLowerInvariant()} of {faction}",

            EventKind.PolityLegitimacyCrisis when e.GetString("response") == "largesse"
                                                 && e.Outcome == Outcome.Failed =>
                $"{faction} spends {Num(e, "spent")} silver buying goodwill for the " +
                $"{Ordinal(e.GetInt("attempt"))} time, and is not believed",

            EventKind.PolityLegitimacyCrisis when e.GetString("response") == "largesse" =>
                $"{faction} spends {Num(e, "spent")} silver to buy back goodwill",

            EventKind.PolityLegitimacyCrisis =>
                $"{faction} has no claimant to its seat",

            // Same reason as the war declaration above: a bare score sitting in a sentence full
            // of years is a date waiting to be invented. The band says what the number meant.
            EventKind.PolityRevolt =>
                $"{place} rises against {faction}, whose standing had fallen {Band(e.GetInt("legitimacy"))}",

            // The founder is named. A breakaway installs its first ruler here and emits no
            // succession event, so a log that left him out gave the reader no way to know who
            // led the new power — a man who held a seat for twelve of his faction's thirty-two
            // years appeared only as an ordinary member being cast out at the end of it.
            EventKind.PolitySecession when !e.Subject.IsNone =>
                $"{place} breaks from {faction} as {e.GetString("name")}, with {subject} taking its seat",

            EventKind.PolitySecession =>
                $"{place} breaks from {faction} as {e.GetString("name")}",

            EventKind.PolityPartition =>
                $"{faction} is divided: {subject} carries off {Num(e, "places")} place(s) as {e.GetString("name")}",

            EventKind.PolityCollapse when e.GetInt("places") == 0 =>
                $"{faction} is finished — landless, its last {Plural(e.GetInt("scattered"), "follower")} " +
                $"{(e.GetInt("scattered") == 1 ? "scatters" : "scatter")}",

            EventKind.PolityCollapse =>
                $"{faction} comes to an end; its {Plural(e.GetInt("places"), "place")} " +
                $"{(e.GetInt("places") == 1 ? "falls" : "fall")} to no one",

            EventKind.DiploInsult =>
                $"{faction} publicly slights {obj}",

            EventKind.DiploTributeDemanded when e.Outcome == Outcome.Succeeded =>
                $"{faction} extracts {Num(e, "demand")} silver in tribute from {obj}",

            EventKind.DiploTributeDemanded =>
                $"{faction} demands tribute of {obj} and is refused",

            EventKind.DiploAllianceFormed when e.Outcome == Outcome.Failed =>
                $"{faction} seeks an alliance with {obj} and is turned away",

            EventKind.DiploAllianceFormed =>
                $"{faction} and {obj} swear an alliance",

            EventKind.DiploAllianceBroken =>
                $"{faction} breaks its alliance with {obj}",

            EventKind.DiploWarDeclared when e.GetEntity("over") is { IsNone: false } over =>
                $"{faction} declares war on {obj} for {Label(state, over)}",

            // No bare score. "(grievance 169)" was written into a passage as "over a grievance
            // from year 169" — a bookkeeping figure read as a date, and a date the world does
            // not contain. The prompt already forbids quoting these; not offering the number is
            // cheaper than forbidding it.
            EventKind.DiploWarDeclared =>
                $"{faction} declares war on {obj} over long-standing grievance",

            EventKind.DiploPeaceSigned =>
                $"{faction} and {obj} make peace after {Num(e, "years")} years, {e.GetString("reason")}",

            // Three outcomes, not two. A raid that got through and took nothing was falling in
            // with the ones that came home laden, because the only distinction the log drew was
            // beaten off or not — so "carrying off 0 ore" was read, reasonably, as plunder. A
            // raid that reached its target, killed sixteen people and returned empty is a
            // different event, and the more interesting one.
            EventKind.ConflictRaid when e.Outcome == Outcome.Succeeded && e.GetInt("loot") > 0 =>
                $"{faction} raids {place}, carrying off {Num(e, "loot")} {e.GetString("resource")?.ToLowerInvariant()} and killing {Num(e, "deaths")}",

            EventKind.ConflictRaid when e.Outcome == Outcome.Succeeded =>
                $"{faction} raids {place} and kills {Num(e, "deaths")}, but takes nothing",

            EventKind.ConflictRaid =>
                $"{faction}'s raid on {place} is beaten off",

            EventKind.ConflictBattle when e.GetInt("allied") > 0 =>
                $"{faction} and its allies defeat {Label(state, e.GetEntity("loserFaction"))} " +
                $"at {place} ({Num(e, "dead")} dead)",

            EventKind.ConflictBattle =>
                $"{faction} defeats {Label(state, e.GetEntity("loserFaction"))} at {place} ({Num(e, "dead")} dead)",

            EventKind.ConflictSiege =>
                $"{faction} lays siege to {place}",

            EventKind.ConflictConquest when e.GetString("mode") == "occupation" =>
                $"{faction} occupies {place}, which no one held",

            EventKind.ConflictConquest =>
                $"{faction} takes {place} from {obj}",

            EventKind.ConflictAssassination when e.Outcome == Outcome.Succeeded =>
                $"{subject} has {obj} murdered at {place}",

            EventKind.ConflictAssassination when e.GetInt("exposed") == 1 =>
                $"{subject}'s attempt on {obj} fails and is traced back",

            EventKind.ConflictAssassination =>
                $"an attempt on {obj} fails at {place}, unattributed",

            EventKind.IntrigueBetrayal =>
                $"{subject} abandons {faction} for {Label(state, e.GetEntity("toFaction"))}",

            EventKind.IntrigueGrievanceSettled =>
                $"{faction} and {obj} settle an old score",

            _ => $"{EventKinds.Name(e.Kind)} {subject} {obj} {place}".TrimEnd(),
        };
    }

    private static string Label(WorldState state, EntityId id) => id.IsNone ? "someone" : state.Label(id);

    private static string Num(Event e, string key) => e.GetLong(key).ToString(CultureInfo.InvariantCulture);

    private static string Ordinal(int n) => n switch
    {
        1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth",
        6 => "sixth", 7 => "seventh", 8 => "eighth", 9 => "ninth",
        _ => n.ToString(CultureInfo.InvariantCulture) + "th",
    };

    private static string Plural(int count, string noun) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {noun}{(count == 1 ? "" : "s")}";

    private static string Won(Event e) => e.Outcome == Outcome.Succeeded ? "wins" : "loses";

    /// <summary>A legitimacy score in words, so no number leaves the engine to be read as a year.</summary>
    private static string Band(int legitimacy) => legitimacy switch
    {
        <= 15 => "to nothing",
        <= 30 => "very low",
        <= 45 => "low",
        _ => "somewhat",
    };

    /// <summary>Rank at the time of the event — the reducer leaves a dead actor's title intact
    /// precisely so replay can print it.</summary>
    private static string Rank(WorldState state, EntityId actor, string faction)
    {
        if (actor.IsNone || actor.Kind != EntityKind.Actor) return "of no house";
        Title title = state.ActorOf(actor).Title;
        return faction == "someone" ? title.ToString().ToLowerInvariant() : $"{title.ToString().ToLowerInvariant()} of {faction}";
    }

    private static EntityId FactionOf(WorldState state, EntityId actor) =>
        actor.IsNone || actor.Kind != EntityKind.Actor ? EntityId.None : state.ActorOf(actor).Faction;
}
