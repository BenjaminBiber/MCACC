using System.Text;
using Mcacc.Core.Terminal;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Terminal;

public class PowerShellScriptBuilderTests
{
    [Fact]
    public void Profile_script_scrubs_sets_the_config_dir_changes_to_home_sets_the_title_and_runs_claude()
    {
        PowerShellScriptBuilder.Build(LaunchRequests.WindowsProfile()).ShouldBe(
            """Remove-Item Env:CLAUDECODE,Env:CLAUDE_CODE_ENTRYPOINT,Env:CLAUDE_CODE_SSE_PORT -ErrorAction Ignore; $env:CLAUDE_CONFIG_DIR='C:\Users\Jö O''Neil’’s 100%;$x\.mcacc\profiles\privat\claude'; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%;$x'; $Host.UI.RawUI.WindowTitle='Claude Code · Privat'; claude""");
    }

    [Fact]
    public void Default_script_removes_the_config_dir_variable()
    {
        PowerShellScriptBuilder.Build(LaunchRequests.WindowsDefault()).ShouldBe(
            """Remove-Item Env:CLAUDECODE,Env:CLAUDE_CODE_ENTRYPOINT,Env:CLAUDE_CODE_SSE_PORT -ErrorAction Ignore; Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction Ignore; Set-Location -LiteralPath 'C:\Users\Jö O''Neil’’s 100%;$x'; $Host.UI.RawUI.WindowTitle='Claude Code · Standard'; claude""");
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
}
