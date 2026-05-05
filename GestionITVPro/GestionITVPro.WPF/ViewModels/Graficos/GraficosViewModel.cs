using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Enums;
using GestionITVPro.Models;
using GestionITVPro.Service.Citas;
using Serilog;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GestionITVPro.WPF.ViewModels;

public partial class GraficosViewModel : ObservableObject 
{
    private readonly ILogger _logger = Log.ForContext<GraficosViewModel>();
    private readonly ICitasService _citasService;

    // --- PROPIEDADES PARA LAS TARJETAS (STATS) ---
    [ObservableProperty] private int _totalCitas;
    [ObservableProperty] private int _citasParaHoy;
    [ObservableProperty] private double _cilindradaMedia;
    [ObservableProperty] private double _porcentajeVehiculosEco;
    [ObservableProperty] private DateTime _ultimaCitaProgramada;
    [ObservableProperty] private string _statusMessage = "";

    // --- PROPIEDADES PARA LOS GRÁFICOS (LiveCharts) ---
    [ObservableProperty] private IEnumerable<ISeries> _motorSeries;
    [ObservableProperty] private IEnumerable<ISeries> _ecoSeries;
    [ObservableProperty] private IEnumerable<ISeries> _calendarioSeries;
    [ObservableProperty] private Axis[] _xAxesCalendario;

    public GraficosViewModel(ICitasService citasService) 
    {
        _citasService = citasService;
        LoadStatistics();
    }

    [RelayCommand]
    private void LoadStatistics() 
    {
        try 
        {
            var citas = _citasService.GetAll().ToList();
            
            // 1. Cálculos de Tarjetas
            TotalCitas = citas.Count;
            CitasParaHoy = citas.Count(c => c.FechaInspeccion.Date == DateTime.Today);
            CilindradaMedia = citas.Any() ? citas.Average(c => c.Cilindrada) : 0;
            
            int vehiculosEco = citas.Count(c => c.Motor == Motor.Electrico || c.Motor == Motor.Hibrido);
            PorcentajeVehiculosEco = citas.Any() ? (double)vehiculosEco / TotalCitas : 0;
            
            UltimaCitaProgramada = citas.Any() ? citas.Max(c => c.FechaInspeccion) : DateTime.Today;

            // 2. Gráfico de Motores (PieChart)
            MotorSeries = new ISeries[]
            {
                new PieSeries<int> { Values = new[] { citas.Count(c => c.Motor == Motor.Diesel) }, Name = "Diésel" },
                new PieSeries<int> { Values = new[] { citas.Count(c => c.Motor == Motor.Gasolina) }, Name = "Gasolina" },
                new PieSeries<int> { Values = new[] { citas.Count(c => c.Motor == Motor.Hibrido) }, Name = "Híbrido" },
                new PieSeries<int> { Values = new[] { citas.Count(c => c.Motor == Motor.Electrico) }, Name = "Eléctrico" }
            };

            // 3. Gráfico Eco vs Combustión
            EcoSeries = new ISeries[]
            {
                new PieSeries<int> { Values = new[] { vehiculosEco }, Name = "ECO", Fill = new SolidColorPaint(SKColors.SpringGreen) },
                new PieSeries<int> { Values = new[] { TotalCitas - vehiculosEco }, Name = "Combustión", Fill = new SolidColorPaint(SKColors.Crimson) }
            };

            // 4. Gráfico de Calendario (Próximos 7 días)
            var dias = Enumerable.Range(0, 7).Select(d => DateTime.Today.AddDays(d)).ToList();
            var conteoPorDia = dias.Select(d => citas.Count(c => c.FechaInspeccion.Date == d.Date)).ToArray();

            CalendarioSeries = new ISeries[]
            {
                new ColumnSeries<int> 
                { 
                    Values = conteoPorDia, 
                    Name = "Citas",
                    Fill = new SolidColorPaint(SKColors.Cyan)
                }
            };

            XAxesCalendario = new Axis[] { new Axis { Labels = dias.Select(d => d.ToString("dd/MM")).ToArray() } };

            StatusMessage = "Datos actualizados";
        }
        catch (Exception ex) 
        {
            _logger.Error(ex, "Error al cargar estadísticas");
            StatusMessage = "Error de conexión";
        }
    }
}