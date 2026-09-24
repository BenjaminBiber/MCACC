using System.Windows.Input;

namespace Mcacc.App.ViewModels;

/// <summary>The dashed tile after the accounts that opens the editor for a new one.</summary>
public sealed class AddAccountTileViewModel(string title, ICommand command) : ViewModelBase
{
    public string Title { get; } = title;

    public ICommand Command { get; } = command;
}
