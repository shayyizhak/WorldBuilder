using System.Globalization;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Rendering;

namespace WorldBuilder.Inference;

/// <summary>
/// One person's spell holding a seat. <see cref="From"/> is when they actually took it, which
/// may predate the window being described — clamping it to the window reported a man who took
/// the seat in 39 as having held it "since 51".
/// </summary>
public sealed record Tenure(string Holder, int From, int To, string Ended)
{
    /// <summary>True if the spell was already running when this period began.</summary>
    public bool BeganEarlier { get; init; }

    /// <summary>
    /// Who held it, by identity rather than by name. Reign scopes are matched on this: the
    /// name generator can produce the same surname twice in a long run, and a reign attributed
    /// to a namesake would be a whole invented career.
    /// </summary>
    public EntityId HolderId { get; init; } = EntityId.None;

    /// <summary>
    /// The person at whose hands this rule ended — the killer, or whoever took the seat next.
    /// Empty where nobody ended it: a natural death, or a spell still running.
    ///
    /// Carried so a rendered claim about a rule ending can be checked against the event that
    /// actually ended it. A passage reported a rule as ended by the man who had challenged for
    /// it two years earlier and lost, and every name in that sentence was real; what was false
    /// was which of them ended the other. Only the seat history can see that.
    /// </summary>
    public string EndedBy { get; init; } = "";

    public int Years => Math.Max(0, To - From);
}

/// <summary>
/// A place changing hands, named and dated.
///
/// Bare counts were ambiguous in a way that produced a wrong sentence: "places taken 2, places
/// lost 2" over a window where two named towns had been taken came back as "took Laehiford and
/// Hadale but lost both", when the two losses were a different pair entirely and one of the
/// named towns was not lost until seven years after the window closed. Naming them removes the
/// gap the model was filling, and makes both ends of the clamp checkable against the log.
/// </summary>
public sealed record HoldingChange(string Place, int Year, string Other);

/// <summary>
/// One raid, named and dated. Same reasoning as <see cref="HoldingChange"/>, and the same
/// failure behind it: handed "three raids suffered" and no members, a passage enumerated three
/// raids anyway and built the third out of a faction and a town that both appear in the records
/// but never in one raid — right count, invented particulars, every word in vocabulary.
/// </summary>
/// <summary>How a raid came out. Three states, because a raid can fail in two different ways.</summary>
public enum RaidResult
{
    /// <summary>Turned back at the target.</summary>
    BeatenOff,
    /// <summary>Reached the target and came home with nothing.</summary>
    EmptyHanded,
    /// <summary>Reached the target and came home laden.</summary>
    Plunder,
}

public sealed record RaidRecord(string Place, int Year, string Other, RaidResult Result);

/// <summary>
/// One killing this power ordered, named and dated. Two of these a year apart were collapsed
/// into a single sentence sharing one date, because the figure said "two" and the dates were
/// left to be recovered from the events.
/// </summary>
public sealed record KillingRecord(string Victim, string Place, int Year);

/// <summary>
/// One battle, from this subject's side of it. <see cref="Won"/> is the whole point: the count
/// was direction-blind, so a faction's three defeats and its three victories were one figure —
/// and a passage that had to guess which kept the wins and dropped the losses, twice, which
/// biases every history toward competence.
/// </summary>
public sealed record BattleRecord(string Place, int Year, string Other, bool Won, int Dead);

/// <summary>
/// One year of a famine or a plague. Handed only "three years of hunger, 186 dead", a passage
/// wrote "killing hundreds and driving many away over the next two years" — three years became
/// two and exact figures became adjectives.
/// </summary>
public sealed record DisasterRecord(string Place, int Year, string Kind, int Dead, int Fled);

/// <summary>
/// A marriage binding this power to another, from either side.
///
/// Which side is the question the prose settles: a passage that says a marriage "bound the
/// commune to other powers" is describing a tie, and a tie has two ends. Counting only the
/// marriages where the subject happened to be named first gave three where the world had eight,
/// and the passage then named more than its own figure allowed.
/// </summary>
public sealed record MarriageRecord(string Year, string Other);

/// <summary>
/// A place rising against its holder, with the year and how far the holder's standing had
/// fallen by then.
///
/// Enumerated for the same reason as everything else here: given only the events, two revolts
/// two years apart came back dated 15 and 17 when the log says 13 and 15 — twice, in two
/// separate renders, after the date was corrected once already. The grade travels with it
/// because the change between "very low" and "to nothing" is the story of a decline, and a
/// passage handed both flattened them into one.
/// </summary>
public sealed record RevoltRecord(string Place, int Year, string Standing);

/// <summary>
/// The arithmetic of a period, computed by the engine so the renderer never has to.
///
/// Two lessons are built into how this is derived. The first is that a language model asked to
/// count across sixty records gets it wrong — so the counting happens here. The second is
/// subtler and cost a round: the first version counted the *pack's* filtered events, which
/// meant it inherited every exclusion the narration layer applies, and reported figures that
/// were confidently wrong. Wrong engine figures are worse than model guesses, because they are
/// stated as fact and cached as canon.
///
/// So seat-holders are derived by replaying the log and watching the seat actually change
/// hands. That is immune by construction to the trap of enumerating the events that can cause
/// a change — succession, open challenge, secession, partition, founding — and missing one.
/// </summary>
public sealed record PackDigest
{
    public required int FromYear { get; init; }
    public required int ToYear { get; init; }

    public required IReadOnlyList<Tenure> Tenures { get; init; }

    /// <summary>
    /// In tenths of a year. Integer division reported eleven rulers across twenty years as
    /// "average 1 years each", which is both wrong and ungrammatical; 1.8 is neither.
    /// </summary>
    public required int MeanTenureTenths { get; init; }

    /// <summary>
    /// Also in tenths, and properly interpolated for an even count. Taking the upper of two
    /// values reported "average 4.5 years, median 9" for a pair — arithmetically impossible,
    /// and stated as fact in the prose.
    /// </summary>
    public required int MedianTenureTenths { get; init; }
    public required IReadOnlyList<(string How, int Count)> HowRulesEnded { get; init; }

    public required int Battles { get; init; }

    /// <summary>Split by which side of them the subject was on, and enumerated.</summary>
    public required int BattlesWon { get; init; }
    public required int BattlesLost { get; init; }
    public required IReadOnlyList<BattleRecord> BattleList { get; init; }

    public required int WarsDeclared { get; init; }

    /// <summary>
    /// Raids this subject launched, and raids launched against it. Previously one number
    /// counted both, so a faction section reported every raid it was merely involved in as
    /// though it had ridden out itself.
    /// </summary>
    public required int RaidsLaunched { get; init; }
    public required int RaidsLaunchedBeatenOff { get; init; }

    /// <summary>Got through and took nothing — neither a repulse nor a haul.</summary>
    public required int RaidsLaunchedEmpty { get; init; }
    public required int RaidsSuffered { get; init; }

    /// <summary>The raids themselves, so an enumeration can be copied rather than reconstructed.</summary>
    public required IReadOnlyList<RaidRecord> RaidsOut { get; init; }
    public required IReadOnlyList<RaidRecord> RaidsIn { get; init; }

    /// <summary>
    /// Role-aware. Counting every assassination the subject appeared in reported a man as
    /// having survived seven attempts on his life when two of the seven were murders he
    /// ordered. Perpetrator and target are distinct in the schema; the statistic must be too.
    /// </summary>
    public required int AttemptsOnSubject { get; init; }
    public required int AttemptsOnSubjectFatal { get; init; }
    /// <summary>
    /// Killings ordered by this faction against people outside it, and killings of its own
    /// people by its own people. A house that murders its own rulers eight times is a different
    /// thing from one that assassinates eight rivals, and one combined total described both as
    /// being done "against others".
    ///
    /// The two are counted from different events, which is the correction of round 1. An
    /// internal killing is read from the <c>LIFE.DEATH_VIOLENT</c> record, because a seat taken
    /// by open challenge kills the loser without any assassination being ordered — keying the
    /// count on assassinations missed a third of them, and produced a paragraph naming a ruler
    /// as violently ended two sentences after a total that excluded him. An external killing is
    /// read from the assassination, because that is where the sponsor is recorded; the death
    /// itself is filed under the victim's house, not the one that paid for it.
    /// </summary>
    public required int KillingsOfOutsiders { get; init; }
    public required int KillingsOfItsOwn { get; init; }

    /// <summary>Who those outsiders were, where, and in what year.</summary>
    public required IReadOnlyList<KillingRecord> Killings { get; init; }
    /// <summary>Taken from the peace events themselves, never recomputed from the span.</summary>
    public required int WarYears { get; init; }

    /// <summary>
    /// People actually expelled. An outlawing pronounced against someone who had already gone
    /// is a judgement, not an expulsion, and is counted apart — folding the two together
    /// inflated the figure with sentences passed on men who were serving elsewhere.
    /// </summary>
    public required int Exiles { get; init; }
    public required int Outlawries { get; init; }
    public required int ExileReturns { get; init; }

    /// <summary>
    /// Expulsions split by the reason recorded on each. The bare total was read as though every
    /// one shared the reason of the ones the passage happened to name: "five cast out for
    /// attempted murder" where four were, and the fifth for a lost claim.
    /// </summary>
    public required IReadOnlyList<(string Reason, int Count)> ExilesByReason { get; init; }

    /// <summary>Named and dated, and clamped at both ends of the window. See <see cref="HoldingChange"/>.</summary>
    public required IReadOnlyList<HoldingChange> PlacesTaken { get; init; }
    public required IReadOnlyList<HoldingChange> PlacesLost { get; init; }

    public required int StrickenYears { get; init; }
    public required int DisasterDeaths { get; init; }

    /// <summary>Each stricken year with its own dead and displaced, so neither is summarised away.</summary>
    public required IReadOnlyList<DisasterRecord> Disasters { get; init; }

    /// <summary>
    /// People courted away from this power's ruler. A pattern — five defections in four years —
    /// that was being narrated one event at a time because only the events carried it.
    /// </summary>
    public required int Defections { get; init; }

    /// <summary>Marriages tying this power to another, counted from both ends. See <see cref="MarriageRecord"/>.</summary>
    public required IReadOnlyList<MarriageRecord> Marriages { get; init; }

    /// <summary>Places that rose against it, dated, with how far its standing had fallen.</summary>
    public required IReadOnlyList<RevoltRecord> Revolts { get; init; }

    /// <summary>
    /// Who was courted away, by whom, and when.
    ///
    /// Enumerated despite the instruction to summarise these rather than list them, and the two
    /// are not in conflict: handing over the members does not compel naming them, and the raids
    /// have been enumerated for two rounds without provoking a list. What the count alone did
    /// compel was reconstruction — one man courted away twice in successive years came back
    /// attributed to the wrong year, twice, in two separate renders.
    /// </summary>
    public required IReadOnlyList<(string Who, string By, int Year)> DefectionList { get; init; }

    public required string RecurringRivalry { get; init; }

    /// <summary>Whether the figures describe a person or a power, which changes their wording.</summary>
    public required bool SubjectIsPerson { get; init; }

    /// <summary>
    /// Whether the subject is a power, which is not the negation of <see cref="SubjectIsPerson"/>.
    ///
    /// A chronicle only ever asks for figures about a person or a power, so the two were one
    /// question and the negation was harmless. The query layer asks about places — "how many
    /// died in the plague at Griwick" — and a place was silently treated as a power, producing
    /// "killings it ordered against people of other powers: 0" about a town. Zero is even true,
    /// and a line that is true and absurd still costs a reader their trust in the ones beside it.
    /// </summary>
    public required bool SubjectIsPower { get; init; }

    /// <summary>
    /// Who the figures are about, by name.
    ///
    /// Stated in the block itself because a total silently changed scope in the prose: eight
    /// raids over a power's whole thirty-three years were attributed to one of its three
    /// rulers, two of which he was not even in the seat for. A figure has to carry the thing
    /// it is a figure of.
    /// </summary>
    public required string SubjectName { get; init; }

    /// <summary>Inclusive, and said so in the prompt — an off-by-one here becomes canon.</summary>
    public int Years => ToYear - FromYear + 1;

    public static PackDigest Empty(int from, int to) => new()
    {
        FromYear = from,
        ToYear = to,
        Tenures = [],
        MeanTenureTenths = 0,
        MedianTenureTenths = 0,
        HowRulesEnded = [],
        Battles = 0,
        BattlesWon = 0,
        BattlesLost = 0,
        BattleList = [],
        Disasters = [],
        Defections = 0,
        Marriages = [],
        DefectionList = [],
        Revolts = [],
        WarsDeclared = 0,
        WarYears = 0,
        RaidsLaunched = 0,
        RaidsLaunchedBeatenOff = 0,
        RaidsLaunchedEmpty = 0,
        RaidsSuffered = 0,
        RaidsOut = [],
        RaidsIn = [],
        Killings = [],
        ExilesByReason = [],
        AttemptsOnSubject = 0,
        AttemptsOnSubjectFatal = 0,
        KillingsOfOutsiders = 0,
        KillingsOfItsOwn = 0,
        Exiles = 0,
        Outlawries = 0,
        ExileReturns = 0,
        PlacesTaken = [],
        PlacesLost = [],
        StrickenYears = 0,
        DisasterDeaths = 0,
        RecurringRivalry = "none",
        SubjectIsPerson = false,
        SubjectIsPower = false,
        SubjectName = "",
    };

    /// <summary>
    /// Statistics for a subject over a window. Counted from the whole log rather than from the
    /// narratable subset, because the question "how many raids were there" is about the world.
    /// </summary>
    public static PackDigest Of(WorldView view, EntityId subject, int from, int to) =>
        Of(view, subject, from, to, view.Log.ForEntity(subject));

    /// <summary>
    /// The same statistics counted over a supplied set of records rather than over the log.
    ///
    /// The query layer needs this and a chronicle does not, for two reasons that both bite. A
    /// figure counted from the whole log can include a record retrieval deliberately withheld —
    /// the digest has no secrecy filter of its own, and never needed one while its only caller
    /// counted for a chronicle whose events came from the same walk. And an answer states its
    /// figures at the scope they were computed for: an answer built from five records that
    /// reports a total of seven has told the reader something the records beside it contradict.
    /// </summary>
    public static PackDigest Of(
        WorldView view, EntityId subject, int from, int to, IEnumerable<EventId> source)
    {
        if (from == int.MinValue) from = view.FirstYear;
        if (to == int.MaxValue) to = view.LastYear;

        List<Tenure> tenures = subject.Kind == EntityKind.Faction
            ? SeatHistory(view, subject, from, to)
            : [];

        int battles = 0, battlesWon = 0, battlesLost = 0, wars = 0, warYears = 0, defections = 0;
        List<BattleRecord> battleList = [];
        List<DisasterRecord> disasters = [];
        List<MarriageRecord> marriages = [];
        List<(string, string, int)> defectionList = [];
        List<RevoltRecord> revolts = [];
        int raidsOut = 0, raidsOutFailed = 0, raidsOutEmpty = 0, raidsIn = 0;
        int attemptsOn = 0, attemptsOnFatal = 0, killedOwn = 0, killedOthers = 0;
        int exiles = 0, outlawries = 0, returns = 0;
        int deaths = 0;
        List<HoldingChange> taken = [], lost = [];
        List<RaidRecord> raidsOutList = [], raidsInList = [];
        List<KillingRecord> killings = [];
        Dictionary<string, int> exileReasons = [];
        HashSet<int> stricken = [];
        Dictionary<string, int> clashes = [];

        foreach (EventId id in source)
        {
            Event e = view.Log.Get(id);
            if (e.Year < from || e.Year > to) continue;

            // Which side of the event the subject is on. The schema has carried this all along;
            // ignoring it is what produced counts that mixed victims with perpetrators.
            bool isActor = e.Subject == subject || e.Faction == subject;
            bool isTarget = e.Object == subject;

            switch (e.Kind)
            {
                case EventKind.ConflictBattle:
                {
                    battles++;
                    Clash(e);

                    EntityId loser = e.GetEntity("loserFaction");
                    bool won = e.Faction == subject;
                    if (!won && loser != subject) break;   // present but on neither side

                    if (won) battlesWon++; else battlesLost++;
                    battleList.Add(new BattleRecord(
                        view.State.NameOf(e.Where), e.Year,
                        Name(view, won ? loser : e.Faction), won, e.GetInt("dead")));
                    break;
                }

                case EventKind.DiploWarDeclared: wars++; break;

                // The peace event already carries the duration, and it is rendered directly in
                // the prose elsewhere. Computing a second figure from the span produced two
                // durations for one war that disagreed by a year, inside the same document.
                //
                // Clamped at the far end too. A war that began before this period and ended
                // inside it contributed its whole length to a window that only saw part of it —
                // the same one-sided clamp that reported a place lost seven years after the
                // period closed as lost inside it.
                case EventKind.DiploPeaceSigned:
                {
                    int began = e.Year - Math.Max(0, e.GetInt("years"));
                    warYears += Math.Max(0, Math.Min(e.Year, to) - Math.Max(began, from));
                    break;
                }

                // Launched or suffered, by role and nothing else. The fallback here counted any
                // raid with a place attached as one suffered, which is every raid there is.
                case EventKind.ConflictRaid:
                {
                    RaidResult result = e.Outcome != Outcome.Succeeded ? RaidResult.BeatenOff
                        : e.GetInt("loot") > 0 ? RaidResult.Plunder
                        : RaidResult.EmptyHanded;

                    string where = view.State.NameOf(e.Where);

                    if (isActor)
                    {
                        raidsOut++;
                        if (result == RaidResult.BeatenOff) raidsOutFailed++;
                        if (result == RaidResult.EmptyHanded) raidsOutEmpty++;
                        raidsOutList.Add(new RaidRecord(where, e.Year, Name(view, e.Object), result));
                    }
                    else if (isTarget)
                    {
                        raidsIn++;
                        raidsInList.Add(new RaidRecord(where, e.Year, Name(view, e.Faction), result));
                    }
                    break;
                }

                case EventKind.ConflictAssassination:
                    if (isTarget)
                    {
                        attemptsOn++;
                        if (e.Outcome == Outcome.Succeeded) attemptsOnFatal++;
                    }
                    else if (e.Faction == subject && e.Outcome == Outcome.Succeeded
                             && !view.Members.WasIn(e.Object, subject, e.Id))
                    {
                        // Ordered by this house against someone who was not one of theirs. The
                        // ones who were are counted from the death record instead, below, so
                        // that killings with no assassination behind them are not missed.
                        killedOthers++;
                        killings.Add(new KillingRecord(
                            view.State.NameOf(e.Object), view.State.NameOf(e.Where), e.Year));
                    }
                    break;

                // A killing inside the house, whoever ordered it and however it came about.
                // Both people must have answered to the subject as the event began — which is
                // a membership question, and membership is folded state, so it is read from the
                // index rather than from the end-state faction of two people decades later.
                case EventKind.LifeDeathViolent:
                    if (view.Members.WasIn(e.Subject, subject, e.Id)
                        && view.Members.WasIn(e.Object, subject, e.Id))
                    {
                        killedOwn++;
                    }
                    break;

                case EventKind.PolityExile:
                {
                    if (e.Faction != subject) break;
                    if (e.GetInt("outlaw") == 1) { outlawries++; break; }

                    exiles++;
                    string reason = e.GetString("reason") ?? "no reason recorded";
                    exileReasons[reason] = exileReasons.GetValueOrDefault(reason) + 1;
                    break;
                }

                case EventKind.PolityExileReturn: returns++; break;

                // Direction matters and was not being read. A conquest counted as a gain
                // whichever end of it the subject was on, and a secession counted as a loss for
                // the polity it *created* — which is how a faction that never lost a place in
                // thirty-two years was reported as having lost one, on the strength of the
                // event that founded it.
                case EventKind.ConflictConquest:
                    if (e.Faction == subject) taken.Add(Holding(view, e, e.Object));
                    else if (e.Object == subject) lost.Add(Holding(view, e, e.Faction));
                    break;

                case EventKind.PolitySecession:
                    if (e.Faction == subject)
                        lost.Add(new HoldingChange(view.State.NameOf(e.Where), e.Year, e.GetString("name") ?? "a new power"));
                    break;

                case EventKind.PolityPartition:
                    if (e.Faction == subject) lost.AddRange(Partitioned(view, e));
                    break;

                case EventKind.EconomyFamine:
                case EventKind.EconomyPlague:
                    stricken.Add(e.Year);
                    deaths += e.GetInt("deaths");
                    disasters.Add(new DisasterRecord(
                        view.State.NameOf(e.Where), e.Year,
                        e.Kind == EventKind.EconomyFamine ? "hunger" : "sickness",
                        e.GetInt("deaths"), e.GetInt("left")));
                    break;

                // Someone courted away from this power's ruler. Counted, not listed: five of
                // these in four years is one fact about a house coming apart, and it was being
                // told as five sentences because a count for it did not exist.
                case EventKind.PolityRevolt:
                    if (e.Faction != subject || e.Where.IsNone) break;
                    revolts.Add(new RevoltRecord(
                        view.State.NameOf(e.Where), e.Year, Standing(e.GetInt("legitimacy"))));
                    break;

                case EventKind.PolityCourtsSupport:
                    if (e.Faction != subject) break;
                    defections++;
                    defectionList.Add((
                        view.State.NameOf(e.Object), view.State.NameOf(e.Subject), e.Year));
                    break;

                // A marriage tying this power to another, from whichever end it is recorded.
                case EventKind.LifeMarriage when e.GetInt("crossFaction") == 1:
                {
                    EntityId here = e.Faction;
                    EntityId there = view.Members.Before(e.Object, e.Id);
                    if (here != subject && there != subject) break;

                    EntityId other = here == subject ? there : here;
                    if (other.IsNone || other == subject) break;

                    marriages.Add(new MarriageRecord(
                        e.Year.ToString(CultureInfo.InvariantCulture), view.State.NameOf(other)));
                    break;
                }
                default: break;
            }
        }

        // Tenure lengths are measured *inside the window*, so they partition the period and the
        // mean cannot exceed it. Using the true start — which may predate the window — made
        // eleven rulers across twenty years average 1.9 instead of 1.8.
        List<int> lengths = [];
        Dictionary<string, int> endings = [];
        foreach (Tenure t in tenures)
        {
            lengths.Add(Math.Max(0, t.To - Math.Max(t.From, from)));

            // Every ruler falls in exactly one category, including the one still in the seat
            // when the period ends. Leaving that person uncounted is why the distribution
            // summed to ten against eleven rulers.
            endings[t.Ended] = endings.GetValueOrDefault(t.Ended) + 1;
        }
        lengths.Sort();

        List<(string, int)> howEnded = Ranked(endings);

        string rivalry = "none";
        int worst = 1;
        foreach ((string pair, int count) in clashes)
            if (count > worst) { worst = count; rivalry = $"{pair}, {count} battles between them"; }

        return new PackDigest
        {
            FromYear = from,
            ToYear = to,
            Tenures = tenures,
            MeanTenureTenths = lengths.Count == 0 ? 0 : Sum(lengths) * 10 / lengths.Count,
            MedianTenureTenths = MedianOf(lengths),
            HowRulesEnded = howEnded,
            Battles = battles,
            BattlesWon = battlesWon,
            BattlesLost = battlesLost,
            BattleList = battleList,
            Disasters = disasters,
            Defections = defections,
            Marriages = marriages,
            DefectionList = defectionList,
            Revolts = revolts,
            WarsDeclared = wars,
            WarYears = warYears,
            RaidsLaunched = raidsOut,
            RaidsLaunchedBeatenOff = raidsOutFailed,
            RaidsLaunchedEmpty = raidsOutEmpty,
            RaidsSuffered = raidsIn,
            RaidsOut = raidsOutList,
            RaidsIn = raidsInList,
            Killings = killings,
            ExilesByReason = Ranked(exileReasons),
            AttemptsOnSubject = attemptsOn,
            AttemptsOnSubjectFatal = attemptsOnFatal,
            KillingsOfOutsiders = killedOthers,
            KillingsOfItsOwn = killedOwn,
            Exiles = exiles,
            Outlawries = outlawries,
            ExileReturns = returns,
            PlacesTaken = taken,
            PlacesLost = lost,
            StrickenYears = stricken.Count,
            DisasterDeaths = deaths,
            RecurringRivalry = rivalry,
            SubjectIsPerson = subject.Kind == EntityKind.Actor,
            SubjectIsPower = subject.Kind == EntityKind.Faction,
            SubjectName = view.State.NameOf(subject),
        };

        void Clash(Event e)
        {
            EntityId a = e.Faction;
            EntityId b = e.GetEntity("loserFaction");
            if (a.IsNone || b.IsNone) return;

            string key = a.CompareTo(b) < 0
                ? $"{view.State.NameOf(a)} and {view.State.NameOf(b)}"
                : $"{view.State.NameOf(b)} and {view.State.NameOf(a)}";
            clashes[key] = clashes.GetValueOrDefault(key) + 1;
        }
    }

    /// <summary>Counts as a list, commonest first, with ties broken by name so runs are stable.</summary>
    private static List<(string, int)> Ranked(Dictionary<string, int> counts)
    {
        List<(string Label, int Count)> ordered = [];
        foreach ((string label, int count) in counts) ordered.Add((label, count));

        ordered.Sort(static (a, b) => a.Count != b.Count
            ? b.Count.CompareTo(a.Count)
            : string.CompareOrdinal(a.Label, b.Label));

        List<(string, int)> result = [];
        foreach ((string label, int count) in ordered) result.Add((label, count));
        return result;
    }

    private static string Name(WorldView view, EntityId id) => id.IsNone ? "no one" : view.State.NameOf(id);

    /// <summary>
    /// A legitimacy score in the same words the log uses, so the grade in the digest and the
    /// grade in the event line cannot disagree.
    /// </summary>
    private static string Standing(int legitimacy) => legitimacy switch
    {
        <= 15 => "standing fallen to nothing",
        <= 30 => "standing very low",
        <= 45 => "standing low",
        _ => "standing somewhat fallen",
    };

    private static HoldingChange Holding(WorldView view, Event e, EntityId other) =>
        new(view.State.NameOf(e.Where), e.Year, other.IsNone ? "no one" : view.State.NameOf(other));

    /// <summary>
    /// The places a partition carried off, read from the <c>ctrl:</c> deltas that moved them.
    /// The event's own "places" figure is only a count; the deltas name them.
    /// </summary>
    private static List<HoldingChange> Partitioned(WorldView view, Event e)
    {
        List<HoldingChange> gone = [];
        string other = e.GetString("name") ?? "a new power";

        foreach (KeyValuePair<string, string> kv in e.Data)
        {
            string[] parts = kv.Key.Split(':');
            if (parts.Length != 3 || parts[0] != "ctrl") continue;
            if (!EntityId.TryParse($"{parts[1]}:{parts[2]}", out EntityId place)) continue;
            gone.Add(new HoldingChange(view.State.NameOf(place), e.Year, other));
        }
        return gone;
    }

    /// <summary>
    /// Who held the seat, found by replaying the world and watching it change hands.
    ///
    /// Not by counting POLITY.SUCCESSION events: a seat also passes by open challenge, by
    /// secession and partition installing a leader, and by a founding. Counting one event type
    /// reported three rulers for a house that had five. Observing the state cannot miss a path,
    /// because it does not need to know what the paths are.
    /// </summary>
    /// <summary>
    /// Every seat, and everyone who ever sat in it, from one replay of the log.
    ///
    /// Computed for all factions at once because the callers need it that way: a reign is a
    /// spell in <em>some</em> faction's history and the actor does not know which, so asking
    /// per faction meant one full fold per faction per question.
    /// </summary>
    public static Dictionary<EntityId, List<Tenure>> AllSeatHistories(WorldView view)
    {
        Dictionary<EntityId, List<Tenure>> spells = [];
        Dictionary<EntityId, (EntityId Holder, int Since)> current = [];

        Replay.Walk(view.Log, view.Seed, (state, e) =>
        {
            foreach (Faction f in state.Factions)
            {
                (EntityId holder, int since) = current.GetValueOrDefault(f.Id, (EntityId.None, e.Year));
                if (f.Leader == holder) continue;

                if (!holder.IsNone)
                {
                    (string fate, string by) = Ending(view, holder, e);
                    if (!spells.TryGetValue(f.Id, out List<Tenure>? list)) spells[f.Id] = list = [];
                    list.Add(new Tenure(state.NameOf(holder), since, e.Year, fate)
                    {
                        EndedBy = by,
                        HolderId = holder,
                    });
                }

                current[f.Id] = (f.Leader, e.Year);
            }
        }, view.Board);

        foreach ((EntityId faction, (EntityId holder, int since)) in current)
        {
            if (holder.IsNone) continue;
            if (!spells.TryGetValue(faction, out List<Tenure>? list)) spells[faction] = list = [];
            list.Add(new Tenure(view.State.NameOf(holder), since, view.LastYear, "still holding")
            {
                HolderId = holder,
            });
        }

        // Where the seat simply passed to the next man, he is who ended the rule. That is only
        // knowable once the following spell is known, so it is filled in here rather than at
        // the moment the change was seen.
        foreach (List<Tenure> list in spells.Values)
        {
            for (int i = 0; i + 1 < list.Count; i++)
            {
                if (list[i].EndedBy.Length > 0) continue;
                if (list[i].Ended is not ("replaced" or "beaten in open challenge")) continue;
                list[i] = list[i] with { EndedBy = list[i + 1].Holder };
            }
        }

        return spells;
    }

    private static List<Tenure> SeatHistory(WorldView view, EntityId faction, int from, int to)
    {
        List<Tenure> spells = AllSeatHistories(view).GetValueOrDefault(faction, []);

        // Only spells overlapping the window, clipped to it. A spell that really ended outside
        // the window must not report how it ended: within 22–41 a man killed in 51 was simply
        // still holding, and saying "killed" there dates his death twenty years early.
        List<Tenure> inWindow = [];
        foreach (Tenure t in spells)
        {
            if (t.To < from || t.From > to) continue;

            bool endsInside = t.To <= to;
            inWindow.Add(t with
            {
                // From keeps its real value. A ruler who came to power before the period began
                // did not begin ruling when the chapter starts.
                To = Math.Min(t.To, to),
                Ended = endsInside ? t.Ended : "still holding at the end of this period",
                EndedBy = endsInside ? t.EndedBy : "",
                BeganEarlier = t.From < from,
            });
        }
        return inWindow;
    }

    /// <summary>
    /// What became of a ruler who left the seat.
    ///
    /// The event that moves the seat is often not the event that says why. A challenger who
    /// wins takes it in a succession, and the man he beat is exiled a moment later in the same
    /// year — so classifying on the seat-moving event alone filed him as "replaced", and a
    /// house that cast out two of its five rulers reported one. What happened to the person is
    /// the category, so the person is followed to the end of the year and the hardest fate
    /// found there wins: killed over died, died over cast out, and only then the bare mechanics
    /// of how the seat moved.
    /// </summary>
    private static (string Fate, string EndedBy) Ending(WorldView view, EntityId holder, Event change)
    {
        int best = int.MaxValue;
        string? fate = null;
        string by = "";

        Consider(change);
        foreach (EventId id in view.Log.ForEntity(holder))
        {
            if (id.Value < change.Id.Value) continue;
            Event e = view.Log.Get(id);
            if (e.Year != change.Year) break;
            Consider(e);
        }

        if (fate is not null) return (fate, by);

        return change.Kind switch
        {
            EventKind.PolityChallenge => ("beaten in open challenge", ""),
            EventKind.PolitySuccession => ("replaced", ""),
            EventKind.PolityCollapse => ("its house ended", ""),
            _ => ((view.State.ActorOf(holder).DeathYear ?? int.MaxValue) > change.Year ? "replaced" : "died", ""),
        };

        void Consider(Event e)
        {
            if (e.Subject != holder) return;

            (int rank, string label) = e.Kind switch
            {
                EventKind.LifeDeathViolent => (0, "killed"),
                EventKind.LifeDeathNatural => (1, "died"),
                EventKind.PolityExile when e.GetInt("outlaw") == 0 => (2, "cast out"),
                EventKind.IntrigueBetrayal => (3, "defected"),
                _ => (int.MaxValue, ""),
            };

            if (rank >= best) return;

            best = rank;
            fate = label;

            // Only a violent death names the hand that ended the rule. Nobody is recorded as
            // having cast a man out — the house did — and a natural death has no agent at all.
            by = e.Kind == EventKind.LifeDeathViolent && e.Object.Kind == EntityKind.Actor
                ? view.State.NameOf(e.Object)
                : "";
        }
    }

    /// <summary>Median in tenths, interpolating the middle pair when the count is even.</summary>
    private static int MedianOf(List<int> sorted)
    {
        if (sorted.Count == 0) return 0;
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid] * 10
            : (sorted[mid - 1] + sorted[mid]) * 5;
    }

    private static int Sum(List<int> values)
    {
        int total = 0;
        foreach (int v in values) total += v;
        return total;
    }

    /// <summary>
    /// The digest as prompt text.
    ///
    /// Careful to describe the world and not the archive. An earlier version offered "64
    /// recorded events", which the model dutifully wrote into the prose — a fact about a log
    /// file, in a passage that is supposed to be history.
    /// </summary>
    /// <summary>
    /// Whether this period is large enough for statistics to mean anything.
    ///
    /// A one-year reign produced "One person held the seat and was killed; one person held the
    /// seat and remained holding it. One attempt on a life killed its target." That is the
    /// statistics block read aloud. A distribution needs a population to be a distribution of.
    /// </summary>
    public bool WorthSummarising => Years >= 5 && Tenures.Count + Battles + RaidsLaunched
                                    + AttemptsOnSubject + Exiles >= 6;

    public string ToPromptBlock()
    {
        if (!WorthSummarising)
        {
            return "\nThis is a short period. No totals are offered for it. Tell it as a story:\n"
                 + "no counting, no arithmetic, and nothing about how often things happened.\n";
        }

        StringBuilder sb = new();
        sb.Append("\nWHAT THESE YEARS ADD UP TO — already counted for you. State these figures as\n");
        sb.Append("given. Do not count, add, average or work out an interval yourself.\n");
        Scope(sb);
        Figures(sb);
        return sb.ToString();
    }

    /// <summary>
    /// The same figures for an answer rather than for a passage.
    ///
    /// <see cref="WorthSummarising"/> is deliberately not consulted, and that is the whole
    /// difference. It asks whether a period is long enough for a distribution to mean anything,
    /// which is the right question for prose and the wrong one for a question: "how many died in
    /// the plague at Griwick" spans four years, fails that test, and is a question whose entire
    /// answer is a total the engine has already computed. Withholding it leaves the model to add
    /// three figures together — which it is forbidden to do, and does anyway.
    /// </summary>
    public string ToQueryBlock()
    {
        // Built first and inspected, because a block of headings with nothing under them is
        // worse than no block. Asked how many died in the plague at Griwick, the subject is a
        // town, and every figure this counts is about powers: the section came out as a scope
        // warning followed by two zeroes about killings a town did not order.
        StringBuilder figures = new();
        Figures(figures, everyTenure: true);
        if (figures.Length == 0) return "";

        StringBuilder sb = new();
        sb.Append("\nWHAT THESE RECORDS ADD UP TO — already counted for you, over exactly the\n");
        sb.Append("records above and nothing else. State these figures as given. Do not count,\n");
        sb.Append("add, average or work out an interval yourself.\n");
        Scope(sb);
        sb.Append(figures);
        return sb.ToString();
    }

    private void Scope(StringBuilder sb)
    {
        string who = SubjectName.Length > 0 ? SubjectName : "this subject";

        // Whose figures these are, said before any of them. Eight raids belonging to a power's
        // whole life were written as one ruler's, two of them from before he held the seat.
        sb.Append("  EVERY FIGURE BELOW IS FOR ").Append(who.ToUpperInvariant())
          .Append(" ACROSS THE WHOLE PERIOD ").Append(N(FromYear)).Append('–').Append(N(ToYear))
          .Append(".\n  Do not attach any of them to one ruler or to part of the period. If you write\n")
          .Append("  \"under so-and-so\", the figure that follows must be one you worked out from the\n")
          .Append("  dated events themselves — and you may not work figures out, so do not write it.\n");

        sb.Append("  the period runs from ").Append(N(FromYear)).Append(" to ").Append(N(ToYear))
          .Append(" inclusive, which is ").Append(N(Years)).Append(" years\n");
    }

    private void Figures(StringBuilder sb, bool everyTenure = false)
    {

        if (Tenures.Count > 0)
        {
            sb.Append("  held the seat: ").Append(N(Tenures.Count)).Append(" people");

            // Averages are only offered where they mean something. Two tenures inside a single
            // year produced "average 0.0 years each, median 0", which the model wrote down.
            if (Tenures.Count >= 3 && MeanTenureTenths > 0)
            {
                sb.Append(", average ").Append(Decimal(MeanTenureTenths))
                  .Append(" years each, median ").Append(Decimal(MedianTenureTenths));
            }
            sb.Append('\n');

            // The seat passed from one person to another this many times. Handed only the head
            // count, a passage turned "three people held the seat" into "the leadership changed
            // hands three times", which is one too many and is arithmetic the engine can do.
            if (Tenures.Count >= 2)
            {
                sb.Append("  the seat passed from one person to another ")
                  .Append(N(Tenures.Count - 1))
                  .Append(Tenures.Count == 2 ? " time" : " times").Append(" inside this period\n");
            }

            // A question about who ruled gets every holder, and gets each as a span.
            //
            // The chronicle's eliding is right for prose — a section naming eleven rulers in a
            // row is the log with the numbers spelled out — and wrong for an answer to "who
            // ruled X", where the elided four are four sixths of what was asked. And an
            // accession year alone is not a reign: handed "in 29, in 45, in 46", an answer read
            // Stald Gearngoll's sixteen years and Thres Thrild's one identically.
            if (everyTenure)
            {
                foreach (Tenure t in Tenures) Span(sb, t);
            }
            else if (Tenures.Count <= 4)
            {
                foreach (Tenure t in Tenures) Spell(sb, t);
            }
            else
            {
                Spell(sb, Tenures[0]);
                sb.Append("    - (").Append(N(Tenures.Count - 2))
                  .Append(" more held it in between, all named in the events above)\n");
                Spell(sb, Tenures[^1]);
            }

            if (HowRulesEnded.Count > 0)
            {
                sb.Append("    how those rules ended (these add up to all ")
                  .Append(N(Tenures.Count)).Append("): ");
                Join(sb, HowRulesEnded);
                sb.Append('\n');
            }

            // Deliberately no per-ruler fate list here, though the temptation is strong: a
            // passage had listed four men as "cast out in 33, 35, 37 and 38" when two of them
            // were killed. Supplying all eleven fates fixed that sentence and cost the whole
            // section, which came back as one sentence per record — the round-2 failure mode,
            // reintroduced through the very block meant to lift the prose above it.
            //
            // The fates are on the events already. What was missing was not the data but a
            // check, and there is one now: a fate claimed for a person is validated against
            // that person's actual departure, including across an elided list.
        }

        // Wins and losses, separately and by name. One combined figure let a passage keep the
        // victories and drop the defeats — twice — which reads as competence the world does
        // not record.
        if (Battles > 0)
        {
            sb.Append("  battles fought: ").Append(N(Battles))
              .Append(" — ").Append(N(BattlesWon)).Append(" won, ")
              .Append(N(BattlesLost)).Append(" lost\n");

            if (BattleList.Count > 0)
            {
                sb.Append("    they were: ");
                for (int i = 0; i < BattleList.Count; i++)
                {
                    if (i > 0) sb.Append("; ");
                    sb.Append(BattleList[i].Won ? "beat " : "lost to ")
                      .Append(BattleList[i].Other).Append(" at ").Append(BattleList[i].Place)
                      .Append(" in ").Append(N(BattleList[i].Year))
                      .Append(", ").Append(N(BattleList[i].Dead)).Append(" dead");
                }
                sb.Append(". A defeat is not optional; report the losses as well as the wins.\n");
            }
        }

        Line(sb, "wars begun", WarsDeclared);
        if (WarYears > 0) sb.Append("  years spent at war: ").Append(N(WarYears)).Append('\n');

        if (RaidsLaunched > 0)
        {
            // Three outcomes. Reporting "beaten off" against "carried off plunder" put every
            // raid that got through and took nothing into the plunder column.
            sb.Append("  raids it sent out: ").Append(N(RaidsLaunched)).Append(" — ")
              .Append(N(RaidsLaunchedBeatenOff)).Append(" beaten off, ")
              .Append(N(RaidsLaunchedEmpty)).Append(" got through but took nothing, ")
              .Append(N(RaidsLaunched - RaidsLaunchedBeatenOff - RaidsLaunchedEmpty))
              .Append(" carried off plunder\n");
            Raids(sb, "    they were", RaidsOut, "against");
        }

        if (RaidsSuffered > 0)
        {
            sb.Append("  raids it suffered: ").Append(N(RaidsSuffered)).Append('\n');
            Raids(sb, "    they were", RaidsIn, "by");
        }

        // Spelled out at length because the shorter wording was misread: "of which 3 killed
        // their target" came back as "three resulted in the death of the attacker". Phrased for
        // whichever kind of subject this is — "its own people" is nonsense about a person.
        if (AttemptsOnSubject > 0)
        {
            sb.Append(SubjectIsPerson
                ? "  attempts made on this person's life: "
                : "  attempts made on the lives of its own people: ");

            sb.Append(N(AttemptsOnSubject)).Append(" in total — of these, ")
              .Append(N(AttemptsOnSubjectFatal))
              .Append(" killed the intended victim and ")
              .Append(N(AttemptsOnSubject - AttemptsOnSubjectFatal))
              .Append(" failed. The total is ").Append(N(AttemptsOnSubject))
              .Append(", counting the fatal one. No attacker died unless an event says so.\n");
        }
        // Both figures, always, even at zero. Reporting one and silently dropping the other
        // invited exactly the misreading the split was introduced to prevent: a section that
        // opened on "internal purges and external killings" gave a number for the first and
        // none for the second, leaving the reader to assume the one figure covered both.
        if (SubjectIsPower)
        {
            sb.Append("  killings it ordered against people of other powers: ")
              .Append(N(KillingsOfOutsiders)).Append('\n');

            for (int i = 0; i < Killings.Count; i++)
            {
                sb.Append(i == 0 ? "    they were: " : "; ");
                sb.Append(Killings[i].Victim).Append(" at ").Append(Killings[i].Place)
                  .Append(" in ").Append(N(Killings[i].Year));
            }
            if (Killings.Count > 0) sb.Append(". Each has its own year; they are not all one year.\n");

            sb.Append("  its own people murdered from within, by its own people: ")
              .Append(N(KillingsOfItsOwn))
              .Append(KillingsOfItsOwn == 0 ? "\n" : " — these were purges, not strikes at outsiders\n");
        }

        if (Exiles > 0)
        {
            sb.Append("  people cast out: ").Append(N(Exiles));

            // The reasons, because the total was read as though all of them shared whichever
            // reason the passage happened to name.
            if (ExilesByReason.Count > 0)
            {
                sb.Append(" — ");
                for (int i = 0; i < ExilesByReason.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(N(ExilesByReason[i].Count)).Append(" for ").Append(ExilesByReason[i].Reason);
                }
            }
            sb.Append('\n');
        }
        Line(sb, "people already gone who were declared outlaw", Outlawries);
        Line(sb, "exiles who returned", ExileReturns);

        // Named, dated, and complete for the window. The bare counts were bound by the model to
        // whichever place names were nearest to hand, which put a loss seven years outside the
        // period inside it.
        Holdings(sb, "places taken in these years", PlacesTaken, "from");
        Holdings(sb, "places lost in these years", PlacesLost, "to");
        if (StrickenYears > 0)
        {
            sb.Append("  years of hunger or sickness: ").Append(N(StrickenYears))
              .Append(", killing ").Append(N(DisasterDeaths)).Append(" in all\n");

            // Each year with its own dead and displaced. The totals alone came back as
            // "killing hundreds and driving many away over the next two years" — the count of
            // years wrong and the two figures the engine had computed thrown away.
            sb.Append("    year by year: ");
            int fled = 0;
            for (int i = 0; i < Disasters.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(Disasters[i].Kind).Append(" at ").Append(Disasters[i].Place)
                  .Append(" in ").Append(N(Disasters[i].Year))
                  .Append(", ").Append(N(Disasters[i].Dead)).Append(" dead");
                if (Disasters[i].Fled > 0) sb.Append(" and ").Append(N(Disasters[i].Fled)).Append(" fled");
                fled += Disasters[i].Fled;
            }
            sb.Append(". Use these numbers. Never write \"hundreds\" or \"many\" for a figure\n")
              .Append("    you have been given");
            if (fled > 0) sb.Append("; ").Append(N(fled)).Append(" left their homes in all");
            sb.Append(".\n");
        }

        // Counted from both ends of the tie, which is what "bound to other powers" means. Only
        // the marriages naming this power first were countable before, so a passage could name
        // more ties than its own figure allowed and be right about every one of them.
        if (Marriages.Count > 0)
        {
            sb.Append("  marriages tying it to other powers: ").Append(N(Marriages.Count))
              .Append(" — counted from both ends: a marriage into this power and one out of it\n")
              .Append("    both bind it. They were: ");

            for (int i = 0; i < Marriages.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(Marriages[i].Other).Append(" in ").Append(Marriages[i].Year);
            }
            sb.Append(". Those are all of them.\n");
        }

        if (Revolts.Count > 0)
        {
            sb.Append("  places that rose against it: ").Append(N(Revolts.Count)).Append(" — ");
            for (int i = 0; i < Revolts.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(Revolts[i].Place).Append(" in ").Append(N(Revolts[i].Year))
                  .Append(", ").Append(Revolts[i].Standing);
            }
            sb.Append(". Use these years and these words for how far it had fallen; the grade\n")
              .Append("    changes between them and the change is the story.\n");
        }

        if (Defections > 0)
        {
            sb.Append("  people courted away from its ruler: ").Append(N(Defections))
              .Append(" — this is a pattern of a house coming apart, not a list of incidents.\n")
              .Append("    Say it once with the number; do not give one sentence to each.\n")
              .Append("    If you do name any, these are the facts — one man may be courted\n")
              .Append("    away more than once, in different years and by different people: ");

            for (int i = 0; i < DefectionList.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(DefectionList[i].By).Append(" won ").Append(DefectionList[i].Who)
                  .Append(" away in ").Append(N(DefectionList[i].Year));
            }
            sb.Append('\n');
        }
        if (RecurringRivalry != "none") sb.Append("  the quarrel that kept returning: ").Append(RecurringRivalry).Append('\n');
    }

    private static void Spell(StringBuilder sb, Tenure t)
    {
        sb.Append("    - ").Append(t.Holder);
        sb.Append(t.BeganEarlier ? ", had already held the seat since " : ", took the seat in ");
        sb.Append(N(t.From)).Append(", ").Append(t.Ended).Append('\n');
    }

    /// <summary>
    /// One spell as a span, which is what a reign is.
    ///
    /// A year of accession on its own is the one fact a reader cannot use: it does not say
    /// whether the man held the seat for sixteen years or for one, and an answer given six
    /// accession years wrote six holders who all read as though they ruled for a moment. The
    /// end year is already known — it is what the next accession closed.
    /// </summary>
    private static void Span(StringBuilder sb, Tenure t)
    {
        sb.Append("    - ").Append(t.Holder).Append(" held it from ").Append(N(t.From));

        // A spell still running has no closing year, and inventing one from the end of the
        // period would date a reign to an editorial boundary rather than to an event.
        sb.Append(t.Ended.StartsWith("still holding", StringComparison.Ordinal)
            ? $" onwards — {t.Ended}"
            : $" to {N(t.To)} — {t.Ended}");

        sb.Append('\n');
    }

    /// <summary>
    /// A holdings line, listing every place by name. "and no others" is spelled out because a
    /// bare list reads as a sample, and a chronicle a reader cannot tell a sample from is not a
    /// reference.
    /// </summary>
    private static void Holdings(StringBuilder sb, string label, IReadOnlyList<HoldingChange> changes, string preposition)
    {
        if (changes.Count == 0) return;

        sb.Append("  ").Append(label).Append(": ").Append(N(changes.Count)).Append(" — ");
        for (int i = 0; i < changes.Count; i++)
        {
            if (i > 0) sb.Append("; ");
            sb.Append(changes[i].Place).Append(" in ").Append(N(changes[i].Year))
              .Append(' ').Append(preposition).Append(' ').Append(changes[i].Other);
        }
        sb.Append(". That is all of them for these years; any other change of hands fell outside.\n");
    }

    /// <summary>
    /// The raids themselves, one dense line. A figure without its members forces the passage to
    /// reconstruct them, and reconstruction from nouns lying around the document is how a raid
    /// that never happened got a faction, a town and a year, all three wrong and all three in
    /// vocabulary.
    /// </summary>
    private static void Raids(StringBuilder sb, string lead, IReadOnlyList<RaidRecord> raids, string preposition)
    {
        if (raids.Count == 0) return;

        sb.Append(lead).Append(": ");
        for (int i = 0; i < raids.Count; i++)
        {
            if (i > 0) sb.Append("; ");
            sb.Append(raids[i].Place).Append(" in ").Append(N(raids[i].Year))
              .Append(' ').Append(preposition).Append(' ').Append(raids[i].Other)
              .Append(raids[i].Result switch
              {
                  RaidResult.BeatenOff => ", beaten off",
                  RaidResult.EmptyHanded => ", which got through but took nothing",
                  _ => ", carrying off plunder",
              });
        }
        sb.Append(". Those are all of them; name no others.\n");
    }

    private static void Line(StringBuilder sb, string label, int value)
    {
        if (value > 0) sb.Append("  ").Append(label).Append(": ").Append(N(value)).Append('\n');
    }

    private static void Join(StringBuilder sb, IReadOnlyList<(string How, int Count)> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(N(items[i].Count)).Append(' ').Append(items[i].How);
        }
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(int tenths) =>
        (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." +
        (tenths % 10).ToString(CultureInfo.InvariantCulture);
}
