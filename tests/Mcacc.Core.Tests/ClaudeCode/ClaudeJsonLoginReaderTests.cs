using Mcacc.Core.ClaudeCode;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.ClaudeCode;

public sealed class ClaudeJsonLoginReaderTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string WriteFixture(string name)
    {
        var path = Path.Join(_temp.Path, ".claude.json");
        File.WriteAllText(path, FixtureFiles.ReadAllText(name));
        return path;
    }

    private string Write(string content)
    {
        var path = Path.Join(_temp.Path, ".claude.json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Reads_the_login_past_project_keys_that_differ_only_in_case()
    {
        var info = ClaudeJsonLoginReader.Read(WriteFixture("claude-json-duplicate-case-keys.json"));

        info.ShouldBe(new ClaudeLoginInfo(
            "max.muster@example.com",
            "Example GmbH",
            "Benjamin Biber",
            "00000000-0000-0000-0000-000000000001"));
    }

    [Fact]
    public void Truncated_json_reads_as_no_login()
    {
        ClaudeJsonLoginReader.Read(WriteFixture("claude-json-truncated.json")).ShouldBeNull();
    }

    [Fact]
    public void A_file_without_oauth_account_reads_as_no_login()
    {
        ClaudeJsonLoginReader.Read(WriteFixture("claude-json-without-oauth.json")).ShouldBeNull();
    }

    [Fact]
    public void A_missing_file_reads_as_no_login()
    {
        ClaudeJsonLoginReader.Read(Path.Join(_temp.Path, "nothing-here", ".claude.json")).ShouldBeNull();
    }

    [Fact]
    public void An_empty_email_reads_as_no_login()
    {
        ClaudeJsonLoginReader.Read(Write("""{ "oauthAccount": { "emailAddress": "  " } }""")).ShouldBeNull();
    }

    [Fact]
    public void Organization_and_display_name_are_optional()
    {
        var info = ClaudeJsonLoginReader.Read(Write("""{ "oauthAccount": { "emailAddress": "privat@example.com" } }"""));

        info.ShouldBe(new ClaudeLoginInfo("privat@example.com", null, null));
    }

    [Fact]
    public void Trailing_commas_and_comments_are_tolerated()
    {
        var json = """
            {
              // written by an editor
              "oauthAccount": { "emailAddress": "privat@example.com", },
            }
            """;

        ClaudeJsonLoginReader.Read(Write(json))!.Email.ShouldBe("privat@example.com");
    }

    [Fact]
    public void A_json_array_reads_as_no_login()
    {
        ClaudeJsonLoginReader.Read(Write("[1, 2, 3]")).ShouldBeNull();
    }

    [Fact]
    public void A_file_another_writer_holds_open_is_still_read()
    {
        var path = WriteFixture("claude-json-duplicate-case-keys.json");

        using var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        ClaudeJsonLoginReader.Read(path)!.Email.ShouldBe("max.muster@example.com");
    }

    [Fact]
    public void A_directory_in_place_of_the_file_reads_as_no_login()
    {
        var path = Path.Join(_temp.Path, ".claude.json");
        Directory.CreateDirectory(path);

        ClaudeJsonLoginReader.Read(path).ShouldBeNull();
    }
}
