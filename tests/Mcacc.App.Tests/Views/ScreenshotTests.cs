using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Mcacc.App.Tests.TestSupport;
using Mcacc.App.ViewModels;
using Mcacc.App.Views;
using Mcacc.Core.Model;
using Mcacc.Core.Stats;
using Mcacc.Core.Usage;
using Xunit;

namespace Mcacc.App.Tests.Views;

public sealed class ScreenshotTests : IDisposable
{
    private static readonly Account Standard = Account.CreateDefault();
    private static readonly Account Privat = new("privat", "Privat", "#6A9BCC", IsDefault: false);

    private readonly TestServices _services = new(OsKind.Windows, Standard, Privat);

    public void Dispose() => _services.Dispose();

    /// <summary>Set MCACC_SHOTS to a directory to render the pages as PNGs; without it these tests are skipped.</summary>
    private static string? OutDir => Environment.GetEnvironmentVariable("MCACC_SHOTS");

    private void Save(Window window, string name)
    {
        if (OutDir is null)
        {
            Assert.Skip("Set MCACC_SHOTS to render the screenshots.");
            return;
        }

        window.UpdateLayout();
        Directory.CreateDirectory(OutDir);
        using var frame = window.CaptureRenderedFrame()!;
        using var stream = File.Create(Path.Join(OutDir, name + ".png"));
        frame.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private Window Window(PageViewModel page, ThemeVariant variant, double width = 760, double height = 540)
    {
        _services.Navigator.Reset(page);
        Avalonia.Application.Current!.RequestedThemeVariant = variant;
        var window = new MainWindow { DataContext = _services.Window, Width = width, Height = height };
        window.Show();
        return window;
    }

    private void Seed()
    {
        _services.SetLogin(Standard, "benjamin@firma.de");
        _services.SetLogin(Privat, "privat@example.com");
        var now = DateTimeOffset.Now;
        _services.Usage.Cached["standard"] = new AccountUsage(
            UsageState.Ok,
            new UsageSnapshot(new UsageWindow(42, now.AddHours(2)), new UsageWindow(7, now.AddDays(3)), now));
        _services.Usage.Cached["privat"] = new AccountUsage(
            UsageState.RateLimited,
            new UsageSnapshot(new UsageWindow(94, now.AddMinutes(35)), new UsageWindow(null, null), now));
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Account_picker(string theme)
    {
        Seed();
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var picker = new AccountPickerViewModel(_services.Services);
        picker.Tiles[1].IsRunning = true;
        Save(Window(picker, variant), $"picker-{theme}");
    }

    /// <summary>The minimum window width, where the tiles fill one column.</summary>
    [AvaloniaFact]
    public void Account_picker_at_the_minimum_width()
    {
        Seed();
        Save(Window(new AccountPickerViewModel(_services.Services), ThemeVariant.Light, width: 560), "picker-narrow");
    }

    /// <summary>A wide window, where the add tile sits in the row of the accounts.</summary>
    [AvaloniaFact]
    public void Account_picker_in_a_wide_window()
    {
        Seed();
        Save(Window(new AccountPickerViewModel(_services.Services), ThemeVariant.Light, width: 1100), "picker-wide");
    }

    [AvaloniaFact]
    public void Account_picker_with_a_token_of_another_account()
    {
        Seed();
        _services.Usage.Cached["standard"] = new AccountUsage(UsageState.WrongAccount, null, "privat@example.com");
        Save(Window(new AccountPickerViewModel(_services.Services), ThemeVariant.Light), "picker-wrong-account");
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Target_picker(string theme)
    {
        Seed();
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Save(Window(new TargetPickerViewModel(_services.Services, Privat), variant), $"target-{theme}");
    }

    [AvaloniaFact]
    public async Task Settings()
    {
        var hour = DateTimeOffset.Now.AddHours(-1);
        TokenBucket Bucket(string project, long input) =>
            new(hour, "claude-opus-4-5", project, "s1", new TokenCounts(input, 0, 0, 0, 0), 1);
        _services.Stats.Buckets.AddRange(
        [
            Bucket(@"C:\Users\ben\RiderProjects\MCACC", 48_000_000),
            Bucket(@"C:\Users\ben\RiderProjects\Shop", 12_500_000),
            Bucket(@"C:\Users\ben\Spielwiese", 3_200_000),
        ]);
        _services.Accounts.UpdateSettings(settings => settings with
        {
            ExcludedProjects = [@"C:\Users\ben\Spielwiese", @"C:\Users\ben\alt\Prototyp"],
        });
        var page = new SettingsViewModel(_services.Services);
        await page.ActivateAsync(CancellationToken.None);

        Save(Window(page, ThemeVariant.Light, height: 860), "settings");
    }

    [AvaloniaFact]
    public void Conflict_dialog()
    {
        Seed();
        var window = Window(new TargetPickerViewModel(_services.Services, Privat), ThemeVariant.Light);
        _services.Window.Dialog = new ConfirmDialogViewModel(
            Mcacc.App.Localization.Texts.ConflictDialogTitle,
            Mcacc.App.Localization.Texts.ConflictDialogText("Standard", "Privat"),
            Mcacc.App.Localization.Texts.ConflictDialogRestart);
        Save(window, "conflict");
    }

    [AvaloniaFact]
    public void Editor_dialog()
    {
        Seed();
        var window = Window(new AccountPickerViewModel(_services.Services), ThemeVariant.Light);
        var editor = AccountEditorViewModel.ForCreate(_services.Services);
        editor.CopySource = Standard;
        _services.Window.Dialog = editor;
        Save(window, "editor");
    }

    [AvaloniaFact]
    public void Remove_dialog()
    {
        var window = Window(new AccountPickerViewModel(_services.Services), ThemeVariant.Light);
        var dialog = new RemoveAccountDialogViewModel(_services.Services, Privat);
        dialog.DeleteData = true;
        _services.Window.Dialog = dialog;
        Save(window, "remove");
    }

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Info_page(string theme)
    {
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var page = InfoPage();

        var window = Window(page, variant, height: 1750);
        await page.ActivateAsync(TestContext.Current.CancellationToken);
        Save(window, $"info-{theme}");
    }

    /// <summary>A wide window, where the account and the limits sit side by side; once per view of the limits.</summary>
    [AvaloniaTheory]
    [InlineData("now")]
    [InlineData("history")]
    public async Task Info_page_in_a_wide_window(string view)
    {
        var page = InfoPage();
        page.ShowLimitsViewCommand.Execute(view);

        var window = Window(page, ThemeVariant.Light, width: 1300, height: 1400);
        await page.ActivateAsync(TestContext.Current.CancellationToken);
        Save(window, $"info-wide-{view}");
    }

    /// <summary>The limit history with the gaps of 2026-09-24: a lone point before a reset, and a night without MCACC.</summary>
    [AvaloniaFact]
    public async Task Info_page_with_gaps_in_the_history()
    {
        var page = InfoPage();
        var now = DateTimeOffset.Now;
        UsageHistoryPoint At(double hoursAgo, double fiveHour, double resetsInHours, double sevenDay) =>
            new(now.AddHours(-hoursAgo), fiveHour, now.AddHours(-hoursAgo + resetsInHours), sevenDay, now.AddDays(6));
        _services.Usage.History["privat"] =
        [
            At(23.4, 97, 0.53, 31),
            .. Enumerable.Range(0, 40).Select(i => At(22.3 - i * 0.08, 5 + i * 1.3, 4.9 - i * 0.08, 32 + i * 0.15)),
            .. Enumerable.Range(0, 36).Select(i => At(3 - i * 0.08, 6 + i * 1.3, 4.9 - i * 0.08, 2 + i * 0.15)),
        ];
        page.ShowLimitsViewCommand.Execute("history");

        var window = Window(page, ThemeVariant.Light, width: 1300, height: 1400);
        await page.ActivateAsync(TestContext.Current.CancellationToken);
        Save(window, "info-history-gaps");
    }

    /// <summary>The tokens per day as a calendar, in the default and in a wide window.</summary>
    [AvaloniaTheory]
    [InlineData(760)]
    [InlineData(1300)]
    public async Task Info_page_with_the_calendar(int width)
    {
        var page = InfoPage();
        // Claude's summary knows the logged month and, in weekday bursts, the two months before it.
        var today = DateOnly.FromDateTime(DateTime.Now);
        _services.Stats.Summary.AddRange(Enumerable.Range(0, 90)
            .Where(i => i < 30 || today.AddDays(-i).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Select(i => new ClaudeDailyTokens(today.AddDays(-i), 20_000 + 7_000 * (i % 4))));
        page.ShowUsageViewCommand.Execute("calendar");

        var window = Window(page, ThemeVariant.Light, width: width, height: 1750);
        await page.ActivateAsync(TestContext.Current.CancellationToken);
        Save(window, $"info-calendar-{width}");
    }

    private AccountInfoViewModel InfoPage()
    {
        Seed();
        var now = DateTimeOffset.Now;
        _services.Usage.Profiles["privat"] = new AccountProfile(
            "uuid", "privat@example.com", "Ben Biber", "Ben", true, false, now.AddYears(-1), "Privat", "claude_max",
            "stripe_subscription", "default_claude_max_20x");
        _services.Usage.History["privat"] = Enumerable.Range(0, 24 * 12)
            .Select(i => new UsageHistoryPoint(
                now.AddMinutes(-5 * i), Math.Max(0, 94 - i * 0.4), null, 30 + i * 0.02, null))
            .ToList();
        string[] models = ["claude-opus-4-5", "claude-sonnet-4-5", "claude-haiku-4-5"];
        string[] projects = ["/code/mcacc", "/code/shop", "/code/blog"];
        _services.Stats.Buckets.AddRange(Enumerable.Range(0, 30 * 3).Select(i => new TokenBucket(
            now.AddDays(-(i / 3)).AddHours(-1),
            models[i % 3],
            projects[(i / 7) % 3],
            $"s{i / 4}",
            new TokenCounts(20_000 * (i % 5 + 1), 8_000 * (i % 3 + 1), 30_000, 5_000, 400_000 * (i % 4 + 1)),
            Messages: 10 + i % 7)));
        return new AccountInfoViewModel(_services.Services, Privat);
    }
}
