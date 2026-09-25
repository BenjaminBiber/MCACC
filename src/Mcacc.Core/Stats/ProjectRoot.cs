namespace Mcacc.Core.Stats;

/// <summary>
/// Maps the working directory of a session to the project it belongs to. A worktree is recognized by its path
/// alone, since the worktrees are usually deleted by the time the stats are read; a subfolder by the repository
/// above it.
/// </summary>
public static class ProjectRoot
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// The worktree folders by convention: <c>.claude/worktrees/&lt;name&gt;</c> of Claude Code and the app, and
    /// <c>.worktrees/&lt;name&gt;</c>, both inside the repository.
    /// </summary>
    private static readonly string[][] Markers = [[".claude", "worktrees"], [".worktrees"]];

    /// <returns>The repository for a path in a worktree, anything else unchanged.</returns>
    public static string Of(string cwd) => Find(cwd) is { } found ? cwd[..found.Repository] : cwd;

    /// <summary>
    /// The git repository a session worked in: the worktree's repository, then the nearest folder at or above the
    /// path that holds a <c>.git</c>, so a session that went into a subfolder still counts to its repository. The
    /// subfolder may be gone; only the repository has to exist.
    /// </summary>
    /// <param name="isRepository">Whether a folder holds a <c>.git</c>; the file system when null.</param>
    /// <returns>The path without a worktree when no repository is found.</returns>
    public static string RepositoryOf(string cwd, Func<string, bool>? isRepository = null)
    {
        isRepository ??= HasGit;
        var root = Of(cwd).TrimEnd(Separators);
        for (var folder = root; folder.Length > 0; folder = Parent(folder))
        {
            if (isRepository(folder))
            {
                return folder;
            }
        }

        return Of(cwd);
    }

    /// <summary><see cref="RepositoryOf"/> for each distinct path, checked against the file system.</summary>
    public static IReadOnlyDictionary<string, string> Resolve(IEnumerable<string> cwds)
    {
        var known = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool Cached(string folder) => known.TryGetValue(folder, out var has) ? has : known[folder] = HasGit(folder);
        return cwds
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(cwd => cwd, cwd => RepositoryOf(cwd, Cached), StringComparer.Ordinal);
    }

    private static bool HasGit(string folder)
    {
        try
        {
            // A drive or share root alone ("C:") would mean the current folder of that drive.
            var git = Path.Join(folder.EndsWith(':') ? folder + Path.DirectorySeparatorChar : folder, ".git");
            return Directory.Exists(git) || File.Exists(git);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <returns>The path up to its last separator; empty at the top.</returns>
    private static string Parent(string path)
    {
        var slash = path.TrimEnd(Separators).LastIndexOfAny(Separators);
        return slash <= 0 ? "" : path[..slash].TrimEnd(Separators);
    }

    /// <returns>The worktree folder a path is in or below; null outside a worktree.</returns>
    public static string? WorktreeOf(string cwd) => Find(cwd) is { } found ? cwd[..found.Worktree] : null;

    /// <returns>Where the repository path and the worktree path end in <paramref name="cwd"/>.</returns>
    private static (int Repository, int Worktree)? Find(string cwd)
    {
        var segments = Segments(cwd);
        for (var i = 1; i < segments.Count; i++)
        {
            foreach (var marker in Markers)
            {
                // The worktree needs a name after the marker, else the path is the folder itself.
                if (i + marker.Length < segments.Count && Matches(cwd, segments, i, marker))
                {
                    return (segments[i - 1].End, segments[i + marker.Length].End);
                }
            }
        }

        return null;
    }

    private static bool Matches(string cwd, List<Segment> segments, int start, string[] marker)
    {
        for (var j = 0; j < marker.Length; j++)
        {
            var segment = segments[start + j];
            if (!cwd.AsSpan(segment.Start, segment.End - segment.Start).Equals(marker[j], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static List<Segment> Segments(string path)
    {
        var segments = new List<Segment>();
        var start = 0;
        while (start < path.Length)
        {
            var end = path.IndexOfAny(Separators, start);
            if (end < 0)
            {
                end = path.Length;
            }

            if (end > start)
            {
                segments.Add(new Segment(start, end));
            }

            start = end + 1;
        }

        return segments;
    }

    private readonly record struct Segment(int Start, int End);
}
