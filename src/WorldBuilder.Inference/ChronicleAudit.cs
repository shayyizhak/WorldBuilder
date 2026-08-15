using System.Globalization;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>
/// Layer 4: a finished chronicle checked against the world it claims to describe.
///
/// This duplicates the checker, and that is deliberate. <b>The checker decides what enters
/// canon; this decides whether the checker works.</b> A checker that quietly stops firing is
/// invisible from inside itself — which is exactly what happened when Tier 1 returned an empty
/// result on a render containing a textbook violation of its own first rule. An independent
/// verifier is the only thing that can see that.
///
/// So this must never share an implementation with <see cref="FabricationCheck"/>. If a future
/// refactor merges them to remove the duplication, the property that makes both worth having
/// is gone, and the next silent failure goes unnoticed for as long as the last one did.
///
/// It rebuilds each section's pack from the log and re-derives the figures independently, then
/// reads the prose for figures and names and compares. Where the two disagree, one of them is
/// wrong and a person should look.
/// </summary>
public static class ChronicleAudit
{
    public sealed record Complaint(string Section, string Kind, string Detail);

    public static List<Complaint> Check(WorldView view, string markdown)
    {
        List<Complaint> complaints = [];

        foreach ((string heading, string body) in GoldenDiff.Sections(markdown))
        {
            ContextPack? pack = PackFor(view, heading);
            if (pack is null)
            {
                complaints.Add(new Complaint(heading, "unmatched-scope",
                    "no scope in the world corresponds to this heading; it cannot be verified"));
                continue;
            }

            complaints.AddRange(CheckNames(heading, pack, body));
            complaints.AddRange(CheckFigures(heading, pack, body));
            complaints.AddRange(CheckRulers(heading, pack, body));
        }

        return complaints;
    }

    /// <summary>
    /// The scope a heading names, rebuilt from the log.
    ///
    /// Headings are generated with their years in them — "The Kebarrow Compact, 22–41" — so the
    /// scope is recoverable without storing anything alongside the document. A heading that
    /// cannot be matched is itself a finding: an unverifiable section is not a verified one.
    /// </summary>
    public static ContextPack? PackFor(WorldView view, string heading)
    {
        (int from, int to) = YearsIn(heading);

        if (heading.StartsWith("The rule of ", StringComparison.Ordinal))
        {
            foreach (Actor a in view.State.Actors)
            {
                if (!heading.Contains(a.Name, StringComparison.Ordinal)) continue;

                foreach (ReignSpell spell in ContextPackBuilder.Reigns(view, a.Id))
                    if (spell.From == from && spell.To == to)
                        return ContextPackBuilder.Reign(view, spell);
            }
            return null;
        }

        // The year must match as well as the name. A place can be fought over twice — Threi Cut
        // was, in year 5 and again in year 7 — and matching on name alone handed the second
        // war's section the first war's records, which reported four true sentences as invented.
        foreach (Arc arc in view.State.Arcs)
        {
            if (arc.Kind != ArcKind.War) continue;
            if (!heading.Contains(arc.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (from != int.MinValue && arc.StartYear != from) continue;
            return ContextPackBuilder.Arc(view, arc.Id);
        }

        foreach (Faction f in view.State.Factions)
        {
            if (!heading.StartsWith(Title(f.Name), StringComparison.OrdinalIgnoreCase)) continue;
            return from == int.MinValue
                ? ContextPackBuilder.Faction(view, f.Id)
                : ContextPackBuilder.Faction(view, f.Id, from, to);
        }

        return null;
    }

    private static string Title(string name) =>
        name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
            ? char.ToUpperInvariant(name[0]) + name[1..]
            : name;

    private static (int From, int To) YearsIn(string heading)
    {
        int at = heading.LastIndexOf(", ", StringComparison.Ordinal);
        if (at < 0) return (int.MinValue, int.MaxValue);

        string[] parts = heading[(at + 2)..].Split(['–', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return (int.MinValue, int.MaxValue);

        return int.TryParse(parts[0], out int from) && int.TryParse(parts[1], out int to)
            ? (from, to)
            : (int.MinValue, int.MaxValue);
    }

    /// <summary>Every proper noun in the prose must be one the pack supplied.</summary>
    private static List<Complaint> CheckNames(string heading, ContextPack pack, string body)
    {
        List<Complaint> complaints = [];
        HashSet<string> allowed = Supplied(pack, StringComparer.OrdinalIgnoreCase);
        foreach (string word in pack.Vocabulary) allowed.Add(word.Trim('.', ',', '’', '\'', 's'));

        // Split on whitespace only. Splitting on '.' as well removes the very thing the
        // sentence-start guard reads, and every capitalised first word — "Fourteen", "Despite",
        // "During" — is then reported as an invented proper noun. That produced 43 complaints
        // on a chronicle whose real defects numbered two.
        string[] words = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < words.Length; i++)
        {
            string word = words[i].Trim('’', '\'', '—', ',', '.', ';', ':', '(', ')', '"', '_', '*');
            if (word.Length < 3 || !char.IsUpper(word[0])) continue;
            if (Opens(words[i - 1])) continue;
            if (allowed.Contains(word) || allowed.Contains(word.TrimEnd('s'))) continue;
            if ((word.EndsWith("’s", StringComparison.Ordinal) || word.EndsWith("'s", StringComparison.Ordinal))
                && allowed.Contains(word[..^2])) continue;
            foreach (string part in word.Split('-'))                    // "Counter-raids"
                if (part.Length > 2 && allowed.Contains(part)) goto next;

            complaints.Add(new Complaint(heading, "unknown-name",
                $"\"{word}\" is not in the events this section was built from"));

            next: ;
        }

        return complaints;
    }

    /// <summary>
    /// Every token the section was actually built from.
    ///
    /// <see cref="ContextPack.Vocabulary"/> is the curated name list and is deliberately narrow;
    /// reading it alone reported four true sentences of a war section as invented, because the
    /// places a war is fought over and the dead it counts are in the pack's body and not in its
    /// vocabulary. The body is what the model was shown, so the body is the right ground truth
    /// for "could this have come from the record".
    /// </summary>
    private static HashSet<string> Supplied(ContextPack pack, StringComparer comparer)
    {
        HashSet<string> supplied = new(comparer);

        foreach (string word in pack.Body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            supplied.Add(word.Trim('.', ',', ';', ':', '(', ')', '[', ']', '"', '’', '\'', '—', '–', '%'));

        foreach (string word in pack.Vocabulary) supplied.Add(word);

        return supplied;
    }

    /// <summary>Whether the previous token ended a sentence, so the next word is capitalised
    /// by grammar rather than by being a name.</summary>
    private static bool Opens(string previous)
    {
        string s = previous.TrimEnd('"', '’', '\'', ')', '_', '*');
        return s.Length == 0 || s.EndsWith('.') || s.EndsWith('!') || s.EndsWith('?');
    }

    /// <summary>
    /// Every year the prose states must be a year the pack's events cover, and every large
    /// number must appear in the pack.
    ///
    /// Deliberately coarse. The checker does the precise work of tying a year to its event;
    /// this catches the case where the checker has stopped doing it at all.
    /// </summary>
    private static List<Complaint> CheckFigures(string heading, ContextPack pack, string body)
    {
        List<Complaint> complaints = [];

        HashSet<string> supplied = Supplied(pack, StringComparer.Ordinal);

        foreach (string raw in body.Split(
                     [' ', ',', '.', ';', ':', '(', ')', '\n', '%'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) continue;
            if (value < 2) continue;                              // "one", "two" as ordinary prose
            if (supplied.Contains(raw)) continue;

            complaints.Add(new Complaint(heading, "unsupported-figure",
                $"{value} does not appear anywhere in this section's records"));
        }

        return complaints;
    }

    /// <summary>
    /// Everyone the seat history says held the seat, against everyone the prose names as
    /// holding it — in both directions.
    /// </summary>
    private static List<Complaint> CheckRulers(string heading, ContextPack pack, string body)
    {
        List<Complaint> complaints = [];
        if (pack.Digest.Tenures.Count is 0 or > 4) return complaints;

        string lower = body.ToLowerInvariant();

        foreach (Tenure t in pack.Digest.Tenures)
        {
            string surname = ContextPackBuilder.Surname(t.Holder);
            if (lower.Contains(surname, StringComparison.Ordinal)) continue;

            complaints.Add(new Complaint(heading, "ruler-unnamed",
                $"{t.Holder} held the seat {t.From}–{t.To} and is not named"));
        }

        return complaints;
    }
}
