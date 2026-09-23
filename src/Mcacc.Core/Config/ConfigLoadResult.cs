using Mcacc.Core.Model;

namespace Mcacc.Core.Config;

public sealed record ConfigLoadResult(
    AppConfig Config,
    ConfigLoadStatus Status,
    bool IsReadOnly,
    IReadOnlyList<string> Repairs);
