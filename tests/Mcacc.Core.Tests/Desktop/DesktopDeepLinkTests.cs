using Mcacc.Core.Desktop;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopDeepLinkTests
{
    private const string MsixExe = @"C:\Program Files\WindowsApps\Claude_2.7032.0.0_x64__pzs8sxrjxfjjc\app\Claude.exe";
    private const string LoginUrl = "claude://login/google-auth?code=4%2F0AXlqoi45&anon_id=claudeai.v1.4f398956&hop_nonce=1BkdtqPC";

    [Fact]
    public void Quoted_url_behind_the_quoted_exe_is_read_whole()
    {
        DesktopDeepLink.TryGetUrl($"\"{MsixExe}\" \"{LoginUrl}\"", out var url).ShouldBeTrue();

        url.ShouldBe(LoginUrl);
    }

    [Fact]
    public void Unquoted_url_ends_at_the_next_argument()
    {
        DesktopDeepLink.TryGetUrl($"\"{MsixExe}\" {LoginUrl} --lang=de", out var url).ShouldBeTrue();

        url.ShouldBe(LoginUrl);
    }

    [Fact]
    public void Url_that_ends_the_command_line_is_read_to_the_end()
    {
        DesktopDeepLink.TryGetUrl($"\"{MsixExe}\" {LoginUrl}", out var url).ShouldBeTrue();

        url.ShouldBe(LoginUrl);
    }

    [Fact]
    public void Scheme_is_matched_without_regard_to_case()
    {
        DesktopDeepLink.TryGetUrl($"\"{MsixExe}\" \"Claude://login/app-google-auth\"", out var url).ShouldBeTrue();

        url.ShouldBe("Claude://login/app-google-auth");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"""C:\app\Claude.exe""")]
    [InlineData(@"""C:\app\Claude.exe"" --user-data-dir=C:\profiles\privat\desktop")]
    public void Command_line_without_a_deep_link_yields_nothing(string? commandLine)
    {
        DesktopDeepLink.TryGetUrl(commandLine, out var url).ShouldBeFalse();

        url.ShouldBeEmpty();
    }

    [Fact]
    public void Bare_scheme_without_a_target_is_not_a_deep_link()
    {
        DesktopDeepLink.TryGetUrl(@"""C:\app\Claude.exe"" ""claude://""", out _).ShouldBeFalse();
    }
}
