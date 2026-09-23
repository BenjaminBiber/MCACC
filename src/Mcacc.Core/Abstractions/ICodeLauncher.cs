using Mcacc.Core.Model;
using Mcacc.Core.Terminal;

namespace Mcacc.Core.Abstractions;

public interface ICodeLauncher
{
    CodeLaunchResult Launch(Account account, AppSettings settings);
}
