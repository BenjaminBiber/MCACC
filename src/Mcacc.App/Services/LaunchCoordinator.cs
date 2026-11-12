using Mcacc.App.Localization;
using Mcacc.App.ViewModels;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Terminal;

namespace Mcacc.App.Services;

/// <summary>
/// Starts Claude Desktop or Claude Code for an account, with the dialogs, messages and closing that belong to it. The
/// target picker and the tray menu both start through it, so a start behaves the same from either.
/// </summary>
public sealed class LaunchCoordinator(AppServices services)
{
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>What a start of Claude Desktop would run into; a failed check counts as ready.</summary>
    public async Task<DesktopLaunchCheck> CheckDesktopAsync(Account account, CancellationToken ct)
    {
        try
        {
            return await services.Desktop
                .PrepareAsync(account, services.Accounts.Config.Accounts, ct)
                .ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            services.Log.Warn("Checking the running instances failed.", e);
            return new DesktopLaunchCheck.Ready();
        }
    }

    public async Task LaunchDesktopAsync(Account account)
    {
        var accounts = services.Accounts.Config.Accounts;
        var check = await CheckDesktopAsync(account, CancellationToken.None).ConfigureAwait(true);
        if (check is DesktopLaunchCheck.NotInstalled)
        {
            ShowError(Texts.ErrorDesktopNotFound(account.Provider));
            return;
        }

        var quitConflicts = false;
        if (check is DesktopLaunchCheck.Conflict conflict)
        {
            var dialog = new ConfirmDialogViewModel(
                Texts.ConflictDialogTitle(account.Provider),
                Texts.ConflictDialogText(account.Provider, OwnerName(conflict, account), account.Name),
                Texts.ConflictDialogRestart);
            services.Lifetime.ShowWindow();
            if (await services.Dialogs.ShowAsync(dialog).ConfigureAwait(true) != true)
            {
                return;
            }

            quitConflicts = true;
        }

        DesktopLaunchOutcome outcome;
        using (services.Dialogs.Busy(quitConflicts ? Texts.BusyQuitting(account.Provider) : Texts.BusyStarting(account.Provider)))
        {
            try
            {
                outcome = await services.Desktop
                    .LaunchAsync(account, accounts, quitConflicts, CancellationToken.None)
                    .ConfigureAwait(true);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                services.Log.Error($"Launching Claude Desktop for '{account.Id}' failed.", e);
                outcome = DesktopLaunchOutcome.StartFailed;
            }
        }

        switch (outcome)
        {
            case DesktopLaunchOutcome.Launched:
                services.Log.Info($"Claude Desktop started for '{account.Id}'.");
                if (account.IsDefault)
                {
                    // A login coming back now lands in the default profile by itself. A watch still running for
                    // another account would take it away, so it ends here, and its hint goes with it.
                    if (services.Redirect.StopWatching(account.Provider))
                    {
                        services.Shell.ClearError();
                    }

                    await FinishAsync(account, LaunchTarget.Desktop, autoClose: true).ConfigureAwait(true);
                    return;
                }

                // A login through the browser comes back to the app default profile, so MCACC stays until it can
                // put it on this account's profile instead.
                await FinishAsync(account, LaunchTarget.Desktop, autoClose: false).ConfigureAwait(true);
                await WatchLoginAsync(account).ConfigureAwait(true);
                return;
            case DesktopLaunchOutcome.NotInstalled:
                ShowError(Texts.ErrorDesktopNotFound(account.Provider));
                return;
            case DesktopLaunchOutcome.QuitFailed:
                ShowError(Texts.ErrorQuitFailed(account.Provider));
                return;
            case DesktopLaunchOutcome.ConflictNotResolved:
                return;
            default:
                ShowError(Texts.ErrorDesktopStartFailed(account.Provider));
                return;
        }
    }

    public async Task LaunchCodeAsync(Account account)
    {
        CodeLaunchResult result;
        try
        {
            result = services.Code.Launch(account, services.Accounts.Config.Settings);
        }
        catch (Exception e)
        {
            services.Log.Error($"Launching Claude Code for '{account.Id}' failed.", e);
            ShowError(Texts.ErrorTerminalStartFailed);
            return;
        }

        switch (result.Outcome)
        {
            case CodeLaunchOutcome.NoTerminal:
                ShowError(Texts.ErrorNoTerminal);
                return;
            case CodeLaunchOutcome.StartFailed:
                ShowError(Texts.ErrorTerminalStartFailed);
                return;
            default:
                services.Log.Info($"Claude Code started for '{account.Id}' in '{result.Terminal?.Id}'.");

                // Claude Code renews an expired token as it starts; without a recheck the tile would say expired
                // until the next tick.
                _ = services.Poller.RecheckAsync(account);
                if (result.CliNotFound)
                {
                    // The login shell may still find claude, so this is a hint and the switcher stays open.
                    ShowError(Texts.HintCliMissing(account.Provider));
                }

                await FinishAsync(account, LaunchTarget.Code, autoClose: !result.CliNotFound).ConfigureAwait(true);
                return;
        }
    }

    public static string OwnerName(DesktopLaunchCheck.Conflict conflict, Account account) =>
        conflict.Conflicts.Select(c => c.Owner?.Name).FirstOrDefault(name => name is not null) ?? account.Name;

    /// <summary>A message only helps when it is seen, so a start from the tray brings the window up for it.</summary>
    private void ShowError(string message)
    {
        services.Lifetime.ShowWindow();
        services.Shell.ShowError(message);
    }

    /// <summary>
    /// Keeps the window as it is while a browser login may still come back, and hides it afterwards. The hint does not
    /// bring a hidden window up: the watch runs just as well from the tray, and a failure still shows.
    /// </summary>
    private async Task WatchLoginAsync(Account account)
    {
        services.Shell.ShowError(Texts.HintLoginWatch);
        DesktopRedirectOutcome outcome;
        try
        {
            outcome = await services.Redirect.WatchAsync(account, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            services.Log.Warn($"Watching the Claude Desktop login for '{account.Id}' failed.", e);
            outcome = DesktopRedirectOutcome.Failed;
        }

        if (outcome == DesktopRedirectOutcome.Failed)
        {
            ShowError(Texts.ErrorLoginRedirectFailed(account.Provider));
            return;
        }

        if (outcome == DesktopRedirectOutcome.Superseded)
        {
            // A newer start took the watch over; the hint and the window are its to handle now.
            return;
        }

        services.Shell.ClearError();
        if (outcome == DesktopRedirectOutcome.Redirected)
        {
            services.Log.Info($"Claude Desktop login handed to '{account.Id}'.");
        }

        if (services.Accounts.Config.Settings.CloseAfterLaunch)
        {
            services.Lifetime.HideWindow();
        }
    }

    private async Task FinishAsync(Account account, LaunchTarget target, bool autoClose)
    {
        try
        {
            services.Accounts.UpdateSettings(settings => settings with
            {
                LastAccountId = account.Id,
                LastTarget = target,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            services.Log.Warn("Saving the last choice failed.", e);
        }

        if (autoClose && services.Accounts.Config.Settings.CloseAfterLaunch)
        {
            await Task.Delay(CloseDelay).ConfigureAwait(true);
            services.Lifetime.HideWindow();
        }
    }
}
