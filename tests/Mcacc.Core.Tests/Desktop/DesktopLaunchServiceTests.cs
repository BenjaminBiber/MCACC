using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopLaunchServiceTests
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);
    private static readonly Account Arbeit = new("arbeit", "Arbeit", "#788C5D", IsDefault: false);
    private static readonly IReadOnlyList<Account> Accounts = [Standard, Privat, Arbeit];

    private static readonly PlatformInfo Windows =
        new(OsKind.Windows, @"C:\Users\USER", @"C:\Users\USER\AppData\Roaming", @"C:\Users\USER\AppData\Local");

    private static readonly PlatformInfo Mac = new(OsKind.MacOS, "/Users/USER", "", "");

    private static readonly DesktopInstallation Msix =
        new(DesktopInstallKind.Msix, @"C:\Users\USER\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe");

    private static readonly DesktopInstallation Bundle = new(DesktopInstallKind.MacBundle, "/Applications/Claude.app");

    private static readonly DesktopInstance StandardInstance = Instance(100, userDataDir: null);
    private static readonly DesktopInstance PrivatInstance = Instance(200, @"C:\Users\USER\.mcacc\profiles\privat\desktop");
    private static readonly DesktopInstance UnknownInstance = Instance(300, @"C:\Users\USER\.mcacc\profiles\geloescht\desktop");

    private static readonly DateTimeOffset Start = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start, TimeZoneInfo.Utc);
    private readonly FakeDetector _detector = new();
    private readonly FakeTerminator _terminator = new();
    private readonly FakeProcessRunner _runner = new();
    private DateTimeOffset? _startedAt;
    private bool _allowParallel;

    public DesktopLaunchServiceTests()
    {
        _runner.OnStart = _ => _startedAt = _time.GetUtcNow();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DesktopInstance Instance(int mainPid, string? userDataDir) =>
        new(mainPid, StartTime: null, userDataDir, [mainPid, mainPid + 1]);

    private static AccountPathResolver ResolverFor(PlatformInfo platform) =>
        new(McaccPaths.FromEnvironment(platform, _ => null), platform);

    private DesktopLaunchService Service(PlatformInfo? platform = null, bool installed = true)
    {
        platform ??= Windows;
        var resolver = ResolverFor(platform);
        DesktopInstallation? installation = !installed ? null : platform.Os == OsKind.Windows ? Msix : Bundle;
        return new DesktopLaunchService(
            new FakeLocator(installation),
            _detector,
            _terminator,
            _runner,
            resolver,
            new DesktopInstanceMatcher(resolver, platform.PathStyle),
            platform,
            _time,
            () => _allowParallel);
    }

    private TimeSpan Elapsed => _time.GetUtcNow() - Start;

    [Fact]
    public async Task Prepare_reports_not_installed_without_looking_for_instances()
    {
        _detector.Script([StandardInstance]);

        var check = await Service(installed: false).PrepareAsync(Privat, Accounts, Ct);

        check.ShouldBe(new DesktopLaunchCheck.NotInstalled());
        _detector.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Prepare_is_ready_when_no_instance_runs()
    {
        (await Service().PrepareAsync(Privat, Accounts, Ct)).ShouldBe(new DesktopLaunchCheck.Ready());
    }

    [Fact]
    public async Task Prepare_reports_the_same_account_running()
    {
        _detector.Script([PrivatInstance]);

        (await Service().PrepareAsync(Privat, Accounts, Ct)).ShouldBe(new DesktopLaunchCheck.RunningSameAccount());
    }

    [Fact]
    public async Task Prepare_matches_the_target_by_its_id()
    {
        _detector.Script([PrivatInstance]);

        var renamed = Privat with { Name = "Privat neu", Color = "#788C5D" };

        (await Service().PrepareAsync(renamed, Accounts, Ct)).ShouldBe(new DesktopLaunchCheck.RunningSameAccount());
    }

    [Fact]
    public async Task Prepare_reports_an_instance_of_another_account_as_a_conflict_with_its_owner()
    {
        _detector.Script([StandardInstance]);

        var check = await Service().PrepareAsync(Privat, Accounts, Ct);

        check.ShouldBeOfType<DesktopLaunchCheck.Conflict>().Conflicts.ShouldBe([new DesktopConflict(StandardInstance, Standard)]);
    }

    [Fact]
    public async Task Prepare_reports_an_instance_of_an_unknown_profile_as_a_conflict_without_owner()
    {
        _detector.Script([UnknownInstance]);

        var check = await Service().PrepareAsync(Standard, Accounts, Ct);

        check.ShouldBeOfType<DesktopLaunchCheck.Conflict>().Conflicts.ShouldBe([new DesktopConflict(UnknownInstance, null)]);
    }

    [Fact]
    public async Task Prepare_lists_only_the_other_instances_when_the_target_runs_too()
    {
        _detector.Script([StandardInstance, PrivatInstance, UnknownInstance]);

        var check = await Service().PrepareAsync(Privat, Accounts, Ct);

        check.ShouldBeOfType<DesktopLaunchCheck.Conflict>().Conflicts.ShouldBe(
            [new DesktopConflict(StandardInstance, Standard), new DesktopConflict(UnknownInstance, null)]);
    }

    [Fact]
    public async Task Launch_reports_not_installed_and_does_nothing()
    {
        _detector.Script([StandardInstance]);

        var outcome = await Service(installed: false).LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.NotInstalled);
        _detector.Calls.ShouldBe(0);
        _terminator.Quits.ShouldBeEmpty();
        _runner.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task Launch_starts_the_planned_process_once_after_500_ms_when_nothing_runs()
    {
        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: false, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _runner.Started.ShouldHaveSingleItem().ShouldBeEquivalentTo(
            DesktopLaunchPlanner.PlanWindows(Msix, ResolverFor(Windows).Resolve(Privat), Windows.HomeDir));
        (_startedAt - Start).ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task Launch_starts_the_mac_plan_on_macos()
    {
        var outcome = await Service(Mac).LaunchAsync(Standard, Accounts, quitConflicts: false, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _runner.Started.ShouldHaveSingleItem().ShouldBeEquivalentTo(
            DesktopLaunchPlanner.PlanMac(Bundle, ResolverFor(Mac).Resolve(Standard)));
    }

    [Fact]
    public async Task Launch_starts_without_quitting_when_only_the_same_account_runs()
    {
        _detector.Script([PrivatInstance]);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _terminator.Quits.ShouldBeEmpty();
        _runner.Started.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Launch_without_permission_to_quit_leaves_a_conflict_alone_and_starts_nothing()
    {
        _detector.Script([StandardInstance]);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: false, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.ConflictNotResolved);
        _terminator.Quits.ShouldBeEmpty();
        _runner.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task Launch_quits_only_the_conflicting_instances_with_4_s_grace_on_windows_then_starts_once()
    {
        _detector.Script([StandardInstance, PrivatInstance, UnknownInstance], [PrivatInstance]);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _terminator.Quits.ShouldBe([(StandardInstance, TimeSpan.FromSeconds(4)), (UnknownInstance, TimeSpan.FromSeconds(4))]);
        _runner.Started.Count.ShouldBe(1);
        (_startedAt - Start).ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task Launch_gives_a_mac_instance_8_s_to_quit()
    {
        var macPrivat = Instance(200, "/Users/USER/.mcacc/profiles/privat/desktop");
        _detector.Script([macPrivat], []);

        var outcome = await Service(Mac).LaunchAsync(Standard, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _terminator.Quits.ShouldBe([(macPrivat, TimeSpan.FromSeconds(8))]);
    }

    [Fact]
    public async Task Launch_detects_again_and_quits_nothing_when_the_conflict_has_gone_since_prepare()
    {
        _detector.Script([StandardInstance], []);
        var service = Service();

        (await service.PrepareAsync(Privat, Accounts, Ct)).ShouldBeOfType<DesktopLaunchCheck.Conflict>();
        var outcome = await service.LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _terminator.Quits.ShouldBeEmpty();
    }

    [Fact]
    public async Task Launch_polls_every_250_ms_until_the_quit_instance_is_gone()
    {
        _detector.Script([StandardInstance], [StandardInstance], [StandardInstance], []);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _detector.Calls.ShouldBe(4);
        (_startedAt - Start).ShouldBe(TimeSpan.FromMilliseconds(250 + 250 + 500));
    }

    [Theory]
    [InlineData(QuitResult.Exited)]
    [InlineData(QuitResult.Killed)]
    [InlineData(QuitResult.Failed)]
    public async Task Launch_fails_without_starting_when_a_conflict_still_runs_after_5_s(QuitResult result)
    {
        _terminator.Result = result;
        _detector.Script([StandardInstance]);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.QuitFailed);
        _runner.Started.ShouldBeEmpty();
        Elapsed.ShouldBe(TimeSpan.FromSeconds(5));
        _detector.Calls.ShouldBe(1 + 21);
    }

    [Fact]
    public async Task Launch_trusts_detection_over_a_failed_quit()
    {
        _terminator.Result = QuitResult.Failed;
        _detector.Script([StandardInstance], []);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
    }

    [Fact]
    public async Task Launch_reports_start_failed_when_the_runner_throws()
    {
        _runner.OnStart = _ => throw new LaunchException("claude-desktop.exe did not start.");

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.StartFailed);
        _runner.Started.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Launch_that_is_canceled_starts_nothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => Service().LaunchAsync(Privat, Accounts, quitConflicts: true, cts.Token));

        _runner.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task Launch_on_linux_is_not_supported()
    {
        var linux = new PlatformInfo(OsKind.Linux, "/home/user", "", "");
        _detector.Script([UnknownInstance]);

        await Should.ThrowAsync<PlatformNotSupportedException>(
            () => Service(linux).LaunchAsync(Privat, Accounts, quitConflicts: true, Ct));

        _terminator.Quits.ShouldBeEmpty();
        _runner.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task Side_by_side_prepare_is_ready_although_other_accounts_run()
    {
        _allowParallel = true;
        _detector.Script([StandardInstance, UnknownInstance]);

        (await Service().PrepareAsync(Privat, Accounts, Ct)).ShouldBe(new DesktopLaunchCheck.Ready());
    }

    [Fact]
    public async Task Side_by_side_prepare_still_reports_the_same_account_running()
    {
        _allowParallel = true;
        _detector.Script([StandardInstance, PrivatInstance]);

        (await Service().PrepareAsync(Privat, Accounts, Ct)).ShouldBe(new DesktopLaunchCheck.RunningSameAccount());
    }

    [Fact]
    public async Task Side_by_side_launch_starts_next_to_another_account_and_quits_nothing()
    {
        _allowParallel = true;
        _detector.Script([StandardInstance]);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: false, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _terminator.Quits.ShouldBeEmpty();
        _runner.Started.ShouldHaveSingleItem().ShouldBeEquivalentTo(
            DesktopLaunchPlanner.PlanWindows(Msix, ResolverFor(Windows).Resolve(Privat), Windows.HomeDir));
        (_startedAt - Start).ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task Side_by_side_launch_quits_nothing_even_with_permission_and_does_not_wait_on_a_detection()
    {
        _allowParallel = true;
        _detector.Script([StandardInstance]);

        var outcome = await Service().LaunchAsync(Privat, Accounts, quitConflicts: true, Ct);

        outcome.ShouldBe(DesktopLaunchOutcome.Launched);
        _terminator.Quits.ShouldBeEmpty();
        _detector.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task The_switch_is_asked_at_every_start()
    {
        _detector.Script([StandardInstance]);
        var service = Service();

        (await service.LaunchAsync(Privat, Accounts, quitConflicts: false, Ct))
            .ShouldBe(DesktopLaunchOutcome.ConflictNotResolved);

        _allowParallel = true;

        (await service.LaunchAsync(Privat, Accounts, quitConflicts: false, Ct)).ShouldBe(DesktopLaunchOutcome.Launched);
    }

    private sealed class FakeLocator(DesktopInstallation? installation) : IDesktopInstallationLocator
    {
        public DesktopInstallation? Locate(ProviderDescriptor provider) => installation;
    }

    /// <summary>Each detection returns the next scripted snapshot; the last one repeats. Unscripted, nothing runs.</summary>
    private sealed class FakeDetector : IDesktopInstanceDetector
    {
        private readonly Queue<IReadOnlyList<DesktopInstance>> _snapshots = new();
        private IReadOnlyList<DesktopInstance> _current = [];

        public int Calls { get; private set; }

        public void Script(params IReadOnlyList<DesktopInstance>[] snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                _snapshots.Enqueue(snapshot);
            }
        }

        public Task<IReadOnlyList<DesktopInstance>> DetectAsync(ProviderDescriptor provider, CancellationToken ct)
        {
            Calls++;
            if (_snapshots.TryDequeue(out var next))
            {
                _current = next;
            }

            return Task.FromResult(_current);
        }
    }

    private sealed class FakeTerminator : IDesktopInstanceTerminator
    {
        public List<(DesktopInstance Instance, TimeSpan Grace)> Quits { get; } = [];

        public QuitResult Result { get; set; } = QuitResult.Exited;

        public Task<QuitResult> QuitAsync(DesktopInstance instance, TimeSpan grace, CancellationToken ct)
        {
            Quits.Add((instance, grace));
            return Task.FromResult(Result);
        }
    }
}
