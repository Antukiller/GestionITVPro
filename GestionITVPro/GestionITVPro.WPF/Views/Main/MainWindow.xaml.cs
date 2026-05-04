using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionITVPro.Views.Dashboard;
using GestionITVPro.Views.Citas;
using GestionITVPro.WPF; // Asumo que aquí está tu vista de tabla
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GestionITVPro.Views.Main;

public partial class MainWindow : Window {
    private bool _exitConfirmedViaMenu;

    public MainWindow() {
        InitializeComponent();

        // Inyección de dependencias para el ViewModel
        var viewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
        DataContext = viewModel;

        Log.Information("🏠 MainWindow de Umbrella ITV inicializada con diseño de Base de Datos");

        // Manejador del cierre de la ventana
        Closing += (s, e) => {
            if (_exitConfirmedViaMenu) return;
            var result = MessageBox.Show("¿Desea cerrar el sistema Umbrella ITV?", "Confirmar salida", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No) e.Cancel = true;
        };
    }

    // ==================== ACCIONES DE NAVEGACIÓN Y EVENTOS ====================

    private void OnDashboardClick(object sender, RoutedEventArgs e) {
        Log.Debug("Navegando a Dashboard");
        // Si usas Frame para cambiar el contenido de la tabla:
        // MainFrame.Navigate(new DashboardView());
    }

    private void OnCitasClick(object sender, RoutedEventArgs e) {
        Log.Debug("Navegando a Gestión de Citas");
        // Aquí iría la lógica para cargar los datos en el DataGrid DGVehiculos
    }

    private void OnExportarClick(object sender, RoutedEventArgs e) {
        Log.Information("Iniciando exportación de datos...");
        MessageBox.Show("Exportando base de datos a Excel/PDF...", "Umbrella System", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSalirClick(object sender, RoutedEventArgs e) {
        _exitConfirmedViaMenu = true;
        Log.Information("👋 Cierre de sesión manual");
        Application.Current.Shutdown();
    }

    // ==================== SHORTCUTS (TECLADO) ====================

    private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
            OnSalirClick(sender, e);
            e.Handled = true;
        }
        
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E) {
            OnExportarClick(sender, e);
            e.Handled = true;
        }
    }
}