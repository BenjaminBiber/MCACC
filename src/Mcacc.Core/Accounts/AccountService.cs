using Mcacc.Core.Abstractions;
using Mcacc.Core.Config;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Accounts;

/// <remarks>
/// Every change throws <see cref="InvalidOperationException"/> before touching the disk when the config is read-only.
/// </remarks>
public sealed class AccountService
{
    private readonly IConfigStore _store;
    private readonly McaccPaths _paths;
    private readonly AccountPathResolver _resolver;
    private readonly ConfigCopier _copier;
    private readonly ProfileDataDeleter _deleter;

    public AccountService(
        IConfigStore store, McaccPaths paths, AccountPathResolver resolver, ConfigCopier copier, ProfileDataDeleter deleter)
    {
        _store = store;
        _paths = paths;
        _resolver = resolver;
        _copier = copier;
        _deleter = deleter;
        LoadResult = store.Load();
        Config = LoadResult.Config;
    }

    public ConfigLoadResult LoadResult { get; }

    public AppConfig Config { get; private set; }

    /// <exception cref="AccountValidationException">The name is invalid.</exception>
    /// <exception cref="ArgumentException">The color is invalid or the copy source is unknown.</exception>
    public CreateAccountResult Create(string name, string color, CopyRequest? copy)
    {
        EnsureWritable();
        var validName = ValidName(name, exceptId: null);
        EnsureValidColor(color);
        var copySource = copy is null
            ? null
            : Find(copy.SourceAccountId)
              ?? throw new ArgumentException($"Unknown copy source account '{copy.SourceAccountId}'.", nameof(copy));

        var id = SlugGenerator.Generate(validName, slug => Find(slug) is not null || Path.Exists(_paths.ProfileDir(slug)));
        var account = new Account(id, validName, color, IsDefault: false);
        Directory.CreateDirectory(_paths.DesktopDataDir(id));
        Directory.CreateDirectory(_paths.ClaudeConfigDir(id));
        var report = copy is not null && copySource is not null ? CopyConfig(copySource, account, copy) : null;

        Save(Config with { Accounts = [.. Config.Accounts, account] });
        return new CreateAccountResult(account, report);
    }

    /// <exception cref="AccountValidationException">The name is invalid.</exception>
    /// <exception cref="ArgumentException">The color is invalid.</exception>
    /// <exception cref="InvalidOperationException">The account is unknown.</exception>
    public Account Update(string id, string name, string color)
    {
        EnsureWritable();
        var index = IndexOf(id);
        var validName = ValidName(name, exceptId: id);
        EnsureValidColor(color);

        var updated = Config.Accounts[index] with { Name = validName, Color = color };
        var accounts = Config.Accounts.ToList();
        accounts[index] = updated;
        Save(Config with { Accounts = accounts });
        return updated;
    }

    /// <exception cref="InvalidOperationException">The account is the default or unknown, or its data cannot be deleted.</exception>
    public void Remove(string id, bool deleteData)
    {
        EnsureWritable();
        var account = Config.Accounts[IndexOf(id)];
        if (account.IsDefault)
        {
            throw new InvalidOperationException("The default account cannot be removed.");
        }

        // Deleting first keeps the account listed if deletion fails, so the user can retry.
        if (deleteData)
        {
            _deleter.Delete(account, _paths);
        }

        var settings = Config.Settings.LastAccountId == id ? Config.Settings with { LastAccountId = null } : Config.Settings;
        Save(Config with { Accounts = Config.Accounts.Where(a => a.Id != id).ToList(), Settings = settings });
    }

    public void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        EnsureWritable();
        Save(Config with { Settings = change(Config.Settings) });
    }

    private CopyReport CopyConfig(Account source, Account target, CopyRequest request)
    {
        var sourcePaths = _resolver.Resolve(source);
        var targetPaths = _resolver.Resolve(target);
        var plan = ConfigCopyPlanner.Plan(sourcePaths, targetPaths, request);
        return _copier.Execute(plan, sourcePaths, targetPaths);
    }

    private void EnsureWritable()
    {
        if (LoadResult.IsReadOnly)
        {
            throw new InvalidOperationException("The config was written by a newer MCACC version and is read-only.");
        }
    }

    private string ValidName(string name, string? exceptId) =>
        AccountNameValidator.Validate(name, Config.Accounts, exceptId) is { } error
            ? throw new AccountValidationException(error)
            : name.Trim();

    private static void EnsureValidColor(string color)
    {
        if (!AccountPalette.IsValidColor(color))
        {
            throw new ArgumentException($"'{color}' is not a #RRGGBB color.", nameof(color));
        }
    }

    private Account? Find(string id) => Config.Accounts.FirstOrDefault(a => a.Id == id);

    private int IndexOf(string id)
    {
        for (var i = 0; i < Config.Accounts.Count; i++)
        {
            if (Config.Accounts[i].Id == id)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Unknown account '{id}'.");
    }

    private void Save(AppConfig config)
    {
        _store.Save(config);
        Config = config;
    }
}
