using System.Windows;

namespace GestionITVPro.WPF.Views.Cita;

public partial class CitaDetails : Window {
    public CitaDetails() {
        InitializeComponent();
    }

    private void OnVolverPanelClick(object sender, RoutedEventArgs e) {
        Close();
    }
}