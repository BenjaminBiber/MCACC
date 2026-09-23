using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;

namespace Mcacc.App.ViewModels;

/// <summary>A yes-or-no question; cancelling counts as no.</summary>
public sealed partial class ConfirmDialogViewModel(string title, string text, string confirmText, bool isDangerous = false)
    : DialogViewModel<bool>
{
    public override string Title => title;

    public string Text => text;

    public string ConfirmText => confirmText;

    public string CancelText => Texts.Cancel;

    public bool IsDangerous => isDangerous;

    [RelayCommand]
    public void Confirm() => Complete(true);

    [RelayCommand]
    public void Reject() => Complete(false);
}
