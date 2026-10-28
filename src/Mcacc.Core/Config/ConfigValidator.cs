using Mcacc.Core.Accounts;
using Mcacc.Core.Model;

namespace Mcacc.Core.Config;

public static class ConfigValidator
{
    // Null checks guard against hand-edited JSON; the model's annotations describe valid configs only.
    public static (AppConfig Config, IReadOnlyList<string> Repairs) Repair(AppConfig config)
    {
        var repairs = new List<string>();
        var version = MigrateVersion(config.Version, repairs);
        var accounts = KeepValidUniqueAccounts(config.Accounts, repairs);
        RepairProviders(accounts, repairs);
        EnsureSingleDefaultPerProvider(accounts, repairs);
        RepairColors(accounts, repairs);
        RepairNames(accounts, repairs);
        var settings = RepairSettings(config.Settings, accounts, repairs);

        return repairs.Count == 0
            ? (config, repairs)
            : (config with { Version = version, Accounts = accounts, Settings = settings }, repairs);
    }

    /// <summary>
    /// Only ever forward. A file from a newer MCACC keeps its version and is loaded read-only, so lowering it here
    /// would turn a protected file into one this version believes it may write.
    /// </summary>
    private static int MigrateVersion(int version, List<string> repairs)
    {
        if (version >= AppConfig.CurrentVersion)
        {
            return version;
        }

        repairs.Add($"Migrated the config from version {version} to {AppConfig.CurrentVersion}.");
        return AppConfig.CurrentVersion;
    }

    private static List<Account> KeepValidUniqueAccounts(IReadOnlyList<Account>? source, List<string> repairs)
    {
        if (source is null)
        {
            repairs.Add("Account list was missing.");
            return [];
        }

        var accounts = new List<Account>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var account in source)
        {
            if (account is null)
            {
                repairs.Add("Dropped an empty account entry.");
            }
            else if (!AccountId.IsValid(account.Id))
            {
                repairs.Add($"Dropped account with invalid id '{account.Id}'.");
            }
            else if (!ids.Add(account.Id))
            {
                repairs.Add($"Dropped duplicate account id '{account.Id}'.");
            }
            else
            {
                accounts.Add(account);
            }
        }

        return accounts;
    }

    /// <summary>
    /// A provider outside the enum can only come from a hand-edited file — a newer MCACC would have raised the
    /// version and the file would be read-only. Such an account reads as Claude and the repair says so, rather than
    /// the account disappearing from the list over one unknown word.
    /// </summary>
    private static void RepairProviders(List<Account> accounts, List<string> repairs)
    {
        for (var i = 0; i < accounts.Count; i++)
        {
            if (!Enum.IsDefined(accounts[i].Provider))
            {
                repairs.Add($"Account '{accounts[i].Id}' named an unknown provider; treating it as Claude.");
                accounts[i] = accounts[i] with { Provider = ProviderKind.Claude };
            }
        }
    }

    /// <summary>
    /// Each provider has at most one default account: the one that uses the provider's own home directory instead
    /// of a profile. Claude's is also inserted when it is missing, because it is what keeps the list from being
    /// empty; another provider's is never created here — that is the user's decision, not a repair.
    /// </summary>
    private static void EnsureSingleDefaultPerProvider(List<Account> accounts, List<string> repairs)
    {
        foreach (var provider in Enum.GetValues<ProviderKind>())
        {
            ClearExtraDefaults(accounts, provider, repairs);
        }

        EnsureClaudeDefault(accounts, repairs);
    }

    private static void ClearExtraDefaults(List<Account> accounts, ProviderKind provider, List<string> repairs)
    {
        var first = accounts.FindIndex(a => a.Provider == provider && a.IsDefault);
        if (first < 0)
        {
            return;
        }

        for (var i = first + 1; i < accounts.Count; i++)
        {
            if (accounts[i].Provider == provider && accounts[i].IsDefault)
            {
                accounts[i] = accounts[i] with { IsDefault = false };
                repairs.Add($"Cleared extra default flag on account '{accounts[i].Id}'.");
            }
        }
    }

    private static void EnsureClaudeDefault(List<Account> accounts, List<string> repairs)
    {
        if (accounts.Any(a => a is { Provider: ProviderKind.Claude, IsDefault: true }))
        {
            return;
        }

        var standard = accounts.FindIndex(a => a.Provider == ProviderKind.Claude && a.Id == Account.DefaultId);
        if (standard >= 0)
        {
            accounts[standard] = accounts[standard] with { IsDefault = true };
            repairs.Add($"Made account '{Account.DefaultId}' the default account.");
            return;
        }

        accounts.Insert(0, Account.CreateDefault());
        repairs.Add($"Inserted missing default account '{Account.DefaultId}'.");
    }

    private static void RepairColors(List<Account> accounts, List<string> repairs)
    {
        var used = accounts.Select(a => a.Color).Where(AccountPalette.IsValidColor).ToList();
        for (var i = 0; i < accounts.Count; i++)
        {
            if (!AccountPalette.IsValidColor(accounts[i].Color))
            {
                var color = AccountPalette.NextFreeColor(used);
                used.Add(color);
                accounts[i] = accounts[i] with { Color = color };
                repairs.Add($"Replaced invalid color of account '{accounts[i].Id}' with {color}.");
            }
        }
    }

    private static void RepairNames(List<Account> accounts, List<string> repairs)
    {
        for (var i = 0; i < accounts.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(accounts[i].Name))
            {
                accounts[i] = accounts[i] with { Name = accounts[i].Id };
                repairs.Add($"Named account '{accounts[i].Id}' after its id.");
            }
        }
    }

    private static AppSettings RepairSettings(AppSettings? source, List<Account> accounts, List<string> repairs)
    {
        var settings = source;
        if (settings is null)
        {
            repairs.Add("Settings were missing; using defaults.");
            settings = new AppSettings();
        }

        if (settings.LastAccountId is { } lastId && accounts.All(a => a.Id != lastId))
        {
            repairs.Add($"Cleared unknown last account id '{lastId}'.");
            settings = settings with { LastAccountId = null };
        }

        return settings;
    }
}
