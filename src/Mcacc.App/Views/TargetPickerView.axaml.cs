using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Mcacc.App.ViewModels;
using Mcacc.Core.Model;

namespace Mcacc.App.Views;

public partial class TargetPickerView : UserControl
{
    public TargetPickerView() => InitializeComponent();

    /// <summary>Focuses the target used last, so two times Enter is enough to launch.</summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is AccountPickerViewModel)
        {
            return;
        }

        var preferred = (DataContext as TargetPickerViewModel)?.PreferredTarget ?? LaunchTarget.Desktop;
        var tile = preferred == LaunchTarget.Code ? CodeTile : DesktopTile;
        tile.Focus(NavigationMethod.Directional);
    }
}
