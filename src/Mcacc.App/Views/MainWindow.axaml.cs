using Avalonia.Controls;
using Avalonia.Input;
using Mcacc.App.ViewModels;

namespace Mcacc.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>Keys reach the current page only if no control consumed them first.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && DataContext is MainWindowViewModel model && model.OnKeyDown(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }
}
