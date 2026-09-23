using Mcacc.Core.Config;
using Mcacc.Core.Model;
using Mcacc.Platform.Common;
using Mcacc.Platform.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Platform.Tests.Common;

[Trait("Category", "Integration")]
public sealed class DiagnosticsReportTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [WindowsFact]
    public async Task Builds_a_report_for_the_default_account_and_names_the_written_file()
    {
        var services = PlatformServices.Create() with { Paths = new(_temp.Path, PlatformServices.Create().Info.PathStyle) };
        var accounts = DefaultConfigFactory.Create().Accounts;

        var report = await DiagnosticsReport.BuildAsync(services, accounts, Ct);

        report.ShouldContain("MCACC diagnose");
        report.ShouldContain("== System ==");
        report.ShouldContain("== Claude Desktop ==");
        report.ShouldContain("== Terminals ==");
        report.ShouldContain("== Claude Code CLI ==");
        report.ShouldContain("== Running instances ==");
        report.ShouldContain("== Accounts ==");
        report.ShouldContain(Account.DefaultId);
        report.ShouldNotContain("sk-ant");
        report.ShouldContain(_temp.Path);

        var file = Path.Join(_temp.Path, "diagnose.txt");
        await File.WriteAllTextAsync(file, report, Ct);
        TestContext.Current.TestOutputHelper?.WriteLine($"diagnose written to {file}");
    }
}
