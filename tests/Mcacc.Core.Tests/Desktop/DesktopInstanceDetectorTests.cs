using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopInstanceDetectorTests
{
    [Fact]
    public async Task Classifies_the_snapshot_of_the_provider_with_the_given_rules()
    {
        var provider = new FakeProcessSnapshotProvider(WmiCsvFixture.Load("wmi-default.csv"));
        var detector = new DesktopInstanceDetector(provider, DesktopProcessRules.ForOs(OsKind.Windows));
        var ct = TestContext.Current.CancellationToken;

        var instances = await detector.DetectAsync(ct);

        var instance = instances.ShouldHaveSingleItem();
        instance.MainPid.ShouldBe(14820);
        instance.ProcessIds.ShouldBe([14820, 15032, 15480, 15512, 16204]);
        provider.ReceivedTokens.ShouldBe([ct]);
    }

    [Fact]
    public async Task Rules_of_another_os_find_no_instance_in_the_same_snapshot()
    {
        var provider = new FakeProcessSnapshotProvider(WmiCsvFixture.Load("wmi-default.csv"));
        var detector = new DesktopInstanceDetector(provider, DesktopProcessRules.ForOs(OsKind.MacOS));

        (await detector.DetectAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    private sealed class FakeProcessSnapshotProvider(IReadOnlyList<ProcessInfo> processes) : IProcessSnapshotProvider
    {
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task<IReadOnlyList<ProcessInfo>> GetClaudeProcessesAsync(CancellationToken ct)
        {
            ReceivedTokens.Add(ct);
            return Task.FromResult(processes);
        }
    }
}
