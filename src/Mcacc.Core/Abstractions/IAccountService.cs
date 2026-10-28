using Mcacc.Core.Accounts;
using Mcacc.Core.Config;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;

namespace Mcacc.Core.Abstractions;

/// <summary>The account list and the settings as the UI changes them; every change is saved right away.</summary>
public interface IAccountService
{
    ConfigLoadResult LoadResult { get; }

    AppConfig Config { get; }

    CreateAccountResult Create(string name, string color, CopyRequest? copy, ProviderKind provider = ProviderKind.Claude);

    Account Update(string id, string name, string color);

    void Remove(string id, bool deleteData);

    void UpdateSettings(Func<AppSettings, AppSettings> change);
}
