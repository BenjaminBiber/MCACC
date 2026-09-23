using System.Text.RegularExpressions;
using Mcacc.Core.Model;

namespace Mcacc.Core.Config;

public static partial class ConfigValidator
{
    // Null checks guard against hand-edited JSON; the model's annotations describe valid configs only.
    public static (AppConfig Config, IReadOnlyList<string> Repairs) Repair(AppConfig config)
    {
        var repairs = new List<string>();
        var accounts = KeepValidUniqueAccounts(config.Accounts, repairs);
        EnsureSingleDefault(accounts, repairs);
        RepairColors(accounts, repairs);
        RepairNames(accounts, repairs);
        var settings = RepairSettings(config.Settings, accounts, repairs);

        return repairs.Count == 0
            ? (config, repairs)
            : (config with { Accounts = accounts, Settings = settings }, repairs);
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
            else if (account.Id is null || !IdPattern().IsMatch(account.Id))
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

    private static void EnsureSingleDefault(List<Account> accounts, List<string> repairs)
    {
        var defaultIndex = accounts.FindIndex(a => a.IsDefault);
        if (defaultIndex < 0)
        {
            var standardIndex = accounts.FindIndex(a => a.Id == Account.DefaultId);
            if (standardIndex >= 0)
            {
                accounts[standardIndex] = accounts[standardIndex] with { IsDefault = true };
                repairs.Add($"Made account '{Account.DefaultId}' the default account.");
            }
            else
            {
                accounts.Insert(0, Account.CreateDefault());
                repairs.Add($"Inserted missing default account '{Account.DefaultId}'.");
            }

            return;
        }

        for (var i = defaultIndex + 1; i < accounts.Count; i++)
        {
            if (accounts[i].IsDefault)
            {
                accounts[i] = accounts[i] with { IsDefault = false };
                repairs.Add($"Cleared extra default flag on account '{accounts[i].Id}'.");
            }
        }
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

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex IdPattern();
}
