using Mcacc.Core.Processes;

namespace Mcacc.Core.Desktop;

public static class DesktopProcessClassifier
{
    /// <returns>One instance per main process, ordered by main pid. Desktop processes without a main are ignored.</returns>
    public static IReadOnlyList<DesktopInstance> Classify(IEnumerable<ProcessInfo> processes, DesktopProcessRules rules)
    {
        var desktop = processes.Where(rules.IsDesktopProcess).ToList();
        var mains = desktop.Where(rules.IsMain).OrderBy(p => p.Pid).ToList();
        var mainPids = mains.Select(p => p.Pid).ToHashSet();

        // Another main is never a descendant, so a relaunched instance and a stale parent pid stay separate.
        var childrenByParent = desktop.Where(p => !mainPids.Contains(p.Pid)).ToLookup(p => p.ParentPid);

        return mains.Select(main => ToInstance(rules, main, Descendants(main, childrenByParent))).ToList();
    }

    private static List<ProcessInfo> Descendants(ProcessInfo main, ILookup<int, ProcessInfo> childrenByParent)
    {
        var visited = new HashSet<int> { main.Pid };
        var descendants = new List<ProcessInfo>();
        var pending = new Queue<int>([main.Pid]);
        while (pending.TryDequeue(out var parentPid))
        {
            foreach (var child in childrenByParent[parentPid])
            {
                if (visited.Add(child.Pid))
                {
                    descendants.Add(child);
                    pending.Enqueue(child.Pid);
                }
            }
        }

        descendants.Sort((a, b) => a.Pid.CompareTo(b.Pid));
        return descendants;
    }

    private static DesktopInstance ToInstance(DesktopProcessRules rules, ProcessInfo main, List<ProcessInfo> descendants)
    {
        var userDataDir = descendants.Prepend(main)
            .Select(p => CommandLineSwitch.TryGetValue(p.CommandLine, DesktopLaunchPlanner.UserDataDirSwitch, out var dir) ? dir : null)
            .FirstOrDefault(dir => dir is not null);

        // Only the main process is asked: the OS puts the deep link on the launch it starts, never on a child.
        var deepLink = DesktopDeepLink.TryGetUrl(rules.Provider, main.CommandLine, out var url) ? url : null;

        return new DesktopInstance(
            main.Pid,
            main.StartTime,
            userDataDir,
            [main.Pid, .. descendants.Select(p => p.Pid)],
            deepLink);
    }
}
