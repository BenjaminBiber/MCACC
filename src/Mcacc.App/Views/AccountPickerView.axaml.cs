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

        TileOf(model.PreferredTile)?.Focus(NavigationMethod.Directional);
    }

    /// <summary>F2 and Del act on the focused tile, which only the view knows.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || DataContext is not AccountPickerViewModel model)
        {
            return;
        }

        if (e.Key is Key.F2 or Key.Delete && model.OnKeyDown(e.Key, e.KeyModifiers, FocusedTile(model)))
        {
            e.Handled = true;
        }
    }

    private AccountTileViewModel? FocusedTile(AccountPickerViewModel model) =>
        this.GetVisualDescendants()
            .OfType<LaunchTile>()
            .FirstOrDefault(tile => tile.IsFocused)
            ?.DataContext as AccountTileViewModel;

    private LaunchTile? TileOf(AccountTileViewModel tile) =>
        this.GetVisualDescendants()
            .OfType<LaunchTile>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, tile));
}
