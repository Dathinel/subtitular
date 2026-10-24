using Microsoft.UI.Xaml;

namespace Subtitular.App;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        // Lo que escape de un manejador se muestra en la ventana, nunca como cuadro con traza.
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            _window?.ShowError("Error inesperado: " + e.Message);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
