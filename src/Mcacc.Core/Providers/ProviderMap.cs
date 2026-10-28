using Mcacc.Core.Model;

namespace Mcacc.Core.Providers;

/// <summary>
/// One implementation per provider, picked by an account's <see cref="Account.Provider"/>. A missing entry throws:
/// quietly using another provider's implementation would read the wrong files or start the wrong program.
/// </summary>
public sealed class ProviderMap<T>
    where T : class
{
    private readonly IReadOnlyDictionary<ProviderKind, T> _byKind;

    /// <exception cref="ArgumentException">The map holds no entry at all.</exception>
    public ProviderMap(IReadOnlyDictionary<ProviderKind, T> byKind)
    {
        if (byKind.Count == 0)
        {
            throw new ArgumentException($"No {typeof(T).Name} was registered for any provider.", nameof(byKind));
        }

        _byKind = byKind;
    }

    public static ProviderMap<T> Of(T claude) =>
        new(new Dictionary<ProviderKind, T> { [ProviderKind.Claude] = claude });

    public static ProviderMap<T> Of(T claude, T codex) =>
        new(new Dictionary<ProviderKind, T> { [ProviderKind.Claude] = claude, [ProviderKind.Codex] = codex });

    /// <exception cref="NotSupportedException">The account's provider has no implementation.</exception>
    public T For(Account account) => For(account.Provider);

    /// <exception cref="NotSupportedException">The provider has no implementation.</exception>
    public T For(ProviderKind provider) =>
        _byKind.TryGetValue(provider, out var implementation)
            ? implementation
            : throw new NotSupportedException($"MCACC has no {typeof(T).Name} for provider '{provider}'.");
}
