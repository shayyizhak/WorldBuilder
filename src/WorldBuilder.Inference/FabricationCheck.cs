using System.Globalization;

namespace WorldBuilder.Inference;

public sealed record Fabrication(string Token, string Kind, string Context)
{
    /// <summary>
    /// Whether this finding makes the passage false, as opposed to hard to read.
    ///
    /// The distinction earns its keep now that a failed check keeps a passage out of canon.
    /// "The Compact" in a document with two Compacts is a real defect and worth reporting, but
    /// it is not a falsehood, and letting it suppress six true sections would trade a
    /// readability problem for a missing history. Canon must be true; it may be imperfect.
    /// </summary>
    public bool BlocksCanon => Blocks(Kind);

    /// <summary>The same judgement made of a kind alone, for callers holding only the name.</summary>
    public static bool Blocks(string kind) => kind is not
        ("ambiguous-short-name" or "hedged-exhaustive-list"
         or "vague-quantity" or "incomplete-enumeration" or "hedged-outcome"
         or "too-aggregate" or "year-by-year" or "stray-capital" or "rule-inert");

    /// <summary>
    /// Whether a second inference pass could plausibly fix this.
    ///
    /// Distinct from <see cref="BlocksCanon"/>, and the distinction is the point. A section that
    /// names one of its eleven rulers is not false and must not be held out of the chronicle for
    /// it — but it is worth asking the model again, which costs one call and no correctness. Any
    /// falsehood is worth retrying; so is a defect of shape that a retry can act on. What is not
    /// is a finding the model cannot do anything about, like two powers sharing a short name.
    /// </summary>
    public bool WorthRetrying => BlocksCanon || Kind is
        "too-aggregate" or "year-by-year" or "hedged-exhaustive-list" or "incomplete-enumeration";

    /// <summary>
    /// Which tier found this. Tier 1 needs no access to the world and so can be run against any
    /// finished document, including one written before the rule existed; the rest need the pack.
    /// </summary>
    public bool SelfConsistencyOnly => Kind is
        "count-vs-list" or "count-vs-narration" or "partition-sum" or "date-disagreement";
}

public sealed record FabricationReport
{
    public required IReadOnlyList<Fabrication> Findings { get; init; }
    public required int CheckedTokens { get; init; }

    /// <summary>
    /// How much of the passage each rule read. See <see cref="Inference.Coverage"/>.
    ///
    /// The answer to "did this pass?" is worth much less than it looks without this beside it,
    /// because a rule that never ran and a rule that found nothing give the same answer.
    /// </summary>
    public required Coverage Coverage { get; init; }

    public bool Clean => Findings.Count == 0;

    /// <summary>The findings that make the passage false. Empty means it may be canon.</summary>
    public IReadOnlyList<Fabrication> Blocking
    {
        get
        {
            List<Fabrication> blocking = [];
            foreach (Fabrication f in Findings)
                if (f.BlocksCanon) blocking.Add(f);
            return blocking;
        }
    }

    public bool Truthful => Blocking.Count == 0;

    /// <summary>The findings a second pass should be told about.</summary>
    public IReadOnlyList<Fabrication> Retryable
    {
        get
        {
            List<Fabrication> worth = [];
            foreach (Fabrication f in Findings)
                if (f.WorthRetrying) worth.Add(f);
            return worth;
        }
    }

    public int RatePerThousand => CheckedTokens == 0 ? 0 : Findings.Count * 1000 / CheckedTokens;
}

/// <summary>
/// Checks a passage against the pack it was rendered from.
///
/// The rule being enforced is narrow and mechanical: every capitalised name and every number in
/// the prose must appear in the source records. It cannot catch a subtly wrong claim built from
/// correct nouns, and it is not meant to — it catches the failure that actually matters here,
/// which is the model quietly minting a person, a place or a date that the world does not
/// contain and the cache then promoting to canon.
/// </summary>
public static class FabricationCheck
{
    /// <summary>
    /// Ordinary English that may legitimately start a sentence or appear capitalised. Kept
    /// deliberately short: anything longer starts hiding real fabrications.
    /// </summary>
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "after", "again", "against", "all", "although", "among",
        "another", "any", "are", "arms", "army", "autumn", "battle", "been", "before", "began",
        "between", "both", "but", "by", "city", "council", "council's", "crown", "death", "died",
        "during", "each", "eight", "eighteen", "eleven", "even", "every", "famine", "few",
        "fifteen", "fifty", "five", "for", "forty", "four", "fourteen", "from", "grain", "half",
        "he", "her", "here", "his", "house", "however", "hundred", "if", "in", "into", "it",
        "its", "king", "land", "lands", "last", "later", "league", "lord", "many", "men", "more",
        "most", "much", "neither", "nine", "nineteen", "no", "none", "nor", "not", "now", "of",
        "on", "once", "one", "only", "or", "ore", "other", "others", "over", "own", "people",
        "plague", "queen", "realm", "republic", "rule", "ruler", "seat", "seven", "seventeen",
        "several", "she", "silver", "since", "six", "sixteen", "sixty", "so", "some", "still",
        "ten", "that", "the", "their", "then", "there", "these", "they", "third", "thirteen",
        "thirty", "this", "those", "though", "thousand", "three", "throne", "thus", "till",
        "to", "twelve", "twenty", "two", "under", "until", "up", "upon", "war", "was", "were",
        "what", "when", "where", "which", "while", "who", "whose", "why", "with", "within",
        "without", "year", "years", "yet",

        // Connectives, which land capitalised at the start of a sentence and are the whole
        // point of aggregation — the prose exists to join events together.
        "afterwards", "although", "beyond", "consequently", "despite", "elsewhere",
        "eventually", "finally", "following", "having", "meanwhile", "nevertheless",
        "nonetheless", "shortly", "soon", "subsequently", "therefore", "thereafter",
        "throughout", "together", "toward", "towards", "whereupon", "whether",
        "amid", "amidst", "political", "military", "economic", "internal", "external",

        // The nouns this world's prose is made of. They are not names, and one of them turned
        // up capitalised inside a compound the model coined — "Counter-raids" — and was
        // reported as an invented place.
        "counter", "raid", "raids", "attack", "attacks", "revolt", "revolts", "uprising",
        "conquest", "marriage", "marriages", "alliance", "exile", "exiles", "holder", "holders",
        "place", "places", "settlement", "settlements", "power", "powers", "succession",
    };

    /// <summary>
    /// Words that assert a *manner* of taking power, or a mind. Each may only appear if the
    /// records support it — an election rendered as a violent seizure inverts the meaning of
    /// the event it describes, which is worse than inventing a name.
    /// </summary>
    private static readonly (string Word, string Requires)[] LoadBearing =
    [
        ("coup", "coup"),
        ("seizure", "coup"),
        ("seized", "coup"),
        ("usurped", "coup"),
        ("overthrew", "coup"),
        // "executed a coup" is the verb, not a killing — matched on the passive forms only.
        ("was executed", "put to death"),
        ("were executed", "put to death"),
        ("execution of", "put to death"),
        ("assassinated", "murdered"),
    ];

    /// <summary>
    /// Relative-time expressions. The model computes these wrong — "the following year" for two
    /// events in the same year — and no amount of supplying the gaps stopped it, so they are
    /// forbidden outright and measured rather than merely discouraged.
    /// </summary>
    private static readonly string[] RelativeTime =
    [
        "the following year", "years later", "year later", "years after that",
        "shortly after", "soon after", "the next year", "a year after",
        "some years", "decades later", "months later",
        // "over the next two years" for a three-year plague: a span counted rather than read,
        // and wrong, in a sentence where every year was supplied with its own figures.
        "over the next", "in the years that followed", "over the following",
    ];

    /// <summary>
    /// Language about the archive rather than the world. A chronicle that says "six recorded
    /// events" has stopped being history and become a description of a log file.
    /// </summary>
    private static readonly string[] ArchiveWords =
    [
        "recorded event", "recorded events", "the records show", "the records indicate",
        "the record shows", "these records", "the entries", "log entries", "data",
        "events occurred", "events are recorded", "recorded in the", "documented",
    ];

    // Public so the suite can enumerate it. A lexicon-completeness test that keeps its own
    // copy of the list tests the copy, and the gap that matters is the one between the list
    // the rule reads and the words a person remembered to put in it.
    public static readonly string[] MindWords =
    [
        "paranoia", "paranoid", "resentment", "simmering", "desperate", "desperation",
        "emboldened", "ambitious", "feared", "fearful", "hoped", "believed", "intended",
        "furious", "bitter", "jealous", "vengeful", "reluctant", "confident", "humiliated",
        // A revolt following a ruler's death is a causal edge the log carries; "exploiting" the
        // weakness it left is a purpose nobody recorded.
        "exploiting", "seeking to", "in the hope", "opportunistically", "sensing",
    ];

    /// <summary>
    /// Outcomes given as a shrug where the record gives them exactly.
    ///
    /// The rule established for the plague — supplied figures are stated, not summarised —
    /// applies to results too. Raids now have three distinct outcomes and every one of them is
    /// in the digest, so "most beaten off" and "met with resistance or plunder" throw away
    /// something known and leave a reader unable to say which raid did which.
    /// </summary>
    private static readonly string[] HedgedOutcomes =
    [
        "most beaten off", "most were beaten off", "most of them beaten off",
        "resistance or plunder", "plunder or resistance", "some beaten off",
        "several beaten off", "mostly beaten off",
    ];

    /// <summary>
    /// Checks a passage against its pack.
    ///
    /// <paramref name="wholeSection"/> says whether the passage is a finished section or a
    /// fragment of one. It gates the completeness rules, which ask whether everything that had
    /// to be told was told and are meaningless of a single sentence — run on fragments they
    /// reported three true test sentences as defective, which is the same way round 10 put
    /// seven correct sections out of canon. Default false, so a caller that has not thought
    /// about it gets the safe answer.
    /// </summary>
    public static FabricationReport Check(ContextPack pack, string passage, bool wholeSection = false)
    {
        Coverage cover = new();

        // A rule that was switched off must not report as a rule that found nothing.
        //
        // The completeness rules only run on a finished section, and registering them anyway
        // listed two of them as inert against every answer the query layer produces — an inert
        // count is the one signal that says "this rule never saw the input", and spending it on
        // rules that were deliberately not offered the input makes it worth less everywhere.
        foreach (string rule in RuleNames.All)
        {
            if (!wholeSection && rule is RuleNames.Coverage or RuleNames.Shape) continue;
            cover.Ran(rule);
        }

        List<Fabrication> claims = CheckClaims(pack, passage, wholeSection, cover);
        HashSet<string> allowed = new(StringComparer.OrdinalIgnoreCase);
        foreach (string word in pack.Vocabulary) allowed.Add(Strip(word));

        List<Fabrication> findings = [];
        int checkedTokens = 0;

        string[] words = passage.Split(
            [' ', '\n', '\r', '\t', '(', ')', '"', '“', '”'],
            StringSplitOptions.RemoveEmptyEntries);

        // A genuine proper noun turns up capitalised in the middle of a sentence. Ordinary
        // words only get a capital because a sentence starts with them, and chasing those with
        // an ever-growing stopword list was a losing game — "Consequently", "Amidst",
        // "Concurrently", "Diplomatic" all had to be added one at a time. This is the signal
        // itself rather than a list of exceptions to it.
        HashSet<string> capitalisedMidSentence = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < words.Length; i++)
        {
            if (EndsSentence(words[i - 1])) continue;
            string t = Strip(words[i]);
            if (t.Length > 0 && char.IsUpper(t[0])) capitalisedMidSentence.Add(t);
        }

        for (int i = 0; i < words.Length; i++)
        {
            string raw = words[i];
            string token = Strip(raw);
            if (token.Length == 0) continue;

            bool sentenceStart = i == 0 || EndsSentence(words[i - 1]);

            if (IsNumber(token))
            {
                checkedTokens++;
                // Small numbers are ordinary prose ("two houses"); a year or a body count is a
                // claim, and has to be in the records.
                if (token.Length >= 3 && !allowed.Contains(token))
                    findings.Add(new Fabrication(token, "number", Context(words, i)));
                continue;
            }

            // Numbers written as words evade the digit check entirely. The first version of
            // this checker passed a passage that invented a date because the model had written
            // "in year twelve" rather than "in year 12".
            if (SpelledNumber(words, i) is int spelled)
            {
                checkedTokens++;
                if (spelled >= 10 && !allowed.Contains(spelled.ToString(CultureInfo.InvariantCulture)))
                    findings.Add(new Fabrication(spelled.ToString(CultureInfo.InvariantCulture),
                        "number-in-words", Context(words, i)));
                continue;
            }

            if (!char.IsUpper(token[0])) continue;
            if (Common.Contains(token)) continue;

            // Capitalised only because it opens a sentence, and never used as a name elsewhere
            // in the passage: ordinary English, not an invented person or place.
            //
            // Unless it is a mangling of a name the pack supplied, which ordinary English is
            // not. An answer opened with "Hdale broke from the Kebarrow Compact" and its retry
            // opened with "Hale", and this branch waved both through: the word appears nowhere
            // else in three sentences, so nothing marked it as being used as a name.
            //
            // Dropping the exemption entirely is the obvious repair and was measured: it reports
            // "Simultaneously" as an invented place and costs a true section. Common is the
            // defence against ordinary English and it cannot be completed by hand — that is the
            // losing game this exemption was written to end. So the test is nearness to a real
            // name, which no ordinary word is.
            if (sentenceStart && !capitalisedMidSentence.Contains(token)
                && !Mangled(allowed, token))
            {
                continue;
            }

            checkedTokens++;
            if (allowed.Contains(token)) continue;

            // A possessive or plural of a known name is the same name.
            if (token.EndsWith('s') && allowed.Contains(token[..^1])) continue;

            // A hyphenated compound of ordinary words is a stylistic capital, not a person:
            // "Counter-raids" is a word the prose made up, not a place the world does not have.
            if (token.Contains('-', StringComparison.Ordinal) && OrdinaryCompound(token)) continue;

            findings.Add(new Fabrication(token, "name", Context(words, i)));
        }

        findings.AddRange(claims);

        // The vocabulary scan is one rule over every token in the passage, so its coverage is
        // simply how much text it read. Recorded like any other, because a scan that suddenly
        // reads a tenth of what it read last round has a tokeniser problem and nothing else
        // would say so.
        cover.Extracted(RuleNames.Naming, checkedTokens);
        cover.Checked(RuleNames.Naming, checkedTokens);

        // Firing counts are derived rather than incremented at each of the forty-odd places a
        // finding is raised. One table beats forty call sites, and a rule added without a table
        // entry reports under its own kind rather than silently under nothing.
        foreach (Fabrication f in findings) cover.Fired(RuleNames.Of(f.Kind));

        // Inertness is not added to the findings. It is a fact about the checker, not about the
        // passage, and putting it in the same list would make a clean section read as dirty and
        // a section's fate depend on how many rules had nothing to say about it. It goes to the
        // sidecar, which is where the question "what was never examined" is asked.

        return new FabricationReport
        {
            Findings = findings,
            CheckedTokens = checkedTokens + claims.Count,
            Coverage = cover,
        };
    }

    /// <summary>
    /// Whether a word is a name the pack supplied with letters gone wrong, rather than a word
    /// of English.
    ///
    /// Two shapes, both observed and both from the same answer. One letter changed, inserted or
    /// dropped — "Hdale" for "Hadale". Or letters simply missing, in order: "Hale" is "Hadale"
    /// with two gone, which is two edits and would need a distance test loose enough to start
    /// matching real words. Ordered omission is the tighter statement of the same thing, and it
    /// is what a model that mangles a name actually does.
    ///
    /// Both require a shared first letter and enough length that the match is not coincidence,
    /// which is what keeps this from becoming a second stopword list.
    /// </summary>
    private static bool Mangled(HashSet<string> allowed, string token)
    {
        // Four, not five. "Hdale" was caught at five and the retry produced "Hale", which is
        // the same defect one letter shorter. Ordinary short English is already gone by here:
        // Common is consulted first, and it holds the four-letter words this prose is made of.
        const int shortest = 4;
        if (token.Length < shortest || allowed.Contains(token)) return false;

        foreach (string known in allowed)
        {
            if (known.Length < shortest) continue;
            if (char.ToLowerInvariant(known[0]) != char.ToLowerInvariant(token[0])) continue;

            if (Math.Abs(known.Length - token.Length) <= 1 && OneEditApart(known, token)) return true;

            // Strictly shorter, and every letter of it appears in the name in order. A word of
            // English is not usually a subsequence of a proper noun, and one that opens a
            // sentence, is absent from the pack and reads as that name's skeleton is that name.
            if (token.Length < known.Length && Subsequence(token, known)) return true;
        }

        return false;
    }

    /// <summary>Whether every letter of <paramref name="part"/> appears in order in the whole.</summary>
    private static bool Subsequence(string part, string whole)
    {
        int at = 0;
        foreach (char c in whole)
        {
            if (at < part.Length && char.ToLowerInvariant(c) == char.ToLowerInvariant(part[at])) at++;
        }
        return at == part.Length;
    }

    /// <summary>
    /// Whether two words differ by exactly one insertion, deletion or substitution. A full edit
    /// distance is not needed and would cost more: the question is only ever "one edit or not".
    /// </summary>
    private static bool OneEditApart(string a, string b)
    {
        if (a.Length == b.Length)
        {
            int differences = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i])) continue;
                if (++differences > 1) return false;
            }
            return differences == 1;
        }

        // One is longer by exactly one letter. Walk both, allowing a single skip in the longer.
        string longer = a.Length > b.Length ? a : b;
        string shorter = a.Length > b.Length ? b : a;

        int at = 0;
        bool skipped = false;

        for (int i = 0; i < longer.Length; i++)
        {
            if (at < shorter.Length
                && char.ToLowerInvariant(longer[i]) == char.ToLowerInvariant(shorter[at]))
            {
                at++;
                continue;
            }

            if (skipped) return false;
            skipped = true;
        }

        return at == shorter.Length;
    }

    /// <summary>
    /// Catches the two failures that survive a proper-noun check: a manner of taking power the
    /// records do not support, and any attribution of feeling or intent.
    /// </summary>
    private static List<Fabrication> CheckClaims(ContextPack pack, string passage, bool wholeSection, Coverage cover)
    {
        List<Fabrication> findings = [];
        string prose = passage.ToLowerInvariant();
        string source = pack.Body.ToLowerInvariant();

        foreach ((string word, string requires) in LoadBearing)
        {
            if (!prose.Contains(word, StringComparison.Ordinal)) continue;
            if (source.Contains(requires, StringComparison.Ordinal)) continue;

            // Two different terms used to land in one slot — "prose claims 'seizure' but the
            // records never say 'coup'" reads as a non-sequitur even though the judgement
            // underneath is right. Say what was written and what would have had to be true.
            findings.Add(new Fabrication(word, "unsupported-manner",
                word == requires
                    ? $"prose says '{word}'; no record does"
                    : $"prose says '{word}', which would need a record of '{requires}'; there is none"));
        }

        foreach (string word in MindWords)
        {
            if (!prose.Contains(word, StringComparison.Ordinal)) continue;
            if (source.Contains(word, StringComparison.Ordinal)) continue;
            findings.Add(new Fabrication(word, "invented-mind",
                $"prose attributes '{word}' to someone; no record states it"));
        }

        foreach (string phrase in RelativeTime)
        {
            int at = prose.IndexOf(phrase, StringComparison.Ordinal);
            if (at < 0) continue;

            // The surrounding words, so the retry knows which sentence to repair rather than
            // being told a rule it has already read. A generic message survived two attempts.
            int from = Math.Max(0, at - 60);
            int to = Math.Min(passage.Length, at + phrase.Length + 30);

            findings.Add(new Fabrication(phrase, "relative-time",
                $"\"…{passage[from..to].Trim()}…\" — replace \"{phrase}\" with the absolute year " +
                "from the record, or drop the time reference"));
        }

        foreach (string phrase in ArchiveWords)
        {
            if (!prose.Contains(phrase, StringComparison.Ordinal)) continue;
            findings.Add(new Fabrication(phrase, "describes-the-archive",
                "a chronicle describes the world, not the log it was compiled from"));
        }

        foreach (string phrase in HedgedOutcomes)
        {
            if (!prose.Contains(phrase, StringComparison.Ordinal)) continue;
            findings.Add(new Fabrication(phrase, "hedged-outcome",
                $"\"{phrase}\" — every raid's result is given; say which did which, or give the split"));
        }

        findings.AddRange(CheckPairs(pack, passage));
        findings.AddRange(CheckSuccessions(pack, passage, cover));
        findings.AddRange(CheckRuleEnds(pack, passage));
        findings.AddRange(CheckRaidDirection(pack, passage));
        findings.AddRange(CheckHedgedLists(pack, passage));
        findings.AddRange(CheckAmbiguousNames(pack, passage));
        findings.AddRange(CheckFates(pack, passage, cover));
        findings.AddRange(CheckRaidClaims(pack, passage, cover));
        findings.AddRange(CheckSeatClaims(pack, passage));
        findings.AddRange(CheckKillingClaims(pack, passage, cover));
        findings.AddRange(CheckWhoKilledWhom(pack, passage));
        findings.AddRange(CheckDatedActs(pack, passage, cover));
        findings.AddRange(CheckCollapses(pack, passage));
        findings.AddRange(CheckPlaceEvents(pack, passage, ClaimIndex.Revolt,
            ["rose against", "rising against", "uprising", "revolt", "revolted"]));
        findings.AddRange(CheckSuccessionRoles(pack, passage));
        findings.AddRange(CheckDuplicatedKillings(pack, passage));
        findings.AddRange(SelfConsistency.Check(passage, cover));
        findings.AddRange(CheckVagueQuantities(pack, passage));
        findings.AddRange(CheckIncompleteEnumerations(pack, passage));
        if (wholeSection)
        {
            // Seat-holder coverage belongs here with the other completeness rules. It asks
            // whether every ruler was named, which three sentences lifted out of a section can
            // never satisfy — it reported three ungiven rulers against a true sentence about
            // raids, and did so from outside the gate that exists for exactly this.
            findings.AddRange(CheckSeatHolderCoverage(pack, passage));
            findings.AddRange(CheckCoverage(pack, passage));
            findings.AddRange(CheckShape(pack, passage));
        }
        findings.AddRange(CheckDatedRosters(pack, passage, cover));
        findings.AddRange(CheckRaidHauls(pack, passage));
        findings.AddRange(CheckTenureWindow(pack, passage, cover));
        findings.AddRange(CheckAscribedTenures(pack, passage));
        findings.AddRange(CheckScopeTotals(pack, passage));
        findings.AddRange(CheckReignAttribution(pack, passage, cover));
        findings.AddRange(CheckInventedParticulars(pack, passage));
        findings.AddRange(CheckRelativeAnchors(pack, passage));
        findings.AddRange(CheckNarrativeOrder(pack, passage));
        return findings;
    }

    /// <summary>
    /// Lead-ins that attach a year to a named person or power, and the act each records.
    ///
    /// Two similar events a year apart get pulled onto one date — it has happened to ordered
    /// killings and to men courted away from a ruler — so any act the index knows about gets
    /// its year checked, not only the ones a previous round happened to catch.
    /// </summary>
    private static readonly (string Lead, string Act, bool NameFollows)[] DatedActs =
    [
        ("won ", ClaimIndex.WonAway, true),
        ("winning ", ClaimIndex.WonAway, true),
        ("courted ", ClaimIndex.WonAway, true),
    ];

    private static List<Fabrication> CheckDatedActs(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            foreach ((string lead, string act, bool nameFollows) in DatedActs)
            {
                int from = 0;
                while (true)
                {
                    int at = lower.IndexOf(lead, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + lead.Length;

                    string tail = sentence[from..];
                    string? who = nameFollows ? FirstKnownName(tail, pack) : SubjectBefore(sentence[..at], pack);

                    // Subject, act and year together are the assertion. Any one of them missing
                    // and there is nothing here of the kind this rule checks.
                    if (who is null) continue;

                    cover.Extracted(RuleNames.Date);

                    if (!pack.Claims.Knows(act, who))
                    {
                        // Gated on the pack holding this act at all — see
                        // <see cref="ClaimIndex.Witnesses"/>. The lead-ins here are ordinary
                        // English before they are anything else: "won" is a defection in a pack
                        // full of defections and a battle in a pack full of battles, and a rule
                        // that accuses on the second reading has cost more than it has caught.
                        if (pack.Claims.Witnesses(act))
                        {
                            cover.Checked(RuleNames.Date);
                            if (!reported.Add($"{who}|{act}|none")) continue;

                            findings.Add(new Fabrication(who, "no-such-act",
                                $"…{Shorten(sentence)}… — nothing records {who} being {act}"));
                            continue;
                        }

                        cover.Unresolvable(RuleNames.Date,
                            "these records hold no act of that kind, so the phrase is read as ordinary prose",
                            Shorten(sentence));
                        continue;
                    }

                    if (FirstYear(UntilNewClause(tail)) is not int stated)
                    {
                        cover.Unresolvable(RuleNames.Date, "no year in the act's own clause",
                            Shorten(sentence));
                        continue;
                    }

                    cover.Checked(RuleNames.Date);

                    // Where the sentence names who did it as well, the pair is what gets
                    // checked. One man courted away twice in successive years by two different
                    // people is supported at the person level for either year, and false at the
                    // pair level for one of them.
                    string? doer = nameFollows ? SubjectBefore(sentence[..at], pack) : null;
                    string subject = who;

                    if (doer is not null && doer != who
                        && pack.Claims.Knows(act, ClaimIndex.Between(doer, who)))
                    {
                        subject = ClaimIndex.Between(doer, who);
                    }

                    if (pack.Claims.Supports(act, subject, stated)) continue;
                    if (!reported.Add($"{subject}|{act}|{stated}")) continue;

                    findings.Add(new Fabrication($"{who} {stated}", "wrong-year",
                        $"…{Shorten(sentence)}… — that was in " +
                        $"{string.Join(" and ", pack.Claims.Years(act, subject))}, not {stated}"));
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Checks the year on something that happened to a place.
    ///
    /// Written generically because the date errors keep arriving in whichever act the previous
    /// check did not cover — an assassination, then a courting-away, then a revolt. The list of
    /// acts is the thing that has to grow, not the machinery.
    /// </summary>
    private static List<Fabrication> CheckPlaceEvents(
        ContextPack pack, string passage, string act, string[] marks)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            bool relevant = false;
            foreach (string mark in marks)
                if (lower.Contains(mark, StringComparison.Ordinal)) relevant = true;
            if (!relevant) continue;

            // Every "<place> in <year>" the sentence carries, each judged on its own.
            string[] words = sentence.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> judged = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < words.Length; i++)
            {
                // One word or two: half the places in this world are "Threi Cut" or "Vea Lode",
                // and a single-word scan matched none of them.
                string place = Strip(words[i]).ToLowerInvariant();
                int span = 1;

                if (!pack.Claims.Knows(act, place) && i + 1 < words.Length)
                {
                    place = $"{place} {Strip(words[i + 1]).ToLowerInvariant()}";
                    span = 2;
                }
                if (!pack.Claims.Knows(act, place)) continue;

                // Only the first mention of a place in a sentence is judged. A place named
                // twice — "Threi Cut rose against the Compact in 31, and the Vea Lode Covenant
                // took Threi Cut in 34" — otherwise picks up the second mention's year, which
                // belongs to the conquest and not to the revolt.
                if (!judged.Add(place)) continue;

                for (int ahead = span; ahead <= span + 3 && i + ahead + 1 < words.Length; ahead++)
                {
                    if (Strip(words[i + ahead]).ToLowerInvariant() is not ("in" or "of")) continue;
                    if (!int.TryParse(Strip(words[i + ahead + 1]), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int stated)) continue;

                    if (pack.Claims.Supports(act, place, stated)) break;
                    if (!reported.Add($"{place}|{stated}")) break;

                    findings.Add(new Fabrication($"{place} {stated}", "wrong-year",
                        $"…{Shorten(sentence)}… — {place} was {act} in " +
                        $"{string.Join(" and ", pack.Claims.Years(act, place))}, not {stated}"));
                    break;
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Checks who contested a succession against who actually did.
    ///
    /// Fixed at round 8 and returned at round 10, in a document where the same construction
    /// renders correctly two sections earlier. That is what a prompt-level fix looks like when
    /// it fails: right most of the time and silently wrong the rest. The heir is set aside in
    /// thirteen disputes out of fifteen, so "the man who took the seat must be the one who
    /// contested" scores well and is exactly backwards here.
    /// </summary>
    private static List<Fabrication> CheckSuccessionRoles(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            foreach (string verb in new[] { "contested", "contesting", "disputed the claim" })
            {
                int at = lower.IndexOf(verb, StringComparison.Ordinal);
                if (at < 0) continue;

                string? who = SubjectBefore(sentence[..at], pack);
                if (who is null) continue;

                // Only judgeable where this person really is one of the two parties.
                if (!pack.Claims.Knows(ClaimIndex.NamedHeir, who)) continue;
                if (pack.Claims.Knows(ClaimIndex.Contested, who)) continue;
                if (!reported.Add(who)) continue;

                findings.Add(new Fabrication(who, "wrong-role",
                    $"…{Shorten(sentence)}… — {who} was the named heir, not the one contesting"));
            }
        }

        return findings;
    }

    /// <summary>
    /// One killing told twice in a sentence.
    ///
    /// A murder is two events — the order and the death — and every other passage merges them.
    /// Where one did not, it read as a man being murdered and then killed again by the same
    /// hand, in the same year, at the same place.
    /// </summary>
    private static List<Fabrication> CheckDuplicatedKillings(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            HashSet<string> once = new(StringComparer.OrdinalIgnoreCase);

            foreach ((string killer, string victim) in KillingsAsserted(sentence, pack))
            {
                string pair = $"{killer}|{victim}";
                if (once.Add(pair)) continue;
                if (!reported.Add(pair)) continue;

                findings.Add(new Fabrication($"{killer} -> {victim}", "event-told-twice",
                    $"…{Shorten(sentence)}… — one killing, told twice"));
            }
        }

        return findings;
    }

    /// <summary>
    /// Every ruler a short seat history contains must be named in the passage.
    ///
    /// The text-only version of this does not work: a section that says "three holders" and
    /// names two is a plain contradiction, but counting narrated rulers from prose cannot tell
    /// a man named as a ruler from a man named as a murder victim, and the one dropped here was
    /// named — as somebody Math Ham had killed. So the count comes from the seat history rather
    /// than from the sentence.
    ///
    /// Only short histories. Where a house had eleven rulers the prompt asks for the number and
    /// a few examples, so a missing name is compression working as intended.
    /// </summary>
    private static List<Fabrication> CheckSeatHolderCoverage(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];

        IReadOnlyList<Tenure> tenures = pack.Digest.Tenures;
        if (tenures.Count is 0 or > 4) return findings;

        // Named is not enough — the man dropped here appears in his own section, in a list of
        // people somebody else had murdered. What is missing is any sentence connecting him to
        // the seat he held, so that is what gets looked for.
        List<string> sentences = [.. passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries)];

        foreach (Tenure t in tenures)
        {
            string surname = ContextPackBuilder.Surname(t.Holder);
            bool asRuler = false;

            foreach (string sentence in sentences)
            {
                string lower = sentence.ToLowerInvariant();
                if (!lower.Contains(surname, StringComparison.Ordinal)) continue;

                foreach (string word in SeatWords)
                    if (lower.Contains(word, StringComparison.Ordinal)) asRuler = true;
            }

            if (asRuler) continue;

            findings.Add(new Fabrication(surname, "missing-ruler",
                $"{t.Holder} held the seat from {t.From} to {t.To}, and no sentence here says so"));
        }

        return findings;
    }

    /// <summary>Ways of saying a power was destroyed.</summary>
    private static readonly string[] CollapseWords =
        ["collapsed", "was finished", "was destroyed", "came to an end", "ceased to exist", "collapse"];

    /// <summary>
    /// Checks which power a passage says collapsed, and when.
    ///
    /// A bare "(collapse)" on the peace that followed named no party, and a section inverted it
    /// — reporting the power it was about as having collapsed when that power had just
    /// destroyed the other, and then omitting the destruction of a founding realm entirely.
    /// The engine now names the party; this makes the claim checkable either way.
    /// </summary>
    private static List<Fabrication> CheckCollapses(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            int at = -1;
            foreach (string word in CollapseWords)
            {
                int found = lower.IndexOf(word, StringComparison.Ordinal);
                if (found >= 0 && (at < 0 || found < at)) at = found;
            }
            if (at < 0) continue;

            // Whose collapse it is: the power named nearest before the word, not every power
            // the sentence happens to mention. Judging all of them reported two findings
            // against a true sentence for naming the power that did the destroying.
            string? who = NearestPowerBefore(pack, lower[..at]);
            if (who is null) continue;

            if (!pack.Claims.Knows(ClaimIndex.Collapsed, who))
            {
                // Claimed to have collapsed, with no such record anywhere in the pack.
                if (!AnyCollapseRecorded(pack)) continue;
                if (!reported.Add(who)) continue;

                findings.Add(new Fabrication(who, "wrong-collapse",
                    $"…{Shorten(sentence)}… — nothing here records {who} being destroyed"));
                continue;
            }

            if (FirstYear(UntilNewClause(sentence[at..])) is not int stated) continue;
            if (pack.Claims.Supports(ClaimIndex.Collapsed, who, stated)) continue;
            if (!reported.Add($"{who}|{stated}")) continue;

            findings.Add(new Fabrication($"{who} {stated}", "wrong-year",
                $"…{Shorten(sentence)}… — {who} was finished in " +
                $"{string.Join(" and ", pack.Claims.Years(ClaimIndex.Collapsed, who))}, not {stated}"));
        }

        return findings;
    }

    /// <summary>
    /// The power named immediately before this point — the subject of the verb that follows.
    ///
    /// Immediately matters. "…and the Vea Lode Covenant fought two battles between them, and the
    /// power ceased to exist" has an anaphoric subject, and taking the nearest name anywhere in
    /// the clause pinned the ending on the power that had done the destroying. Three words of
    /// slack covers "the Wurn League collapsed" and stops short of a different clause; where
    /// the subject is a pronoun this finds nothing, which is the right answer.
    /// </summary>
    private static string? NearestPowerBefore(ContextPack pack, string before)
    {
        string[] words = before.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

        for (int back = 1; back <= 3 && words.Length - back >= 0; back++)
        {
            string word = Strip(words[^back]).ToLowerInvariant();
            if (pack.PowerWords.Contains(word)) return word;
        }

        return null;
    }

    /// <summary>Whether this pack witnesses any power ending, so a claim about one is judgeable.</summary>
    private static bool AnyCollapseRecorded(ContextPack pack)
    {
        foreach (string word in pack.PowerWords)
            if (pack.Claims.Knows(ClaimIndex.Collapsed, word)) return true;
        return false;
    }

    /// <summary>Words that stand in for a number the records already give.</summary>
    private static readonly string[] Vague =
        ["hundreds", "dozens", "scores of", "thousands", "a great many", "countless", "numerous"];

    /// <summary>
    /// Catches a supplied figure thrown away for an adjective.
    ///
    /// This is the inverse of the arithmetic problem and it took a round to see: the engine
    /// counts 474 dead and 504 driven out, and the passage says "killing hundreds and driving
    /// many away". Vagueness reads as safe and is not — it discards the only content the
    /// renderer can state with certainty, and here it came with a wrong number of years
    /// attached. Reported as style, because it is a waste rather than a falsehood.
    /// </summary>
    private static List<Fabrication> CheckVagueQuantities(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.Digest.Disasters.Count == 0 && pack.Digest.DisasterDeaths == 0) return findings;

        string prose = passage.ToLowerInvariant();

        foreach (string word in Vague)
        {
            if (!prose.Contains(word, StringComparison.Ordinal)) continue;

            findings.Add(new Fabrication(word, "vague-quantity",
                $"the records give exact figures here — {pack.Digest.DisasterDeaths} dead across " +
                $"{pack.Digest.StrickenYears} stricken years — and \"{word}\" throws them away"));
        }

        return findings;
    }

    /// <summary>
    /// Catches a list that states its own size and then names fewer than that.
    ///
    /// The mirror of the "including" check. "Seven exiles returned: A, B, C, D, E, F" leaves a
    /// reader to wonder which one is missing, and the one missing here had returned and been
    /// cast out in the same year, having lost the seat to the man the section is about.
    /// </summary>
    private static List<Fabrication> CheckIncompleteEnumerations(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];

        HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in pack.ActorPairs)
            foreach (string name in pair.Split('|'))
                known.Add(name);
        if (known.Count == 0) return findings;

        foreach (string sentence in passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            // Only where the sentence presents a list: a colon, or a dash introducing names.
            int colon = sentence.IndexOfAny([':', '—']);
            if (colon < 0) continue;

            // Hedged lists are the other check's business.
            if (lower.Contains("including", StringComparison.Ordinal)) continue;
            if (lower.Contains("among them", StringComparison.Ordinal)) continue;

            int? stated = null;
            foreach (string raw in sentence[..colon].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                string word = Strip(raw);
                if (Units.TryGetValue(word, out int n)) stated = n;
                else if (int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int d)) stated = d;
            }
            if (stated is not int count || count < 2) continue;

            // A partition is not an enumeration. "Three people were cast out: two for the
            // losing claim and one, Beas Krouthea, for conspiracy" divides the three up; it
            // does not undertake to name them, and reading it as a list of one was wrong.
            if (PartitionsRatherThanNames(sentence[colon..])) continue;

            HashSet<string> listed = new(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in sentence[colon..].Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
            {
                string word = Strip(raw);
                if (known.Contains(word)) listed.Add(word);
            }

            // One name is an example, not a short list. Two is the least that can be read as an
            // attempt to enumerate.
            if (listed.Count < 2 || listed.Count >= count) continue;

            findings.Add(new Fabrication($"{listed.Count} of {count}", "incomplete-enumeration",
                $"…{Shorten(sentence)}… — the sentence says {count} and names {listed.Count}"));
        }

        return findings;
    }

    private static readonly string[] KillingVerbs = ["killed", "murdered", "killing", "murdering"];

    /// <summary>
    /// Checks that the person a passage says did a killing is the person who did it.
    ///
    /// The longest-running fabrication in this project, and the one every other check misses.
    /// A killing and a seat-taking that sit near each other get fused into a single claim —
    /// "Turaer Danpa holding the seat after killing Befu Seirn" — where the man, the victim and
    /// the succession are all real and only the join is invented. Proper nouns pass. Shared
    /// events pass, because the death really did cause the succession. The pair does not.
    ///
    /// It also does the work of a summary-against-body check without needing to know which is
    /// which: where an opening paragraph and the body name different killers for one victim,
    /// at most one of them can match the record, so the false one is reported.
    /// </summary>
    private static List<Fabrication> CheckWhoKilledWhom(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            foreach ((string killer, string victim) in KillingsAsserted(sentence, pack))
            {
                // Only judge where the records have something to say about both. A killing
                // outside this pack is not this pack's to contradict.
                if (!pack.Claims.Knows(ClaimIndex.Killed, victim)) continue;
                if (!pack.Claims.EverKilled(killer)) continue;
                if (pack.Claims.Killer(killer, victim)) continue;
                if (!reported.Add($"{killer}|{victim}")) continue;

                string? truth = pack.Claims.KillerOf(victim);
                findings.Add(new Fabrication($"{killer} -> {victim}", "wrong-killer",
                    $"…{Shorten(sentence)}… — {killer} did not kill {victim}" +
                    (truth is null ? "" : $"; {truth} did")));
            }
        }

        return findings;
    }

    /// <summary>
    /// The (killer, victim) pairs a sentence asserts.
    ///
    /// Voice decides everything here and a fixed set of phrases cannot see it: "Theald Va was
    /// murdered in 29 by Wilwound Ska" and "Wilwound Ska murdered Theald Va" put the same two
    /// names on the same two sides of the same verb and mean opposite things. So the verb is
    /// found first and then read — passive with an agent, the engine's own "had X murdered",
    /// a participle, or plain active. Anything that does not fit one of those is left alone;
    /// a missed claim costs nothing, and a misparsed one accuses a real passage of lying.
    /// </summary>
    private static List<(string Killer, string Victim)> KillingsAsserted(string sentence, ContextPack pack)
    {
        List<(string, string)> pairs = [];
        string[] words = sentence.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i++)
        {
            string verb = Strip(words[i]).ToLowerInvariant();
            if (Array.IndexOf(KillingVerbs, verb) < 0) continue;

            string before = string.Join(' ', words[..i]);
            string after = string.Join(' ', words[(i + 1)..]);
            string previous = i > 0 ? Strip(words[i - 1]).ToLowerInvariant() : "";

            // Passive: the subject suffered it, and the killer follows "by". Without an agent
            // the sentence names no killer and there is nothing to check.
            //
            // "by" immediately after settles it whatever came before — a second passive in a
            // compound sentence drops its auxiliary ("murdered by X in 18 and killed by X"),
            // and reading that half as active made killer and victim the same man, so the
            // claim was discarded instead of compared.
            bool passive = previous is "was" or "were" or "been" or "being"
                || (i + 1 < words.Length && Strip(words[i + 1]).Equals("by", StringComparison.OrdinalIgnoreCase));

            if (passive)
            {
                // The agent immediately follows, and "by" is the very next word — searching for
                // " by " with a leading space skipped it and found the *next* agent in an
                // elided list, so "Ho was killed by Ham, Maer by Danpa" paired Ho with Danpa.
                string agentPart =
                    after.StartsWith("by ", StringComparison.OrdinalIgnoreCase) ? after[3..]
                    : after.IndexOf(" by ", StringComparison.OrdinalIgnoreCase) is var by and >= 0 ? after[(by + 4)..]
                    : "";

                if (agentPart.Length == 0) continue;

                string? agent = FirstKnownName(agentPart, pack);
                string? sufferer = SubjectBefore(before, pack);
                if (agent is not null && sufferer is not null && agent != sufferer) pairs.Add((agent, sufferer));
                continue;
            }

            // "A had B murdered" — the engine's own construction for an ordered killing. The
            // victim sits between the two, which no reading of word order alone would give.
            if (Mentions(pack, previous))
            {
                int had = before.LastIndexOf(" had ", StringComparison.OrdinalIgnoreCase);
                if (had < 0) continue;

                string? orderer = SubjectBefore(before[..had], pack);
                if (orderer is not null && orderer != previous) pairs.Add((orderer, previous));
                continue;
            }

            // Participle or plain active: the nearest preceding name did it, the next one
            // suffered it. "after killing X" attaches to whoever the clause is about.
            string? killer = LastKnownName(before, pack);
            string? victim = FirstKnownName(after, pack);
            if (killer is not null && victim is not null && killer != victim) pairs.Add((killer, victim));
        }

        return pairs;
    }

    /// <summary>
    /// Checks the year on a killing named as somebody's doing.
    ///
    /// "Paernmel Has ordered the murder of Veillpea Dourn at Vea Lode and Thres Thrild at
    /// Griwick" collapses two killings a year apart onto one date. The fate check does not see
    /// it, because neither victim is the subject of a passive verb — the sentence is about the
    /// man who ordered them.
    /// </summary>
    private static List<Fabrication> CheckKillingClaims(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in DatedClauses(passage))
        {
            string lower = sentence.ToLowerInvariant();

            // "murder of X" only. The other forms put the victim on either side depending on
            // voice — "had X murdered", "was murdered by Y" — and guessing which cost three
            // findings against true sentences, two of them naming the killer as the victim.
            // This one form is unambiguous, and it is the one the failure appeared in.
            const string lead = "murder of ";

            int start = lower.IndexOf(lead, StringComparison.Ordinal);
            if (start < 0) continue;

            // A year stated before the killings governs any victim that carries none of their
            // own: "In 46, he ordered the murder of Dourn at Vea Lode and Thrild at Griwick"
            // dates both, and one of them is a year out.
            int? sentenceYear = FirstYear(sentence[..start]);

            foreach ((string victim, int? own) in Victims(sentence[(start + lead.Length)..], pack))
            {
                cover.Extracted(RuleNames.Succession);

                // The lookup was made and the record does not hold it, which is not the same
                // thing as being unable to make the lookup — and reporting it as the second was
                // a quiet miss in a chronicle and is a wrong answer to a direct question. "The
                // murder of X" cannot mean anything but a murder, so there is no ambiguity here
                // to hide behind: the passage asserts a killing the world does not record.
                //
                // Judged before the date, and that ordering is the point. Whether a killing
                // happened at all does not depend on the prose troubling to date it, and while
                // an undated claim fell out of this loop before the question was asked, "he
                // rose after the murder of X" asserted a death that never happened and was
                // read as a sentence with nothing in it to check.
                if (!pack.Claims.Knows(ClaimIndex.Killed, victim))
                {
                    cover.Checked(RuleNames.Succession);
                    if (!reported.Add($"{victim}|killed")) continue;

                    findings.Add(new Fabrication(victim, "no-such-killing",
                        $"…{Shorten(sentence)}… — nothing records the killing of {victim}"));
                    continue;
                }

                cover.Checked(RuleNames.Succession);

                // The killing is real and the prose gives it no year. There is nothing further
                // here to be right or wrong about.
                if ((own ?? sentenceYear) is not int stated) continue;

                if (pack.Claims.Supports(ClaimIndex.Killed, victim, stated)) continue;
                if (!reported.Add($"{victim}|{stated}")) continue;

                findings.Add(new Fabrication($"{victim} {stated}", "wrong-year",
                    $"…{Shorten(sentence)}… — {victim} was killed in " +
                    $"{string.Join(" and ", pack.Claims.Years(ClaimIndex.Killed, victim))}, not {stated}"));
            }
        }

        return findings;
    }

    /// <summary>
    /// Phrases that assign a fate to a person, and the act each one asserts.
    ///
    /// Ordered longest-first within a fate so "was put to death" is not read as "died".
    /// </summary>
    private static readonly (string Phrase, string Act)[] FateVerbs =
    [
        ("was declared outlaw", ClaimIndex.Outlaw),
        ("were declared outlaws", ClaimIndex.Outlaw),
        ("was cast out", ClaimIndex.Exile),
        ("were cast out", ClaimIndex.Exile),
        ("was exiled", ClaimIndex.Exile),
        ("were exiled", ClaimIndex.Exile),
        ("was put to death", ClaimIndex.Killed),
        ("was murdered", ClaimIndex.Killed),
        ("was killed", ClaimIndex.Killed),
        ("were killed", ClaimIndex.Killed),
        ("died", ClaimIndex.DiedNaturally),
    ];

    /// <summary>
    /// Checks what a passage says happened to a person against what happened to them.
    ///
    /// The construction this exists for is elision: "Le Vild was cast out in 33, Heth Fal in 35,
    /// Nael War in 37, and Paernrom Sir in 38" carries one verb across four men, two of whom
    /// were killed — in a section whose own totals said five killed and five cast out. So the
    /// verb is distributed the way a reader distributes it, and every name it reaches is
    /// checked, along with the year attached to each.
    /// </summary>
    private static List<Fabrication> CheckFates(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            foreach ((string phrase, string act) in FateVerbs)
            {
                int at = lower.IndexOf(phrase, StringComparison.Ordinal);
                if (at < 0) continue;

                cover.Extracted(RuleNames.Departure);
                cover.Checked(RuleNames.Departure);

                // A body count is not a person. "at Laehiford, where 142 died" would otherwise
                // attach the death to whichever name the sentence began with.
                if (CountedRatherThanNamed(sentence[..at])) continue;

                // Who it is about. Not simply the nearest name: "Stour attempted on Paernmel
                // Has, was cast out" puts the target between the subject and its verb, and
                // taking the nearest name reported the victim of an attempt as its exile.
                //
                // A relative pronoun overrides that: "the claim of Deargund Keirem, who was
                // cast out" is about Keirem however the phrase before him is built.
                string head = sentence[..at];
                string? first = EndsWithRelativePronoun(head)
                    ? LastKnownName(head, pack)
                    : SubjectBefore(head, pack);

                if (first is null) continue;

                // The verb reaches this person, and then every later "<Name> in <year>" item.
                foreach ((string name, int? year) in Items(sentence, at, phrase.Length, first, pack))
                {
                    if (!pack.Claims.Knows(act, name))
                    {
                        string? truth = Contradiction(pack, name, act);
                        if (truth is null) continue;    // nothing here rules the claim out

                        if (reported.Add($"{name}|{act}"))
                        {
                            findings.Add(new Fabrication(name, "wrong-fate",
                                $"…{Shorten(sentence)}… — the record has {name} {truth}, not {act}"));
                        }
                        continue;
                    }

                    if (year is not int stated || pack.Claims.Supports(act, name, stated)) continue;

                    if (reported.Add($"{name}|{act}|{stated}"))
                    {
                        findings.Add(new Fabrication($"{name} {stated}", "wrong-year",
                            $"…{Shorten(sentence)}… — {name} was {act} in " +
                            $"{string.Join(" and ", pack.Claims.Years(act, name))}, not {stated}"));
                    }
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// The people a fate verb reaches, with the year attached to each: the person before the
    /// verb, then every later name in the same sentence that carries a bare year and no verb
    /// of its own.
    /// </summary>
    private static List<(string Name, int? Year)> Items(
        string sentence, int verbAt, int verbLength, string first, ContextPack pack)
    {
        List<(string, int?)> items = [];

        string tail = sentence[(verbAt + verbLength)..];
        items.Add((first, FirstYear(FirstClause(tail))));

        // Everything after the first item's year is an elided continuation until a new verb
        // appears; a fresh verb starts a new claim and is handled by its own pass.
        //
        // A continuation must carry its own year — "Heth Fal in 35" — because that is the
        // construction the elision actually takes. A bare name with no year after it is doing
        // something else in the sentence: "Thra Bround was murdered in 18, and Krir Nur
        // similarly took the seat" names Krir Nur next but does not say he was murdered.
        string[] words = tail.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        string? pending = null;

        for (int i = 0; i < words.Length; i++)
        {
            string word = Strip(words[i]);
            if (Verbish(word)) break;

            if (Mentions(pack, word.ToLowerInvariant()))
            {
                pending = word.ToLowerInvariant();
                continue;
            }

            if (pending is null) continue;
            if (word == "in" && i + 1 < words.Length
                && int.TryParse(Strip(words[i + 1]), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                items.Add((pending, y));
                pending = null;
            }
        }

        // The first item is already in; drop a duplicate of it picked up by the scan.
        List<(string Name, int? Year)> distinct = [];
        foreach ((string name, int? year) in items)
        {
            int existing = distinct.FindIndex(d => d.Name == name);
            if (existing >= 0)
            {
                if (distinct[existing].Year is null && year is not null) distinct[existing] = (name, year);
                continue;
            }
            distinct.Add((name, year));
        }
        return distinct;
    }

    /// <summary>
    /// The victims a "murder of" governs, each with its own year where the prose gives one.
    ///
    /// A list of victims elides the verb the same way a list of fates does — "the murder of X at
    /// Vea Lode and Y at Griwick" is two killings — so the scan runs on past the first name
    /// until something starts a new assertion.
    /// </summary>
    private static List<(string Victim, int? Year)> Victims(string tail, ContextPack pack)
    {
        List<(string, int?)> victims = [];
        string[] words = tail.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        string? pending = null;

        for (int i = 0; i < words.Length; i++)
        {
            string word = Strip(words[i]);
            if (Verbish(word) || word.Equals("declaring", StringComparison.OrdinalIgnoreCase)) break;

            if (Mentions(pack, word.ToLowerInvariant()))
            {
                if (pending is not null) victims.Add((pending, null));
                pending = word.ToLowerInvariant();
                continue;
            }

            if (pending is null) continue;
            if (word == "in" && i + 1 < words.Length
                && int.TryParse(Strip(words[i + 1]), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                victims.Add((pending, y));
                pending = null;
            }
        }

        if (pending is not null) victims.Add((pending, null));
        return victims;
    }

    /// <summary>
    /// Whether the verb is introduced by a relative pronoun, which binds it to the name
    /// immediately before it regardless of what that name is otherwise doing in the sentence.
    /// </summary>
    private static bool EndsWithRelativePronoun(string before)
    {
        string[] words = before.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && Strip(words[^1]).ToLowerInvariant() is "who" or "whom" or "which";
    }

    /// <summary>
    /// Whether the last thing before the verb is a quantity rather than a person, as in
    /// "at Laehiford, where 142 died" — otherwise the death attaches to whichever name the
    /// sentence began with.
    /// </summary>
    private static bool CountedRatherThanNamed(string before)
    {
        string[] words = before.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return false;

        string last = Strip(words[^1]);
        return IsNumber(last) || Units.ContainsKey(last) || Tens.ContainsKey(last);
    }

    /// <summary>
    /// Words that make the name after them somebody else's business — a preposition, or a
    /// transitive verb whose object it is. A name in either position is not what the following
    /// passive verb is about.
    /// </summary>
    private static readonly HashSet<string> Governing = new(StringComparer.OrdinalIgnoreCase)
    {
        "by", "on", "upon", "against", "of", "with", "to", "for", "from", "over",
        "killed", "killing", "murdered", "murdering", "defeated", "defeating",
        "challenged", "challenging", "beat", "beating", "deposed", "deposing",
        "exiled", "exiling", "succeeded", "replaced", "unseated", "set", "aside",
        // Infinitives, which govern an object just as their finite forms do: "four members
        // attempted to assassinate Paernmel Has and were cast out" is about the four members.
        "assassinate", "kill", "murder", "depose", "unseat", "challenge", "replace", "succeed",
        // And the verb of the attempt itself: "Naell attempted Paernmel Has's life and was cast
        // out" is about Naell, and reading Has as the subject reported his exile instead.
        "attempted", "attempting", "attempts", "ordered", "ordering",
    };

    /// <summary>
    /// The subject a verb belongs to: the nearest preceding name that is not the object of
    /// something else. Given names are stepped over, so "on Paernmel Has" and "after killing
    /// Weallhous Dreld" are both recognised from two words back.
    ///
    /// Where every candidate is governed this returns nothing and the claim goes unchecked,
    /// which is the right way for a heuristic like this to fail.
    /// </summary>
    private static string? SubjectBefore(string text, ContextPack pack)
    {
        string[] words = text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries);

        for (int i = words.Length - 1; i >= 0; i--)
        {
            string word = Strip(words[i]).ToLowerInvariant();
            if (!Mentions(pack, word)) continue;

            bool governed = false;
            for (int back = 1; back <= 2 && i - back >= 0; back++)
                if (Governing.Contains(Strip(words[i - back]))) governed = true;

            if (!governed) return word;
        }
        return null;
    }

    /// <summary>Words that start a fresh assertion, ending an elided run.</summary>
    private static bool Verbish(string word) => word.ToLowerInvariant() is
        "was" or "were" or "took" or "held" or "killed" or "murdered" or "challenged"
        or "returned" or "before" or "after" or "while" or "when" or "who" or "which"
        // "by" ends the run too: what follows is who did it, not another who suffered it.
        or "by" or "against" or "on" or "upon";

    /// <summary>The first clause of a fragment — up to the comma that ends it.</summary>
    private static string FirstClause(string text)
    {
        int stop = text.IndexOf(',', StringComparison.Ordinal);
        return stop < 0 ? text : text[..stop];
    }

    /// <summary>
    /// The fragment up to whatever starts a new claim about a different moment. A year past one
    /// of these belongs to that new claim, not to the verb this fragment began with.
    /// </summary>
    private static string UntilNewClause(string text)
    {
        int stop = text.Length;
        foreach (string boundary in new[]
                 { ",", " and ", " until ", " but ", " before ", " when ", " after ", " which ", " holding " })
        {
            int at = text.IndexOf(boundary, StringComparison.OrdinalIgnoreCase);
            if (at >= 0 && at < stop) stop = at;
        }
        return text[..stop];
    }

    /// <summary>
    /// Sentences, split again wherever a conjunction is followed straight away by a date.
    ///
    /// A year stated at the head of a sentence governs everything in it that carries no date of
    /// its own, which is what lets "In 46 he ordered the murder of Dourn and Thrild" be caught
    /// as two killings a year apart. But the same governance ran past a second date: writing it
    /// correctly — "In 46 … Dourn …, and in 47 … Thrild" — was then reported as two errors,
    /// each of them the other clause's year. A conjunction followed immediately by "in &lt;year&gt;"
    /// is a new dated clause and nothing before it governs anything after.
    /// </summary>
    private static List<string> DatedClauses(string passage)
    {
        List<string> clauses = [];

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int cut = 0;
            string lower = sentence.ToLowerInvariant();

            for (int i = 0; i < sentence.Length; i++)
            {
                foreach (string joiner in DateJoiners)
                {
                    if (i + joiner.Length >= lower.Length) continue;
                    if (string.CompareOrdinal(lower, i, joiner, 0, joiner.Length) != 0) continue;

                    // Only where a number actually follows, so "and in the years after" is left
                    // as one clause.
                    int after = i + joiner.Length;
                    if (after >= sentence.Length || !char.IsDigit(sentence[after])) continue;

                    clauses.Add(sentence[cut..i]);
                    cut = i;
                    break;
                }
            }

            clauses.Add(sentence[cut..]);
        }

        return clauses;
    }

    private static readonly string[] DateJoiners =
        [", and in ", " and in ", ", then in ", ", and again in ", " and again in "];

    private static int? FirstYear(string text)
    {
        foreach (string raw in text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(Strip(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) return y;
        return null;
    }

    /// <summary>
    /// A year, as opposed to whichever number the sentence reaches first.
    ///
    /// "Its raid on Hadale killed 16 but took forty head" carries no date at all, and reading the
    /// first number as one turned a body count into a year and reported a true sentence as a
    /// raid in a year Hadale was not raided. A date in this prose is always introduced — "in 43",
    /// "of 43" — so the introduction is what is looked for.
    /// </summary>
    private static int? DatedYear(string text)
    {
        string[] words = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i + 1 < words.Length; i++)
        {
            if (Strip(words[i]).ToLowerInvariant() is not ("in" or "of" or "during")) continue;
            if (int.TryParse(Strip(words[i + 1]), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int year))
            {
                return year;
            }
        }

        return null;
    }

    /// <summary>
    /// A recorded fate that rules the claimed one out, or null.
    ///
    /// Not every pair is exclusive, and treating them as if they were would invent findings. A
    /// man cast out inside this period may well have died outside it, so a record of exile does
    /// not falsify "he died"; a record of his violent death does. Only genuinely incompatible
    /// pairs are reported.
    /// </summary>
    private static string? Contradiction(ContextPack pack, string name, string claimed)
    {
        string[] incompatible = claimed switch
        {
            ClaimIndex.Exile or ClaimIndex.Outlaw => [ClaimIndex.Killed, ClaimIndex.DiedNaturally],
            ClaimIndex.Killed => [ClaimIndex.Exile, ClaimIndex.DiedNaturally],
            ClaimIndex.DiedNaturally => [ClaimIndex.Killed],
            _ => [],
        };

        foreach (string act in incompatible)
        {
            if (!pack.Claims.Knows(act, name)) continue;
            return $"{act} in {string.Join(" and ", pack.Claims.Years(act, name))}";
        }
        return null;
    }

    /// <summary>
    /// Checks enumerated raids against real ones.
    ///
    /// A passage handed the figure "three raids suffered" and no members named three: two real,
    /// and a third assembled from a power and a town and a year that occur separately in the
    /// document and never together in a raid. The count was right, every word was in
    /// vocabulary, and the raid did not happen.
    /// </summary>
    private static List<Fabrication> CheckRaidClaims(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string phrase, int? year, string sentence) in RaidTargetsNamed(passage))
        {
            // A dated raid is the assertion. A raid mentioned without a year is prose this
            // rule has nothing to say about, not an assertion it failed to check.
            if (year is not int stated) continue;

            cover.Extracted(RuleNames.Action);

            string? named = TargetNamed(pack, phrase);

            if (named is null)
            {
                // Nothing in the phrase names a target the records were raided at. While an
                // unfound lookup was silent this branch was where a whole class of extraction
                // failure went to be forgotten: the phrase-reader takes up to four words after
                // the preposition, so "its raid on Hadale killed 16 but took…" arrived as a
                // target called "hadale killed 16 but" and quietly resolved to nothing. It is a
                // finding now, so it has to be right — which means the phrase is narrowed to
                // the longest leading name the records know before this branch is reached.
                if (pack.Claims.Witnesses(ClaimIndex.Raid))
                {
                    cover.Checked(RuleNames.Action);
                    if (!reported.Add($"{phrase}|none")) continue;

                    findings.Add(new Fabrication($"raid on {phrase}", "no-such-event",
                        $"…{Shorten(sentence)}… — nothing records a raid on {phrase}"));
                    continue;
                }

                cover.Unresolvable(RuleNames.Action,
                    "these records hold no raid at all, so the phrase is read as ordinary prose",
                    Shorten(sentence));
                continue;
            }

            cover.Checked(RuleNames.Action);
            if (pack.Claims.Supports(ClaimIndex.Raid, named, stated)) continue;
            if (!reported.Add($"{named}|{stated}")) continue;

            findings.Add(new Fabrication($"raid on {named} in {stated}", "no-such-event",
                $"…{Shorten(sentence)}… — {named} was raided in " +
                $"{string.Join(" and ", pack.Claims.Years(ClaimIndex.Raid, named))}, not in {stated}"));
        }

        return findings;
    }

    /// <summary>
    /// The raid target a phrase names: the longest run of its leading words the records know as
    /// something that was raided.
    ///
    /// The phrase-reader cannot tell where a name ends — it takes words until a verb, a mark of
    /// punctuation or a count of four stops it, and English supplies none of those between
    /// "Hadale" and "killed". Rather than teach it grammar, the candidate is narrowed against
    /// what was actually raided, which is the only authority on where these names end.
    /// </summary>
    private static string? TargetNamed(ContextPack pack, string phrase)
    {
        string[] words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int take = words.Length; take > 0; take--)
        {
            string candidate = string.Join(' ', words[..take]);
            if (pack.Claims.Knows(ClaimIndex.Raid, candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Checks that a seat said to have been taken was that seat, by that person, in that year.
    ///
    /// A reign scope keyed on the actor rather than on the seat produced a section titled for
    /// one power that opened by describing its subject taking the seat of another.
    /// </summary>
    private static List<Fabrication> CheckSeatClaims(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            int at = lower.IndexOf("took the seat", StringComparison.Ordinal);
            if (at < 0) continue;

            string? who = SubjectBefore(sentence[..at], pack);
            if (who is null || !pack.Claims.Knows(ClaimIndex.TookSeat, who)) continue;

            // Only a year attached to the taking itself. "took the seat by election and held it
            // until year 15" carries a year that belongs to the end of the rule, and reading it
            // as the start reported a true sentence as false.
            int? year = FirstYear(UntilNewClause(sentence[at..]));
            if (year is not int stated) continue;

            if (!pack.Claims.Supports(ClaimIndex.TookSeat, who, stated))
            {
                if (reported.Add($"{who}|{stated}"))
                {
                    findings.Add(new Fabrication($"{who} {stated}", "wrong-year",
                        $"…{Shorten(sentence)}… — {who} took a seat in " +
                        $"{string.Join(" and ", pack.Claims.Years(ClaimIndex.TookSeat, who))}, not {stated}"));
                }
                continue;
            }

            // The right year: then it must be the right seat.
            string? seat = pack.Claims.SeatTaken(who, stated);
            if (seat is null) continue;

            List<string> distinctive = ContextPackBuilder.Distinctive(seat);
            bool namesIt = distinctive.Count == 0;
            foreach (string word in distinctive)
                if (lower.Contains(word, StringComparison.Ordinal)) namesIt = true;

            // Only complain where the sentence names some *other* power, not where it names none.
            if (namesIt || !NamesAnotherPower(pack, lower, distinctive)) continue;

            if (reported.Add($"{who}|{stated}|seat"))
            {
                findings.Add(new Fabrication($"{who} -> {seat}", "wrong-seat",
                    $"…{Shorten(sentence)}… — the seat {who} took in {stated} was {seat}"));
            }
        }

        return findings;
    }

    /// <summary>Whether the sentence names some power other than the one it should have.</summary>
    private static bool NamesAnotherPower(ContextPack pack, string lower, List<string> theRightOne)
    {
        foreach (string word in pack.PowerWords)
        {
            if (theRightOne.Contains(word)) continue;
            if (lower.Contains(word, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>Ways of saying that somebody stopped ruling.</summary>
    private static readonly string[] RuleEndMarkers =
    [
        " ended", " came to an end", " was brought to an end", " lost the seat",
        " was deposed", " was driven from", " was unseated", " was removed from",
    ];

    /// <summary>What the sentence must be about for an ending to be an ending of a rule.</summary>
    private static readonly string[] RuleWords = ["rule", "reign", "seat", "power"];

    /// <summary>Words that place a person in the seat rather than merely in the story.</summary>
    private static readonly string[] SeatWords =
        ["seat", "ruled", "rule", "reign", "held it", "succeed", "holder", "took power", "in power"];

    /// <summary>
    /// Checks who is said to have ended a rule against who actually did.
    ///
    /// This is the round-6 failure and nothing else could see it: "The rule of Weallhous Dreld
    /// ended when he was beaten in an open challenge by Saern Meastouth" names two real men who
    /// really fought, in the year they really fought, and inverts the result. Every earlier
    /// check passes it. The seat history does not: Meastouth lost, and Dreld's rule ended two
    /// years later at another man's hands.
    /// </summary>
    private static List<Fabrication> CheckRuleEnds(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.RuleEnders.Count == 0) return findings;

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            int at = -1;
            foreach (string phrase in RuleEndMarkers)
            {
                int found = lower.IndexOf(phrase, StringComparison.Ordinal);
                if (found >= 0 && (at < 0 || found < at)) at = found;
            }
            if (at < 0) continue;

            // An ending is only an ending of a rule if the sentence is about one. Without this
            // the check fires on famines ending and wars ending, which is most of a chronicle.
            string head = lower[..at];
            bool aboutRule = false;
            foreach (string word in RuleWords)
                if (head.Contains(word, StringComparison.Ordinal)) aboutRule = true;
            if (!aboutRule) continue;

            // Whose rule: the last person named before the marker.
            string? ruler = LastKnownName(sentence[..at], pack);
            if (ruler is null || !pack.RuleEnders.TryGetValue(ruler, out string? actual)) continue;

            // At whose hands: the first person named after an agentive "by".
            string tail = sentence[at..];
            int by = tail.IndexOf(" by ", StringComparison.OrdinalIgnoreCase);
            if (by < 0) continue;

            string? claimed = FirstKnownName(tail[(by + 4)..], pack);
            if (claimed is null) continue;
            if (string.Equals(claimed, ruler, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(claimed, actual, StringComparison.OrdinalIgnoreCase)) continue;

            findings.Add(new Fabrication($"{ruler} <- {claimed}", "wrong-ender",
                actual.Length == 0
                    ? $"…{Shorten(sentence)}… — nobody ended {ruler}'s rule; it ended another way"
                    : $"…{Shorten(sentence)}… — {actual} ended {ruler}'s rule, not {claimed}"));
        }

        return findings;
    }

    /// <summary>
    /// Checks that anything described as raided actually was.
    ///
    /// The failure this exists for reads as ordinary prose: "Hadale broke away after a raid on
    /// the Compact was beaten off". What happened was the Compact's own raid failing. Reversed,
    /// a defeat becomes a victory and the secession it caused becomes a non-sequitur — the
    /// causal edge is preserved and its meaning is inverted, which is worse than dropping it.
    /// </summary>
    private static List<Fabrication> CheckRaidDirection(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.RaidTargets.Count == 0) return findings;

        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string named, int? _, string sentence) in RaidTargetsNamed(passage))
        {
            if (Names(pack, named) is false) continue;   // an invented name; the name check has it

            // A short form that names no power in particular is a different complaint, and
            // is reported as one below rather than as a direction error.
            List<string> distinctive = ContextPackBuilder.Distinctive(named);
            if (distinctive.Count == 0) continue;
            if (Raided(pack, distinctive)) continue;
            if (!reported.Add(named)) continue;

            findings.Add(new Fabrication(named, "wrong-direction",
                $"…{Shorten(sentence)}… — nothing in these records raided {named}; " +
                "check which side attacked"));
        }

        return findings;
    }

    /// <summary>
    /// Everything a passage says was raided, with the year attached where it carries one.
    ///
    /// Not keyed on "raid on X" alone, because the failure came in an enumeration: "three
    /// raids: one by A on Hadale in 23, one by B on Kebarrow in 23, and one by C on Kebarrow in
    /// 32". The word "raid" appears once and governs three targets, so the sentence is the unit
    /// and every "on X … in Y" inside it is a claim.
    /// </summary>
    private static List<(string Named, int? Year, string Sentence)> RaidTargetsNamed(string passage)
    {
        List<(string, int?, string)> claims = [];

        // Clauses, not sentences. "A raid on Griwick was beaten off, and in 31, fourteen died
        // while 33 abandoned the place" gave the raid the famine's year and reported a true
        // sentence as a raid that never happened.
        foreach (string sentence in DatedClauses(passage))
        {
            string lower = sentence.ToLowerInvariant();
            if (!lower.Contains("raid", StringComparison.Ordinal)) continue;

            foreach (string preposition in new[] { " on ", " against ", " upon " })
            {
                int from = 0;
                while (true)
                {
                    int at = lower.IndexOf(preposition, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    from = at + preposition.Length;

                    // The preposition must belong to the raid, not to something else the
                    // sentence happens to mention. "…both beaten off, and it ordered two
                    // killings against people of other powers" was read as a raid on people.
                    if (!GovernedByRaid(lower, at)) continue;

                    string named = NounPhrase(lower[from..]);
                    if (named.Length == 0) continue;

                    // The year belongs to this item only if it comes before the next item does.
                    int nextItem = lower.IndexOf(preposition, from, StringComparison.Ordinal);
                    string span = nextItem < 0 ? lower[from..] : lower[from..nextItem];
                    claims.Add((named, DatedYear(span), sentence));
                }
            }
        }

        return claims;
    }

    /// <summary>
    /// Whether anything actually raided here shares a distinguishing word with the phrase. Not
    /// mere containment: "the Compact" is a substring of "the Griwick Compact" and naming one
    /// is not naming the other.
    /// </summary>
    /// <summary>Whether every part of a hyphenated word is ordinary English.</summary>
    private static bool OrdinaryCompound(string token)
    {
        foreach (string part in token.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            string word = part.TrimEnd('s');
            if (!Common.Contains(word) && !Common.Contains(part)) return false;
        }
        return true;
    }

    /// <summary>
    /// Whether a fragment divides a total into categories rather than naming its members.
    /// "two for the losing claim and one for conspiracy" accounts for three people without
    /// undertaking to name any of them.
    /// </summary>
    private static bool PartitionsRatherThanNames(string text)
    {
        string[] words = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        int parts = 0;

        for (int i = 0; i + 1 < words.Length; i++)
        {
            string word = Strip(words[i]).ToLowerInvariant();
            if (!Units.ContainsKey(word) && !int.TryParse(word, out _)) continue;

            string next = Strip(words[i + 1]).ToLowerInvariant();
            if (next is "for" or "were" or "was" or "of" or "by" or "in") parts++;
        }

        return parts >= 2;
    }

    /// <summary>Whether one of these verbs sits within a few words either side of a mention.</summary>
    private static bool VerbNear(string[] words, int at, int span, string[] marks)
    {
        int from = Math.Max(0, at - 4);
        int to = Math.Min(words.Length, at + span + 4);
        string window = string.Join(' ', words[from..to]).ToLowerInvariant();

        foreach (string mark in marks)
            if (window.Contains(mark, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// Whether the nearest thing before this preposition that could govern it is a raid.
    ///
    /// A fixed window was wrong in both directions: too small and an enumerated list of raids
    /// lost its governing noun three items in, too large and "…raids were beaten off, and it
    /// ordered two killings against people of other powers" read as a raid on people. Nearest
    /// wins, which is how a reader resolves it too.
    /// </summary>
    private static bool GovernedByRaid(string lower, int at)
    {
        string before = lower[..at];
        int raid = before.LastIndexOf("raid", StringComparison.Ordinal);
        if (raid < 0) return false;

        foreach (string other in new[] { "killing", "murder", " war", "battle", "claim", "attempt" })
            if (before.LastIndexOf(other, StringComparison.Ordinal) > raid) return false;

        return true;
    }

    private static bool Raided(ContextPack pack, List<string> distinctive)
    {
        foreach (string target in pack.RaidTargets)
            foreach (string word in ContextPackBuilder.Distinctive(target))
                if (distinctive.Contains(word)) return true;
        return false;
    }

    /// <summary>
    /// Short forms that identify nothing, in a passage where two powers end in the same word.
    ///
    /// The prose is not wrong so much as unusable: a war section called the Griwick Compact
    /// "the Compact" throughout, in a document where every other section meant Kebarrow by it.
    /// A reader who guesses wrong is never corrected.
    /// </summary>
    private static List<Fabrication> CheckAmbiguousNames(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.AmbiguousShortNames.Count == 0) return findings;

        string[] words = passage.Split(
            [' ', '\n', '\r', '\t', '(', ')'], StringSplitOptions.RemoveEmptyEntries);

        HashSet<string> reported = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < words.Length; i++)
        {
            string word = Strip(words[i]).ToLowerInvariant();
            if (!pack.AmbiguousShortNames.Contains(word)) continue;

            // Qualified by the word before it — "the Griwick Compact" — is exactly right.
            string previous = Strip(words[i - 1]).ToLowerInvariant();
            if (previous is not ("the" or "a" or "that" or "this")) continue;
            if (!reported.Add(word)) continue;

            findings.Add(new Fabrication($"the {word}", "ambiguous-short-name",
                $"two powers here are called \"{word}\"; the short form says neither"));
        }

        return findings;
    }

    /// <summary>Whether every word of a phrase is one the pack supplied — an article aside.</summary>
    private static bool Names(ContextPack pack, string phrase)
    {
        HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
        foreach (string word in pack.Vocabulary) known.Add(Strip(word));

        bool any = false;
        foreach (string word in phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word is "the" or "a" or "an") continue;
            if (!known.Contains(Strip(word))) return false;
            any = true;
        }
        return any;
    }

    /// <summary>
    /// The noun phrase a preposition governs: everything up to the verb or punctuation that
    /// ends it. Deliberately short — four words is more than any name here needs.
    /// </summary>
    private static string NounPhrase(string text)
    {
        List<string> words = [];
        foreach (string raw in text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = raw.TrimEnd(',', '.', ';', ':');
            if (word.Length == 0) break;
            if (word is "was" or "were" or "is" or "had" or "in" or "that" or "which" or "and") break;

            words.Add(word);
            if (raw.Length > word.Length) break;    // punctuation ended the phrase
            if (words.Count == 4) break;
        }

        string phrase = string.Join(' ', words);
        return phrase.StartsWith("the ", StringComparison.Ordinal) ? phrase[4..] : phrase;
    }

    /// <summary>
    /// Catches "including" in front of a list that is in fact the whole of it.
    ///
    /// "Four people were murdered from within, including A, B, C and D" tells the reader there
    /// were others. There were not. A chronicle a reader cannot tell a sample from is not usable
    /// as a reference, which is the entire point of the render layer.
    /// </summary>
    private static List<Fabrication> CheckHedgedLists(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];

        HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in pack.ActorPairs)
            foreach (string name in pair.Split('|'))
                known.Add(name);
        foreach (string holder in pack.SeatHolders) known.Add(holder);
        if (known.Count == 0) return findings;

        foreach (string sentence in passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int at = sentence.IndexOf("including", StringComparison.OrdinalIgnoreCase);
            if (at < 0) continue;

            string[] before = sentence[..at].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            int? stated = null;
            foreach (string word in before)
                if (Units.TryGetValue(Strip(word), out int n)) stated = n;
                else if (int.TryParse(Strip(word), NumberStyles.Integer, CultureInfo.InvariantCulture, out int d)) stated = d;
            if (stated is null) continue;

            HashSet<string> listed = new(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in sentence[at..].Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
            {
                string word = Strip(raw);
                if (known.Contains(word)) listed.Add(word);
            }

            if (listed.Count == 0 || listed.Count < stated) continue;

            findings.Add(new Fabrication("including", "hedged-exhaustive-list",
                $"…{Shorten(sentence)}… — all {stated} are named, so \"including\" is wrong"));
        }

        return findings;
    }

    /// <summary>Constructions that assert one person followed another in office.</summary>
    /// <summary>
    /// Bare forms, because the model varies the wording. "who was in turn set aside by" does
    /// not contain "was set aside by", and matching only the fuller phrases let one form of
    /// the same fabrication through while catching the other.
    /// </summary>
    private static readonly string[] SuccessionPhrases =
    [
        "succeeded by", "replaced by", "followed by", "set aside by", "deposed by",
        "overthrown by", "ousted by", "gave way to", "supplanted by",
    ];

    /// <summary>
    /// Validates claims that A was followed in office by B against the actual seat history.
    ///
    /// This is the check the last two rounds needed and did not have. "Ska was killed by
    /// Stonand Ker, who was succeeded by Le Vild" names three real people and every word of it
    /// appears in the source; what is false is that Ker ever held the seat at all. Proper-noun
    /// presence cannot see that. The seat history can.
    /// </summary>
    private static List<Fabrication> CheckSuccessions(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        if (pack.SeatHolders.Count == 0) return findings;

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            foreach (string phrase in SuccessionPhrases)
            {
                int at = lower.IndexOf(phrase, StringComparison.Ordinal);
                if (at < 0) continue;

                string before = sentence[..at];
                string after = sentence[(at + phrase.Length)..];

                cover.Extracted(RuleNames.Succession);

                string? predecessor = LastKnownName(before, pack);
                string? successor = FirstKnownName(after, pack);

                if (predecessor is null || successor is null)
                {
                    cover.Unresolvable(RuleNames.Succession, "one side of the succession is unnamed", Shorten(sentence));
                    continue;
                }

                cover.Checked(RuleNames.Succession);

                // Anyone said to have been succeeded must actually have held the seat.
                if (!pack.SeatHolders.Contains(predecessor))
                {
                    findings.Add(new Fabrication(predecessor, "never-held-the-seat",
                        $"…{Shorten(sentence)}… — {predecessor} never held it"));
                    continue;
                }

                if (!pack.SuccessionPairs.Contains($"{predecessor}|{successor}"))
                {
                    findings.Add(new Fabrication($"{predecessor} -> {successor}", "false-succession",
                        $"…{Shorten(sentence)}… — {successor} did not follow {predecessor}"));
                }
            }
        }

        return findings;
    }

    private static string? LastKnownName(string text, ContextPack pack)
    {
        string? found = null;
        foreach (string raw in text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Strip(raw).ToLowerInvariant();
            if (Mentions(pack, word)) found = word;
        }
        return found;
    }

    private static string? FirstKnownName(string text, ContextPack pack)
    {
        foreach (string raw in text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Strip(raw).ToLowerInvariant();
            if (Mentions(pack, word)) return word;
        }
        return null;
    }

    private static bool Mentions(ContextPack pack, string surname)
    {
        if (pack.SeatHolders.Contains(surname)) return true;
        foreach (string pair in pack.ActorPairs)
            foreach (string name in pair.Split('|'))
                if (string.Equals(name, surname, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Two people named in the same sentence must have shared at least one event.
    ///
    /// Checking only that each proper noun appears somewhere in the source let the model join
    /// adjacent facts into relationships that never existed — a man who never held the seat
    /// described as having been deposed from it. Both names were real; the link was not.
    /// </summary>
    private static List<Fabrication> CheckPairs(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.ActorPairs.Count == 0) return findings;

        // Every surname the pack knows, so only real people are paired up.
        HashSet<string> known = new(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in pack.ActorPairs)
            foreach (string name in pair.Split('|'))
                known.Add(name);

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            // Split on conjunctions, so two people listed as separate victims of one act —
            // "murdered Dourn at Vea Lode and Thrild at Griwick" — are not read as having met.
            //
            // But NOT before a relative pronoun. Splitting on every comma is what let the one
            // hard fabrication through two fix rounds: "killed by Stonand Ker, who was
            // succeeded by Le Vild" was cut in half at the comma, so the two names the sentence
            // actually links were never compared with each other.
            foreach (string clause in SplitClauses(sentence))
            {
                List<(string Name, int At)> named = [];
                string[] words = clause.Split([' ', '(', ')', '\''], StringSplitOptions.RemoveEmptyEntries);

                for (int w = 0; w < words.Length; w++)
                {
                    string word = Strip(words[w]).ToLowerInvariant();
                    if (known.Contains(word) && !named.Exists(n => n.Name == word)) named.Add((word, w));
                }

                for (int i = 0; i < named.Count; i++)
                {
                    for (int j = i + 1; j < named.Count; j++)
                    {
                        string key = ContextPackBuilder.Pair(named[i].Name, named[j].Name);
                        if (pack.ActorPairs.Contains(key)) continue;

                        // Only when something between them actually asserts a relation.
                        if (!Relates(words, named[i].At, named[j].At)) continue;

                        findings.Add(new Fabrication(
                            $"{named[i].Name} + {named[j].Name}", "unshared-pair",
                            $"…{Shorten(clause)}… — these two never appear in the same event"));
                    }
                }
            }
        }

        return findings;
    }

    private static readonly Dictionary<string, int> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6,
        ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11,
        ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
        ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19,
    };

    private static readonly Dictionary<string, int> Tens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50,
        ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90,
    };

    /// <summary>
    /// Reads "forty-nine", "forty nine" or "twelve" as a value. Only fires where the words are
    /// being used as a quantity — a bare "one" or "two" in ordinary prose is left alone by the
    /// caller's threshold.
    /// </summary>
    private static int? SpelledNumber(string[] words, int at)
    {
        string token = Strip(words[at]);

        int dash = token.IndexOfAny(['-', '–']);
        if (dash > 0)
        {
            string left = token[..dash];
            string right = token[(dash + 1)..];
            if (Tens.TryGetValue(left, out int t) && Units.TryGetValue(right, out int u)) return t + u;
        }

        if (Tens.TryGetValue(token, out int tens))
        {
            if (at + 1 < words.Length && Units.TryGetValue(Strip(words[at + 1]), out int next) && next < 10)
                return tens + next;
            return tens;
        }

        return Units.TryGetValue(token, out int unit) ? unit : null;
    }

    private static bool EndsSentence(string previous) =>
        previous.EndsWith('.') || previous.EndsWith('!') || previous.EndsWith('?') || previous.EndsWith(':');

    private static bool IsNumber(string token) =>
        long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

    private static string Strip(string word)
    {
        string s = word.Trim('.', ',', ':', ';', '!', '?', '\'', '"', '’', '—', '-', '(', ')', '[', ']');

        // Possessives are the same name. Both apostrophe forms, because the model emits the
        // curly one and trimming alone leaves "Dra’s" looking like an unknown proper noun.
        if (s.EndsWith("’s", StringComparison.Ordinal) || s.EndsWith("'s", StringComparison.Ordinal))
            s = s[..^2];
        else if (s.EndsWith('’') || s.EndsWith('\'')) s = s[..^1];

        return s;
    }

    /// <summary>
    /// Breaks a sentence where a genuinely new subject starts, keeping relative clauses joined
    /// to what they modify — "X, who was succeeded by Y" is one claim about X and Y, not two.
    /// </summary>
    private static List<string> SplitClauses(string sentence)
    {
        string working = sentence;

        // Protect ", who / whom / whose / which" from the comma split that follows.
        foreach (string pronoun in new[] { "who", "whom", "whose", "which" })
            working = working.Replace($", {pronoun} ", $" {pronoun} ", StringComparison.OrdinalIgnoreCase);

        return [.. working.Split(
            [" and ", " or ", ", ", " while ", " whereas "],
            StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>
    /// Words that turn two adjacent names into a claim about the pair. Without this the check
    /// fired on any two people mentioned near each other, which is most of a chronicle.
    /// </summary>
    private static readonly HashSet<string> Relational = new(StringComparer.OrdinalIgnoreCase)
    {
        "by", "who", "whom", "whose", "against", "over", "from", "replaced", "succeeded",
        "deposed", "murdered", "killed", "challenged", "contested", "aside", "cast",
        "defeated", "married", "betrayed", "exiled", "overthrew", "attacked",
    };

    private static bool Relates(string[] words, int a, int b)
    {
        int lo = Math.Min(a, b) + 1;
        int hi = Math.Max(a, b);
        if (hi - lo > 8) return false;

        for (int i = lo; i < hi; i++)
            if (Relational.Contains(Strip(words[i]))) return true;
        return false;
    }

    private static string Shorten(string sentence)
    {
        string trimmed = sentence.Trim();
        return trimmed.Length <= 90 ? trimmed : trimmed[..89] + "…";
    }

    private static string Context(string[] words, int at)
    {
        int from = Math.Max(0, at - 4);
        int to = Math.Min(words.Length, at + 5);
        return string.Join(' ', words[from..to]);
    }

    // ---- round 12: scope, attribution, particulars, anchors, order --------

    /// <summary>
    /// Every "&lt;Name&gt; in &lt;year&gt;" pair in a dated roster, against the record.
    ///
    /// This is the single most productive shape in the chronicle and the least checked. A
    /// section says four exiles returned and then lists them with a year each; the count is
    /// right, the names are right, the list is exhaustive, and one of the years is off by one.
    /// Nothing looked. "Thosruld Lul in 39" for a return the log dates to 38 survived every
    /// rule in the file because each of them was checking something else.
    ///
    /// Which act the roster is about comes from the words that introduce it, so the same
    /// machinery covers returns, exiles and courtings-away without a rule for each.
    /// </summary>
    private static List<Fabrication> CheckDatedRosters(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];

        foreach (string sentence in passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            string? act = null;
            foreach ((string cue, string kind) in RosterCues)
                if (lower.Contains(cue, StringComparison.Ordinal)) { act = kind; break; }

            if (act is null) continue;

            string[] words = sentence.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);

            // A roster has more than one member. One "<Name> in <year>" in a sentence carrying
            // one of these words is ordinary prose, and reading it as a roster charged Teillmol
            // Lund with a year that belonged to the man who lost a challenge to him.
            if (DatedPairs(words) < 2) continue;

            for (int i = 1; i < words.Length - 1; i++)
            {
                // "<Surname> in <year>" — the surname is the capitalised word before "in".
                if (!string.Equals(Strip(words[i]), "in", StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(Strip(words[i + 1]), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int year)) continue;

                string name = Strip(words[i - 1]).ToLowerInvariant();
                if (name.Length < 3 || !char.IsUpper(Strip(words[i - 1])[0])) continue;

                cover.Extracted(RuleNames.Date);

                IReadOnlyCollection<int> actual = pack.Claims.Years(act, name);

                if (actual.Count == 0)
                {
                    // A roster names people and dates them, so a name inside one that the
                    // records do not have under that act is a person given a fate they never
                    // met. Gated all the same: the name here is any capitalised word before a
                    // year, which is a place or a power as often as it is a person.
                    if (pack.Claims.Witnesses(act))
                    {
                        cover.Checked(RuleNames.Date);
                        findings.Add(new Fabrication(name, "no-such-act",
                            $"…{name} in {year}… — nothing records {name} being {act}"));
                    }
                    else
                    {
                        cover.Unresolvable(RuleNames.Date,
                            "these records hold no act of that kind, so the roster is read as ordinary prose",
                            $"{name} in {year}");
                    }
                    continue;
                }

                cover.Checked(RuleNames.Date);
                if (actual.Contains(year)) continue;

                findings.Add(new Fabrication(name, "wrong-year",
                    $"…{name} in {year}… — the record has {act} for {name} in " +
                    string.Join(", ", actual.OrderBy(y => y))));
            }
        }

        return findings;
    }

    /// <summary>How many "&lt;Name&gt; in &lt;year&gt;" pairs a sentence carries.</summary>
    private static int DatedPairs(string[] words)
    {
        int pairs = 0;

        for (int i = 1; i < words.Length - 1; i++)
        {
            if (!string.Equals(Strip(words[i]), "in", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(Strip(words[i + 1]), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) continue;

            string previous = Strip(words[i - 1]);
            if (previous.Length > 2 && char.IsUpper(previous[0])) pairs++;
        }

        return pairs;
    }

    /// <summary>
    /// The words that say what a dated roster is a roster of.
    ///
    /// Order matters: the first cue found wins, so the more specific phrasings come first.
    /// </summary>
    private static readonly (string Cue, string Act)[] RosterCues =
    [
        ("returned from exile", ClaimIndex.Returned),
        ("returned to take service", ClaimIndex.Returned),
        ("returns", ClaimIndex.Returned),
        ("returned", ClaimIndex.Returned),
        ("took service", ClaimIndex.Returned),
        ("declared outlaw", ClaimIndex.Outlaw),
        ("cast out", ClaimIndex.Exile),
        ("courted away", ClaimIndex.WonAway),
        ("won away", ClaimIndex.WonAway),
    ];

    /// <summary>
    /// A year inside the section's window that the section never mentions, and should.
    ///
    /// The Kebarrow window of 2–21 told fourteen of its twenty years and left out year 20, in
    /// which a war ended, a battle was won, a settlement changed hands and a power ceased to
    /// exist. Nothing was false; the busiest year of the period was simply not there, and no
    /// rule that checks statements can find a year that produced none.
    ///
    /// Reported as a readability finding rather than a falsehood. An omission does not make the
    /// rest untrue, and a rule that kept sections out of canon for being incomplete would empty
    /// the chronicle — but a reader should be told, and so should the retry.
    /// </summary>
    /// <summary>
    /// The two ways a section's shape fails, which are opposites of each other.
    ///
    /// One window named one of its eleven rulers and was accurate, verifiable and empty of
    /// people; the next was one sentence per year in log order, which is the transliteration
    /// failure from round 2. Both had been right two rounds earlier. That oscillation is the
    /// signal that prompt wording had stopped converging — a nudge toward particulars produces
    /// a chronicle of dates, a nudge toward patterns produces a chronicle of statistics, and
    /// each round trades one for the other.
    ///
    /// So the balance stops being a matter of instruction and becomes a measurement. These are
    /// style findings — an aggregate section is dull, not false, and canon must be true rather
    /// than good — but they are worth a second pass, which is a distinct thing from blocking.
    /// </summary>
    private static List<Fabrication> CheckShape(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.Kind is not PackKind.FactionArc) return findings;

        // Too aggregate: a window with a cast, narrated as statistics.
        IReadOnlyList<Tenure> tenures = pack.Digest.Tenures;
        if (tenures.Count >= 5)
        {
            string lower = passage.ToLowerInvariant();
            int named = 0;
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (Tenure t in tenures)
            {
                string surname = ContextPackBuilder.Surname(t.Holder);
                if (!seen.Add(surname)) continue;
                if (lower.Contains(surname.ToLowerInvariant(), StringComparison.Ordinal)) named++;
            }

            // Half, rounded up. Not all of them: a long window has to summarise somewhere, and
            // demanding every name back would swing the section into the other failure.
            int wanted = (seen.Count + 1) / 2;
            if (named < wanted)
            {
                findings.Add(new Fabrication("rulers", "too-aggregate",
                    $"{seen.Count} people held the seat here and the section names {named}; " +
                    $"at least {wanted} should appear by name"));
            }
        }

        // The opposite: a year-by-year walk of the log with no shape imposed on it.
        List<string> sentences = [.. passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0)];

        int dated = 0, ascending = 0, previous = int.MinValue;

        foreach (string sentence in sentences)
        {
            string opening = sentence.ToLowerInvariant();
            if (!opening.StartsWith("in ", StringComparison.Ordinal)) { previous = int.MinValue; continue; }

            string[] words = sentence.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2) continue;
            if (!int.TryParse(Strip(words[1]), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int year)) continue;

            dated++;
            if (year >= previous && previous != int.MinValue) ascending++;
            previous = year;
        }

        // Six is well past a stylistic tic. The section that prompted this opened eight
        // consecutive sentences with a year, in order, which is the log with joining words.
        if (dated >= 6 && ascending >= dated - 2 && dated * 2 >= sentences.Count)
        {
            findings.Add(new Fabrication("in <year>", "year-by-year",
                $"{dated} of {sentences.Count} sentences open with a year, in order — this is " +
                "the log transliterated rather than a history of the period"));
        }

        return findings;
    }

    private static List<Fabrication> CheckCoverage(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];

        // Reigns are told as one story and need no year-by-year coverage; a war has its own
        // span. This is about the wide windows a power's history is cut into.
        if (pack.Kind is not PackKind.FactionArc) return findings;
        if (pack.ToYear - pack.FromYear < 5) return findings;

        string lower = passage.ToLowerInvariant();

        // Places changing hands. The one thing a window cannot leave out and still be a history
        // of the power, and the omission is checkable without judging prose: if a settlement
        // changed hands, its name is in the record and must be in the section.
        foreach (HoldingChange change in pack.Digest.PlacesTaken)
            Require(change.Place, [change.Place.ToLowerInvariant()],
                $"{change.Place} was taken in {change.Year} and is never named");

        foreach (HoldingChange change in pack.Digest.PlacesLost)
            Require(change.Place, [change.Place.ToLowerInvariant()],
                $"{change.Place} was lost in {change.Year} and is never named");

        // And the end of a power, which nothing else in the record can stand in for. Year 20 of
        // the Kebarrow window held a war, a battle, a conquest and the destruction of the Wurn
        // League, and the section told none of it while mentioning the year in passing — which
        // is why looking for the numeral was not enough.
        int? collapse = YearOfEvent(pack, "is finished");
        if (collapse is not null)
            Require("collapse", ["collapse", "was finished", "ceased", "destroyed", "came to an end",
                                 "landless", "scattered", "no longer existed"],
                $"a power was destroyed in {collapse} and the section never says so");

        return findings;

        void Require(string token, string[] evidence, string complaint)
        {
            foreach (string phrase in evidence)
                if (lower.Contains(phrase, StringComparison.Ordinal)) return;

            findings.Add(new Fabrication(token, "incomplete-enumeration", complaint));
        }
    }

    /// <summary>
    /// Plunder claimed for a raid that took nothing.
    ///
    /// The engine distinguishes three outcomes — beaten off, got through and took nothing, got
    /// through with a haul — and the prose keeps collapsing the middle one into the third,
    /// because "the raid succeeded" reads as "the raid gained something". It does not. A raid
    /// that carried off zero grain and zero ore is a different event from one that carried off
    /// thirty-one, and the log says which.
    /// </summary>
    private static List<Fabrication> CheckRaidHauls(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.Digest.RaidsOut.Count == 0) return findings;

        foreach (string sentence in DatedClauses(passage))
        {
            string lower = sentence.ToLowerInvariant();

            foreach (string phrase in PlunderPhrases)
            {
                int at = lower.IndexOf(phrase, StringComparison.Ordinal);
                if (at < 0) continue;

                // Only the clause the claim governs.
                //
                // A section wrote the truth in one sentence — "three of which carried off
                // plunder from Kebarrow in years 4 and 17 …, while three were beaten off at
                // Hadale in years 7 and 22 …" — and reading the whole sentence charged the
                // plunder claim with all three repulses it had just correctly reported. A
                // contrastive conjunction ends the claim's reach; nothing after it is its.
                string governed = Governed(sentence, at);
                string governedLower = governed.ToLowerInvariant();

                foreach (RaidRecord raid in pack.Digest.RaidsOut)
                {
                    if (raid.Result == RaidResult.Plunder) continue;

                    int place = governedLower.IndexOf(raid.Place.ToLowerInvariant(), StringComparison.Ordinal);
                    if (place < 0) continue;
                    if (!YearsIn(governed, pack).Contains(raid.Year)) continue;

                    // The claim stops at the first word that reverses it. "three carried off
                    // plunder (…) and three were beaten off (Hadale in 7, …)" joins both halves
                    // with "and" rather than a contrast, so the clause split cannot separate
                    // them — but a repulse standing between the claim and the place says plainly
                    // that the place is not the claim's.
                    if (Repulsed(governedLower, place)) continue;

                    findings.Add(new Fabrication(raid.Place, "no-such-event",
                        $"…{Shorten(governed)}… — the raid on {raid.Place} in {raid.Year} " +
                        (raid.Result == RaidResult.BeatenOff ? "was beaten off" : "took nothing")));
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// The stretch of a sentence a claim at <paramref name="at"/> speaks for: from the clause
    /// boundary before it to the contrastive conjunction after it.
    /// </summary>
    private static string Governed(string sentence, int at)
    {
        string lower = sentence.ToLowerInvariant();

        int from = 0;
        int to = sentence.Length;

        foreach (string boundary in ClauseBreaks)
        {
            int before = lower.LastIndexOf(boundary, Math.Max(0, at - 1), StringComparison.Ordinal);
            if (before >= 0 && before + boundary.Length > from) from = before + boundary.Length;

            int after = lower.IndexOf(boundary, at, StringComparison.Ordinal);
            if (after >= 0 && after < to) to = after;
        }

        return sentence[from..to];
    }

    private static readonly string[] ClauseBreaks =
        [", while ", " while ", ", but ", " but ", ", whereas ", ", though ", ", although ", "; "];

    /// <summary>Whether a repulse is reported between the start of the span and a place.</summary>
    private static bool Repulsed(string governed, int place)
    {
        string before = governed[..place];

        foreach (string phrase in new[] { "beaten off", "took nothing", "repulsed", "driven off", "failed" })
            if (before.Contains(phrase, StringComparison.Ordinal)) return true;

        return false;
    }

    private static readonly string[] PlunderPhrases =
        ["carried off plunder", "carrying off plunder", "took plunder", "with plunder",
         "carried off spoil", "laden with"];

    /// <summary>
    /// A tenure dated to a year the section has no record of.
    ///
    /// "Pouldrir Ho, who held the seat since year 1" — the window opens in year 4 and the
    /// readable log opens in year 2, so nothing here can say when he took it. The claim is not
    /// contradicted by the records; it is simply not in them, which is the harder case and the
    /// one that has now reached canon twice. The same shape gave Heth Fal a Laehiford seat from
    /// 33, a year in which he held a different one.
    ///
    /// Only the tenure phrasings, and only where the year falls outside the window. A section
    /// may mention an earlier year as context; what it may not do is date its own seat from one.
    /// </summary>
    private static List<Fabrication> CheckTenureWindow(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            foreach (string phrase in SeatVerbs)
            {
                int at = lower.IndexOf(phrase, StringComparison.Ordinal);
                if (at < 0) continue;

                // The date need not follow the verb directly — "held the seat of the Sworn Men
                // of Laehiford from 33" puts the whole title in between — so the clause after
                // the verb is scanned for whichever preposition carries the year.
                string[] words = lower[(at + phrase.Length)..]
                    .Split([' ', ',', ')'], StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < words.Length - 1; i++)
                {
                    if (Array.IndexOf(SeatPrepositions, Strip(words[i])) < 0) continue;

                    string next = Strip(words[i + 1]);
                    if (string.Equals(next, "year", StringComparison.Ordinal) && i + 2 < words.Length)
                        next = Strip(words[i + 2]);

                    // A dated tenure is the assertion; a seat verb with no date after it is
                    // ordinary prose. Extracting on the verb alone counted forty-six assertions
                    // where the section made six, and left forty of them leaving the rule by a
                    // path that recorded nothing at all.
                    if (!int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year))
                        continue;

                    cover.Extracted(RuleNames.Tenure);
                    cover.Checked(RuleNames.Tenure);

                    // A reign is bounded by the reign; a window is not.
                    //
                    // The window's edges are an editorial cut into twenty-year eras, and a power
                    // whose ruler took the seat the year before the cut may say so — reading the
                    // boundary as the edge of knowledge held a true section out of canon over
                    // "held the seat since 23" in a window opening in 24. What no section may do
                    // is date a tenure from before the world, which is what "since year 1" does.
                    //
                    // A reign scope is different: it is one person in one seat over one stretch,
                    // and the log says exactly when that stretch began. Heth Fal held a Kebarrow
                    // seat in 33 and a Laehiford one from 39, and the section for the second
                    // dated it from the first.
                    bool wrong = pack.Kind == PackKind.Reign
                        ? year != pack.FromYear
                        : year < pack.WorldFromYear;

                    if (!wrong) continue;

                    findings.Add(new Fabrication(year.ToString(CultureInfo.InvariantCulture),
                        "outside-the-window",
                        pack.Kind == PackKind.Reign
                            ? $"…{Shorten(sentence)}… — this seat was taken in {pack.FromYear}, not {year}"
                            : $"…{Shorten(sentence)}… — the record opens in {pack.WorldFromYear} " +
                              $"and holds nothing from year {year}"));
                    break;
                }

                break;
            }
        }

        return findings;
    }

    private static readonly string[] SeatVerbs =
        ["held the seat", "held it", "took the seat", "came to power", "ruled"];

    private static readonly string[] SeatPrepositions = ["since", "from"];

    /// <summary>
    /// A tenure ascribed to somebody who never held the seat.
    ///
    /// <see cref="CheckSuccessions"/> catches this only where the prose says one person
    /// succeeded another. It does not catch the shorter form — "ending Skul's tenure" — which
    /// asserts the same false thing in three words and no succession verb. Hehum Skul was the
    /// named heir whose claim was set aside; he never held anything to end.
    /// </summary>
    private static List<Fabrication> CheckAscribedTenures(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.SeatHolders.Count == 0) return findings;

        foreach (string sentence in passage.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            foreach (string noun in TenureNouns)
            {
                // "<name>'s tenure" — the possessive is the claim.
                for (int at = lower.IndexOf(noun, StringComparison.Ordinal); at > 0;
                     at = lower.IndexOf(noun, at + 1, StringComparison.Ordinal))
                {
                    string? owner = LastKnownName(sentence[..at], pack);
                    if (owner is null || pack.SeatHolders.Contains(owner)) continue;

                    findings.Add(new Fabrication(owner, "never-held-the-seat",
                        $"…{Shorten(sentence)}… — {owner} never held the seat, so there was no " +
                        "tenure to end"));
                    break;
                }
            }
        }

        return findings;
    }

    /// <summary>Possessive constructions that assert their owner held the seat.</summary>
    private static readonly string[] TenureNouns = ["’s tenure", "'s tenure", "’s reign", "'s reign"];

    /// <summary>
    /// A count that belongs to a wider scope than the sentence claims it for.
    ///
    /// The recurring shape of this is a figure that is true of the power's whole life appearing
    /// inside a window that saw fewer — nine people courted away from the Hadale Commune over
    /// twenty-four years, narrated as four. The digest is already scoped, so the comparison is
    /// direct; what was missing was anyone making it.
    /// </summary>
    private static List<Fabrication> CheckScopeTotals(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        PackDigest digest = pack.Digest;

        (string Noun, string[] Verbs, int Actual)[] countables =
        [
            ("people", ["courted away", "won away"], digest.Defections),
            ("", ["courted away from", "won away from"], digest.Defections),
        ];

        string lower = passage.ToLowerInvariant();
        string[] words = passage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        foreach ((string _, string[] verbs, int actual) in countables)
        {
            if (actual == 0) continue;

            foreach (string verb in verbs)
            {
                int at = lower.IndexOf(verb, StringComparison.Ordinal);
                if (at < 0) continue;

                // The count is the last number before the verb, within the same sentence.
                int stated = -1;
                int sentence = lower.LastIndexOfAny(['.', '\n'], Math.Max(0, at - 1));

                for (int i = 0; i < words.Length; i++)
                {
                    int offset = lower.IndexOf(words[i].ToLowerInvariant(), StringComparison.Ordinal);
                    if (offset < 0 || offset > at || offset < sentence) continue;

                    int? value = SpelledNumber(words, i);
                    if (value is not null) stated = value.Value;
                    else if (IsNumber(Strip(words[i]))) stated = int.Parse(Strip(words[i]), CultureInfo.InvariantCulture);
                }

                if (stated < 2 || stated == actual) continue;

                findings.Add(new Fabrication(stated.ToString(CultureInfo.InvariantCulture),
                    "wrong-scope-total",
                    $"says {stated} were {verb.Replace(" from", "", StringComparison.Ordinal)}, " +
                    $"but this scope records {actual}"));
                break;
            }
        }

        return findings;
    }

    /// <summary>
    /// Events dated outside the reign the sentence attributes them to.
    ///
    /// "Under Kreathbeas, the Sworn Men sent eight raids: two … in 20 and 23" — eight is the
    /// power's lifetime total and Kreathbeas took the seat in 25, so two of the eight happened
    /// before he held it. The figure is right and the attribution is not, which is why every
    /// count-based check passed it twice.
    /// </summary>
    private static List<Fabrication> CheckReignAttribution(ContextPack pack, string passage, Coverage cover)
    {
        List<Fabrication> findings = [];
        if (pack.Digest.Tenures.Count == 0) return findings;

        foreach (string sentence in passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            foreach (string marker in ReignMarkers)
            {
                int at = lower.IndexOf(marker, StringComparison.Ordinal);
                if (at < 0) continue;

                // Either part of the name. The prose refers to a ruler by whichever half reads
                // better in the sentence — "Under Kreathbeas" for a man the digest calls Waeth —
                // and matching only the surname made this rule silent on the case it was for.
                Tenure? tenure = NamedIn(sentence[(at + marker.Length)..], pack.Digest.Tenures);
                if (tenure is null) continue;

                cover.Extracted(RuleNames.Quantity);

                cover.Checked(RuleNames.Quantity);

                foreach (int year in YearsIn(sentence, pack))
                {
                    if (year >= tenure.From && year <= tenure.To) continue;

                    findings.Add(new Fabrication(year.ToString(CultureInfo.InvariantCulture),
                        "outside-the-reign",
                        $"…{Shorten(sentence)}… — {year} falls outside {tenure.Holder}'s tenure " +
                        $"of {tenure.From}–{tenure.To}"));
                }

                break;
            }
        }

        return findings;
    }

    private static readonly string[] ReignMarkers = ["under ", "during the rule of ", "in the reign of "];

    /// <summary>The tenure whose holder the text names, by either part of the name.</summary>
    private static Tenure? NamedIn(string text, IReadOnlyList<Tenure> tenures)
    {
        foreach (string raw in text.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Strip(raw);
            if (word.Length < 3) continue;

            foreach (Tenure t in tenures)
                foreach (string part in t.Holder.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    if (string.Equals(part, word, StringComparison.OrdinalIgnoreCase)) return t;
        }

        return null;
    }

    /// <summary>Every year the sentence states that falls inside the pack's window.</summary>
    private static List<int> YearsIn(string sentence, ContextPack pack)
    {
        List<int> years = [];

        foreach (string raw in sentence.Split(
                     [' ', ',', '.', ';', ':', '(', ')', '\n', '–', '—'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(Strip(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) continue;
            if (value < pack.FromYear || value > pack.ToYear) continue;      // a count, not a year
            years.Add(value);
        }

        return years;
    }

    /// <summary>
    /// A particular attached to a figure the record states plainly.
    ///
    /// "killing 149 men" — the record counts 149 dead and says nothing about who they were. The
    /// number is right, which is what makes this survive every check that compares numbers, and
    /// the detail is invented, which is what makes it a fabrication. It has now reached canon
    /// twice.
    /// </summary>
    private static List<Fabrication> CheckInventedParticulars(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        string source = pack.Body.ToLowerInvariant();
        string[] words = passage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length - 1; i++)
        {
            string token = Strip(words[i]);
            if (!IsNumber(token)) continue;

            string next = Strip(words[i + 1]).ToLowerInvariant();
            if (!Particulars.Contains(next)) continue;

            // Only a fabrication where the record gives that figure without the particular.
            if (source.Contains($"{token} {next}", StringComparison.Ordinal)) continue;

            findings.Add(new Fabrication($"{token} {next}", "invented-particular",
                $"…{Context(words, i)}… — the record counts {token}, and does not say {next}"));
        }

        return findings;
    }

    /// <summary>
    /// Nouns that say something about the dead beyond how many there were.
    ///
    /// Deliberately only those the engine never records. It counts people; it has no sex, no
    /// rank and no trade for them, so any of these words after a figure is the model filling in.
    /// </summary>
    private static readonly HashSet<string> Particulars = new(StringComparer.OrdinalIgnoreCase)
    {
        "men", "women", "warriors", "soldiers", "fighters", "knights", "peasants",
        "villagers", "townsfolk", "children", "families", "households",
    };

    /// <summary>
    /// A span of years measured from the wrong event.
    ///
    /// "peace in year 51, two years after the collapse" — the collapse was year 50 and the two
    /// years is the length of the war. The arithmetic is real and attached to the wrong anchor,
    /// and the same shape put Tor Nathgoull in the seat "when his house ended" two years before
    /// it did.
    /// </summary>
    private static List<Fabrication> CheckRelativeAnchors(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];

        int? collapse = YearOfEvent(pack, "is finished");

        string[] words = passage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length - 3; i++)
        {
            int? span = SpelledNumber(words, i)
                        ?? (IsNumber(Strip(words[i])) ? int.Parse(Strip(words[i]), CultureInfo.InvariantCulture) : null);
            if (span is null or < 1 or > 200) continue;

            if (!string.Equals(Strip(words[i + 1]), "years", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(Strip(words[i + 2]), "after", StringComparison.OrdinalIgnoreCase)) continue;

            string tail = string.Join(' ', words[(i + 3)..Math.Min(words.Length, i + 9)]).ToLowerInvariant();
            if (!tail.Contains("collapse", StringComparison.Ordinal)
                && !tail.Contains("house ended", StringComparison.Ordinal)) continue;

            if (collapse is null) continue;

            // The year the sentence itself states is the far end of the span.
            List<int> stated = YearsIn(string.Join(' ', words), pack);
            if (stated.Count == 0) continue;

            int latest = stated.Max();
            if (latest - collapse.Value == span.Value) continue;

            findings.Add(new Fabrication($"{span} years after", "relative-time",
                $"…{Context(words, i)}… — the collapse was {collapse}, which is " +
                $"{latest - collapse.Value} year(s) before {latest}, not {span}"));
        }

        findings.AddRange(CheckSimultaneityAnchors(pack, passage, collapse));
        return findings;
    }

    /// <summary>
    /// A year given as the year some other event happened, where it was not.
    ///
    /// "Tor Nathgoull, who took the seat in 48 when his house ended" — he took the seat in 48
    /// and the house ended in 50. Both facts are in the record and welding them with "when"
    /// asserts a third thing that is not. The same construction is how "since year 1" got in.
    /// </summary>
    private static List<Fabrication> CheckSimultaneityAnchors(
        ContextPack pack, string passage, int? collapse)
    {
        List<Fabrication> findings = [];
        if (collapse is null) return findings;

        foreach (string sentence in passage.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string lower = sentence.ToLowerInvariant();

            int when = lower.IndexOf("when ", StringComparison.Ordinal);
            if (when < 0) continue;

            string tail = lower[(when + 5)..];
            bool aboutTheCollapse = false;
            foreach (string phrase in CollapsePhrases)
                if (tail.StartsWith(phrase, StringComparison.Ordinal)
                    || tail.Contains(phrase, StringComparison.Ordinal)) aboutTheCollapse = true;

            if (!aboutTheCollapse) continue;

            // The year the clause before "when" states is the one being equated to the collapse.
            List<int> stated = YearsIn(lower[..when], pack);
            if (stated.Count == 0 || stated.Contains(collapse.Value)) continue;

            findings.Add(new Fabrication(stated[^1].ToString(CultureInfo.InvariantCulture), "relative-time",
                $"…{Shorten(sentence)}… — the house ended in {collapse}, not {stated[^1]}"));
        }

        return findings;
    }

    private static readonly string[] CollapsePhrases =
        ["his house ended", "its house ended", "the house ended", "the power ended",
         "it collapsed", "the collapse", "was finished"];


    /// <summary>
    /// A consequence narrated before the cause the log gives it.
    ///
    /// The Wuldweald reign told the election first and the murder that forced it last, which
    /// reads as two unconnected facts and hides the only causal link in the section. Ordering is
    /// not a matter of taste where the log records the edge.
    /// </summary>
    private static List<Fabrication> CheckNarrativeOrder(ContextPack pack, string passage)
    {
        List<Fabrication> findings = [];
        if (pack.Digest.Tenures.Count == 0) return findings;

        string lower = passage.ToLowerInvariant();

        foreach (Tenure tenure in pack.Digest.Tenures)
        {
            string holder = ContextPackBuilder.Surname(tenure.Holder).ToLowerInvariant();

            int tookSeat = Earliest(lower,
                [$"{holder} took the seat", $"{holder} came to power", $"{holder} took power"]);
            if (tookSeat < 0) continue;

            // The killing read off the pack rather than the digest. A reign's statistics count
            // what the ruler did; the death that made him ruler belongs to his predecessor and
            // is in the events without being in the numbers.
            foreach (string victim in KilledIn(pack, tenure.From))
            {
                string surname = ContextPackBuilder.Surname(victim).ToLowerInvariant();
                if (string.Equals(surname, holder, StringComparison.OrdinalIgnoreCase)) continue;

                int murder = KillingOf(lower, surname);
                if (murder < 0 || murder < tookSeat) continue;

                findings.Add(new Fabrication(victim, "out-of-order",
                    $"the killing of {victim} in {tenure.From} is what opened the seat, and is " +
                    $"told after {tenure.Holder} takes it"));
            }
        }

        return findings;
    }

    /// <summary>
    /// The year of the one event in the pack whose description contains a phrase.
    ///
    /// Read off the body rather than the digest because the anchors that go wrong are usually
    /// things that happened to somebody else — the power a section's subject destroyed is in its
    /// events and not in its own statistics. Null where the phrase is absent or ambiguous; an
    /// anchor that appears twice cannot be checked against, and guessing which one was meant is
    /// how a check starts inventing findings.
    /// </summary>
    private static int? YearOfEvent(ContextPack pack, string phrase)
    {
        int? found = null;

        foreach (string line in pack.Body.ReplaceLineEndings("\n").Split('\n'))
        {
            if (!line.Contains(phrase, StringComparison.OrdinalIgnoreCase)) continue;

            int open = line.IndexOf("[year ", StringComparison.Ordinal);
            if (open < 0) continue;

            int close = line.IndexOf(']', open);
            if (close < 0) continue;

            if (!int.TryParse(line[(open + 6)..close], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int year)) continue;

            if (found is not null && found != year) return null;
            found = year;
        }

        return found;
    }

    /// <summary>
    /// Where a passage says this person was killed, or -1.
    ///
    /// The verb and the surname are not adjacent — prose writes "murdered Paernmel Has", with
    /// the given name in between — so this looks for the surname preceded by a killing verb
    /// within a short reach rather than for a fixed phrase.
    /// </summary>
    private static int KillingOf(string lower, string victim)
    {
        for (int at = lower.IndexOf(victim, StringComparison.Ordinal); at >= 0;
             at = lower.IndexOf(victim, at + 1, StringComparison.Ordinal))
        {
            string before = lower[Math.Max(0, at - 40)..at];

            foreach (string verb in new[] { "murder", "killed", "killing", "had", "put to death" })
                if (before.Contains(verb, StringComparison.Ordinal)) return at;
        }

        return -1;
    }

    /// <summary>The surnames of everyone the pack records as killed in a given year.</summary>
    private static List<string> KilledIn(ContextPack pack, int year)
    {
        List<string> victims = [];
        string marker = $"[year {year.ToString(CultureInfo.InvariantCulture)}]";

        foreach (string line in pack.Body.ReplaceLineEndings("\n").Split('\n'))
        {
            if (!line.Contains(marker, StringComparison.Ordinal)) continue;
            if (!line.Contains("is killed by", StringComparison.Ordinal)
                && !line.Contains("murdered", StringComparison.Ordinal)) continue;

            // "  e:998 [year 51] Wuldweald Valdrith (a:70) has Paernmel Has (a:50) murdered …"
            // The victim is the name immediately before the verb.
            int verb = line.IndexOf("is killed by", StringComparison.Ordinal);
            string head = verb >= 0 ? line[..verb] : line[..line.IndexOf("murdered", StringComparison.Ordinal)];

            // Stop at the first comma. A death line carries the victim's office after it —
            // "Paernmel Has (a:50), ruler of the Kebarrow Compact (f:2), is killed by …" — and
            // reading to the verb made the last capitalised word "Compact".
            int comma = head.IndexOf(',', StringComparison.Ordinal);
            if (comma > 0) head = head[..comma];

            // The last whole run of capitalised words before the verb.
            //
            // The last, because the two forms put the victim in different places: "Paernmel Has
            // … is killed by …" leads with him, and "Wuldweald Valdrith … has Paernmel Has …
            // murdered" leads with the killer. In both, the victim is the name nearest the verb.
            // The whole run rather than its final word, because reporting the surname alone
            // produced "the killing of has in 51".
            List<string> run = [], last = [];

            foreach (string raw in head.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
            {
                string word = Strip(raw);

                if (word.Length > 2 && char.IsUpper(word[0])) { run.Add(word); continue; }
                if (run.Count > 0) { last = run; run = []; }
            }

            if (run.Count > 0) last = run;

            if (last.Count > 0 && !victims.Contains(string.Join(' ', last)))
                victims.Add(string.Join(' ', last));
        }

        return victims;
    }

    private static int Earliest(string text, string[] phrases)
    {
        int best = -1;
        foreach (string phrase in phrases)
        {
            int at = text.IndexOf(phrase, StringComparison.Ordinal);
            if (at >= 0 && (best < 0 || at < best)) best = at;
        }
        return best;
    }
}
