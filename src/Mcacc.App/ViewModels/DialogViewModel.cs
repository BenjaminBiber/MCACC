using CommunityToolkit.Mvvm.ComponentModel;

namespace Mcacc.App.ViewModels;

/// <summary>A dialog without its result type, which is all the overlay host needs to know.</summary>
public abstract class DialogViewModel : ObservableObject
{
    public abstract string Title { get; }

    /// <summary>Called when the overlay is dismissed with Esc or the close button.</summary>
    public abstract void Cancel();
}

/// <summary>A dialog that finishes with a <typeparamref name="TResult"/>; a cancel completes with the default.</summary>
public abstract class DialogViewModel<TResult> : DialogViewModel
{
    private readonly TaskCompletionSource<TResult?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TResult?> Completion => _completion.Task;

    public override void Cancel() => Complete(default);

    /// <remarks>Only the first completion counts, so a double click cannot resolve a dialog twice.</remarks>
    public void Complete(TResult? result) => _completion.TrySetResult(result);
}
