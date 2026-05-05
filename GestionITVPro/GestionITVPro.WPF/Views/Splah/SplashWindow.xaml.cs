using System.Windows;
using GestionITVPro.Views;
using GestionITVPro.Views.Main;
using Serilog;

namespace GestionITVPro.WPF.Views.Splash;

/// <summary>
/// Venatana de spalsh que se muestra al iniciar la aplicacion .
/// </summary>
public partial class SplashWindow : Window {
    private CancellationTokenSource _cts = new CancellationTokenSource();

    public SplashWindow() {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    // Usamos 'async void' porque es un controlador de eventos (Event Handler)
    private async void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Log.Information("SplashWindow cargada. Iniciando cuenta atrás...");

        try {
            for (int i = 0; i <= 100; i++) {
                // Comprobamos si el botón de cancelar ha sido pulsado
                if (_cts.Token.IsCancellationRequested) return;

                // Actualizamos la barra que dibujaste en el XAML
                MiProgressBar.Value = i;

                // 'await Task.Delay' es como un cronómetro que no congela la pantalla
                await Task.Delay(50, _cts.Token);
            }

            Log.Information("Carga completa");

            // Abrimos la principal y cerramos esta
            var principal = new MainWindow();
            principal.Show();
            this.Close();
        }
        catch (OperationCanceledException) {
            // Esto ocurre si pulsas el botón de cancelar
            Log.Warning("Usuario canceló");
            MessageBox.Show("¡Misión abortada!");
            Application.Current.Shutdown();
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e) {
        // Esto activa la señal de cancelación
        _cts.Cancel();
    }
}