using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Models;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Report;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Dashboard;

public partial class DashboardView : ObservableObject 
{
    private readonly ILogger _logger = Log.ForContext<DashboardViewModel>();
    private readonly ICitasService _citasService;
    private readonly IReportService _reportService;

    [ObservableProperty] 
    private InformeCita _informe;

    // Acción para comunicar la navegación a la View
    public Action<string>? NavigateAction { get; set; }

    public DashboardView(ICitasService citasService, IReportService reportService) 
    {
        _citasService = citasService;
        _reportService = reportService;
        LoadStatistics();
    }

    private void LoadStatistics() 
    {
        try 
        {
            // Obtenemos todas las citas activas para generar las estadísticas
            var citas = _citasService.GetAll(null, null, null, null, null, 1, int.MaxValue, false);
            Informe = _reportService.GenerarInformeEstadistico(citas);
        }
        catch (Exception ex) 
        {
            _logger.Error(ex, "❌ Error al cargar estadísticas");
        }
    }

    [RelayCommand]
    private void Refrescar() => LoadStatistics();

    [RelayCommand]
    private void VerGraficos() 
    {
        // Solo lanzamos la navegación si la vista está escuchando
        NavigateAction?.Invoke("Graficos");
    }
}