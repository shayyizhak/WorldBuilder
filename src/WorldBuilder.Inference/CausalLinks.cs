using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>
/// Whether a causal answer joins two records the world actually joins.
///
/// The engine stores causality as an edge, which is the whole reason a "why" question can be
/// answered at all — and nothing checked that an answer's links were those edges. Asked why
/// Threi Cut rose, an answer put the Covenant's fallen standing between the killing and the
/// revolt, as a thing the killing produced and the revolt followed from. The record has one
/// edge, revolt to death, and standing is a description carried on the revolt rather than a
/// separate thing anything caused. Every name and year in that sentence was right.
///
/// This is the same class as the fabricated succession links the chronicle produced for five
/// rounds: real people, real events, an invented relation between them. It lives here rather
/// than in <see cref="FabricationCheck"/> because it reads the citations, and the fabrication
/// check is deliberately given prose with the citations stripped out.
/// </summary>
public static class CausalLinks
{
    /// <summary>Ways an answer says one thing brought about another.</summary>
    private static readonly string[] Connectives =
    [
        "because", "caused by", "led to", "led directly to", "resulted in", "resulting in",
        "brought about", "as a result of", "owing to", "which produced", "followed the",
        "following the", "prompted by", "triggered by",
    ];

    /// <summary>
    /// Checks every sentence that claims a link between two cited records.
    ///
    /// Only within one sentence, and only where two records are actually cited. An answer that
    /// asserts a link across a sentence boundary with a demonstrative — "This collapse followed
    /// the killing" — is not judged here, because resolving what "this" refers to is guesswork
    /// and a rule that guesses is a rule that accuses the innocent. That case is left to the
    /// prompt and to the terminology rule, and the limit is recorded rather than hidden.
    /// </summary>
    public static List<Fabrication> Check(
        WorldView view, ContextPack pack, string answer, Coverage cover)
    {
        List<Fabrication> findings = [];
        HashSet<EventId> inPack = [.. pack.Events];

        foreach (string sentence in answer.Split(['.', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Claims(sentence)) continue;

            List<EventId> cited = Cited(sentence, inPack);
            if (cited.Count < 2) continue;

            cover.Extracted(RuleNames.Action);
            cover.Checked(RuleNames.Action);

            if (AnyLinked(view, cited)) continue;

            findings.Add(new Fabrication(
                string.Join(", ", cited), "unsupported-link",
                $"…{Shorten(sentence)}… — nothing joins those two; the record carries no such cause"));
        }

        return findings;
    }

    private static bool Claims(string sentence)
    {
        string lower = sentence.ToLowerInvariant();
        foreach (string word in Connectives)
            if (lower.Contains(word, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>The records a sentence cites, in the order it cites them.</summary>
    private static List<EventId> Cited(string sentence, HashSet<EventId> inPack)
    {
        List<EventId> found = [];
        int at = 0;

        while ((at = sentence.IndexOf("[e:", at, StringComparison.Ordinal)) >= 0)
        {
            int close = sentence.IndexOf(']', at);
            if (close < 0) break;

            foreach (string part in sentence[(at + 1)..close]
                         .Split([',', ';'], StringSplitOptions.TrimEntries))
            {
                if (!EventId.TryParse(part, out EventId id)) continue;
                if (inPack.Contains(id) && !found.Contains(id)) found.Add(id);
            }

            at = close + 1;
        }

        return found;
    }

    /// <summary>
    /// Whether any two of the cited records are joined, in either direction and through however
    /// many steps the pack itself carries.
    ///
    /// Through steps, because an answer is allowed to say a thing led to another across a chain
    /// it was shown — that is exactly what a causal answer is for. What it may not do is join
    /// two records with no path between them at all.
    /// </summary>
    private static bool AnyLinked(WorldView view, List<EventId> cited)
    {
        for (int i = 0; i < cited.Count; i++)
            for (int j = i + 1; j < cited.Count; j++)
                if (Reaches(view, cited[i], cited[j]) || Reaches(view, cited[j], cited[i]))
                    return true;

        return false;
    }

    /// <summary>Whether <paramref name="from"/> descends causally from <paramref name="to"/>.</summary>
    private static bool Reaches(WorldView view, EventId from, EventId to)
    {
        HashSet<EventId> seen = [];
        Queue<EventId> queue = new();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            EventId here = queue.Dequeue();
            if (here == to) return true;
            if (!seen.Add(here) || seen.Count > 64) continue;

            foreach (EventId cause in view.Log.Get(here).Causes) queue.Enqueue(cause);
        }

        return false;
    }

    /// <summary>
    /// Words that name one event kind and were used for another.
    ///
    /// "Collapse" and "destroyed" mean a power is finished and gone — it is what
    /// <c>POLITY.COLLAPSE</c> records, and what the answer to "which powers were destroyed"
    /// means by it. An answer used "this collapse" of a legitimacy decline, of a house still
    /// holding three places, in a suite where the neighbouring answer uses the same word for
    /// destruction.
    ///
    /// <b>Answers only, and deliberately.</b> The same test over the chronicle reports "a decade
    /// of internal collapse" and "a state of violent contraction" — figures of speech in prose
    /// that is already canon — and costs a true section for each. A section has surrounding
    /// paragraphs that disambiguate a metaphor. An answer is two sentences, read alone, by
    /// someone who asked a direct question, and has nowhere to carry the qualification.
    /// </summary>
    private static readonly string[] Destruction = ["collapse", "collapsed", "was destroyed"];

    /// <summary>
    /// Checks that an answer saying a power was destroyed is about a power that was.
    ///
    /// Judged over the whole answer rather than sentence by sentence: the failing case wrote
    /// "this collapse", whose subject is a pronoun, and no rule that needs a named power before
    /// the verb can see it. What makes it wrong is not which power it names — it names none —
    /// but that nothing in the material ended at all.
    /// </summary>
    public static List<Fabrication> Terminology(ContextPack pack, string answer, Coverage cover)
    {
        string lower = answer.ToLowerInvariant();

        string? used = null;
        foreach (string word in Destruction)
            if (lower.Contains(word, StringComparison.Ordinal)) { used = word; break; }

        if (used is null) return [];

        cover.Extracted(RuleNames.Action);
        cover.Checked(RuleNames.Action);

        foreach (string power in pack.PowerWords)
            if (pack.Claims.Knows(ClaimIndex.Collapsed, power)) return [];

        return
        [
            new Fabrication(used, "wrong-collapse",
                $"\"{used}\" says a power was finished and gone; nothing here records one " +
                "ending, and a house whose standing has fallen is in decline"),
        ];
    }

    private static string Shorten(string sentence)
    {
        string text = sentence.Trim();
        return text.Length <= 110 ? text : text[..110] + "…";
    }
}
