using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using GestionITVPro.WPF.ViewModels; // Asegúrate de que este namespace sea el correcto
using GestionITVPro.WPF.Views.Vehiculos; // Namespace donde tengas la vista de Vehículos
using GestionITVPro.WPF.Views.Graficos;
using GestionITVPro.WPF.Views.Backup;

namespace GestionITVPro.WPF.Views.Dashboard
{
    /// <summary>
    /// Página del panel de control (Dashboard) versión Gaming.
    /// Centrado exclusivamente en la gestión de vehículos.
    /// </summary>
    public partial class DashboardView : Page {
        public DashboardView() {
            InitializeComponent();

            // Corregido: DashboardViewModel (estaba como VieModel)
            var vm = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
            
            // Asignamos la acción de navegación
            vm.NavigateAction = OnNavigate;
            
            DataContext = vm;

            Log.Debug("📊 DashboardView Gaming cargado correctamente");
        }

        /// <summary>
        /// Maneja la navegación desde el Dashboard hacia otras secciones.
        /// </summary>
        private void OnNavigate(string view) {
            // Buscamos la ventana principal para acceder al Frame de navegación
            var mainWindow = Window.GetWindow(this) as MainWindow;
            
            if (mainWindow == null) return;

            switch (view)
            {
                case "Vehiculos":
                    // Si antes tenías "Estudiantes", ahora navegamos a VehiculosView
                    mainWindow.MainFrame.Navigate(new CitaView());
                    break;

                case "Graficos":
                    mainWindow.MainFrame.Navigate(new GraficosView());
                    break;

                case "Backup":
                    mainWindow.MainFrame.Navigate(new BackupView());
                    break;
                
                default:
                    Log.Warning("⚠️ Intento de navegación a una vista no definida: {View}", view);
                    break;
            }
        }
    }
}