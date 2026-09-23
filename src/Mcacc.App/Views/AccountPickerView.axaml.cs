using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Mcacc.App.Controls;
using Mcacc.App.ViewModels;

namespace Mcacc.App.Views;

public partial class AccountPickerView : UserControl
{
    public AccountPickerView() => InitializeComponent();

    /// <summary>Focuses the account used last, so two times Enter is enough to launch.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is not AccountPickerViewModel model || model.PreferredTile is null)
        {
            return;
        }

        var tile = this.GetVisualDescendants()
            .OfType<LaunchTile>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, model.PreferredTile));
        tile?.Focus(NavigationMethod.Directional);
    }
}
