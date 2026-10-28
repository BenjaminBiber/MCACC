namespace Mcacc.Core.Model;

/// <summary>
/// Which coding assistant an account belongs to. <see cref="Claude"/> is the zero value, so a config file written
/// before providers existed reads as Claude without a conversion step.
/// </summary>
public enum ProviderKind
{
    Claude,
    Codex,
}
