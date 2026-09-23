using Mcacc.Core.Abstractions;
using Mcacc.Core.Paths;

namespace Mcacc.Core.ClaudeCode;

public sealed class ClaudeJsonLoginInfoReader : ILoginInfoReader
{
    public ClaudeLoginInfo? Read(AccountPaths paths) => ClaudeJsonLoginReader.Read(paths.ClaudeJsonFile);
}
