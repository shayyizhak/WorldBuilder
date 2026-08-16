using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WorldBuilder.Chronicle.Tests;

/// <summary>One section of a finished chronicle, with the window its heading declares.</summary>
public sealed record Section(string Heading, string Body, int FromYear, int ToYear)
{
    public bool HasWindow => FromYear > 0 && ToYear > 0;

    /// <summary>A reign section names a person and a seat; a faction section names only the seat.</summary>
    public bool IsReign => Heading.StartsWith("The rule of ", StringComparison.Ordinal);
}

/// <summary>
/// A chronicle, read independently of anything that produced it.
///
/// Its own splitter and its own tokeniser, duplicating what the render side already has. That is
/// the point of the layer: an independent verifier that shares an implementation with the thing
/// it verifies will agree with it about the bug as readily as about the world.
/// </summary>
public static partial class ChronicleReader
{
    /// <summary>
    /// Every section heading, including the ones whose passage was held out of canon.
    ///
    /// Distinct from <see cref="Sections"/>, which returns only sections carrying prose. A held-out
    /// scope appears in the document as a heading and a note saying no verified account exists,
    /// and counting only the ones with prose would report a fifteen-scope chronicle as twelve —
    /// then agree with itself if exclusions ever started happening silently.
    /// </summary>
    public static List<string> Headings(string markdown)
    {
        List<string> headings = [];

        foreach (string line in markdown.ReplaceLineEndings("\n").Split('\n'))
            if (line.StartsWith("### ", StringComparison.Ordinal)) headings.Add(line[4..].Trim());

        return headings;
    }

    public static List<Section> Sections(string markdown)
    {
        List<Section> sections = [];
        string heading = "";
        StringBuilder body = new();

        foreach (string line in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                Flush();
                heading = line[4..].Trim();
                continue;
            }

            if (line.StartsWith('#')) continue;
            if (line.StartsWith('_') || line.StartsWith('>')) continue;
            body.AppendLine(line);
        }

        Flush();
        return sections;

        void Flush()
        {
            string text = body.ToString().Trim();
            body.Clear();
            if (heading.Length == 0 || text.Length == 0) return;

            (int from, int to) = Window(heading);
            sections.Add(new Section(heading, text, from, to));
        }
    }

    /// <summary>
    /// The years a heading declares, from its trailing range.
    ///
    /// Headings end "…, 2–21" or "…, 51–51". The dash is an en dash in the document and a hyphen
    /// in some hand-written fixtures, so both are accepted — a parser that took only one would
    /// silently return no window and every check keyed on it would pass by not running.
    /// </summary>
    public static (int From, int To) Window(string heading)
    {
        Match m = Range().Match(heading);
        if (!m.Success) return (0, 0);

        return (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Every bare number a passage states.</summary>
    public static List<int> Figures(string passage)
    {
        List<int> figures = [];

        foreach (Match m in Number().Matches(passage))
            figures.Add(int.Parse(m.Value, CultureInfo.InvariantCulture));

        return figures;
    }

    /// <summary>
    /// Years the prose dates something to: a number introduced by "in", "by", "since" or a range.
    ///
    /// Narrower than every number, because a body count is not a year and asserting that 124 lies
    /// inside a window would fail on true prose.
    /// </summary>
    public static List<int> YearsStated(string passage)
    {
        List<int> years = [];

        foreach (Match m in DatedYear().Matches(passage))
            years.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));

        return years;
    }

    /// <summary>Capitalised words that are not sentence openers, as the prose's proper nouns.</summary>
    public static List<string> ProperNouns(string passage)
    {
        List<string> names = [];
        string[] sentences = passage.Split(['.', '\n', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

        foreach (string sentence in sentences)
        {
            string[] words = sentence.Split(
                [' ', ',', ';', ':', '(', ')', '"', '“', '”', '—', '–'],
                StringSplitOptions.RemoveEmptyEntries);

            // The first word of a sentence wears a capital for grammar rather than for identity.
            for (int i = 1; i < words.Length; i++)
            {
                // The possessive is a suffix, not a set of characters to strip. Trimming 's' as a
                // character turned "Weallhous" into "Weallhou" and reported a real name as an
                // invention — a checker that accuses true prose is the failure round 10 cost
                // seven correct sections to.
                string word = words[i];
                if (word.EndsWith("'s", StringComparison.Ordinal)
                    || word.EndsWith("’s", StringComparison.Ordinal))
                {
                    word = word[..^2];
                }

                word = word.Trim('\'', '’');
                if (word.Length < 3 || !char.IsUpper(word[0])) continue;
                names.Add(word);
            }
        }

        return names;
    }

    [GeneratedRegex(@"(\d+)\s*[–—-]\s*(\d+)\s*$")]
    private static partial Regex Range();

    [GeneratedRegex(@"\b\d{1,4}\b")]
    private static partial Regex Number();

    [GeneratedRegex(@"\b(?:in|by|since|until|from|to)\s+(\d{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex DatedYear();
}
