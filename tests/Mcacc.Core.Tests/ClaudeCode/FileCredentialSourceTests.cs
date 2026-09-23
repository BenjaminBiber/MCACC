using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ClaudeCode;

public sealed class FileCredentialSourceTests : IDisposable
{
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TempDirectory _temp = new();
    private readonly FileCredentialSource _source = new();

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AccountPaths Paths()
    {
        var platform = new PlatformInfo(
            OperatingSystem.IsWindows() ? OsKind.Windows : OsKind.Linux,
            _temp.Path,
            Path.Join(_temp.Path, "Roaming"),
            Path.Join(_temp.Path, "Local"));
        var paths = new McaccPaths(Path.Join(_temp.Path, ".mcacc"), platform.PathStyle);
        return new AccountPathResolver(paths, platform).Resolve(Privat);
    }

    private void WriteCredentials(string content)
    {
        var file = Paths().CredentialsFile;
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    [Fact]
    public async Task Reads_the_credentials_file_of_the_account()
    {
        WriteCredentials(FixtureFiles.ReadAllText("credentials-ok.json"));

        var result = await _source.ReadAsync(Privat, Paths(), Ct);

        result.Status.ShouldBe(CredentialReadStatus.Found);
        result.Credentials!.AccessToken.ShouldBe("sk-ant-oat01-EXAMPLE-NOT-A-REAL-TOKEN");
    }

    [Fact]
    public async Task A_missing_file_means_not_logged_in()
    {
        var result = await _source.ReadAsync(Privat, Paths(), Ct);

        result.ShouldBe(new CredentialReadResult(CredentialReadStatus.NotLoggedIn, null));
    }

    [Fact]
    public async Task An_unparsable_file_is_an_error()
    {
        WriteCredentials("{ not json");

        var result = await _source.ReadAsync(Privat, Paths(), Ct);

        result.ShouldBe(new CredentialReadResult(CredentialReadStatus.Error, null));
    }

    [Fact]
    public async Task A_directory_in_place_of_the_file_is_an_error()
    {
        Directory.CreateDirectory(Paths().CredentialsFile);

        var result = await _source.ReadAsync(Privat, Paths(), Ct);

        result.Status.ShouldBe(CredentialReadStatus.Error);
    }
}
