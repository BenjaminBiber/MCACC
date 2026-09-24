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
    public void Settings()
    {
        Save(Window(new SettingsViewModel(_services.Services), ThemeVariant.Light), "settings");
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
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var page = new AccountInfoViewModel(_services.Services, Privat);

        var window = Window(page, variant, height: 1750);
        await page.ActivateAsync(TestContext.Current.CancellationToken);
        Save(window, $"info-{theme}");
    }
}
