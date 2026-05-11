using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace GestionITVPro.WPF.Views.About;

public partial class About : Window {
    public About() {
        InitializeComponent();
    }
    
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

    private void OnCerrarClick(object sender, RoutedEventArgs e) {
        Close();
    }
}
