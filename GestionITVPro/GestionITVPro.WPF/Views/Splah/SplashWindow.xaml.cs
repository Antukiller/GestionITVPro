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

    private async void OnWindowLoaded(object sender, RoutedEventArgs e) {
        Log.Information("SplashWindow cargada. Iniciando cuenta atrás...");

        try {
            // Simulamos la carga (esto mantiene el hilo de UI libre)
            for (int i = 0; i <= 100; i++) {
                if (_cts.Token.IsCancellationRequested) return;

                MiProgressBar.Value = i;
                await Task.Delay(50, _cts.Token);
            }

            Log.Information("Carga completa");

            // --- CAMBIO CLAVE AQUÍ ---
            // Solo cerramos esta ventana. 
            // NO creamos la MainWindow aquí. La creación la maneja App.xaml.cs
            // después de que este Splash se cierre.
            this.Close(); 
        }
        catch (OperationCanceledException) {
            Log.Warning("Usuario canceló");
            Application.Current.Shutdown();
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e) {
        _cts.Cancel();
        Application.Current.Shutdown(); // Si cancela, cerramos todo el proceso
    }
}