using System.Windows;
using System.Windows.Controls;
using GestionITVPro.WPF.ViewModels.Dashboard; // Asegúrate de que apunte a TU proyecto
using GestionITVPro.Views.Main;
using GestionITVPro.WPF.Views.Grafico; // Donde esté tu MainWindow
// Importa tus otras vistas según necesites
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GestionITVPro.WPF.Views.Dashboard;

public partial class DashboardView : Page 
{
    public DashboardView() 
    {
        InitializeComponent();

        // Obtenemos el ViewModel del contenedor de servicios
        var vm = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
        
        // Suscribimos la acción de navegación
        vm.NavigateAction = OnNavigate;
        
        DataContext = vm;
        Log.Debug("📊 DashboardView cargado correctamente");
    }

    private void OnNavigate(string view) 
    {
        // Obtenemos la referencia a la ventana principal para usar su Frame
        var mainWindow = (MainWindow)Window.GetWindow(this);
        
        if (mainWindow == null) return;

        switch (view) 
        {
            // En DashboardView.xaml.cs
            case "Graficos":
                var graficosPage = App.ServiceProvider.GetRequiredService<GraficoView>();
                mainWindow.MainFrame.Navigate(graficosPage);
                break;
            case "Citas":
                // mainWindow.MainFrame.Navigate(new CitaView());
                break;
        }
    }
}