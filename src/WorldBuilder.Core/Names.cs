using System.Globalization;

namespace WorldBuilder.Core;

/// <summary>
/// Deterministic invented-name generator. Proper nouns do a surprising amount of the work in
/// making a log readable — "Ironmark raids Dunmoor" is a sentence, "f:2 raids p:14" is a row —
/// so names are generated once from the world seed and never change.
/// </summary>
public sealed class NameForge(ulong seed)
{
    private static readonly string[] SimpleOnsets =
    [
        "b", "d", "f", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "v", "w", "th",
    ];

    private static readonly string[] ClusterOnsets =
    [
        "br", "dr", "fr", "gr", "kr", "tr", "st", "sk", "thr",
    ];

    private static readonly string[] Nuclei =
    [
        "a", "e", "i", "o", "u", "ae", "ei", "ou", "ea",
    ];

    /// <summary>
    /// Single consonants and a few soft pairs only. Earlier versions allowed any coda before
    /// any onset, which produced things like "Mousttheald Skeambyrk" — technically generated,
    /// unreadable in practice, and a log full of names nobody can pronounce is a log nobody
    /// forms attachments to.
    /// </summary>
    private static readonly string[] Codas =
    [
        "", "", "", "l", "n", "r", "s", "m", "ll", "rn", "th", "ld", "nd",
    ];

    private static readonly string[] PlaceSuffixes =
    [
        "mark", "moor", "hollow", "reach", "fell", "wick", "stead",
        "gate", "barrow", "mere", "ford", "crag", "dale", "holt",
    ];

    private static readonly string[] SiteSuffixes =
    [
        "Delve", "Pits", "Seam", "Cut", "Lode", "Shafts",
    ];

    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private readonly ulong _seed = seed;

    /// <summary>
    /// Names are drawn from a stream keyed by an explicit slot number rather than by mutating
    /// shared generator state, so adding a place later does not rename every actor.
    /// </summary>
    private Rng Stream(int slot) => Rng.For(_seed, 0, EntityId.None, RngPurpose.Naming).Branch(slot);

    public string PersonName(int slot)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            Rng rng = Stream(slot).Branch(attempt);
            string given = Word(ref rng, rng.Chance(55) ? 2 : 1);
            string family = Word(ref rng, rng.Chance(35) ? 2 : 1);
            string name = $"{Capitalise(given)} {Capitalise(family)}";
            if (_used.Add(name)) return name;
        }
        return $"Nameless {slot.ToString(CultureInfo.InvariantCulture)}";
    }

    public string PlaceName(int slot, PlaceKind kind)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            Rng rng = Stream(slot + 5000).Branch(attempt);
            string root = Capitalise(Word(ref rng, rng.Chance(30) ? 2 : 1, allowFinalCoda: false));

            string name = kind == PlaceKind.Site
                ? $"{root} {rng.Pick(SiteSuffixes)}"
                : root + rng.Pick(PlaceSuffixes);

            if (_used.Add(name)) return name;
        }
        return $"Waste {slot.ToString(CultureInfo.InvariantCulture)}";
    }

    public string RegionName(int slot)
    {
        Rng rng = Stream(slot + 9000);
        string name = $"the {Capitalise(Word(ref rng, 2))} Vale";
        _used.Add(name);
        return name;
    }

    /// <summary>Faction names lean on a seat or a founder so the map reads as inhabited.</summary>
    public string FactionName(int slot, string seatName, string founderSurname)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            Rng rng = Stream(slot + 7000).Branch(attempt);
            string name = rng.Next(4) switch
            {
                0 => $"House {founderSurname}",
                1 => $"the {seatName} Compact",
                2 => $"the {founderSurname} League",
                _ => $"the {seatName} Crown",
            };
            if (_used.Add(name)) return name;
        }
        return $"the {seatName} Remnant";
    }

    /// <summary>
    /// A breakaway names itself after the place that rose, not the people who led it. Over a
    /// long run the same town rises many times, so there are enough forms here to keep going
    /// without falling back on a numeric suffix — "the Free City of Meigate (133)" is a label,
    /// not a name.
    /// </summary>
    public string SecessionName(int year, string placeName)
    {
        string[] forms =
        [
            $"the Free City of {placeName}",
            $"the {placeName} Commune",
            $"the Sworn Men of {placeName}",
            $"the {placeName} Republic",
            $"Greater {placeName}",
            $"the Second Crown of {placeName}",
            $"the {placeName} Covenant",
            $"the Burghers of {placeName}",
            $"the Rising of {placeName}",
            $"the {placeName} Assembly",
            $"Free {placeName}",
            $"the {placeName} Charter",
        ];

        Rng rng = Rng.For(_seed, year, EntityId.None, RngPurpose.Naming);
        int start = rng.Next(forms.Length);

        for (int i = 0; i < forms.Length; i++)
        {
            string candidate = forms[(start + i) % forms.Length];
            if (_used.Add(candidate)) return candidate;
        }

        // Every form spent on this one town: fall back to an ordinal, as a chronicler would.
        string ordinal = $"the {Ordinal(_used.Count % 9 + 2)} Republic of {placeName}";
        _used.Add(ordinal);
        return ordinal;
    }

    private static string Ordinal(int n) => n switch
    {
        2 => "Second", 3 => "Third", 4 => "Fourth", 5 => "Fifth",
        6 => "Sixth", 7 => "Seventh", 8 => "Eighth", 9 => "Ninth",
        _ => n.ToString(CultureInfo.InvariantCulture) + "th",
    };

    /// <summary>
    /// Builds a pronounceable word of one or two syllables. The only real rule is the junction:
    /// a syllable that ends in a consonant is never followed by one that starts with a cluster,
    /// which is what keeps the output sayable.
    /// </summary>
    private static string Word(ref Rng rng, int syllables, bool allowFinalCoda = true)
    {
        string onset = rng.Chance(25) ? rng.Pick(ClusterOnsets) : rng.Pick(SimpleOnsets);
        string word = onset + rng.Pick(Nuclei);

        if (syllables == 1)
            return allowFinalCoda ? word + rng.Pick(Codas) : word;

        string joint = rng.Pick(Codas);
        word += joint;

        // Cluster onsets are only allowed against an open syllable.
        word += joint.Length == 0 && rng.Chance(20) ? rng.Pick(ClusterOnsets) : rng.Pick(SimpleOnsets);
        word += rng.Pick(Nuclei);

        return allowFinalCoda ? word + rng.Pick(Codas) : word;
    }

    private static string Capitalise(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

/// <summary>Names for the multi-event storylines. Templates only — no cleverness, no model.</summary>
public static class ArcNames
{
    public static string War(ref Rng rng, string overPlace, string aggressor, string grudgeHolder)
    {
        return rng.Next(5) switch
        {
            0 => $"the {overPlace} War",
            1 => $"the War for {overPlace}",
            2 => $"the {aggressor} Aggression",
            3 => $"the War of {grudgeHolder}'s Grudge",
            _ => $"the Long Quarrel over {overPlace}",
        };
    }

    public static string Famine(ref Rng rng, string place, int year)
    {
        return rng.Next(3) switch
        {
            0 => $"the {place} Hunger",
            1 => $"the Lean Years of {place}",
            _ => $"the Famine of {year.ToString(CultureInfo.InvariantCulture)}",
        };
    }

    public static string Plot(ref Rng rng, string plotter, string target)
    {
        return rng.Next(3) switch
        {
            0 => $"{plotter}'s Conspiracy",
            1 => $"the Plot against {target}",
            _ => $"the Whispering against {target}",
        };
    }

    public static string Succession(ref Rng rng, string faction, int year)
    {
        return rng.Next(3) switch
        {
            0 => $"the Disputed Succession of {faction}",
            1 => $"the Two Claims to {faction}",
            _ => $"the {year.ToString(CultureInfo.InvariantCulture)} Crisis in {faction}",
        };
    }
}
