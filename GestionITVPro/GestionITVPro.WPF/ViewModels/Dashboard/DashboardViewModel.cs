using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Models;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Report;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject 
{
    private readonly ILogger _logger = Log.ForContext<DashboardViewModel>();
    private readonly ICitasService _citasService;
    private readonly IReportService _reportService;

    // Propiedad vinculada a la Card Principal y Estadísticas en el XAML
    [ObservableProperty] 
    private InformeCita _informe;

    public DashboardViewModel(ICitasService citasService, IReportService reportService) 
    {
        _citasService = citasService;
        _reportService = reportService;

        // Carga inicial al construir el ViewModel
        LoadStatistics();
    }

    /// <summary>
    /// Carga los datos necesarios para InformeCita usando el Service.
    /// </summary>
    private void LoadStatistics() 
    {
        try 
        {
            _logger.Information("📊 Actualizando estadísticas del Dashboard...");

            // Obtenemos las citas activas del service (pageSize: int.MaxValue para tener todas)
            var citas = _citasService.GetAll(null, null, null, null, null, 1, int.MaxValue, false);

            // El ReportService genera el objeto "Informe" que contiene:
            // TotalCitas, PorcentajeCompletadas, PorcentajePendientes, CitasParaHoy, etc.
            Informe = _reportService.GenerarInformeEstadistico(citas);

            _logger.Information("✅ Estadísticas cargadas correctamente");
        }
        catch (Exception ex) 
        {
            _logger.Error(ex, "❌ Error al cargar estadísticas");
        }
    }

    // --- COMANDOS (Basados estrictamente en tus botones del XAML) ---

    [RelayCommand]
    private void Refrescar() 
    {
        LoadStatistics();
    }

    [RelayCommand]
    private void VerGraficos() 
    {
        // Lógica de navegación o acción para el botón GRÁFICOS 📊
        _logger.Information("Navegando a Gráficos...");
    }
}
    