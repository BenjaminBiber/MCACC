using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.App.Services;

/// <summary>
/// Keeps MCACC's status line in the <c>settings.json</c> of every Claude account in step with the setting: at each
/// start, at each switch and after an account was added. The entry always names the running MCACC, so a moved app is
/// corrected at its next start. A failure is logged per account: the entry must never cost MCACC its start.
/// </summary>
/// <param name="executablePath">Null when MCACC runs from a place that is gone later; then nothing is written.</param>
public sealed class StatusLineSync(AccountPathResolver resolver, string? executablePath, ILog log)
{
    /// <summary>For the running process, skipping a run from a temp folder like the login entry does.</summary>
    public static StatusLineSync ForProcess(AccountPathResolver resolver, ILog log)
    {
        var path = Environment.ProcessPath;
        var tempDirs = new[]
        {
            Path.GetTempPath(),
            Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
        };
        if (path is not null && LoginItemSync.IsTransient(path, tempDirs))
        {
            log.Info($"MCACC runs from {path}, which is gone later; the status line entries stay as they are.");
            path = null;
        }

        return new StatusLineSync(resolver, path, log);
    }

    /// <returns>The Claude accounts whose own status line kept MCACC's out.</returns>
    public IReadOnlyList<Account> Apply(bool record, IEnumerable<Account> accounts)
    {
        string? command = null;
        if (record)
        {
            if (executablePath is null)
            {
                return [];
            }

            command = StatusLineSettings.Command(executablePath);
        }

        var foreign = new List<Account>();
        foreach (var account in accounts.Where(account => account.Provider == ProviderKind.Claude))
        {
            var file = Path.Join(resolver.Resolve(account).EffectiveConfigDir, StatusLineSettings.FileName);
            try
            {
                switch (StatusLineSettings.Apply(file, command))
                {
                    case StatusLineEdit.Foreign:
                        foreign.Add(account);
                        break;
                    case StatusLineEdit.Unreadable:
                        log.Warn($"The settings.json of '{account.Id}' is no json object; its status line stays as it is.");
                        break;
                    case StatusLineEdit.Written or StatusLineEdit.Removed:
                        log.Info($"Status line of '{account.Id}' {(record ? "set" : "removed")}.");
                        break;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                log.Warn($"Updating the status line of '{account.Id}' failed.", e);
            }
        }

        return foreign;
    }
}
