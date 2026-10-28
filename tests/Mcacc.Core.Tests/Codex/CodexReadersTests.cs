using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Codex;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Codex;

/// <summary>
/// The login reader and the credential source work on the same file, so they are exercised against the same
/// on-disk profile.
/// </summary>
public sealed class CodexReadersTests : IDisposable
{
    private static readonly Account Codex =
        new("codex-privat", "Codex Privat", "#5E8C8A", IsDefault: false, ProviderKind.Codex);

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private AccountPaths Paths(string? fixture)
    {
        var dir = Path.Join(_temp.Path, "profiles", "codex-privat", "codex");
        Directory.CreateDirectory(dir);
        var auth = Path.Join(dir, "auth.json");
        if (fixture is not null)
        {
            File.WriteAllText(auth, FixtureFiles.ReadAllText(fixture));
        }

        return new AccountPaths(ProviderDescriptors.Codex, null, dir, dir, auth, auth, []);
    }

    private static async Task<CredentialReadResult> ReadAsync(AccountPaths paths) =>
        await new CodexCredentialSource().ReadAsync(Codex, paths, TestContext.Current.CancellationToken);

    [Fact]
    public void A_logged_in_profile_reports_the_address_from_the_id_token()
    {
        var login = new CodexLoginInfoReader().Read(Codex, Paths("codex-auth.json")).ShouldNotBeNull();

        login.Email.ShouldBe("ben@example.com");
        login.PlanType.ShouldBe("plus");
    }

    [Fact]
    public void A_fresh_profile_reports_no_login()
    {
        new CodexLoginInfoReader().Read(Codex, Paths(fixture: null)).ShouldBeNull();
    }

    /// <summary>An API-key login is a login; it just has no address to show.</summary>
    [Fact]
    public void An_api_key_profile_reports_a_login_without_an_address()
    {
        var login = new CodexLoginInfoReader().Read(Codex, Paths("codex-auth-apikey.json")).ShouldNotBeNull();

        login.Email.ShouldBeNull();
    }

    [Fact]
    public async Task A_logged_in_profile_yields_the_token_and_its_expiry()
    {
        var result = await ReadAsync(Paths("codex-auth.json"));

        result.Status.ShouldBe(CredentialReadStatus.Found);
        var credentials = result.Credentials.ShouldNotBeNull();
        credentials.ExpiresAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1793000000));
        credentials.SubscriptionType.ShouldBe("plus");
        credentials.ToString().ShouldNotContain(credentials.AccessToken);
    }

    [Fact]
    public async Task A_missing_file_is_not_logged_in_rather_than_an_error()
    {
        (await ReadAsync(Paths(fixture: null))).Status.ShouldBe(CredentialReadStatus.NotLoggedIn);
    }

    /// <summary>
    /// An API key is no OAuth token, so there is nothing to ask the limits endpoint with — reported as not logged
    /// in, which leaves the tile without numbers instead of showing an error.
    /// </summary>
    [Fact]
    public async Task An_api_key_profile_has_no_token_for_the_limits_endpoint()
    {
        var result = await ReadAsync(Paths("codex-auth-apikey.json"));

        result.Status.ShouldBe(CredentialReadStatus.NotLoggedIn);
        result.Credentials.ShouldBeNull();
    }

    [Fact]
    public async Task An_unparsable_file_is_an_error_so_the_last_known_limits_stand()
    {
        var paths = Paths("codex-auth.json");
        File.WriteAllText(paths.CredentialsFile, "{ not json");

        (await ReadAsync(paths)).Status.ShouldBe(CredentialReadStatus.Error);
    }

    /// <summary>MCACC logs a profile in by launching Codex there, never by writing the file itself.</summary>
    [Fact]
    public async Task Reading_never_writes_the_file()
    {
        var paths = Paths("codex-auth.json");
        var before = File.GetLastWriteTimeUtc(paths.CredentialsFile);
        var content = File.ReadAllText(paths.CredentialsFile);

        new CodexLoginInfoReader().Read(Codex, paths);
        await ReadAsync(paths);

        File.GetLastWriteTimeUtc(paths.CredentialsFile).ShouldBe(before);
        File.ReadAllText(paths.CredentialsFile).ShouldBe(content);
    }

    /// <summary>Codex keeps the file open while it runs, so a shared handle must not fail the read.</summary>
    [Fact]
    public void A_file_held_open_for_writing_is_still_readable()
    {
        var paths = Paths("codex-auth.json");
        using var holder = new FileStream(
            paths.CredentialsFile,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);

        new CodexLoginInfoReader().Read(Codex, paths).ShouldNotBeNull();
    }
}
