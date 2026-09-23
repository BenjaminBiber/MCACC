using Mcacc.Core.Config;
using Mcacc.Core.Model;

namespace Mcacc.Core.Abstractions;

public interface IConfigStore
{
    ConfigLoadResult Load();

    void Save(AppConfig config);
}
