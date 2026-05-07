using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace GestionITVPro.WPF.Views.AcercaDe;

public partial class AcercaDe : Window {
    public AcercaDe() {
        InitializeComponent();
    }
    
    /// <summary>
    ///     Abre el enlace a GitHub en el navegador o copia al portapapeles.
    /// </summary>
    private void OnGitHubClick(object sender, RoutedEventArgs e) {
        try {
            Process.Start(new ProcessStartInfo {
                FileName = "https://github.com/Antukiller",
                UseShellExecute = true
            });
        }
        catch {
            Clipboard.SetText("https://github.com/Antukiller");
            MessageBox.Show("El enlace se ha copiado al portapapeles.", "GitHub", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    /// <summary>
    ///     Cierra la ventana.
    /// </summary>
    private void OnCerrarClick(object sender, RoutedEventArgs e) {
        Close();
    }
}
