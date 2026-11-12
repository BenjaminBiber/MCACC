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
        // The choices exist before the property is set, because setting it marks the selected one.
        Providers = [.. ProviderDescriptors.All.Select(p => new ProviderChoiceViewModel(p, isSelected: false))];
        SelectedProvider = edited?.Provider ?? ProviderKind.Claude;

        // Claude is the enum's zero value, so setting it changes nothing and the handler would not run.
        ApplySelectedProvider();
    }

    public static AccountEditorViewModel ForCreate(AppServices services) => new(services, edited: null);

    public static AccountEditorViewModel ForEdit(AppServices services, Account account) => new(services, account);

    public override string Title => IsCreate ? Texts.EditorCreateTitle : Texts.EditorEditTitle;

    public bool IsCreate => _edited is null;

    public string NameLabel => Texts.EditorName;

    public string NameWatermark => Texts.EditorNameWatermark;

    public string ColorLabel => Texts.EditorColor;

    public string ProviderLabel => Texts.EditorProvider;

    public string CopyFromLabel => Texts.EditorCopyFrom;

    public string RewritePathsText => Texts.EditorRewritePaths;

    public string SubmitText => IsCreate ? Texts.EditorCreate : Texts.EditorSave;

    public string CancelText => Texts.Cancel;

    public IReadOnlyList<ColorSwatchViewModel> Swatches { get; }

    /// <summary>The two services; a created account keeps the one it was made with.</summary>
    public IReadOnlyList<ProviderChoiceViewModel> Providers { get; }

    /// <summary>The service cannot change afterwards: the profile directory is named after it.</summary>
    public bool IsProviderFixed => !IsCreate;

    /// <summary>Null stands for "Nichts"; it is only ever offered while creating.</summary>
    public ObservableCollection<Account?> CopySources { get; } = [];

    public ObservableCollection<CopyItemViewModel> CopyItemsToShow { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string SelectedColor { get; set; }

    [ObservableProperty]
    public partial Account? CopySource { get; set; }

    [ObservableProperty]
    public partial ProviderKind SelectedProvider { get; set; }

    [ObservableProperty]
    public partial bool RewriteSettingsPaths { get; set; } = true;

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? SubmitError { get; private set; }

    public bool HasCopySource => CopySource is not null;

    /// <summary>
    /// Only shown where it decides anything. For Codex the one file with paths in it is rewritten either way, so a
    /// checkbox there would promise a choice that does not exist.
    /// </summary>
    public bool HasOptionalRewrite =>
        CopyItemsToShow.Any(item => CopyItems.Info(item.Item).Rewrite == PathRewrite.Optional);

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

            var result = _services.Accounts.Create(Name, SelectedColor, CopyRequestOrNull(), SelectedProvider);
            SkippedText = SkippedTextFor(result);
            if (_services.Accounts.Config.Settings.RecordClaudeLimits)
            {
                _services.StatusLine.Apply(record: true, [result.Account]);
            }

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

    [RelayCommand]
    public void PickProvider(ProviderKind provider)
    {
        if (IsCreate)
        {
            SelectedProvider = provider;
        }
    }

    partial void OnSelectedProviderChanged(ProviderKind value) => ApplySelectedProvider();

    /// <summary>
    /// Marks the chosen service and offers only accounts of that service as a copy source: copying across services
    /// would write one provider's files into another's home.
    /// </summary>
    private void ApplySelectedProvider()
    {
        foreach (var choice in Providers)
        {
            choice.IsSelected = choice.Provider.Kind == SelectedProvider;
        }

        RefreshCopySources();
    }

    private void RefreshCopySources()
    {
        var previous = CopySource;
        CopySources.Clear();
        if (!IsCreate)
        {
            CopySource = null;
            return;
        }

        CopySources.Add(null);
        foreach (var account in _services.Accounts.Config.Accounts.Where(a => a.Provider == SelectedProvider))
        {
            CopySources.Add(account);
        }

        CopySource = CopySources.Contains(previous) ? previous : null;
    }

    partial void OnCopySourceChanged(Account? value)
    {
        OnPropertyChanged(nameof(HasCopySource));
        CopyItemsToShow.Clear();
        if (value is null)
        {
            OnPropertyChanged(nameof(HasOptionalRewrite));
            return;
        }

        var available = ConfigCopyPlanner.Available(_services.Resolver.Resolve(value));
        foreach (var item in ProviderDescriptors.For(value).CopyItems)
        {
            CopyItemsToShow.Add(new CopyItemViewModel(item, available.Contains(item)));
        }

        OnPropertyChanged(nameof(HasOptionalRewrite));
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
        CopyItem.CodexConfigToml => Texts.CopyCodexConfigToml,
        CopyItem.AgentsMd => Texts.CopyAgentsMd,
        CopyItem.CodexSkills => Texts.CopyCodexSkills,
        CopyItem.CodexPrompts => Texts.CopyCodexPrompts,
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

/// <summary>One service to pick from while creating an account; the selected one carries a check mark.</summary>
public sealed partial class ProviderChoiceViewModel(ProviderDescriptor provider, bool isSelected) : ViewModelBase
{
    public ProviderDescriptor Provider { get; } = provider;

    public ProviderKind Kind => Provider.Kind;

    public string Label => Texts.ProviderName(Provider.Kind);

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}
