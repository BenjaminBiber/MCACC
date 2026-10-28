using Mcacc.Core.Desktop;
using Mcacc.Core.Model;
using Mcacc.Core.Processes;
using Mcacc.Core.Tests.TestSupport;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Desktop;

public class DesktopProcessClassifierTests
{
    private const string MsixExe = @"C:\Program Files\WindowsApps\Claude_2.7032.0.0_x64__pzs8sxrjxfjjc\app\Claude.exe";
    private const string SquirrelStub = @"C:\Users\USER\AppData\Local\AnthropicClaude\claude.exe";
    private const string SquirrelExe = @"C:\Users\USER\AppData\Local\AnthropicClaude\app-0.14.10\claude.exe";
    private const string CliExe = @"C:\Users\USER\AppData\Roaming\Claude\claude-code\2.1.280\claude.exe";
    private const string RoamingClaude = @"C:\Users\USER\AppData\Roaming\Claude";
    private const string PrivatDesktop = @"C:\Users\USER\.mcacc\profiles\privat\desktop";
    private const string ArbeitDesktop = @"C:\Users\USER\.mcacc\profiles\arbeit\desktop";

    private static readonly DateTimeOffset Started = new(2026, 9, 23, 10, 15, 2, TimeSpan.FromHours(2));
    private static readonly DesktopProcessRules Windows = DesktopProcessRules.For(OsKind.Windows, ProviderDescriptors.Claude);

    private static ProcessInfo MainProcess(int pid, string arguments = "", string exe = MsixExe, int parentPid = 5316) =>
        new(pid, parentPid, exe, $"\"{exe}\"{arguments}", Started);

    private static ProcessInfo ChildProcess(int pid, int parentPid, string arguments, string exe = MsixExe) =>
        new(pid, parentPid, exe, $"\"{exe}\" {arguments}", Started.AddSeconds(1));

    private static string Renderer(string userDataDir) => $"--type=renderer --user-data-dir=\"{userDataDir}\" --lang=de";

    [Fact]
    public void Default_snapshot_has_one_instance_that_takes_its_user_data_dir_from_a_child_and_excludes_cli_processes()
    {
        var instance = DesktopProcessClassifier.Classify(WmiCsvFixture.Load("wmi-default.csv"), Windows).ShouldHaveSingleItem();

        instance.MainPid.ShouldBe(14820);
        instance.StartTime.ShouldBe(Started);
        instance.UserDataDir.ShouldBe(RoamingClaude);
        instance.ProcessIds.ShouldBe([14820, 15032, 15480, 15512, 16204]);
    }

    [Fact]
    public void Flag_on_the_main_process_wins_over_the_children()
    {
        var processes = new[]
        {
            MainProcess(100, $" --user-data-dir={PrivatDesktop}"),
            ChildProcess(110, 100, Renderer(RoamingClaude)),
        };

        DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem().UserDataDir.ShouldBe(PrivatDesktop);
    }

    [Fact]
    public void Without_a_flag_on_the_main_process_the_lowest_descendant_pid_carrying_one_wins()
    {
        var processes = new[]
        {
            MainProcess(100),
            ChildProcess(300, 100, Renderer(ArbeitDesktop)),
            ChildProcess(150, 100, "--type=gpu-process"),
            ChildProcess(200, 100, Renderer(PrivatDesktop)),
        };

        DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem().UserDataDir.ShouldBe(PrivatDesktop);
    }

    [Fact]
    public void Without_any_flag_the_user_data_dir_is_null()
    {
        var processes = new[] { MainProcess(100), ChildProcess(110, 100, "--type=gpu-process") };

        DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem().UserDataDir.ShouldBeNull();
    }

    [Fact]
    public void Descendants_are_transitive_but_never_run_through_a_cli_process()
    {
        var processes = new[]
        {
            MainProcess(100),
            ChildProcess(200, 100, "--type=gpu-process"),
            ChildProcess(210, 200, "--type=utility"),
            new ProcessInfo(300, 100, CliExe, $"\"{CliExe}\" --output-format stream-json", Started),
            ChildProcess(310, 300, Renderer(PrivatDesktop)),
        };

        var instance = DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem();

        instance.ProcessIds.ShouldBe([100, 200, 210]);
        instance.UserDataDir.ShouldBeNull();
    }

    [Fact]
    public void Two_instances_are_ordered_by_main_pid_and_keep_their_own_processes()
    {
        var processes = new[]
        {
            MainProcess(200, $" --user-data-dir={PrivatDesktop}"),
            ChildProcess(220, 200, Renderer(PrivatDesktop)),
            ChildProcess(110, 100, Renderer(RoamingClaude)),
            MainProcess(100),
            ChildProcess(210, 200, "--type=gpu-process"),
            ChildProcess(120, 100, "--type=gpu-process"),
        };

        var instances = DesktopProcessClassifier.Classify(processes, Windows);

        instances.Select(i => i.MainPid).ShouldBe([100, 200]);
        instances[0].ProcessIds.ShouldBe([100, 110, 120]);
        instances[0].UserDataDir.ShouldBe(RoamingClaude);
        instances[1].ProcessIds.ShouldBe([200, 210, 220]);
        instances[1].UserDataDir.ShouldBe(PrivatDesktop);
    }

    [Fact]
    public void Squirrel_instance_is_recognised_without_its_update_stub()
    {
        var processes = new[]
        {
            new ProcessInfo(50, 5316, SquirrelStub, $"\"{SquirrelStub}\"", Started),
            MainProcess(100, exe: SquirrelExe, parentPid: 50),
            ChildProcess(110, 100, "--type=crashpad-handler --user-data-dir=C:\\Users\\USER\\AppData\\Roaming\\Claude /prefetch:4", SquirrelExe),
            ChildProcess(120, 100, Renderer(RoamingClaude), SquirrelExe),
        };

        var instance = DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem();

        instance.MainPid.ShouldBe(100);
        instance.ProcessIds.ShouldBe([100, 110, 120]);
        instance.UserDataDir.ShouldBe(RoamingClaude);
    }

    [Fact]
    public void Children_whose_main_is_gone_are_ignored()
    {
        var processes = new[]
        {
            ChildProcess(110, 999, Renderer(RoamingClaude)),
            ChildProcess(120, 999, "--type=gpu-process"),
            MainProcess(200),
            ChildProcess(210, 200, "--type=gpu-process"),
        };

        var instance = DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem();

        instance.ProcessIds.ShouldBe([200, 210]);
        instance.UserDataDir.ShouldBeNull();
    }

    [Fact]
    public void Only_children_give_no_instance()
    {
        var processes = new[] { ChildProcess(110, 999, Renderer(RoamingClaude)), ChildProcess(120, 999, "--type=gpu-process") };

        DesktopProcessClassifier.Classify(processes, Windows).ShouldBeEmpty();
    }

    [Fact]
    public void A_main_started_by_another_main_is_its_own_instance()
    {
        var processes = new[]
        {
            MainProcess(100),
            MainProcess(200, $" --user-data-dir={PrivatDesktop}", parentPid: 100),
            ChildProcess(210, 200, Renderer(PrivatDesktop)),
        };

        var instances = DesktopProcessClassifier.Classify(processes, Windows);

        instances.Select(i => i.MainPid).ShouldBe([100, 200]);
        instances[0].ProcessIds.ShouldBe([100]);
        instances[0].UserDataDir.ShouldBeNull();
        instances[1].ProcessIds.ShouldBe([200, 210]);
    }

    [Fact]
    public void A_stale_parent_pid_pointing_back_into_the_tree_does_not_loop()
    {
        var processes = new[] { MainProcess(100, parentPid: 110), ChildProcess(110, 100, Renderer(RoamingClaude)) };

        DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem().ProcessIds.ShouldBe([100, 110]);
    }

    [Fact]
    public void Deep_link_on_the_main_process_is_kept_with_the_instance()
    {
        var processes = new[]
        {
            MainProcess(100, " \"claude://login/google-auth?code=4%2F0AXlqoi45\""),
            ChildProcess(110, 100, Renderer(RoamingClaude)),
        };

        var instance = DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem();

        instance.DeepLinkUrl.ShouldBe("claude://login/google-auth?code=4%2F0AXlqoi45");
        instance.UserDataDir.ShouldBe(RoamingClaude);
    }

    [Fact]
    public void Deep_link_of_a_child_is_not_taken_for_the_instance()
    {
        var processes = new[]
        {
            MainProcess(100, $" --user-data-dir={PrivatDesktop}"),
            ChildProcess(110, 100, $"{Renderer(PrivatDesktop)} --url=claude://login/google-auth"),
        };

        DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem().DeepLinkUrl.ShouldBeNull();
    }

    [Fact]
    public void Ordinary_start_carries_no_deep_link()
    {
        var processes = new[] { MainProcess(100, $" --user-data-dir={ArbeitDesktop}") };

        DesktopProcessClassifier.Classify(processes, Windows).ShouldHaveSingleItem().DeepLinkUrl.ShouldBeNull();
    }

    [Fact]
    public void Mac_profile_snapshot_has_two_instances_with_their_helpers()
    {
        var processes = PsOutputParser.Parse(FixtureFiles.ReadAllText("ps-profile.txt"));

        var instances = DesktopProcessClassifier.Classify(processes, DesktopProcessRules.For(OsKind.MacOS, ProviderDescriptors.Claude));

        instances.Select(i => i.MainPid).ShouldBe([812, 1204]);
        instances[0].ProcessIds.ShouldBe([812, 815, 818, 820, 824]);
        instances[0].UserDataDir.ShouldBe("/Users/USER/.mcacc/profiles/privat/desktop");
        instances[0].StartTime.ShouldBe(new DateTimeOffset(new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Local)));
        instances[1].ProcessIds.ShouldBe([1204, 1207, 1210]);
        instances[1].UserDataDir.ShouldBe("/Users/USER/desk top ä");
    }
}
