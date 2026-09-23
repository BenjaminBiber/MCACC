using Mcacc.App.ViewModels;

namespace Mcacc.App.Services;

public interface IDialogService
{
    /// <summary>Shows the dialog as an overlay and waits for it; a cancel yields <c>default</c>.</summary>
    Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog);

    /// <summary>Shows a blocking overlay until the returned handle is disposed.</summary>
    IDisposable Busy(string text);
}

public sealed class DialogService(MainWindowViewModel owner) : IDialogService
{
    public async Task<TResult?> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        var previous = owner.Dialog;
        owner.Dialog = dialog;
        try
        {
            return await dialog.Completion.ConfigureAwait(true);
        }
        finally
        {
            owner.Dialog = previous;
        }
    }

    public IDisposable Busy(string text)
    {
        owner.BusyText = text;
        owner.IsBusy = true;
        return new BusyScope(owner);
    }

    private sealed class BusyScope(MainWindowViewModel owner) : IDisposable
    {
        public void Dispose()
        {
            owner.IsBusy = false;
            owner.BusyText = null;
        }
    }
}
