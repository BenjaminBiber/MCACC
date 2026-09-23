using CommunityToolkit.Mvvm.ComponentModel;
using Mcacc.App.ViewModels;

namespace Mcacc.App.Services;

public sealed partial class Navigator : ObservableObject, INavigator
{
    private readonly Stack<PageViewModel> _back = new();

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; private set; }

    public bool CanGoBack => _back.Count > 0;

    /// <summary>Raised after every change, so the host can activate the new page.</summary>
    public event Action<PageViewModel>? Navigated;

    public void NavigateTo(PageViewModel page)
    {
        if (CurrentPage is { } current)
        {
            _back.Push(current);
        }

        Show(page);
    }

    /// <summary>Replaces the current page without growing the back stack; used for the first page.</summary>
    public void Reset(PageViewModel page)
    {
        _back.Clear();
        Show(page);
    }

    public void Back()
    {
        if (_back.TryPop(out var previous))
        {
            Show(previous);
        }
    }

    private void Show(PageViewModel page)
    {
        CurrentPage = page;
        OnPropertyChanged(nameof(CanGoBack));
        Navigated?.Invoke(page);
    }
}
