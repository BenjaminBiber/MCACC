using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopInstanceDetectorTests
{
    private static PlatformInfo Platform(OsKind os) => new(os, "/home", "", "");

    [Fact]
    public async Task Classifies_the_snapshot_of_the_provider_with_the_given_rules()
    {
        var provider = new FakeProcessSnapshotProvider(WmiCsvFixture.Load("wmi-default.csv"));
        var detector = new DesktopInstanceDetector(provider, Platform(OsKind.Windows));
        var ct = TestContext.Current.CancellationToken;

        var instances = await detector.DetectAsync(ProviderDescriptors.Claude, ct);

        var instance = instances.ShouldHaveSingleItem();
        instance.MainPid.ShouldBe(14820);
        instance.ProcessIds.ShouldBe([14820, 15032, 15480, 15512, 16204]);
        provider.ReceivedTokens.ShouldBe([ct]);
    }

    [Fact]
    public async Task Rules_of_another_os_find_no_instance_in_the_same_snapshot()
    {
        var provider = new FakeProcessSnapshotProvider(WmiCsvFixture.Load("wmi-default.csv"));
        var detector = new DesktopInstanceDetector(provider, Platform(OsKind.MacOS));

        (await detector.DetectAsync(ProviderDescriptors.Claude, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    private sealed class FakeProcessSnapshotProvider(IReadOnlyList<ProcessInfo> processes) : IProcessSnapshotProvider
    {
        public List<CancellationToken> ReceivedTokens { get; } = [];

        public Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(ProviderDescriptor provider, CancellationToken ct)
        {
            ReceivedTokens.Add(ct);
            return Task.FromResult(processes);
        }
    }
}
