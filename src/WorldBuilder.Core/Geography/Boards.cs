namespace WorldBuilder.Core.Geography;

/// <summary>
/// Finding the board a new world is simulated against.
///
/// The board is a repository artefact, not a build output and not a function of anything. It was
/// generated once, it is stored, it is read; §2 settles that and prohibition 5 restates it. So
/// this locates a file and parses it, and there is deliberately no path through here that could
/// make one.
///
/// Held once per process because the all-pairs distance table costs a few milliseconds to build
/// and the test suite constructs several hundred worlds. The board is immutable, so sharing it
/// between worlds is safe; that it must stay immutable is the reason <see cref="Board"/> exposes
/// no mutation at all.
/// </summary>
public static class Boards
{
    /// <summary>The stored board, relative to the repository root.</summary>
    public const string StoredPath = "maps/board-1.wbmap.json";

    private static Board? _stored;
    private static readonly Lock Gate = new();

    /// <summary>
    /// The repository's board.
    ///
    /// Throws where it cannot be found, rather than making one or carrying on without. A world
    /// silently simulated with no geography, in a build whose rules all consult geography, is a
    /// world whose distances are all exactly typical — plausible, uniform, and wrong.
    /// </summary>
    public static Board Stored()
    {
        lock (Gate)
        {
            if (_stored is not null) return _stored;

            string path = Locate();
            return _stored = BoardIo.Read(path);
        }
    }

    /// <summary>Where the stored board is, searching upwards from the running assembly and the
    /// working directory — the same walk the regression corpus uses to find its own files.</summary>
    public static string Locate()
    {
        foreach (string from in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (DirectoryInfo? at = new(from); at is not null; at = at.Parent)
            {
                string candidate = Path.Combine(at.FullName, StoredPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
            }
        }

        throw new FileNotFoundException(
            $"no {StoredPath} above {AppContext.BaseDirectory} or {Directory.GetCurrentDirectory()}. " +
            "The board is a stored artefact — it is imported once and committed, never generated " +
            "on demand. Import one with `wb map import <azgaar-export.json>`.");
    }
}
