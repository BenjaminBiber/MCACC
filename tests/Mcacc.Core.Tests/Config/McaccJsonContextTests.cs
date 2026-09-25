using System.Text.Json;
using Mcacc.Core.Config;
using Mcacc.Core.Model;
using Shouldly;
using Xunit;

namespace Mcacc.Core.Tests.Config;

public class McaccJsonContextTests
{
    private const string FullJson = """
        {
          "version": 1,
          "accounts": [
            {
              "id": "standard",
              "name": "Standard",
              "color": "#D97757",
              "isDefault": true
            },
            {
              "id": "privat",
              "name": "Privat",
              "color": "#6A9BCC",
              "isDefault": false
            }
          ],
          "settings": {
            "terminal": "windows-terminal",
            "windowsTerminalShell": "windowsPowerShell",
            "closeAfterLaunch": false,
            "lastAccountId": "privat",
            "lastTarget": "desktop",
            "launchAtLogin": false
          }
        }
        """;

    private static readonly AppConfig FullConfig = new(
        1,
        [Account.CreateDefault(), new Account("privat", "Privat", "#6A9BCC", IsDefault: false)],
        new AppSettings(
            Terminal: TerminalIds.WindowsTerminal,
            WindowsTerminalShell: WindowsTerminalShell.WindowsPowerShell,
            CloseAfterLaunch: false,
            LastAccountId: "privat",
            LastTarget: LaunchTarget.Desktop,
            LaunchAtLogin: false));

    private static string Serialize(AppConfig config) =>
        JsonSerializer.Serialize(config, McaccJsonContext.Default.AppConfig);

    private static AppConfig Deserialize(string json) =>
        JsonSerializer.Deserialize(json, McaccJsonContext.Default.AppConfig).ShouldNotBeNull();

    [Fact]
    public void Serializes_camel_case_properties_and_camel_case_enum_strings_indented_with_lf()
    {
        Serialize(FullConfig).ShouldBe(FullJson);
    }

    [Fact]
    public void Omits_null_values()
    {
        Serialize(DefaultConfigFactory.Create()).ShouldBe("""
            {
              "version": 1,
              "accounts": [
                {
                  "id": "standard",
                  "name": "Standard",
                  "color": "#D97757",
                  "isDefault": true
                }
              ],
              "settings": {
                "windowsTerminalShell": "auto",
                "closeAfterLaunch": true,
                "launchAtLogin": true
              }
            }
            """);
    }

    [Fact]
    public void Deserializes_what_it_serializes()
    {
        var config = Deserialize(FullJson);

        config.Version.ShouldBe(1);
        config.Accounts.ShouldBe(FullConfig.Accounts);
        config.Settings.ShouldBe(FullConfig.Settings);
        config.Extra.ShouldBeNull();
    }

    [Fact]
    public void Missing_settings_fields_take_their_defaults()
    {
        var config = Deserialize("""{ "version": 1, "accounts": [], "settings": {} }""");

        config.Settings.ShouldBe(new AppSettings());
    }

    [Fact]
    public void Excluded_projects_survive_a_round_trip()
    {
        var plain = DefaultConfigFactory.Create();
        var config = plain with { Settings = plain.Settings with { ExcludedProjects = [@"C:\Repos\Spielwiese", "/tmp/x"] } };

        var json = Serialize(config);

        json.ShouldContain("\"excludedProjects\": [");
        Deserialize(json).Settings.ExcludedProjects.ShouldBe([@"C:\Repos\Spielwiese", "/tmp/x"]);
    }

    [Fact]
    public void A_config_without_excluded_projects_loads_none()
    {
        Deserialize(FullJson).Settings.ExcludedProjects.ShouldBeNull();
    }

    [Fact]
    public void A_config_without_launch_at_login_starts_at_login()
    {
        Deserialize("""{ "version": 1, "accounts": [], "settings": { "closeAfterLaunch": false } }""")
            .Settings.LaunchAtLogin.ShouldBeTrue();
    }

    [Fact]
    public void Unknown_top_level_fields_survive_a_round_trip()
    {
        var config = Deserialize("""
            { "version": 1, "accounts": [], "settings": {}, "future": { "x": [1, 2] }, "flag": true }
            """);

        config.Extra.ShouldNotBeNull().Keys.ShouldBe(["future", "flag"]);
        Serialize(config).ShouldBe("""
            {
              "version": 1,
              "accounts": [],
              "settings": {
                "windowsTerminalShell": "auto",
                "closeAfterLaunch": true,
                "launchAtLogin": true
              },
              "future": {
                "x": [
                  1,
                  2
                ]
              },
              "flag": true
            }
            """);
    }
}
