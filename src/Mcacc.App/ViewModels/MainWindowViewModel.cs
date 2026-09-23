using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Services;

namespace Mcacc.App.ViewModels;

/// <summary>The window shell: the current page, the dialog overlay, the busy overlay and the error banner.</summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
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

    public void ShowError(string message) => ErrorBanner = message;

    [RelayCommand]
    public void DismissError() => ErrorBanner = null;

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
