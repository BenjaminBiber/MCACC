using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;

namespace Mcacc.Core.Accounts;

/// <param name="CopyReport">Null when no copy was requested.</param>
public sealed record CreateAccountResult(Account Account, CopyReport? CopyReport);
