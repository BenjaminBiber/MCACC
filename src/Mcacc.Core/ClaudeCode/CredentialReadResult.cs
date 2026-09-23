namespace Mcacc.Core.ClaudeCode;

/// <param name="Credentials">Set only when <paramref name="Status"/> is <see cref="CredentialReadStatus.Found"/>.</param>
public sealed record CredentialReadResult(CredentialReadStatus Status, OAuthCredentials? Credentials);
