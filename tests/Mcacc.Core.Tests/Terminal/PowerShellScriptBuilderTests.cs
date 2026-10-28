using System.Text;
using Mcacc.Core.Model;
using Mcacc.Core.Terminal;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public class PowerShellScriptBuilderTests
{
    private static string Scrub(ProviderDescriptor provider) =>
        $"Remove-Item {Scrubs.PowerShell(provider)} -ErrorAction Ignore; ";

    [Fact]
    public void Profile_script_scrubs_sets_the_config_dir_changes_to_home_sets_the_title_and_runs_claude()
    {
        PowerShellScriptBuilder.Build(LaunchRequests.WindowsProfile()).ShouldBe(
            Scrub(ProviderDescriptors.Claude) + """$env:CLAUDE_CONFIG_DIR='C:\Users\Jö O''Neil’’s 100%;$x\.mcacc\profiles\privat\claude'; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%;$x'; $Host.UI.RawUI.WindowTitle='Claude Code · Privat'; claude""");
    }

    [Fact]
    public void Default_script_removes_the_config_dir_variable()
    {
        PowerShellScriptBuilder.Build(LaunchRequests.WindowsDefault()).ShouldBe(
            Scrub(ProviderDescriptors.Claude) + """Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction Ignore; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%;$x'; $Host.UI.RawUI.WindowTitle='Claude Code · Standard'; claude""");
    }

    [Fact]
    public void Title_keeps_a_typographic_apostrophe_and_quotes_it()
    {
        PowerShellScriptBuilder.Build(LaunchRequests.WindowsProfile("Jö’s 'Firma'"))
            .ShouldContain("; $Host.UI.RawUI.WindowTitle='Claude Code · Jö’’s Firma'; claude");
    }

    [Fact]
    public void Encoded_script_is_base64_of_the_utf16le_script()
    {
        foreach (var request in new[] { LaunchRequests.WindowsProfile(), LaunchRequests.WindowsDefault() })
        {
            var encoded = PowerShellScriptBuilder.BuildEncoded(request);

            Encoding.Unicode.GetString(Convert.FromBase64String(encoded)).ShouldBe(PowerShellScriptBuilder.Build(request));
        }
    }

    [Fact]
    public void Codex_profile_script_sets_CODEX_HOME_and_runs_codex()
    {
        PowerShellScriptBuilder.Build(LaunchRequests.WindowsCodexProfile()).ShouldBe(
            Scrub(ProviderDescriptors.Codex) + """$env:CODEX_HOME='C:\Users\Jö O''Neil’’s 100%;$x\.mcacc\profiles\codex-privat\codex'; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%;$x'; $Host.UI.RawUI.WindowTitle='Codex · Codex Privat'; codex""");
    }

    [Fact]
    public void A_Codex_script_removes_the_Claude_config_dir_and_the_sqlite_home_but_not_CODEX_HOME()
    {
        var script = PowerShellScriptBuilder.Build(LaunchRequests.WindowsCodexProfile());
        var scrubPart = script[..script.IndexOf("-ErrorAction", StringComparison.Ordinal)];

        scrubPart.ShouldContain("Env:CLAUDE_CONFIG_DIR");
        scrubPart.ShouldContain("Env:CODEX_SQLITE_HOME");
        scrubPart.ShouldNotContain("Env:CODEX_HOME,");
        scrubPart.ShouldNotEndWith("Env:CODEX_HOME ");
    }
}
