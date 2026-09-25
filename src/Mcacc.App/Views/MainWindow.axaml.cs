using Avalonia.Controls;
using Avalonia.Input;
using Mcacc.App.Services;
using Mcacc.App.ViewModels;

namespace Mcacc.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>Where closing by the user goes instead; without it (previews, tests) the window just closes.</summary>
    public IAppLifetime? Lifetime { get; set; }

    /// <summary>Keys reach the current page only if no control consumed them first.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && DataContext is MainWindowViewModel model && model.OnKeyDown(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>MCACC lives on in the tray: the close button only hides the window, a shutdown still closes it.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (Lifetime is { } lifetime && e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            lifetime.HideWindow();
        }
    }
}
