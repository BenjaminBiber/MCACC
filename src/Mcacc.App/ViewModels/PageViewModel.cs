using Avalonia.Input;

namespace Mcacc.App.ViewModels;

public abstract class PageViewModel : ViewModelBase
{
    /// <summary>Runs when the page becomes the current one; background work belongs here, not in the constructor.</summary>
    public virtual Task ActivateAsync(CancellationToken ct) => Task.CompletedTask;

    /// <returns>True when the page handled the key and the window should not act on it.</returns>
    public virtual bool OnKeyDown(Key key, KeyModifiers modifiers) => false;
}
