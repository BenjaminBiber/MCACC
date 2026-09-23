using Mcacc.Core.Model;

namespace Mcacc.Core.Config;

public static class DefaultConfigFactory
{
    public static AppConfig Create() => new(AppConfig.CurrentVersion, [Account.CreateDefault()], new AppSettings());
}
