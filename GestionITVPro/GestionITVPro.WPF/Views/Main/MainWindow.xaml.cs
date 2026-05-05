using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GestionITVPro.WPF;
using GestionITVPro.WPF.ViewModels.Main;
using GestionITVPro.WPF.Views.Dashboard; // Asumo que aquí está tu vista de tabla
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GestionITVPro.Views.Main;
public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();

        var viewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
        DataContext = viewModel;

        // SUSCRIPCIÓN AL EVENTO DE NAVEGACIÓN
        viewModel.OnNavigateRequested += (page) => {
            MainFrame.Navigate(page);
        };

        // Carga la vista inicial por defecto
        MainFrame.Navigate(new DashboardView());

        Log.Information("🏠 MainWindow conectada al Frame de navegación");
    }

    // Los métodos OnClick ahora deben llamar a los Comandos del ViewModel 
    // o puedes eliminarlos y usar Command="{Binding ...}" en el XAML directamente.
    private void OnDashboardClick(object sender, RoutedEventArgs e) => 
        ((MainViewModel)DataContext).NavigateToDashboardCommand.Execute(null);

    private void OnCitasClick(object sender, RoutedEventArgs e) => 
        ((MainViewModel)DataContext).NavigateToCitasCommand.Execute(null);

    private void OnSalirClick(object sender, RoutedEventArgs e) => 
        ((MainViewModel)DataContext).SalirCommand.Execute(null);

    private void OnExportarClick(object sender, RoutedEventArgs e) {
        // Aquí puedes llamar a una lógica de exportación global o del VM
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E) OnExportarClick(sender, e);
    }
}