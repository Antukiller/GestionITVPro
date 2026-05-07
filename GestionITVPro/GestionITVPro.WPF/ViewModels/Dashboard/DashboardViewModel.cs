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

    [ObservableProperty] 
    private InformeCita _informe;

    // Acción para comunicar la navegación a la View (C# del DashboardView)
    public Action<string>? NavigateAction { get; set; }

    public DashboardViewModel(ICitasService citasService, IReportService reportService) 
    {
        _citasService = citasService;
        _reportService = reportService;
        LoadStatistics();
    }

    private void LoadStatistics() 
    {
        try 
        {
            _logger.Information("📊 Cargando estadísticas del Dashboard...");
        
            // ✅ CAMBIO: Cambiamos el último parámetro a 'true'
            // Esto permite que el ReportService vea los coches con IsDeleted = true
            var citas = _citasService.GetAll(1, int.MaxValue, true);
        
            Informe = _reportService.GenerarInformeEstadistico(citas);
        
            _logger.Information("✅ Informe generado: {Total} citas, {Completadas} completadas", 
                Informe.TotalCitas, Informe.CitasCompletadas);
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
        // Esto dispara el evento que escuchará el DashboardView.xaml.cs
        NavigateAction?.Invoke("Graficos");
    }
}