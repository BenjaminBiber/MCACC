using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mcacc.App.Localization;
using Mcacc.App.Services;
using Mcacc.Core.Model;

namespace Mcacc.App.ViewModels;

/// <param name="DeleteData">Whether the profile directories go as well.</param>
public sealed record RemoveAccountResult(bool DeleteData);

public sealed partial class RemoveAccountDialogViewModel : DialogViewModel<RemoveAccountResult>
{
    private readonly Account _account;

    public RemoveAccountDialogViewModel(AppServices services, Account account)
    {
        _account = account;
        var provider = ProviderDescriptors.For(account);
        List<string> folders = [];
        if (provider.Desktop is not null)
        {
            folders.Add(services.Paths.DesktopDataDir(account.Id, provider));
        }

        folders.Add(services.Paths.ConfigDir(account.Id, provider));
        Folders = folders;
    }

    public override string Title => Texts.RemoveDialogTitle(_account.Name);

    public string Text => Texts.RemoveDialogText;

    public string DeleteDataText => Texts.RemoveDialogDeleteData;

    public string DeleteWarning => Texts.RemoveDialogDeleteWarning;

    public string ConfirmText => Texts.Remove;

    public string CancelText => Texts.Cancel;

    /// <summary>The directories the checkbox would delete; shown so nobody deletes them unseen.</summary>
    public IReadOnlyList<string> Folders { get; }

    [ObservableProperty]
    public partial bool DeleteData { get; set; }

    [RelayCommand]
    public void Confirm() => Complete(new RemoveAccountResult(DeleteData));

    [RelayCommand]
    public void Reject() => Cancel();
}
