using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>
/// What the records will actually support, indexed by act rather than by word.
///
/// The checker's first shape was a vocabulary scan: is this proper noun in the source, is this
/// number in the source, does the source contain the word "coup". That catches a passage that
/// mints a name and misses every passage that assembles a falsehood out of real ones — a raid
/// that never happened, built from a power and a town and a year each present somewhere else;
/// two men who were killed described as exiled; two murders a year apart collapsed onto one
/// date. Each of those used only words it had been given.
///
/// The fix is to index what happened rather than what was said: for every kind of act, which
/// subject it touched and in which years. A claim is then checkable as a claim — this person
/// was cast out, in this year — instead of as a bag of tokens.
/// </summary>
public sealed class ClaimIndex
{
    /// <summary>The acts this index knows how to answer for. Kept small and literal.</summary>
    public const string Raid = "raid";
    public const string Exile = "cast out";
    public const string Outlaw = "declared outlaw";
    public const string Killed = "killed";
    public const string DiedNaturally = "died";
    public const string TookSeat = "took the seat";
    public const string WonAway = "courted away from the ruler";
    public const string Collapsed = "finished";
    public const string Returned = "returned from exile";

    /// <summary>A place rising against its holder, keyed on the place.</summary>
    public const string Revolt = "in revolt";

    /// <summary>
    /// The two roles in a disputed succession. Both are indexed because the failure is a swap,
    /// and a swap can only be seen by knowing which man was which.
    /// </summary>
    public const string Contested = "the one who contested";
    public const string NamedHeir = "the named heir";

    private readonly Dictionary<string, HashSet<int>> _years = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>For a seat-taking, which power's seat it was. Keyed "took the seat|surname|year".</summary>
    private readonly Dictionary<string, string> _seatOf = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Who killed whom, as "killer|victim". The relation, not just the fact.
    ///
    /// This is the gap behind the longest-running fabrication in the project. A killing and a
    /// seat-taking that sit two years apart get fused — "Turaer Danpa holding the seat after
    /// killing Befu Seirn", when Danpa killed Heillvar Maer and Bu Rumpirn killed Seirn. Both
    /// men are real, both are in the section, and they are even causally linked, because the
    /// death Danpa did not commit is what opened the seat he took. Every check up to here
    /// passes it; only the pair can see it.
    /// </summary>
    private readonly HashSet<string> _killers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the records have this person killing that one.</summary>
    public bool Killer(string killer, string victim) => _killers.Contains($"{killer}|{victim}");

    /// <summary>Whether the records have this person killing anyone at all.</summary>
    public bool EverKilled(string killer)
    {
        foreach (string pair in _killers)
            if (pair.StartsWith($"{killer}|", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Who the records say killed this person, for a message that states the truth.</summary>
    public string? KillerOf(string victim)
    {
        foreach (string pair in _killers)
        {
            string[] parts = pair.Split('|');
            if (parts.Length == 2 && string.Equals(parts[1], victim, StringComparison.OrdinalIgnoreCase))
                return parts[0];
        }
        return null;
    }

    private static string Key(string act, string subject) => $"{act}|{subject}";

    /// <summary>The subject key for an act between two people, so a pair can be dated.</summary>
    public static string Between(string doer, string done) => $"{doer}>{done}";

    private void Pair(string killer, string victim)
    {
        if (killer.Length > 0 && victim.Length > 0) _killers.Add($"{killer}|{victim}");
    }

    private void Add(string act, string subject, int year)
    {
        if (subject.Length == 0) return;
        string key = Key(act, subject);
        if (!_years.TryGetValue(key, out HashSet<int>? years)) _years[key] = years = [];
        years.Add(year);
    }

    /// <summary>Whether the records have this subject doing or suffering this act at all.</summary>
    public bool Knows(string act, string subject) => _years.ContainsKey(Key(act, subject));

    /// <summary>
    /// Whether the records hold this kind of act at all, for anybody.
    ///
    /// This is what separates a lookup that failed from a lookup that could not be made. Prose
    /// asserting a courting-away that the records do not have, in a pack that records four of
    /// them, is a claim about the world that the world contradicts. The same prose in a pack
    /// holding none is far more likely to be a sentence about something else entirely — "won the
    /// battle" is not a defection — and a rule that cannot tell those apart must not accuse.
    /// </summary>
    public bool Witnesses(string act)
    {
        foreach (string key in _years.Keys)
            if (key.StartsWith($"{act}|", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>Whether they do, in that year.</summary>
    public bool Supports(string act, string subject, int year) =>
        _years.TryGetValue(Key(act, subject), out HashSet<int>? years) && years.Contains(year);

    /// <summary>The years it happened, for a message that says what the truth was.</summary>
    public IReadOnlyCollection<int> Years(string act, string subject) =>
        _years.TryGetValue(Key(act, subject), out HashSet<int>? years) ? years : [];

    /// <summary>Which seat, for a seat-taking already known to have happened that year.</summary>
    public string? SeatTaken(string surname, int year) =>
        _seatOf.TryGetValue($"{TookSeat}|{surname}|{year}", out string? faction) ? faction : null;

    /// <summary>
    /// Builds the index from the pack's own events, so it can only ever support what the
    /// passage was shown. Seat-takings come from the seat history rather than from succession
    /// events, because a breakaway installs its founder without emitting one.
    /// </summary>
    public static ClaimIndex Build(WorldView view, IReadOnlyList<EventId> events, EntityId subject)
    {
        ClaimIndex index = new();

        foreach (EventId id in events)
        {
            Event e = view.Log.Get(id);

            switch (e.Kind)
            {
                // Indexed by every name a passage may reasonably raid: the town, the power the
                // raid was aimed at, and whoever held the town at the time.
                //
                // The place alone was not enough, and the gap only became visible once an
                // unfound lookup started firing. "It sent three raids against the Vea Lode
                // Covenant, targeting Vea Lode in 43" is true and names the power — which was
                // not a key, so the check reported no such raid against a sentence describing
                // three real ones. The pack's own raid-direction rule has indexed all three
                // forms since the day it was written, for exactly this reason.
                case EventKind.ConflictRaid:
                {
                    if (e.Where.IsNone) break;

                    index.Add(Raid, ContextPackBuilder.Bare(view.State.NameOf(e.Where)), e.Year);

                    if (!e.Object.IsNone)
                        index.Add(Raid, ContextPackBuilder.Bare(view.State.NameOf(e.Object)), e.Year);

                    EntityId held = view.State.PlaceOf(e.Where).Controller;
                    if (!held.IsNone)
                        index.Add(Raid, ContextPackBuilder.Bare(view.State.NameOf(held)), e.Year);
                    break;
                }

                // Two revolts two years apart were written as both happening in the later year.
                // The date check has to cover every kind of act, not the three it was built for.
                case EventKind.PolityRevolt:
                    if (!e.Where.IsNone)
                        index.Add(Revolt, ContextPackBuilder.Bare(view.State.NameOf(e.Where)), e.Year);
                    break;

                // Subject is the named heir; object is the man disputing his claim. The event
                // says so in words now, and a passage still reversed them — the roles have to
                // be checkable, not merely legible.
                case EventKind.PolitySuccessionDisputed:
                    index.Add(NamedHeir, Surname(view, e.Subject), e.Year);
                    index.Add(Contested, Surname(view, e.Object), e.Year);
                    break;

                case EventKind.PolityExile:
                    index.Add(e.GetInt("outlaw") == 1 ? Outlaw : Exile, Surname(view, e.Subject), e.Year);
                    break;

                // Returns are the longest dated rosters the prose writes — fourteen names with a
                // year each — and until now nothing checked a single one of those years.
                case EventKind.PolityExileReturn:
                    index.Add(Returned, Surname(view, e.Subject), e.Year);
                    break;

                case EventKind.LifeDeathViolent:
                    index.Add(Killed, Surname(view, e.Subject), e.Year);
                    index.Pair(Surname(view, e.Object), Surname(view, e.Subject));
                    break;

                case EventKind.LifeDeathNatural:
                    index.Add(DiedNaturally, Surname(view, e.Subject), e.Year);
                    break;

                case EventKind.ConflictAssassination when e.Outcome == Outcome.Succeeded:
                    index.Add(Killed, Surname(view, e.Object), e.Year);
                    index.Pair(Surname(view, e.Subject), Surname(view, e.Object));
                    break;

                // Somebody courted away from the subject's ruler, indexed both by who changed
                // sides and by the pair. The person alone is not enough: one man was courted
                // away twice, a year apart and by two different people, so "Wer won him away
                // in 49" is supported at the person level and false at the pair level — which
                // is exactly the error that was made.
                case EventKind.PolityCourtsSupport:
                    index.Add(WonAway, Surname(view, e.Object), e.Year);
                    index.Add(WonAway, Between(Surname(view, e.Subject), Surname(view, e.Object)), e.Year);
                    break;

                // Indexed under each distinguishing word of the name, because that is how a
                // power is looked up elsewhere — "wurn", not "wurn league". Keying the whole
                // bare name meant no lookup ever matched and the check silently did nothing.
                case EventKind.PolityCollapse:
                    if (!e.Faction.IsNone)
                    {
                        foreach (string word in ContextPackBuilder.Distinctive(view.State.NameOf(e.Faction)))
                            index.Add(Collapsed, word, e.Year);
                    }
                    break;

                default: break;
            }
        }

        // Seat history for whichever powers this pack is about. Restricted to those, so a claim
        // about a seat the pack never mentions is left to the proper-noun check.
        HashSet<EntityId> powers = [];
        if (subject.Kind == EntityKind.Faction) powers.Add(subject);
        foreach (EventId id in events)
            foreach (Participant p in view.Log.Get(id).Participants)
                if (p.Id.Kind == EntityKind.Faction) powers.Add(p.Id);

        foreach ((EntityId faction, List<Tenure> spells) in PackDigest.AllSeatHistories(view))
        {
            if (!powers.Contains(faction)) continue;

            foreach (Tenure t in spells)
            {
                string surname = ContextPackBuilder.Surname(t.Holder);
                index.Add(TookSeat, surname, t.From);
                index._seatOf[$"{TookSeat}|{surname}|{t.From}"] = view.State.NameOf(faction);
            }
        }

        return index;
    }

    private static string Surname(WorldView view, EntityId actor) =>
        actor.IsNone || actor.Kind != EntityKind.Actor
            ? ""
            : ContextPackBuilder.Surname(view.State.NameOf(actor));
}
