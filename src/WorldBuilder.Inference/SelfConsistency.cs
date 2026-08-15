using System.Globalization;

namespace WorldBuilder.Inference;

/// <summary>
/// Tier 1 of the checker: does a passage contradict itself?
///
/// Deliberately given no access to the world. Every rule here compares the text against other
/// parts of the same text, which makes it cheap enough to run on every render and — more
/// usefully — impossible for it to be wrong about the world, because it never consults it. If
/// a section says "two marriages" and then names three, one of those is false whatever the
/// events say.
///
/// This tier exists because of a measurement: the round-9 render passed the event-aware checks
/// clean and contained six defects. Everything the older checks caught was a suspicious *word*
/// — "coup", "seizure", "the Compact" — and by round 9 no defect used one. Arithmetic over the
/// prose catches a different class entirely, and catches it for nothing.
///
/// The bias throughout is silence over noise. A checker whose findings are excluded from canon
/// cannot afford to be approximately right: where a construction cannot be parsed confidently,
/// nothing is reported. Every rule below fires only on shapes that are unambiguous.
/// </summary>
public static class SelfConsistency
{
    /// <summary>
    /// A countable thing, the words that introduce one instance of it, and its singular form.
    ///
    /// A lexicon rather than a parser: these are the things this world's prose counts, and the
    /// list is short because the engine's event kinds are. Anything not named here is simply
    /// not counted, which costs a missed finding and never a false one.
    /// </summary>
    /// <summary>
    /// How one instance of a countable thing is recognised, which decides whether a shortfall
    /// can be trusted.
    ///
    /// Counting by year undercounts whenever two instances share one — "battles at Kebarrow in
    /// 32, 33 and 34" is three members and "lost battles in 34 twice" is two members and one
    /// year. Counting by the proper name attached to each instance does not have that problem,
    /// because two conquests of one town are the same town. So a shortfall is only reported
    /// where the instance key is a name.
    /// </summary>
    private enum Members
    {
        /// <summary>Distinct years near a mention. Reliable upward, not downward.</summary>
        ByYear,
        /// <summary>The capitalised name after the verb — "took Laehiford".</summary>
        ByNameAfter,
        /// <summary>The capitalised name before the phrase — "Math Ham held the seat".</summary>
        ByNameBefore,
        /// <summary>One per occurrence of the phrase. For things narrated exactly once each.</summary>
        ByPhrase,

        /// <summary>
        /// One per "&lt;Name&gt; in &lt;year&gt;" item. For lists of people, where the members
        /// are names and two of them may legitimately share a year — the round-11 list has
        /// fourteen people and twelve distinct years.
        /// </summary>
        ByDatedItem,
    }

    private static readonly (string Plural, string Singular, Members Key, string[] Marks)[] Countables =
    [
        ("battles", "battle", Members.ByYear, ["defeated", "beat ", "lost to", "battle at", "was defeated"]),
        ("raids", "raid", Members.ByYear, ["raid on", "raided", "raid against"]),
        ("marriages", "marriage", Members.ByYear, ["married", "marriage"]),
        ("uprisings", "uprising", Members.ByYear, ["rose against", "revolt", "rising against"]),
        ("revolts", "revolt", Members.ByYear, ["rose against", "revolt", "rising against"]),

        ("wars", "war", Members.ByPhrase, ["declared war", "peace was made", "made peace"]),

        // "three places taken from the Wurn League" against "took Laehiford… took Hadale".
        // A world total leaked into a faction's section and the section's own narration
        // disagreed with it, which is visible without leaving the page.
        ("places", "place", Members.ByNameAfter, ["took", "taken", "seized", "captured"]),
        ("settlements", "settlement", Members.ByNameAfter, ["took", "taken", "seized", "captured"]),
        ("conquests", "conquest", Members.ByNameAfter, ["took", "taken", "seized", "captured"]),

        // Lists of people, whose members are names rather than years. "People" is generic
        // enough to need one of its own verbs nearby before it counts as a claim, the same way
        // "places" does — otherwise every mention of people in a section is a count of them.
        ("people", "person", Members.ByDatedItem,
            ["returned", "return", "took service", "murdered", "killed", "cast out", "exiled"]),
        ("exiles", "exile", Members.ByDatedItem, ["returned", "return", "took service"]),
        ("returns", "return", Members.ByDatedItem, ["returned", "return", "took service"]),

        // Deliberately no entry for rulers or holders, though a shortfall there is a real
        // defect and one was found by hand this round.
        //
        // Counting narrated rulers from text alone does not work. Prose introduces them a
        // dozen ways — "took the seat", "taking the seat", "assumed power", "was killed by" —
        // and tightening the verb list produced three findings against three correct sections,
        // while loosening it counted a man named only as a murder victim as a ruler and lost
        // the one true finding. The distinction between "named" and "named as a ruler" is not
        // in the text; it is in the seat history. That check lives with the event-aware ones.
    ];

    /// <summary>
    /// Above this, a shortfall is compression rather than an omission.
    ///
    /// The prompt tells the renderer to summarise long lists and give the number, so a section
    /// naming six of eleven rulers is doing as it was told. Three holders and two named is a
    /// different thing: at that size the enumeration is the passage.
    /// </summary>
    private const int LongestEnumerationExpected = 4;

    /// <summary>Words that mark an enumeration as deliberately partial.</summary>
    /// <summary>
    /// Words that mark an enumeration as deliberately partial.
    ///
    /// "included" was missing, and that one omission is most of why the rule went quiet: the
    /// round-7 case used "including" and was caught, the round-11 case used "included" and was
    /// not. A list of markers is only as good as its least common member.
    /// </summary>
    public static readonly string[] PartialMarkers =
    [
        "including", "included", "include", "among them", "amongst them", "among these",
        "such as", "chief among", "for example", "notably", "namely",
    ];

    /// <summary>
    /// Phrases that mark a subset of a larger count — "two of which", "three of these". A
    /// paragraph containing one of these is doing subset arithmetic rather than enumerating,
    /// and its counts are not comparable to a raw instance tally.
    /// </summary>
    private static readonly string[] SubsetMarkers =
        ["of which", "of these", "of them"];

    /// <summary>The names these rules report coverage under. Stable; the sidecar keys on them.</summary>
    public static class Rules
    {
        public const string CountEnumeration = "count-enumeration";
        public const string CountNarration = "count-narration";
        public const string PartitionSum = "partition-sum";
        public const string DateAgreement = "date-agreement";
        public const string SummaryBody = "summary-body";
        public const string CoinedTerm = "coined-term";
    }

    public static IReadOnlyList<Fabrication> Check(string passage) => Check(passage, new Coverage());

    /// <summary>
    /// Tier 1, recording how much of the passage each rule read.
    ///
    /// The coverage argument is not optional and has no default. A rule that quietly stops
    /// reporting is the failure this whole mechanism exists to catch, and an overload that lets
    /// a caller forget to ask would reintroduce it one call site at a time.
    /// </summary>
    public static IReadOnlyList<Fabrication> Check(string passage, Coverage cover)
    {
        List<Fabrication> findings = [];

        // Registered before they run, so a rule that extracts nothing is recorded as a zero
        // rather than being absent from the report. Absent and zero read the same to a person
        // and differently to the golden diff, which is the consumer that matters.
        foreach (string rule in new[]
        {
            Rules.CountEnumeration, Rules.CountNarration, Rules.PartitionSum,
            Rules.DateAgreement, Rules.SummaryBody, Rules.CoinedTerm,
        })
        {
            cover.Ran(rule);
        }

        findings.AddRange(CountVersusList(passage, cover));
        findings.AddRange(CountVersusNarration(passage, cover));
        findings.AddRange(PartitionSums(passage, cover));
        findings.AddRange(DateAgreement(passage, cover));
        findings.AddRange(Contradictions(passage, cover));
        findings.AddRange(StrayCapitals(passage, cover));

        return findings;
    }

    /// <summary>
    /// A common noun capitalised mid-sentence, which is how the model coins a concept.
    ///
    /// "failed Counter-raids" — the capital does the work of making it a term of art, and
    /// counter-raid is not a thing this world has. The word is harmless and the capital is the
    /// tell, so this catches the tell. Needs no access to the world, which is why it is here:
    /// a proper noun is checked against the pack, and a common noun wearing a capital can be
    /// judged from the sentence alone.
    /// </summary>
    private static List<Fabrication> StrayCapitals(string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        string[] words = passage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < words.Length; i++)
        {
            string word = words[i].Trim('’', '\'', '—', ',', '.', ';', ':', '(', ')', '"', '_', '*');
            if (word.Length < 4 || !char.IsUpper(word[0])) continue;
            if (Opens(words[i - 1])) continue;

            // Only where every part of it is an ordinary word. A hyphenated coinage counts if
            // both halves are common, which is exactly the shape of "Counter-raids".
            bool common = true;
            foreach (string part in word.Split('-', StringSplitOptions.RemoveEmptyEntries))
                if (!CommonNouns.Contains(part)) common = false;

            cover.Extracted(Rules.CoinedTerm);
            cover.Checked(Rules.CoinedTerm);
            if (!common || !reported.Add(word)) continue;

            findings.Add(new Fabrication(word, "stray-capital",
                $"\"{word}\" is an ordinary word wearing a capital; the world has no such term"));
        }

        return findings;
    }

    private static bool Opens(string previous)
    {
        string s = previous.TrimEnd('"', '’', '\'', ')', '_', '*');
        return s.Length == 0 || s.EndsWith('.') || s.EndsWith('!') || s.EndsWith('?');
    }

    /// <summary>
    /// The nouns this world's prose is built from. Short on purpose: every addition is a word
    /// the check will now police, and policing a real name would cost a true section.
    /// </summary>
    private static readonly HashSet<string> CommonNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "raid", "raids", "counter", "battle", "battles", "war", "wars", "peace", "seat",
        "throne", "reign", "rule", "ruler", "rulers", "exile", "exiles", "outlaw", "outlaws",
        "marriage", "marriages", "alliance", "alliances", "famine", "famines", "plague",
        "sickness", "hunger", "revolt", "revolts", "uprising", "uprisings", "conquest",
        "succession", "claim", "claimant", "heir", "steward", "tribute", "grievance",
        "settlement", "settlements", "power", "powers", "people", "person", "killing",
        "killings", "murder", "murders", "conspiracy", "plot", "plots", "harvest", "grain",
        "ore", "silver", "treasury", "legitimacy", "standing", "followers",
    };

    /// <summary>
    /// Rule 1.4: two things asserted of one person in one year that cannot both be true.
    ///
    /// The specification frames this as summary-versus-body, and the body is indeed where the
    /// contradiction usually surfaces — an opening saying a man took the seat at his power's
    /// founding, and a paragraph below saying he took service with it. But which half is the
    /// summary does not matter and cannot be reliably identified; that the two disagree is
    /// enough, and needs nothing but the text.
    /// </summary>
    private static List<Fabrication> Contradictions(string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        // Who is said to have done what, and when. A pronoun carries the previous sentence's
        // subject, which is how "He took service with the power in 20" attaches to the man the
        // sentence before it named.
        Dictionary<string, HashSet<string>> claims = new(StringComparer.OrdinalIgnoreCase);
        string? subject = null;

        foreach (string sentence in Sentences(passage))
        {
            string lower = sentence.ToLowerInvariant();
            int year = YearIn(sentence) ?? int.MinValue;

            foreach ((string one, string other) in Exclusive)
            {
                foreach (string phrase in new[] { one, other })
                {
                    int at = lower.IndexOf(phrase, StringComparison.Ordinal);
                    if (at < 0) continue;

                    // The trigger matched. Whether a subject can be resolved for it is the
                    // next question, and the gap between the two is precisely where this rule
                    // went inert: every sentence matched, and an unstripped possessive meant
                    // none of them resolved to a person.
                    // Only where the sentence carries a year. This rule keys a claim on person
                    // and year, so a pair phrase with no year is not an assertion it can hold
                    // — counting it extracted described twenty-two ordinary sentences as
                    // assertions the rule had dropped.
                    if (year == int.MinValue) continue;

                    cover.Extracted(Rules.SummaryBody);

                    string? who = NameBefore(sentence[..at]) ?? subject;

                    if (who is null)
                    {
                        cover.Unresolvable(Rules.SummaryBody, "no subject for the act", Shorten(sentence));
                        continue;
                    }

                    cover.Checked(Rules.SummaryBody);

                    string key = $"{who}|{year}";
                    if (!claims.TryGetValue(key, out HashSet<string>? acts))
                        claims[key] = acts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    acts.Add(Normalise(phrase));
                }
            }

            // The leading name, not the trailing one: a pronoun in the next sentence refers to
            // what this one was about, and "Realsis Leirpu's rule ended in disgrace" is about
            // Leirpu however it finishes.
            if (LeadingName(sentence) is { } named) subject = named;
        }

        foreach ((string key, HashSet<string> acts) in claims)
        {
            if (acts.Count < 2) continue;

            string who = key.Split('|')[0];
            if (!reported.Add(key)) continue;

            findings.Add(Finding("self-contradiction", passage,
                $"{who} {string.Join(" and ", acts)} in {key.Split('|')[1]}",
                "both cannot be true of one person in one year"));
        }

        return findings;
    }

    /// <summary>Participle and past forms of one act collapse to the same act.</summary>
    private static string Normalise(string phrase) => phrase
        .Replace("taking", "took", StringComparison.OrdinalIgnoreCase);

    // ---- 1.1 count versus enumeration -------------------------------------

    /// <summary>
    /// A stated count against a list in the same sentence.
    ///
    /// Three outcomes, and the rule is symmetric: an exhaustive list must match its count, a
    /// partial one must be shorter than its count, and a partial marker in front of a complete
    /// list is itself the failure — "including" tells a reader there are more when there are
    /// not, which makes the chronicle unusable as a reference.
    /// </summary>
    private static List<Fabrication> CountVersusList(string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        // A sentence and the one after it, because a count and its list are routinely split:
        // "Fourteen people returned… . These returns included A in 22, B in 24, …". Judging one
        // sentence at a time found the count with no list and the list with no count, and so
        // said nothing about a textbook violation of this very rule.
        foreach (string sentence in Windows(passage))
        {
            string lower = sentence.ToLowerInvariant();

            foreach ((string plural, string singular, Members key, string[] marks) in Countables)
            {
                if (!lower.Contains(plural, StringComparison.Ordinal)) continue;
                if (Stated(lower, plural) is not int count || count < 2) continue;

                // A generic noun needs one of its own verbs beside it to be a claim about that
                // thing at all. Without this, "people" counts every mention of people.
                if (key is Members.ByNameAfter or Members.ByDatedItem)
                {
                    int noun = lower.IndexOf(plural, StringComparison.Ordinal);
                    if (!MarkNear(lower[(noun + plural.Length)..], marks)) continue;
                }

                // The list has to be introduced, or there is no list — just prose that mentions
                // the noun. A colon, a dash, or a partiality marker is the introduction.
                bool partial = false;
                int at = -1;

                foreach (string marker in PartialMarkers)
                {
                    int found = lower.IndexOf(marker, StringComparison.Ordinal);
                    if (found < 0) continue;
                    partial = true;
                    at = found + marker.Length;
                    break;
                }

                if (at < 0) at = sentence.IndexOfAny([':', '—']) + 1;
                if (at <= 0) continue;

                // A list introduced before the thing it supposedly enumerates is not its list.
                // Joining a sentence to the next one let a colon from the first pair up with a
                // count from the second, and reported three raids as seven exiles.
                if (at <= lower.IndexOf(plural, StringComparison.Ordinal))
                {
                    continue;
                }

                // The list must belong to this count. Where another counted thing sits between
                // the two — "Seven exiles returned… The Sworn Men sent out three raids: …" —
                // the list is that one's, and pairing them reported seven exiles against three
                // raids. An intervening noun with no number of its own is just a restatement.
                if (AnotherCountBetween(lower, plural, at))
                {
                    continue;
                }

                // Counted here rather than at the count, because an assertion is a count and a
                // list belonging to it. Recording it at the count made the number insensitive
                // to the thing most likely to break it — a missing partiality marker leaves the
                // count perfectly parseable and the list unfindable, which is round 11's first
                // cause — and recording it before these two guards described a count with no
                // list of its own as an assertion the rule had dropped.
                cover.Extracted(Rules.CountEnumeration);

                // The list ends where the next count of the same thing begins, and at the end of
                // its own sentence.
                //
                // Both bounds were learned the same way. Two sentences joined — "sent six raids:
                // <three places> … suffered three raids: <three more>" — put nine members under a
                // heading of six. And a list that ended at its full stop still swallowed the next
                // sentence's date: "…Vea Lode in 19…. The period ended in 23…" counted four
                // members of three, the fourth being a year belonging to nobody.
                //
                // The two bounds are not the same bound, because the two failures are not
                // symmetric. A list may legitimately spill into the sentence after it — "Three
                // marriages: … in 37 … in 48. A third followed in 49" — so a *shortfall* is
                // judged over the whole window. Nothing may be added to a list from a later
                // sentence, so an *excess* is judged over the list's own sentence alone.
                int stop = NextCount(lower, plural, at);
                string window = sentence[at..stop];
                string list = sentence[at..Math.Min(stop, EndOfSentence(sentence, at))];

                int items = key == Members.ByDatedItem
                    ? DatedItems(list)
                    : Instances(list, marks);

                cover.Checked(Rules.CountEnumeration);

                // A list can mark its members with dates rather than by repeating the noun:
                // "two marriages: one to X in 37 and two to Y in 48 and 49" says "marriage"
                // once and names three. Distinct years undercount where two members share a
                // year, so this fallback only ever reports having found MORE than was
                // claimed — a direction it cannot be wrong in.
                if (items == 0)
                {
                    // A roster of people need carry no dates at all — "Four people were murdered
                    // from within, including A, B, C and D" names its four and marks them as a
                    // sample of more. Counting only dated members made that invisible, and it is
                    // the oldest unfixed row in the corpus.
                    if (partial && key == Members.ByDatedItem && NamedItems(list) >= count
                        && reported.Add($"hedged|{plural}|{count}"))
                    {
                        findings.Add(Finding("hedged-exhaustive-list", sentence,
                            $"more than {count} {plural}", $"all {count} of them named"));
                        continue;
                    }

                    int dated = DistinctYearsIn(list);
                    if (dated <= count) continue;

                    findings.Add(Finding("count-vs-list", sentence,
                        $"{count} {(count == 1 ? singular : plural)}", $"{dated} named"));
                    continue;
                }

                if (partial && items >= count)
                {
                    // Reported under the existing kind, and stays a readability finding: a
                    // complete list called partial misleads about the size of the set without
                    // saying anything false about the world.
                    if (!reported.Add($"hedged|{plural}|{count}")) continue;

                    findings.Add(Finding("hedged-exhaustive-list", sentence,
                        $"more than {count} {plural}", $"all {count} of them named"));
                }
                else if (!partial && items != count)
                {
                    // A list may elide its verb after the first member — "beat X at Threi Cut
                    // in 34 and at Griwick in 35" is one verb and two battles — so the dated
                    // members are counted too, and a shortfall is reported only where both
                    // ways of counting fall short.
                    int members = Math.Max(items, DistinctYearsIn(list));

                    // A shortfall gets the benefit of the following sentence; an excess does not.
                    if (members < count)
                        members = Math.Max(members,
                            Math.Max(key == Members.ByDatedItem ? DatedItems(window) : Instances(window, marks),
                                     DistinctYearsIn(window)));
                    if (members == count) continue;
                    if (!reported.Add($"list|{plural}|{count}|{members}")) continue;

                    findings.Add(Finding("count-vs-list", sentence,
                        $"{count} {(count == 1 ? singular : plural)}", $"{members} named"));
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// A stated count against the instances the passage then narrates.
    ///
    /// Split into two rules of very different confidence, because counting instances of a thing
    /// in prose is unreliable in one direction and not the other.
    ///
    /// Naming MORE than you counted is unambiguous — three dated marriages under a heading of
    /// two means one of the two numbers is wrong, whatever the world says. Naming FEWER is
    /// usually just compression, and a compact list ("battles at Kebarrow in 32, 33 and 34")
    /// defeats any instance count I can write. So the "fewer" case is restricted to wars, which
    /// this world narrates one declaration or one peace at a time and never in a compact list.
    ///
    /// The alternative — counting battles and raids in both directions — produced four findings
    /// against four correct paragraphs on its first outing, which is the failure mode this whole
    /// tier is supposed to avoid.
    /// </summary>
    private static List<Fabrication> CountVersusNarration(string passage, Coverage cover)
    {
        List<Fabrication> findings = [];

        // Scoped to the whole section, not the paragraph.
        //
        // A section states its totals in an opening paragraph and narrates them over the ones
        // that follow — "three places taken" up top, "took Laehiford" and "took Hadale" two and
        // four paragraphs down. Judging paragraph by paragraph could never see that, and the
        // shortfall it was built to catch is exactly this shape.
        string lower = passage.ToLowerInvariant();

        foreach ((string plural, string singular, Members key, string[] marks) in Countables)
        {
            // Lists of people are judged only where they are presented as lists, which is the
            // other rule's job. Counting them across a whole section counts every dated name
            // in it — a section that correctly named four returning exiles was reported as
            // naming nine, because nine people are dated somewhere in it.
            if (key == Members.ByDatedItem) continue;

            if (Claimed(passage, plural, key, marks) is not (int count, string phrase)) continue;
            if (count < 2) continue;

            cover.Extracted(Rules.CountNarration);

            // Hedging is judged on the words around this count, not on the sentence.
            // "seven rulers, five of them killed, and three places taken" hedges the rulers and
            // says nothing about the places, and reading the sentence as a whole let "of them"
            // disqualify a claim it had nothing to do with.
            if (Hedged(Around(phrase, plural)))
            {
                cover.Unresolvable(Rules.CountNarration, "the count is hedged, so it is not a claim of exactness", Around(phrase, plural));
                continue;
            }

            int narrated = Narrated(passage, key, marks);

            cover.Checked(Rules.CountNarration);

            // More named than counted. Sound in any key: the instances found are a lower bound
            // on the instances present, so exceeding the stated count is a contradiction.
            if (narrated > count && narrated >= 2)
            {
                findings.Add(Finding("count-vs-narration", phrase,
                    $"{count} {(count == 1 ? singular : plural)}", $"{narrated} named"));
                continue;
            }

            // Fewer told than counted. Only where the instance key is a name or a fixed phrase,
            // and only for a list short enough that enumerating it was the expectation.
            if (key == Members.ByYear) continue;
            if (count > LongestEnumerationExpected) continue;
            if (narrated == 0 || narrated >= count) continue;

            findings.Add(Finding("count-vs-narration", phrase,
                $"{count} {(count == 1 ? singular : plural)}", $"only {narrated} told"));
        }

        return findings;
    }

    /// <summary>Instances of a thing in a passage, counted the way its key says.</summary>
    private static int Narrated(string passage, Members key, string[] marks) => key switch
    {
        Members.ByYear => DatedInstances(passage, marks),
        Members.ByPhrase => Occurrences(passage.ToLowerInvariant(), marks),
        Members.ByNameAfter => NamedInstances(passage, marks, after: true),
        Members.ByDatedItem => DatedItems(passage),
        _ => NamedInstances(passage, marks, after: false),
    };

    /// <summary>
    /// List members that are bare names — runs of capitalised words, separated by commas or
    /// "and".
    ///
    /// Only ever used to decide whether a list marked partial is in fact complete, which is a
    /// readability finding and cannot put a true section out of canon. That is what makes the
    /// looseness affordable: a place name miscounted as a person costs a note and nothing else.
    /// </summary>
    private static int NamedItems(string text)
    {
        int items = 0;
        bool inside = false;

        foreach (string raw in text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Strip(raw);
            bool name = word.Length > 2 && char.IsUpper(word[0]);

            if (name && !inside) items++;

            // The separator ends the run. Stripping punctuation first made "Ska, Nael" one
            // name of two words, and four people counted as two.
            inside = name && !raw.EndsWith(',') && !raw.EndsWith(';') && !raw.EndsWith(':');
        }

        return items;
    }

    /// <summary>
    /// List members of the form "&lt;Name&gt; in &lt;year&gt;", counted as occurrences.
    ///
    /// Occurrences, not distinct years: fourteen people returning over twelve years is fourteen
    /// members, and counting years would have reported the list as two short of its own total
    /// rather than exactly equal to it.
    /// </summary>
    private static int DatedItems(string text)
    {
        string[] words = text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        int items = 0;

        for (int i = 1; i < words.Length; i++)
        {
            if (Strip(words[i - 1]).ToLowerInvariant() is not "in") continue;
            if (!int.TryParse(Strip(words[i]), NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)) continue;
            if (year < 2) continue;

            // A name must sit in front of it, or this is a bare date rather than a list member.
            for (int back = 2; back <= 3 && i - back >= 0; back++)
            {
                string candidate = Strip(words[i - back]);
                if (candidate.Length > 2 && char.IsUpper(candidate[0]) && !Ordinary.Contains(candidate))
                {
                    items++;
                    break;
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Distinct proper names attached to each mention of a thing — the place a verb took, or
    /// the person a phrase is about.
    ///
    /// Relative pronouns are stepped over on the way back, because "Renbeir Surn, who took the
    /// seat in 19" puts "who" where the name should be and would otherwise count as nobody.
    /// </summary>
    private static int NamedInstances(string passage, string[] marks, bool after)
    {
        string[] words = passage.Split([' ', ',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < words.Length; i++)
        {
            string word = Strip(words[i]).ToLowerInvariant();

            // The mark is either this word, or this word and the next two — "held the seat".
            bool isMark = false;
            int length = 1;
            foreach (string mark in marks)
            {
                string[] parts = mark.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (i + parts.Length > words.Length) continue;

                bool all = true;
                for (int p = 0; p < parts.Length; p++)
                    if (!string.Equals(Strip(words[i + p]), parts[p], StringComparison.OrdinalIgnoreCase)) all = false;

                if (!all) continue;
                isMark = true;
                length = parts.Length;
                break;
            }
            if (!isMark) continue;

            if (after)
            {
                // "took Laehiford" — but not "took the seat", which is a different act.
                if (i + length >= words.Length) continue;
                string next = Strip(words[i + length]);
                if (next.Length > 2 && char.IsUpper(next[0]) && !Ordinary.Contains(next))
                    names.Add(next.ToLowerInvariant());
                continue;
            }

            for (int back = 1; back <= 3 && i - back >= 0; back++)
            {
                string candidate = Strip(words[i - back]);
                if (candidate.ToLowerInvariant() is "who" or "whom" or "and" or "then") continue;
                if (candidate.Length > 2 && char.IsUpper(candidate[0]) && !Ordinary.Contains(candidate))
                    names.Add(candidate.ToLowerInvariant());
                break;
            }
        }

        return names.Count;
    }

    private static int Occurrences(string lower, string[] marks)
    {
        int total = 0;
        foreach (string mark in marks) total += Counts(lower, mark);
        return total;
    }

    // ---- 1.2 partition sums -----------------------------------------------

    /// <summary>Ways a set gets divided up, each expecting "&lt;n&gt; &lt;word&gt;".</summary>
    private static readonly string[] PartitionWords =
        ["killed", "cast out", "died", "still holding", "replaced", "defected", "won", "lost",
         "beaten off", "carried off plunder", "carried off", "outlawed", "murdered",

         // The three raid outcomes. Without the middle one, "five raids: one carried off
         // plunder…, three got through but took nothing, and one was beaten off" found two
         // parts of the three and reported a correct partition as adding to two.
         "got through", "took nothing", "repulsed"];

    /// <summary>Words that may stand between a part's number and the word naming it.</summary>
    private static readonly string[] PartLeaders = ["was ", "were ", "of them ", "of which ", "of these "];

    /// <summary>
    /// A total against the parts it is divided into. "Seven people held the seat, with five
    /// killed, one cast out and one still holding" is checkable arithmetic, and it has been got
    /// wrong twice — once summing to ten against eleven, once to five against four.
    /// </summary>
    /// <summary>Nouns a total can be a total of.</summary>
    private static readonly string[] TotalNouns =
        ["people", "holders", "rulers", "men", "raids", "battles", "marriages", "wars",
         "exiles", "places", "settlements", "conspiracies", "individuals"];

    private static List<Fabrication> PartitionSums(string passage, Coverage cover)
    {
        List<Fabrication> findings = [];

        foreach (string sentence in Sentences(passage))
        {
            string lower = sentence.ToLowerInvariant();
            string[] words = lower.Split([' ', ',', ';', ':'], StringSplitOptions.RemoveEmptyEntries);

            // The total is "<n> <noun>", not merely the first number in the sentence. Taking
            // the first number made a year the total and every later year a part, and reported
            // that three seat-holders "add to 44".
            int total = 0, totalAt = -1;
            for (int i = 0; i + 1 < words.Length && totalAt < 0; i++)
            {
                if (Number(Strip(words[i])) is not int value || value < 2) continue;
                if (IsYear(words, i)) continue;
                if (Array.IndexOf(TotalNouns, Strip(words[i + 1])) < 0) continue;
                total = value;
                totalAt = i;
            }
            if (totalAt < 0) continue;

            // A total is only an assertion this rule can hold if the sentence divides it into
            // two or more parts.
            //
            // "Hunger killed 93 people" has a number and one of the total nouns and divides
            // nothing; "it suffered five raids, three of which were beaten off" names a subset
            // rather than a partition. Treating either as an extracted assertion is what put
            // this rule at one check in thirty-three, all of the rest being ordinary sentences
            // it was right to leave alone — and no way to tell that from the number.
            List<int> parts = Parts(words, totalAt);
            if (parts.Count < 2) continue;

            cover.Extracted(Rules.PartitionSum);
            cover.Checked(Rules.PartitionSum);

            int sum = 0;
            foreach (int part in parts) sum += part;

            if (sum == total) continue;

            findings.Add(Finding("partition-sum", sentence,
                $"{total} in total", $"the parts add to {sum}"));
        }

        return findings;
    }

    /// <summary>
    /// Strips every leading copula and subset phrase, repeatedly.
    ///
    /// "five of which were beaten off" carries two of them. Stripping once left "were beaten
    /// off", which starts with no partition word, so a sentence that divided its total correctly
    /// was read as dividing it into nothing.
    /// </summary>
    private static string Unlead(string tail)
    {
        for (bool stripped = true; stripped;)
        {
            stripped = false;

            foreach (string leader in PartLeaders)
            {
                if (!tail.StartsWith(leader, StringComparison.Ordinal)) continue;
                tail = tail[leader.Length..];
                stripped = true;
            }
        }

        return tail;
    }

    /// <summary>
    /// The parts a sentence divides its total into, after the total.
    ///
    /// One reader, used both to decide whether there is a partition here at all and to add it
    /// up. Two readers drifted: the first said a sentence divided its total and the second could
    /// not find the parts, so the rule reported eight assertions it had dropped when what it had
    /// really met was eight sentences that divide nothing.
    ///
    /// The distinction between a total and a quantity is not in the number, it is in what
    /// follows. "Eleven rulers: five killed and six cast out" divides. "Hunger killed 93 people"
    /// does not, and neither does "five raids, three of which were beaten off" — a subset named
    /// is not a set partitioned, and there is no arithmetic to check in it.
    /// </summary>
    private static List<int> Parts(string[] words, int totalAt)
    {
        List<int> parts = [];

        for (int i = totalAt + 1; i < words.Length; i++)
        {
            if (Number(Strip(words[i])) is not int value) continue;
            if (IsYear(words, i)) continue;

            // A number naming a quantity of something is not a part of the total. "33 grain and
            // killed 45" was read as a part of thirty-three because a partition word sat three
            // words away, and a true sentence about three raids reported as adding to 35.
            if (i + 1 < words.Length && Quantities.Contains(Strip(words[i + 1]))) continue;

            // "six for attempted murder" — the preposition gives the category.
            if (i + 1 < words.Length
                && Strip(words[i + 1]).Equals("for", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(value);
                continue;
            }

            // Otherwise the word naming the part, anywhere up to the next number. A
            // prepositional phrase may stand between them — "two against the Kebarrow Compact
            // carried off plunder" is a part of two — and requiring the word to follow the
            // number directly lost every part written that way. Stopping at the next number is
            // what keeps the reach from spilling into the following part.
            int stop = words.Length;
            for (int j = i + 1; j < words.Length; j++)
            {
                if (Number(Strip(words[j])) is null || IsYear(words, j)) continue;
                stop = j;
                break;
            }

            string tail = Unlead(string.Join(' ', words[(i + 1)..stop]));

            foreach (string word in PartitionWords)
            {
                if (!tail.Contains(word, StringComparison.Ordinal)) continue;
                parts.Add(value);
                break;
            }
        }

        return parts;
    }

    /// <summary>Nouns that make the number before them a quantity rather than a part.</summary>
    private static readonly HashSet<string> Quantities = new(StringComparer.OrdinalIgnoreCase)
    {
        "grain", "ore", "silver", "dead", "followers", "years",
    };

    /// <summary>Whether this number is a date rather than a quantity.</summary>
    private static bool IsYear(string[] words, int at) =>
        at > 0 && Strip(words[at - 1]) is "in" or "of" or "by" or "year" or "years" or "until" or "from";

    // ---- 1.3 internal date agreement --------------------------------------

    /// <summary>Things that happen to a named person once, with the phrase that reports them.</summary>
    private static readonly (string Phrase, string Act, bool NameFollows)[] DatedActs =
    [
        ("took the seat", "took the seat", false),
        ("was killed", "was killed", false),
        ("was murdered", "was killed", false),
        ("was cast out", "was cast out", false),
        ("died", "died", false),

        // The same killing said the other way round. "X was killed in 46" and "the murder of X
        // in 47" are one event on two dates, and the second form was invisible.
        ("murder of", "was killed", true),
        ("killing of", "was killed", true),
    ];

    /// <summary>
    /// Acts that cannot both be true of one person in one year. Deliberately a short list of
    /// genuinely exclusive pairs — taking a seat and entering somebody's service are different
    /// stations, and a passage asserting both of one man in one year contradicts itself.
    /// </summary>
    private static readonly (string One, string Other)[] Exclusive =
    [
        ("took the seat", "took service"),
        ("taking the seat", "took service"),
        ("took the seat", "taking service"),
    ];

    /// <summary>
    /// The same thing happening to the same person on two different dates inside one passage.
    ///
    /// One of the two is wrong and the passage says so itself, which is the whole appeal of
    /// this tier: no event lookup can be needed to know that a man did not take one seat in
    /// two different years.
    /// </summary>
    private static List<Fabrication> DateAgreement(string passage, Coverage cover)
    {
        Dictionary<string, (int Year, string Where)> seen = new(StringComparer.OrdinalIgnoreCase);
        List<Fabrication> findings = [];

        // The last person a sentence named, carried forward for the next one's pronoun. "He
        // was cast out after losing a challenge" states an act and a date and no name, and
        // without the antecedent it was three of the four dated acts the rule could not place.
        string? subject = null;

        foreach (string sentence in Sentences(passage))
        {
            string lower = sentence.ToLowerInvariant();

            foreach ((string phrase, string act, bool nameFollows) in DatedActs)
            {
                int at = lower.IndexOf(phrase, StringComparison.Ordinal);
                if (at < 0) continue;

                // The subject may be further back than the four words the near scan reads —
                // "Weallhous Dreld kept the seat after defeating Saern Meastouth's challenge in
                // 23, but was killed by Gatros Hearn in 25" puts it a clause away. The sentence's
                // leading name is the grammatical subject in this prose and is the right
                // fallback; the near scan still wins where it finds anything, because a name
                // beside the verb beats a name at the front of the sentence.
                string? who = nameFollows
                    ? NameAfter(sentence[(at + phrase.Length)..])
                    : NameBefore(sentence[..at]) ?? FirstName(sentence[..at]) ?? subject;

                // A number in front of the verb makes it a toll, not a person. "fourteen died"
                // is a famine's count, and the widened subject search read the nearest proper
                // noun — a place — as the one who died. Tier 1 cannot tell a place from a
                // person, so it must not be asked to.
                if (IsToll(sentence[..at])) continue;

                // Only where a year is present. This rule compares dates, so an undated act is
                // nothing it can hold — recording twenty-three of them as dropped assertions
                // described ordinary undated prose as a gap in the checker.
                int? found = YearIn(sentence[at..]) ?? YearIn(sentence[..at]);
                if (found is null) continue;

                cover.Extracted(Rules.DateAgreement);

                if (who is null)
                {
                    cover.Unresolvable(Rules.DateAgreement,
                        nameFollows ? "no name after the phrase" : "no name before the phrase",
                        Shorten(sentence));
                    continue;
                }

                // After the phrase first, then before it.
                //
                // "In 46, he ordered the murder of Veillpea Dourn" states the year before the
                // act, and reading only forwards discarded thirty of the seventy-one dated acts
                // in the chronicle — the largest single gap in the checker, and invisible until
                // the drop was recorded with its reason.
                int year = found.Value;

                cover.Checked(Rules.DateAgreement);
                string key = $"{who}|{act}";
                if (!seen.TryGetValue(key, out (int Year, string Where) first))
                {
                    seen[key] = (year, sentence);
                    continue;
                }

                if (first.Year == year) continue;

                findings.Add(Finding("date-disagreement", sentence,
                    $"{who} {act} in {first.Year}", $"and in {year}"));
            }

            if (LeadingName(sentence) is { } named) subject = named;
        }

        return findings;
    }

    // ---- shared -----------------------------------------------------------

    private static Fabrication Finding(string rule, string span, string expected, string actual) =>
        new(rule, rule, $"“{Shorten(span)}” — says {expected}, but {actual}");

    private static IEnumerable<string> Sentences(string passage) =>
        passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Whether some other countable thing is itself counted between a noun and a list.</summary>
    private static bool AnotherCountBetween(string lower, string noun, int listAt)
    {
        int from = lower.IndexOf(noun, StringComparison.Ordinal);
        if (from < 0 || from >= listAt) return false;

        string between = lower[(from + noun.Length)..listAt];

        foreach ((string plural, string singular, Members _, string[] _) in Countables)
        {
            if (plural == noun) continue;

            // The singular too. "Seven exiles returned… One marriage tied the power to another:
            // Draes Wild married Ror Rim in 44" put the marriage's list under the exiles' count,
            // because a count of one is written in the singular and nothing looked for it.
            if (Stated(between, plural) is not null) return true;
            if (Stated(between, singular) is not null) return true;
        }

        return false;
    }

    /// <summary>Where the sentence containing <paramref name="from"/> ends.</summary>
    private static int EndOfSentence(string text, int from)
    {
        int at = text.IndexOf('.', from);
        return at < 0 ? text.Length : at;
    }

    /// <summary>
    /// Where the next count of the same noun begins after <paramref name="from"/>, or the end
    /// of the text. Bounds a list to the count that introduced it.
    /// </summary>
    private static int NextCount(string lower, string noun, int from)
    {
        for (int at = lower.IndexOf(noun, from, StringComparison.Ordinal); at > 0;
             at = lower.IndexOf(noun, at + 1, StringComparison.Ordinal))
        {
            string[] before = lower[..at].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (before.Length == 0) continue;
            if (Number(before[^1]) is null) continue;

            // Back up over the number itself, so the new count is outside the old list.
            return at - before[^1].Length - 1;
        }

        return lower.Length;
    }

    /// <summary>Each sentence, and each sentence joined to the one after it.</summary>
    private static IEnumerable<string> Windows(string passage)
    {
        string[] sentences = [.. Sentences(passage)];

        // Pairs only, plus the last sentence on its own.
        //
        // Yielding each sentence alone as well judged a shortfall before its evidence had been
        // read: "Three marriages: … in 37 … in 48. A third followed in 49" is complete, and the
        // first sentence in isolation says three and names two. Every sentence is still examined
        // — as the opening half of its pair — and the excess direction is bounded inside the
        // rule, so widening the window here cannot let a list run past its own full stop.
        for (int i = 0; i + 1 < sentences.Length; i++)
            yield return sentences[i] + ". " + sentences[i + 1];

        if (sentences.Length > 0) yield return sentences[^1];
    }

    /// <summary>The number immediately before a word, in digits or in words.</summary>
    private static int? Stated(string lower, string noun)
    {
        int at = lower.IndexOf(noun, StringComparison.Ordinal);
        if (at <= 0) return null;

        string[] before = lower[..at].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (before.Length == 0) return null;

        return Number(before[^1]);
    }

    /// <summary>
    /// The one count of this thing the section makes, and the sentence making it.
    ///
    /// "One" is load-bearing: a section that says both "lost two places" and "three places
    /// taken" has made two different claims, and neither can be judged against a single tally
    /// of the places it names. Where the noun is ambiguous like that, only occurrences sitting
    /// next to one of the thing's own verbs count as a claim about it — which separates the two
    /// senses of "places" without needing to know what a place is.
    /// </summary>
    private static (int Count, string Phrase)? Claimed(
        string passage, string noun, Members key, string[] marks)
    {
        (int Count, string Phrase)? found = null;

        foreach (string sentence in Sentences(passage))
        {
            string lower = sentence.ToLowerInvariant();
            int at = lower.IndexOf(noun, StringComparison.Ordinal);
            if (at < 0) continue;

            if (key == Members.ByNameAfter && !MarkNear(lower[(at + noun.Length)..], marks)) continue;
            if (Stated(lower, noun) is not int count) continue;

            if (found is not null) return null;      // more than one claim; not judgeable
            found = (count, sentence);
        }

        return found;
    }

    /// <summary>The words immediately around a noun — where its own hedges would sit.</summary>
    private static string Around(string sentence, string noun)
    {
        int at = sentence.IndexOf(noun, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return sentence;

        int from = Math.Max(0, at - 24);
        int to = Math.Min(sentence.Length, at + noun.Length + 48);
        return sentence[from..to];
    }

    /// <summary>Whether one of a thing's own verbs appears in the first few words of a fragment.</summary>
    private static bool MarkNear(string text, string[] marks)
    {
        string[] words = text.Split([' ', ',', ':'], StringSplitOptions.RemoveEmptyEntries);
        int limit = Math.Min(3, words.Length);

        for (int i = 0; i < limit; i++)
            foreach (string mark in marks)
                if (string.Equals(Strip(words[i]), mark.Split(' ')[0], StringComparison.OrdinalIgnoreCase))
                    return true;

        return false;
    }

    private static int Counts(string lower, string needle)
    {
        int n = 0, from = 0;
        while (true)
        {
            int at = lower.IndexOf(needle, from, StringComparison.Ordinal);
            if (at < 0) return n;
            n++;
            from = at + needle.Length;
        }
    }

    /// <summary>How many times any of these markers appears — one instance each.</summary>
    private static int Instances(string text, string[] marks)
    {
        string lower = text.ToLowerInvariant();
        int total = 0;
        foreach (string mark in marks) total += Counts(lower, mark);

        // A list of bare items after one verb — "Threi Cut in 5, Hadale in 6, Griwick in 13" —
        // has one mark and several members, so the dated members are the better count where
        // they outnumber the marks.
        return total == 0 ? 0 : Math.Max(total, DatedInstances(text, marks));
    }

    /// <summary>
    /// Dated instances of a thing: distinct years found close after each mention of it.
    ///
    /// Close matters. Counting every year in the paragraph reported three marriages as nine,
    /// because the paragraph also dated a murder, a plague and a succession — a finding that
    /// was right about the defect and wrong about the arithmetic, which is not much better than
    /// being wrong.
    /// </summary>
    private static int DatedInstances(string text, string[] marks)
    {
        string[] words = text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        HashSet<int> years = [];

        for (int i = 0; i < words.Length; i++)
        {
            // The whole phrase, not its first word. Matching heads alone made "was defeated"
            // fire on every "was" in the section, so a passage narrating two battles was
            // reported as narrating three — a finding against a correct sentence, which is the
            // one thing this tier must not produce.
            int length = MarkAt(words, i, marks);
            if (length == 0) continue;

            for (int ahead = length; ahead <= length + 11 && i + ahead < words.Length; ahead++)
            {
                if (Strip(words[i + ahead - 1]).ToLowerInvariant() is not ("in" or "of")) continue;
                if (Number(Strip(words[i + ahead])) is int year and >= 2) { years.Add(year); break; }
            }
        }

        return years.Count;
    }

    /// <summary>
    /// Every distinct year in a fragment, however it is introduced.
    ///
    /// Numerals only. The prompt has years written as figures and counts written as words, and
    /// leaning on that distinction is what separates "in 48 and 49" — two years — from "and two
    /// to the Sworn Men", which is a quantity that briefly counted as the year 2.
    /// </summary>
    private static int DistinctYearsIn(string text)
    {
        HashSet<int> years = [];
        string[] words = text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < words.Length; i++)
        {
            string previous = Strip(words[i - 1]).ToLowerInvariant();
            if (previous is not ("in" or "of" or "and" or "year" or "years")) continue;

            if (int.TryParse(Strip(words[i]), NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int year) && year >= 2) years.Add(year);
        }

        return years.Count;
    }

    /// <summary>How many words of a mark start at this position, or zero if none do.</summary>
    private static int MarkAt(string[] words, int at, string[] marks)
    {
        foreach (string mark in marks)
        {
            string[] parts = mark.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (at + parts.Length > words.Length) continue;

            bool all = true;
            for (int p = 0; p < parts.Length; p++)
                if (!string.Equals(Strip(words[at + p]), parts[p], StringComparison.OrdinalIgnoreCase)) all = false;

            if (all) return parts.Length;
        }
        return 0;
    }

    /// <summary>The sentence in which a word first appears.</summary>
    private static string SentenceContaining(string paragraph, string needle)
    {
        foreach (string sentence in paragraph.Split('.', StringSplitOptions.RemoveEmptyEntries))
            if (sentence.Contains(needle, StringComparison.OrdinalIgnoreCase)) return sentence;
        return paragraph;
    }

    private static bool Hedged(string text)
    {
        string lower = text.ToLowerInvariant();
        foreach (string marker in SubsetMarkers)
            if (lower.Contains(marker, StringComparison.Ordinal)) return true;
        foreach (string marker in PartialMarkers)
            if (lower.Contains(marker, StringComparison.Ordinal)) return true;
        return false;
    }

    private static (int Value, int After)? FirstNumber(string lower, int from)
    {
        string[] words = lower[from..].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        int cursor = from;

        foreach (string raw in words)
        {
            int at = lower.IndexOf(raw, cursor, StringComparison.Ordinal);
            if (at < 0) break;
            cursor = at + raw.Length;

            if (Number(Strip(raw)) is int value) return (value, cursor);
        }
        return null;
    }

    /// <summary>Whether the words just before an act are a count of how many it happened to.</summary>
    private static bool IsToll(string before)
    {
        string[] words = before.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return false;

        string last = Strip(words[^1]);
        if (Number(last) is not null) return true;

        // "fourteen more died", "nine others died".
        return words.Length > 1
            && last.ToLowerInvariant() is "more" or "others" or "people" or "men"
            && Number(Strip(words[^2])) is not null;
    }

    /// <summary>The first proper name in a fragment — the grammatical subject, in this prose.</summary>
    private static string? FirstName(string text)
    {
        foreach (string raw in text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Strip(raw);
            if (word.Length < 3 || !char.IsUpper(word[0])) continue;
            if (Ordinary.Contains(word)) continue;
            return word.ToLowerInvariant();
        }

        return null;
    }

    private static int? YearIn(string text)
    {
        string[] words = text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);

        for (int i = 1; i < words.Length; i++)
        {
            if (Strip(words[i - 1]).ToLowerInvariant() is not ("in" or "of")) continue;
            if (Number(Strip(words[i])) is int year and >= 2) return year;
        }
        return null;
    }

    /// <summary>
    /// The capitalised name ending this fragment. Text-only, so a name is whatever looks like
    /// one: a run of capitalised words, of which the last is taken as the surname.
    /// </summary>
    private static string? NameBefore(string text)
    {
        string[] words = text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);

        for (int i = words.Length - 1; i >= 0 && i >= words.Length - 4; i--)
        {
            string word = Strip(words[i]);
            if (word.Length < 3 || !char.IsUpper(word[0])) continue;
            if (Ordinary.Contains(word)) continue;
            return word.ToLowerInvariant();
        }
        return null;
    }

    /// <summary>
    /// The last name a sentence mentions — the antecedent a following pronoun most likely
    /// means.
    ///
    /// Not the first: "The period began in 20 when Laehiford broke from the Kebarrow Compact,
    /// with Realsis Leirpu taking the seat" leads with a place, and a "He" after it means
    /// Leirpu. Not merely the last few words either, since "Realsis Leirpu's rule was short and
    /// ended in disgrace" trails off into ordinary English.
    /// </summary>
    private static string? LeadingName(string sentence)
    {
        string[] words = sentence.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        string? found = null;

        foreach (string raw in words)
        {
            string word = Strip(raw);
            if (word.Length > 2 && char.IsUpper(word[0]) && !Ordinary.Contains(word)) found = word;
        }

        return found?.ToLowerInvariant();
    }

    /// <summary>The first capitalised name in a fragment.</summary>
    private static string? NameAfter(string text)
    {
        string[] words = text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        string? last = null;

        foreach (string raw in words)
        {
            string word = Strip(raw);
            if (word.Length < 3 || !char.IsUpper(word[0]) || Ordinary.Contains(word))
                return last?.ToLowerInvariant();

            last = word;      // keep walking: the surname is the last word of the run
        }

        return last?.ToLowerInvariant();
    }

    /// <summary>Capitalised words that are not names — enough to keep sentence openers out.</summary>
    private static readonly HashSet<string> Ordinary = new(StringComparer.OrdinalIgnoreCase)
    {
        "The", "This", "That", "These", "Those", "His", "Her", "Their", "Its", "After", "Before",
        "When", "While", "Within", "During", "Following", "Both", "Each", "Every", "One", "Two",
        "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Compact", "League",
        "Covenant", "Commune", "Men", "Sworn", "Power", "Seat", "House", "Peace", "War",
    };

    private static readonly Dictionary<string, int> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6,
        ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11,
        ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
        ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19,
        ["twenty"] = 20,
    };

    private static int? Number(string token)
    {
        if (Words.TryGetValue(token, out int spelled)) return spelled;
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int digits)
            ? digits : null;
    }

    /// <summary>
    /// A word without its punctuation, possessive included.
    ///
    /// The possessive was the whole reason rule 1.4 stayed silent on the case it was written
    /// for: "Realsis Leirpu’s rule was short" left the subject as "leirpu’s", which is a
    /// different person from "leirpu" as far as a dictionary key is concerned, so the pronoun
    /// in the next sentence attached to nobody.
    /// </summary>
    private static string Strip(string word)
    {
        string s = word.Trim('.', ',', ':', ';', '!', '?', '\'', '"', '’', '—', '-', '(', ')', '[', ']');

        if (s.EndsWith("’s", StringComparison.Ordinal) || s.EndsWith("'s", StringComparison.Ordinal))
            s = s[..^2];
        else if (s.EndsWith('’') || s.EndsWith('\'')) s = s[..^1];

        return s;
    }

    private static string Shorten(string text)
    {
        string trimmed = text.Trim().ReplaceLineEndings(" ");
        return trimmed.Length <= 110 ? trimmed : trimmed[..109] + "…";
    }
}
