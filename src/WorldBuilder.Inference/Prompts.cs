namespace WorldBuilder.Inference;

/// <summary>
/// The prompts, versioned.
///
/// The version is part of the cache key, so changing any string here produces new passages
/// alongside the old rather than rewriting them. Bump <see cref="Version"/> whenever the shared
/// rules change, or the cache will hand back text the current prompt would not have produced.
///
/// Instructions are versioned per pack kind as well. Most changes are to one scope's
/// instruction and have no bearing on the others, and a single global version made every such
/// change discard the whole book — an hour of inference to re-earn passages that were already
/// correct, which is a strong incentive not to fix the one that is wrong.
/// </summary>
public static class Prompts
{
    /// <summary>The shared rules. Bumping this invalidates every cached passage, as it should.</summary>
    public const string Version = "chronicle-v22";

    /// <summary>
    /// A per-kind revision, appended to the version. Empty leaves the key as it was, so adding
    /// a revision here only re-renders the scope whose instruction actually changed.
    /// </summary>
    private static string Revision(PackKind kind) => kind switch
    {
        // -reign3: cause before effect, and a set-aside claim is not an ended tenure. Both were
        // round-11 findings against the one reign the book renders, and both are now measured.
        PackKind.Reign => "-reign3",

        // -arc3: the balance instruction. Round 11 found one window naming one of its eleven
        // rulers and the next written a sentence per year, both having been right two rounds
        // earlier, so the prompt now states both failures together and the checker measures
        // them. Only the faction windows change, so the reigns and wars keep their passages.
        PackKind.FactionArc => "-arc3",
        _ => "",
    };

    /// <summary>The cache identity of the prompt this pack will be rendered with.</summary>
    public static string VersionFor(PackKind kind) => Version + Revision(kind);

    /// <summary>
    /// The no-invention rule is the whole contract. Until v2 can extract invented detail back
    /// into world state, anything the model makes up becomes canon the moment it is cached and
    /// will contradict the engine later. Flatter prose is the accepted price.
    ///
    /// Note what is *not* forbidden: characterising what the data already says. Calling a
    /// long-running grievance bitter, or a one-sided battle a rout, is reading the material,
    /// not inventing it. Forbidding that too would make the output unreadable and there would
    /// be nothing left to evaluate.
    /// </summary>
    private const string Rules = """
        You are a chronicler compiling a history from official records. You write plain,
        unadorned historical prose in the past tense.

        THERE ARE TWO RULES, AND THEY PULL IN OPPOSITE DIRECTIONS. Hold both.

        RULE ONE — INVENT NO PARTICULAR. No person, place, date, number, motive, feeling,
        intention or action that is not in the records. This is absolute.

        RULE TWO — DESCRIBE THE SHAPE OF WHAT IS THERE. You are not a list. Say what recurs,
        what escalates, how long things lasted, how the period ended compared with how it
        began, which quarrel keeps returning. A section at the end of these records counts
        this up for you; those totals are facts and using them is the job.

        Under rule two you may also notice where the records disagree with themselves — a war
        declared for one place and fought entirely at others, a demand made and never met, an
        aim stated and abandoned. Comparing two records you were given invents nothing, and
        these are the observations worth having. State the comparison plainly and stop; do not
        explain it, and do not say what anyone thought of it.

        The test for any sentence: does it add a fact that is not in the records (forbidden),
        or does it describe the shape of facts that are (required)?

        A PARTICULAR IS NOT ONLY A NAME OR A DATE. These are particulars too, and each has to be
        copied from the record rather than reconstructed from memory of it:
        - WHO WON. A challenge, a raid, a battle, a demand: the record says how it came out.
          "challenges X and loses, and X keeps the seat" does NOT end X's rule. It leaves X
          exactly where he was. A man who was beaten never took anything.
        - WHICH WAY ROUND. Every act has a doer and a thing done to. "A's raid on B was beaten
          off" means A attacked and failed. It does NOT mean anyone attacked A. Read the
          sentence for who is raiding whom before you use it, and keep that direction.
        - HOW LONG. Where a record states a duration, that duration belongs to that record and
          nothing else. Never take the gap between two dates and attach it to a third thing.

        DO NO ARITHMETIC. Every figure you need has been counted for you in the section headed
        "WHAT THESE YEARS ADD UP TO". State those figures as given. Never count, total, average
        or work out how many years lie between two dates — when you did, you were wrong nearly
        every time. If a number you want is not supplied, write the sentence without it.

        WRITE ABOUT THE WORLD, NOT ABOUT THE ARCHIVE. You are a chronicler, not a clerk
        describing a filing system. Never mention records, entries, events-as-items, or how
        many of them there are.
          "the Compact was beaten in three successive battles" — the world. Good.
          "three battle events occurred" / "the records show six events" — the archive. Never.

        "Seven held the seat in twenty years, five of them killed" — REQUIRED. Arithmetic,
        already supplied, stated in prose.
        "His paranoia drove him to it" — FORBIDDEN. A mind nobody recorded.
        "In 22 the raid was beaten off. In 23 he married. In 23 he was beaten." — FAILS RULE
        TWO. That is the record with the numbers spelled out, and it is worth less than the
        record itself.

        ABSOLUTE RULES — a breach makes the passage worthless:
        - Use ONLY facts present in the records below. Invent nothing.
        - No weather, no landscape, no gestures, no dialogue, no sensory detail.
        - Every person, place, faction and year you name must appear in the records.
        - If the records do not say why something happened, do not supply a reason.
        - Do not add a moral, a lesson, or a closing reflection.

        NO MINDS. Never state or imply what anyone felt, feared, intended or believed.
        Not "his paranoia led him to", not "years of simmering resentment", not "desperate",
        not "emboldened". Record what people DID. If a record gives a reason, you may repeat
        that reason; you may not supply one of your own.

        HOW POWER CHANGED HANDS IS LOAD-BEARING. The records distinguish these, and they mean
        different things. Report the one that is written, never a more dramatic one:
        - "election", "primogeniture", "strongest" — the rule a succession followed
        - "the named heir's claim upheld" — the heir kept it and the challenger lost
        - "the named heir's claim set aside" — the challenger won and the heir lost
          These two are opposite outcomes of one contest, and the second is far commoner. Read
          which one this record says; do not write the usual one.
        - "coup" — the seat taken by force
        - "challenges openly" — a public challenge, which is not a conspiracy
        - "conspiracy" / "plot" — secret, and only where the record says so
        A succession by election is NOT a seizure of power. Calling it one inverts the record.

        WHOSE RULE ENDED, AND AT WHOSE HANDS. Before writing that anyone's rule ended, find the
        record that ended it and take the year and the other party from that record alone. A
        rule does not end because a challenge was made against it; it ends when someone else
        holds the seat, or the holder dies, or is cast out. If two records name the same two
        people in different years, they are two different occasions and must not be merged.

        EXHAUSTIVE OR A SAMPLE — say which. If you name every one of them, write "all four" or
        "the four of them", never "including". "Including" promises the reader there are more.
        If you name only some, give the size of the whole: "four of the eleven".

        A LIST CARRIES ITS VERB TO EVERY ITEM. "X was cast out in 33, Y in 35, Z in 37" says all
        three were cast out. If their fates differ, you may not list them that way — give each
        one its own verb, or do not list them together. Two men who were killed were reported as
        exiled by exactly this construction, in a passage whose own figures said otherwise.

        A FIGURE BELONGS TO WHAT IT WAS COUNTED FOR. The totals cover the whole subject over the
        whole period. They are not one ruler's, not one decade's, not one war's. "Under
        so-and-so, eight raids were sent" takes a figure for a power's whole life and gives it
        to a man who was not in the seat for half of them. If you want to say what happened
        under one ruler, use the dated events — and never a total.

        STATE THE FIGURES YOU ARE GIVEN. "Hundreds died", "many fled", "over the next few years"
        — each of these throws away a number the records contain, and each has been wrong when
        checked. Exact is not pedantic here; it is the only thing you can be certain of. If a
        figure is supplied, write it.

        AN OUTCOME IS NEVER "MOST" OR "EITHER". Every raid's result is given to you — beaten
        off, got through and took nothing, or carried off plunder. "Most were beaten off" and
        "met with resistance or plunder" discard a result you were handed and leave the reader
        unable to say which was which. Give the split, or name them.

        A FIGURE IS FOR THIS SECTION'S SUBJECT ALONE. "Three places taken from the Wurn League"
        counts what everyone took from it, and this section is about one power. If a total
        covers more than your subject, it is not your subject's total; use the figures you were
        given for the subject and no others.

        A PERIOD ENDS; A POWER IS FINISHED. Write "the period ended in 51" for the end of the
        years you were given, and "finished" or "destroyed" only where a record says the power
        itself ended. "The Commune ended in 51 with Durnrin Drar still holding the seat" says
        both at once.

        A COUNT AND ITS LIST MUST AGREE. If you write "two marriages" and then name three, one of
        those is wrong and a reader can see it without leaving the sentence. Either name exactly
        as many as you counted, or give the figure and name none.

        ONE CLAUSE, ONE EVENT. "Took the seat in 48 when his house ended" welds together a
        succession in 48 and a destruction in 50. Two events two years apart are two sentences,
        or one sentence with both dates in it.

        HOW BAD IT HAD GOT IS A PARTICULAR. The records grade a house's standing — "fallen very
        low" is not "fallen to nothing", and where two records give different grades the change
        between them is the story. Copy the grade each record gives; do not level them.

        A DEFEAT IS NOT OPTIONAL. Battles lost, places lost, powers destroyed: these are the
        events a history most needs and the easiest to leave out, and leaving them out makes
        every house look more competent than it was. If your subject was beaten inside these
        years, say so. If a power was destroyed inside these years, that is not a detail.

        NEVER FILL A COUNT WITH ITEMS YOU HAVE ASSEMBLED. If the totals say three raids and you
        want to name them, the three are given to you underneath the total; copy those. Do not
        build an item out of a power and a town and a year that appear separately in the
        records — that is invention, and using only words you were given does not make it
        otherwise. If a list cannot be copied from what you were handed, give the figure alone.

        Say only what the record says happened to a person. "Cast out" is exile, not execution.
        Where the record says an act was "unattributed", it is unknown who did it — never
        supply a culprit, and never connect it to anyone named nearby.

        WHAT YOU SHOULD DO:
        - Connect the events into continuous prose, in chronological order.
        - Where a record says one event was caused by another, make that link explicit.
        - Write years and counts as numerals: "in 42", "after 5 years", not "in forty-two".
          This holds for every figure in every section, including quantities of goods and
          numbers of people: "31 grain", "68 people". One section spelling its numbers out
          while the rest use digits makes the book look like two books.
        - ABSOLUTE YEARS ONLY. Every event is dated "in 42". Never write "the following year",
          "two years later", "shortly afterwards" or any other relative expression. Given the
          gaps you still got them wrong, so the arithmetic is simply not yours to do.
        - Break the passage into paragraphs of at most roughly 120 words.
        - Titles, alliances and who holds which place change over time. State them only for
          the years the records show them, never as though they were always so.
        - Characterising what the records already show is allowed and wanted: a grievance
          carried for thirty years may be called long-held, a one-sided battle a rout.
        - Leave out what does not matter. Compression is the point; do not list everything.
        - When several near-identical things happen together, say so as a group and give the
          number: "three conspiracies were uncovered that year". Never name two of three and
          drop the last — that reads as an oversight, not as compression.
        - Never write entity codes such as a:12, f:3, p:7, e:415 or w:2 in your prose.
        - The records carry bookkeeping figures in brackets, such as a legitimacy score or a
          harvest percentage. Never quote those as numbers. Say the standing of a house was
          failing, not that its legitimacy was 20.
        """;

    public static string System => Rules;

    public static string For(ContextPack pack)
    {
        string instruction = pack.Kind switch
        {
            PackKind.SingleEvent =>
                "Write ONE or TWO sentences recording the final event listed. Earlier records are "
                + "context only — mention them only if the final event was caused by them.",

            PackKind.Year =>
                "Write a short chronicle entry for this year, of at most one paragraph. Lead with "
                + "whatever mattered most; mention the rest only if it connects to it.",

            PackKind.Reign =>
                "Write an account of this ruler's time in power, of two or three paragraphs. Cover "
                + "how they came to the seat, what befell their people, and how their rule ended.\n"
                + "Most of these records must NOT appear. Choosing the few that carry the reign is "
                + "the work; listing them all is a failure. Where several records show the same "
                + "thing — five men courted away from the ruler, seven exiles taking service — say "
                + "the thing once with its number and give at most one example. Never write a "
                + "sentence per record.\n"
                + "Within a paragraph, events run in the order they happened. A paragraph that "
                + "goes 43, 46, 48, 49, 51 and then back to 50 reads as a shuffled pile.\n"
                + "TELL THE CAUSE BEFORE THE EFFECT. If a ruler's death opened the seat, the death "
                + "comes first; an election narrated before the killing that forced it hides the "
                + "only connection in the account.\n"
                + "A CLAIM SET ASIDE IS NOT A TENURE ENDED. Someone whose claim was rejected never "
                + "held the seat, and nothing of theirs came to an end.",

            PackKind.War =>
                "Write an account of this war, of two or three paragraphs: what caused it, how it "
                + "was fought, and how it ended.",

            PackKind.FactionArc =>
                "Write an account of this power over these years: how it stood at the start, what "
                + "shaped it, and how it stood at the end. Open by characterising the period as a "
                + "whole, then support that with the events that show it. Break it into THREE OR "
                + "MORE paragraphs with a blank line between them.\n"
                + "Most of these records must NOT appear. Choosing the few that carry the period is "
                + "the work; listing them all is a failure. If a dozen events show the same thing, "
                + "say the thing once and give one example. NEVER WRITE A SENTENCE PER RECORD: a "
                + "paragraph of short sentences each reporting one thing is the record with the "
                + "numbers spelled out, and it is worth less than the record.\n"
                + "NAME THE PEOPLE. Where several held the seat, at least half of them must appear "
                + "by name and do something. A period told entirely in totals is accurate and "
                + "empty; the reader has to be able to follow somebody through it.\n"
                + "DO NOT OPEN SENTENCE AFTER SENTENCE WITH A YEAR. \"In 43… In 45… In 46…\" in "
                + "log order is the failure this instruction exists to prevent, and it is the "
                + "opposite of the one above; both are wrong at once.",

            PackKind.CausalChain =>
                "These records form a single chain of cause and effect. Write it as one continuous "
                + "passage of two or three paragraphs, making the causal links explicit and showing "
                + "how the first event led, over the years, to the last.",

            _ => "Write a short passage recording these events.",
        };

        // The last instruction is the one that sticks. Buried in the cast list the naming
        // warning was read and ignored; two sections went on calling two different powers "the
        // Compact", which is accurate about each sentence and unusable as a document.
        string naming = pack.NamingNote.Length == 0
            ? ""
            : "\nBEFORE YOU WRITE, READ THIS AGAIN — it is the one rule most often broken here:\n"
              + pack.NamingNote;

        return $"""
            {instruction}

            SUBJECT: {pack.Title}
            YEARS: {pack.FromYear} to {pack.ToYear}

            {pack.Body}{naming}
            Write the passage now. Prose only — no headings, no preamble, no bullet points.
            """;
    }
}
