namespace Mcacc.Core.Stats;

/// <summary>
/// Leaves the projects the settings exclude out of the statistics. A project is the path the info page groups by
/// (<see cref="ProjectRoot.RepositoryOf"/>); it matches regardless of case and of a trailing separator.
/// </summary>
public static class ProjectFilter
{
    public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>The form a project is compared and stored in.</summary>
    public static string Normalize(string project)
    {
        var trimmed = project.TrimEnd(Separators);
        return trimmed.Length > 0 ? trimmed : project;
    }

    public static bool Contains(IEnumerable<string>? excluded, string project) =>
        excluded is not null && excluded.Any(entry => Comparer.Equals(Normalize(entry), Normalize(project)));

    /// <param name="projectOf">The project of a working directory.</param>
    /// <returns>The buckets of the projects that still count; the same list when nothing is excluded.</returns>
    public static IReadOnlyList<TokenBucket> Without(
        IReadOnlyList<TokenBucket> buckets,
        Func<string, string> projectOf,
        IReadOnlyCollection<string>? excluded)
    {
        if (excluded is not { Count: > 0 })
        {
            return buckets;
        }

        var set = excluded.Select(Normalize).ToHashSet(Comparer);
        return buckets.Where(bucket => !set.Contains(Normalize(projectOf(bucket.Project)))).ToList();
    }
}
