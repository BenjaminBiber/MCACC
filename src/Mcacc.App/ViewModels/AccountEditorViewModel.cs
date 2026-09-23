using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.Accounts;
using Mcacc.Core.ConfigCopy;
using Mcacc.Core.Model;

namespace Mcacc.App.ViewModels;

/// <summary>Creates or renames an account. Only a new account may copy settings from an existing one.</summary>
public sealed partial class AccountEditorViewModel : DialogViewModel<Account>
{
    private readonly AppServices _services;
    private readonly Account? _edited;

    private AccountEditorViewModel(AppServices services, Account? edited)
    {
        _services = services;
        _edited = edited;
        Name = edited?.Name ?? "";
        var color = edited?.Color
                    ?? AccountPalette.NextFreeColor(services.Accounts.Config.Accounts.Select(a => a.Color));
        // The swatches exist before the property is set, because setting it marks the selected one.
        Swatches = [.. AccountPalette.Colors.Select(c => new ColorSwatchViewModel(c, c == color))];
        SelectedColor = color;
        CopySources = edited is null
            ? [null, .. services.Accounts.Config.Accounts]
            : [];
    }

    public static AccountEditorViewModel ForCreate(AppServices services) => new(services, edited: null);

    public static AccountEditorViewModel ForEdit(AppServices services, Account account) => new(services, account);

    public override string Title => IsCreate ? Texts.EditorCreateTitle : Texts.EditorEditTitle;

    public bool IsCreate => _edited is null;

    public string NameLabel => Texts.EditorName;

    public string NameWatermark => Texts.EditorNameWatermark;

    public string ColorLabel => Texts.EditorColor;

    public string CopyFromLabel => Texts.EditorCopyFrom;

    public string RewritePathsText => Texts.EditorRewritePaths;

    public string SubmitText => IsCreate ? Texts.EditorCreate : Texts.EditorSave;

    public string CancelText => Texts.Cancel;

    public IReadOnlyList<ColorSwatchViewModel> Swatches { get; }

    /// <summary>Null stands for "Nichts"; it is only ever offered while creating.</summary>
    public IReadOnlyList<Account?> CopySources { get; }

    public ObservableCollection<CopyItemViewModel> CopyItemsToShow { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string SelectedColor { get; set; }

    [ObservableProperty]
    public partial Account? CopySource { get; set; }

    [ObservableProperty]
    public partial bool RewriteSettingsPaths { get; set; } = true;

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? SubmitError { get; private set; }

    public bool HasCopySource => CopySource is not null;

    /// <summary>What the copy left out, so a silent partial copy cannot happen.</summary>
    public string? SkippedText { get; private set; }

    public string LabelFor(Account? source) => source?.Name ?? Texts.EditorCopyNothing;

    [RelayCommand]
    public void PickColor(string? color)
    {
        if (color is not null)
        {
            SelectedColor = color;
        }
    }

    [RelayCommand]
    public void Submit()
    {
        NameError = ErrorTextFor(AccountNameValidator.Validate(Name, _services.Accounts.Config.Accounts, _edited?.Id));
        if (NameError is not null)
        {
            return;
        }

        try
        {
            if (_edited is { } edited)
            {
                Complete(_services.Accounts.Update(edited.Id, Name, SelectedColor));
                return;
            }

            var result = _services.Accounts.Create(Name, SelectedColor, CopyRequestOrNull());
            SkippedText = SkippedTextFor(result);
            Complete(result.Account);
        }
        catch (AccountValidationException e)
        {
            NameError = ErrorTextFor(e.Error);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException
                                      or ArgumentException)
        {
            _services.Log.Error("Saving the account failed.", e);
            SubmitError = e.Message;
        }
    }

    [RelayCommand]
    public void Reject() => Cancel();

    partial void OnSelectedColorChanged(string value)
    {
        foreach (var swatch in Swatches)
        {
            swatch.IsSelected = swatch.Color == value;
        }
    }

    partial void OnCopySourceChanged(Account? value)
    {
        OnPropertyChanged(nameof(HasCopySource));
        CopyItemsToShow.Clear();
        if (value is null)
        {
            return;
        }

        var available = ConfigCopyPlanner.Available(_services.Resolver.Resolve(value));
        foreach (var item in Enum.GetValues<CopyItem>())
        {
            CopyItemsToShow.Add(new CopyItemViewModel(item, available.Contains(item)));
        }
    }

    private CopyRequest? CopyRequestOrNull()
    {
        if (CopySource is not { } source)
        {
            return null;
        }

        var items = CopyItemsToShow.Where(item => item.IsSelected).Select(item => item.Item).ToHashSet();
        return items.Count == 0 ? null : new CopyRequest(source.Id, items, RewriteSettingsPaths);
    }

    private static string? SkippedTextFor(CreateAccountResult result)
    {
        var skipped = result.CopyReport?.Skipped ?? [];
        return skipped.Count == 0 ? null : Texts.EditorNotCopied(skipped.Select(entry => entry.RelativePath).Distinct());
    }

    private static string? ErrorTextFor(NameValidationError? error) => error switch
    {
        NameValidationError.Empty => Texts.EditorNameEmpty,
        NameValidationError.TooLong => Texts.EditorNameTooLong,
        NameValidationError.Duplicate => Texts.EditorNameDuplicate,
        _ => null,
    };
}

/// <summary>One checkbox of the copy list; an item the source does not have cannot be checked.</summary>
public sealed partial class CopyItemViewModel : ViewModelBase
{
    public CopyItemViewModel(CopyItem item, bool isAvailable)
    {
        Item = item;
        IsAvailable = isAvailable;
        // Scripts are rarely wanted in a second account, so they start unchecked.
        IsSelected = isAvailable && item != CopyItem.Scripts;
    }

    public CopyItem Item { get; }

    public bool IsAvailable { get; }

    public string Label => IsAvailable ? LabelOf(Item) : LabelOf(Item) + Texts.EditorNotAvailableSuffix;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    private static string LabelOf(CopyItem item) => item switch
    {
        CopyItem.ClaudeMd => Texts.CopyClaudeMd,
        CopyItem.SettingsJson => Texts.CopySettingsJson,
        CopyItem.Agents => Texts.CopyAgents,
        CopyItem.Skills => Texts.CopySkills,
        CopyItem.Commands => Texts.CopyCommands,
        CopyItem.Scripts => Texts.CopyScripts,
        CopyItem.DesktopMcpConfig => Texts.CopyDesktopMcp,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };
}

/// <summary>One color swatch of the palette; the selected one carries a check mark.</summary>
public sealed partial class ColorSwatchViewModel(string color, bool isSelected) : ViewModelBase
{
    public string Color { get; } = color;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
