using Mcacc.App.ViewModels;

namespace Mcacc.App.Services;

public interface INavigator
{
    PageViewModel? CurrentPage { get; }

    bool CanGoBack { get; }

    void NavigateTo(PageViewModel page);

    void Back();
}
