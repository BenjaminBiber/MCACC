using Mcacc.Core.Abstractions;
using Mcacc.Core.Desktop;
using Mcacc.Core.Logging;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;
using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopLoginRedirectorTests
{
    private const string LoginUrl = "claude://login/google-auth?code=4%2F0AXlqoi45&hop_nonce=1BkdtqPC";
    private const string PrivatDesktop = @"C:\Users\USER\.mcacc\profiles\privat\desktop";

    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private static readonly PlatformInfo Windows =
        new(OsKind.Windows, @"C:\Users\USER", @"C:\Users\USER\AppData\Roaming", @"C:\Users\USER\AppData\Local");

    private static readonly PlatformInfo Mac = new(OsKind.MacOS, "/Users/USER", "", "");

    private static readonly DesktopInstallation Msix =
        new(DesktopInstallKind.Msix, @"C:\Users\USER\AppData\Local\Microsoft\WindowsApps\claude-desktop.exe");

    private static readonly DesktopInstallation Bundle = new(DesktopInstallKind.MacBundle, "/Applications/Claude.app");

    private static readonly DateTimeOffset Start = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The instance MCACC started for the account, before any login left for the browser.</summary>
    private static readonly DesktopInstance TargetInstance = new(200, Start, PrivatDesktop, [200]);

    /// <summary>What the protocol handler starts: the login URL, and no profile of its own.</summary>
    private static readonly DesktopInstance StrayLogin = new(300, Start, UserDataDir: null, [300], LoginUrl);

    private readonly FakeTimeProvider _time = new(Start, TimeZoneInfo.Utc);
    private readonly FakeDetector _detector = new();
    private readonly FakeTerminator _terminator = new();
    private readonly FakeProcessRunner _runner = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TimeSpan Elapsed => _time.GetUtcNow() - Start;

    private static AccountPathResolver ResolverFor(PlatformInfo platform) =>
        new(McaccPaths.FromEnvironment(platform, _ => null), platform);

    private DesktopLoginRedirector Redirector(PlatformInfo? platform = null, bool installed = true)
    {
        platform ??= Windows;
        DesktopInstallation? installation = !installed ? null : platform.Os == OsKind.Windows ? Msix : Bundle;
        return new DesktopLoginRedirector(
            new FakeLocator(installation),
            _detector,
            _terminator,
            _runner,
            ResolverFor(platform),
            platform,
            NullLog.Instance,
            _time);
    }

    [Fact]
    public async Task The_default_account_needs_no_watch_because_a_stray_login_lands_in_its_profile()
    {
        var outcome = await Redirector().WatchAsync(Standard, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.NotNeeded);
        _detector.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Without_an_installation_there_is_nothing_to_hand_the_login_to()
    {
        var outcome = await Redirector(installed: false).WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.NotNeeded);
        _detector.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task A_login_started_without_a_profile_is_quit_at_once_and_handed_to_the_account()
    {
        _detector.Script([TargetInstance], [TargetInstance, StrayLogin]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.Redirected);
        _terminator.Quits.ShouldBe([(StrayLogin, TimeSpan.Zero)]);
        _runner.Started.ShouldHaveSingleItem().ShouldBeEquivalentTo(
            DesktopLaunchPlanner.PlanWindows(Msix, ResolverFor(Windows).Resolve(Privat), Windows.HomeDir, LoginUrl));
    }

    [Fact]
    public async Task A_login_started_with_another_profile_counts_as_stray_too()
    {
        var otherProfile = new DesktopInstance(300, Start, @"C:\Users\USER\.mcacc\profiles\arbeit\desktop", [300], LoginUrl);
        _detector.Script([TargetInstance, otherProfile]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.Redirected);
        _terminator.Quits.ShouldBe([(otherProfile, TimeSpan.Zero)]);
    }

    [Fact]
    public async Task A_login_the_account_profile_gets_by_itself_is_left_alone()
    {
        var ownLogin = new DesktopInstance(300, Start, PrivatDesktop, [300], LoginUrl);
        _detector.Script([TargetInstance, ownLogin]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.NotSeen);
        _terminator.Quits.ShouldBeEmpty();
        _runner.Started.ShouldBeEmpty();
        Elapsed.ShouldBe(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task An_instance_that_already_ran_before_the_watch_is_not_touched()
    {
        var older = new DesktopInstance(300, Start.AddSeconds(-30), UserDataDir: null, [300], LoginUrl);
        _detector.Script([TargetInstance, older]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.NotSeen);
        _terminator.Quits.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_instance_of_unknown_age_is_taken_along_rather_than_letting_the_login_slip()
    {
        var unknownAge = new DesktopInstance(300, StartTime: null, UserDataDir: null, [300], LoginUrl);
        _detector.Script([TargetInstance, unknownAge]);

        (await Redirector().WatchAsync(Privat, Ct)).ShouldBe(DesktopRedirectOutcome.Redirected);
    }

    [Fact]
    public async Task Polling_runs_every_250_ms_until_the_login_shows_up()
    {
        _detector.Script([TargetInstance], [TargetInstance], [TargetInstance], [TargetInstance, StrayLogin]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.Redirected);
        _detector.Calls.ShouldBe(4);
        Elapsed.ShouldBe(TimeSpan.FromMilliseconds(4 * 250));
    }

    [Fact]
    public async Task Closing_claude_desktop_ends_the_watch_early()
    {
        _detector.Script([TargetInstance], [TargetInstance], []);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.NotSeen);
        _detector.Calls.ShouldBe(3);
        Elapsed.ShouldBe(TimeSpan.FromMilliseconds(3 * 250));
    }

    [Fact]
    public async Task A_target_that_never_shows_up_does_not_end_the_watch()
    {
        _detector.Script([]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.NotSeen);
        Elapsed.ShouldBe(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task The_watch_gives_up_after_three_minutes()
    {
        _detector.Script([TargetInstance]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.NotSeen);
        _detector.Calls.ShouldBe((int)(TimeSpan.FromMinutes(3) / TimeSpan.FromMilliseconds(250)));
        _runner.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_snapshot_that_fails_is_skipped_and_the_next_one_still_catches_the_login()
    {
        _detector.FailNext = 1;
        _detector.Script([TargetInstance, StrayLogin]);

        (await Redirector().WatchAsync(Privat, Ct)).ShouldBe(DesktopRedirectOutcome.Redirected);
        _detector.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task A_start_that_fails_leaves_the_login_unredeemed_and_says_so()
    {
        _runner.OnStart = _ => throw new LaunchException("claude-desktop.exe did not start.");
        _detector.Script([TargetInstance, StrayLogin]);

        var outcome = await Redirector().WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.Failed);
        _terminator.Quits.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_mac_plan_carries_the_login_url_behind_its_args_switch()
    {
        var macTarget = new DesktopInstance(200, Start, "/Users/USER/.mcacc/profiles/privat/desktop", [200]);
        _detector.Script([macTarget, StrayLogin]);

        var outcome = await Redirector(Mac).WatchAsync(Privat, Ct);

        outcome.ShouldBe(DesktopRedirectOutcome.Redirected);
        _runner.Started.ShouldHaveSingleItem().ShouldBeEquivalentTo(
            DesktopLaunchPlanner.PlanMac(Bundle, ResolverFor(Mac).Resolve(Privat), LoginUrl));
    }

    [Fact]
    public async Task A_canceled_watch_hands_nothing_over()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _detector.Script([TargetInstance, StrayLogin]);

        await Should.ThrowAsync<OperationCanceledException>(() => Redirector().WatchAsync(Privat, cts.Token));

        _runner.Started.ShouldBeEmpty();
    }

    private sealed class FakeLocator(DesktopInstallation? installation) : IDesktopInstallationLocator
    {
        public DesktopInstallation? Locate() => installation;
    }

    /// <summary>Each detection returns the next scripted snapshot; the last one repeats.</summary>
    private sealed class FakeDetector : IDesktopInstanceDetector
    {
        private readonly Queue<IReadOnlyList<DesktopInstance>> _snapshots = new();
        private IReadOnlyList<DesktopInstance> _current = [];

        public int Calls { get; private set; }

        /// <summary>How many of the next detections throw the way a WMI timeout does.</summary>
        public int FailNext { get; set; }

        public void Script(params IReadOnlyList<DesktopInstance>[] snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                _snapshots.Enqueue(snapshot);
            }
        }

        public Task<IReadOnlyList<DesktopInstance>> DetectAsync(CancellationToken ct)
        {
            Calls++;
            if (FailNext > 0)
            {
                FailNext--;
                throw new TimeoutException("WMI did not answer within three seconds.");
            }

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

        public Task<QuitResult> QuitAsync(DesktopInstance instance, TimeSpan grace, CancellationToken ct)
        {
            Quits.Add((instance, grace));
            return Task.FromResult(QuitResult.Killed);
        }
    }
}
