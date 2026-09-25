namespace Mcacc.App.Services;

/// <summary>Which accounts Claude Desktop runs with right now, for the tiles and the tray menu alike.</summary>
public static class RunningAccounts
{
    /// <summary>The ids of the accounts with a running instance; a failed detection counts as none.</summary>
    public static async Task<IReadOnlySet<string>> DetectAsync(AppServices services, CancellationToken ct)
    {
        try
        {
            var instances = await services.Detector.DetectAsync(ct).ConfigureAwait(true);
            var accounts = services.Accounts.Config.Accounts;
            return instances
                .Select(instance => services.Matcher.Match(instance, accounts)?.Id)
                .OfType<string>()
                .ToHashSet();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            services.Log.Warn("Detecting the running instances failed.", e);
            return new HashSet<string>();
        }
    }
}
