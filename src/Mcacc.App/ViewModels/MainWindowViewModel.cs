using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Services;

namespace Mcacc.App.ViewModels;

/// <summary>The window shell: the current page, the dialog overlay, the busy overlay and the error banner.</summary>
public sealed partial class MainWindowViewModel : ViewModelBase, IShellPresenter
{
    private bool _startOver;

    public MainWindowViewModel(Navigator navigator)
    {
        Navigator = navigator;
        navigator.Navigated += _ => OnPropertyChanged(nameof(CanGoBack));
    }

    public Navigator Navigator { get; }

    [ObservableProperty]
    public partial DialogViewModel? Dialog { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? BusyText { get; set; }

    [ObservableProperty]
    public partial string? ErrorBanner { get; set; }

    public bool CanGoBack => Navigator.CanGoBack;

    public string DismissErrorText => Localization.Texts.DismissError;

    public void ShowError(string message) => ErrorBanner = message;

    public void ClearError() => ErrorBanner = null;

    [RelayCommand]
    public void DismissError() => ClearError();

    /// <summary>
    /// A hidden window drops what it showed: the banner and an open dialog, which counts as cancelled. The page goes
    /// back to the start on the next <see cref="OnShown"/>, so the start page reads its data when it is seen.
    /// </summary>
    public void OnHidden()
    {
        _startOver = true;
        ClearError();
        Dialog?.Cancel();
    }

    /// <summary>After a hide the window opens on the start page, as after a restart, unless a start still runs.</summary>
    public void OnShown(Func<PageViewModel> startPage)
    {
        if (!_startOver || IsBusy)
        {
            return;
        }

        _startOver = false;
        Navigator.Reset(startPage());
    }

    /// <summary>Esc closes a dialog, otherwise the current page decides.</summary>
    public bool OnKeyDown(Key key, KeyModifiers modifiers)
    {
        if (IsBusy)
        {
            return true;
        }

        if (Dialog is { } dialog)
        {
            if (key != Key.Escape)
            {
                return false;
            }

            dialog.Cancel();
            return true;
        }

        return Navigator.CurrentPage?.OnKeyDown(key, modifiers) == true;
    }
}
