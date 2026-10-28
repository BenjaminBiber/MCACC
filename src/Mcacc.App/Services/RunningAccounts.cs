using Mcacc.Core.Model;

namespace Mcacc.App.Services;

/// <summary>Which accounts a desktop app runs with right now, for the tiles and the tray menu alike.</summary>
public static class RunningAccounts
{
    /// <summary>The ids of the accounts with a running instance; a failed detection counts as none.</summary>
    public static async Task<IReadOnlySet<string>> DetectAsync(AppServices services, CancellationToken ct)
    {
        try
        {
            // One look per provider: a desktop app only ever shows the instances of its own package.
            var accounts = services.Accounts.Config.Accounts;
            var running = new HashSet<string>(StringComparer.Ordinal);
            foreach (var provider in ProviderDescriptors.All.Where(p => p.Desktop is not null))
            {
                var instances = await services.Detector.DetectAsync(provider, ct).ConfigureAwait(true);
                foreach (var id in instances.Select(i => services.Matcher.Match(i, accounts)?.Id).OfType<string>())
                {
                    running.Add(id);
                }
            }

            return running;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            services.Log.Warn("Detecting the running instances failed.", e);
            return new HashSet<string>();
        }
    }
}
