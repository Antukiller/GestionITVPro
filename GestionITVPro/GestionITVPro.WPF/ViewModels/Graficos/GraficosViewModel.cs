using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Enums;
using GestionITVPro.Models;
using GestionITVPro.Service.Citas;
using Serilog;
using System.Collections.ObjectModel;

namespace GestionITVPro.WPF.ViewModels.Graficos;

public partial class GraficoViewModel : ObservableObject {
    private readonly ILogger _logger = Log.ForContext<GraficoViewModel>();
    private readonly ICitasService _citasService;

    [ObservableProperty] private int _totalCitas;
    [ObservableProperty] private int _citasParaHoy;
    [ObservableProperty] private double _cilindradaMedia;
    [ObservableProperty] private double _porcentajeVehiculosEco;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private DateTime _ultimaCitaProgramada;

    // Usamos ObservableCollection para que la vista se entere de los cambios de la lista
    public ObservableCollection<MotorStatItem> MotorStatsList { get; } = new();
    public ObservableCollection<CalendarioStatItem> CalendarioStatsList { get; } = new();

    public GraficoViewModel(ICitasService citasService) {
        _citasService = citasService;
        LoadStatistics();
    }

    [RelayCommand]
    private void LoadStatistics() {
        try {
            var todas = _citasService.GetCitasOrderBy(TipoOrdenamiento.Matricula, 1, 2000, false).ToList();
            
            if (todas == null || !todas.Any()) {
                // DATOS DE PRUEBA (Si la base de datos está vacía)
                TotalCitas = 100;
                CitasParaHoy = 12;
                CilindradaMedia = 1600;
                PorcentajeVehiculosEco = 0.35;
                UltimaCitaProgramada = DateTime.Now;
            } else {
                TotalCitas = todas.Count;
                CitasParaHoy = todas.Count(c => c.FechaInspeccion.Date == DateTime.Today);
                CilindradaMedia = todas.Average(c => c.Cilindrada);
                UltimaCitaProgramada = todas.Max(c => c.FechaInspeccion);
                var ecos = todas.Count(c => c.Motor == Motor.Electrico || c.Motor == Motor.Hibrido);
                PorcentajeVehiculosEco = (double)ecos / TotalCitas;
            }

            CalcularStatsMotores(todas ?? new List<Cita>());
            CalcularStatsCalendario(todas ?? new List<Cita>());

            StatusMessage = "Sincronización completada";
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error en dashboard");
            StatusMessage = "Error de conexión";
        }
    }

    private void CalcularStatsMotores(List<Cita> citas) {
        MotorStatsList.Clear();
        
        if (!citas.Any()) {
            // Mock data para ver las barras si no hay citas
            MotorStatsList.Add(new MotorStatItem { Nombre = "Gasolina", Cantidad = 45, Porcentaje = 45, ColorHex = "#FFB800" });
            MotorStatsList.Add(new MotorStatItem { Nombre = "Diesel", Cantidad = 30, Porcentaje = 30, ColorHex = "#E44D26" });
            MotorStatsList.Add(new MotorStatItem { Nombre = "Eléctrico", Cantidad = 15, Porcentaje = 15, ColorHex = "#00FF88" });
            MotorStatsList.Add(new MotorStatItem { Nombre = "Híbrido", Cantidad = 10, Porcentaje = 10, ColorHex = "#A0FF00" });
        } else {
            var grupos = citas.GroupBy(c => c.Motor);
            foreach (var g in grupos) {
                MotorStatsList.Add(new MotorStatItem {
                    Nombre = g.Key.ToString(),
                    Cantidad = g.Count(),
                    Porcentaje = (double)g.Count() / citas.Count * 100,
                    ColorHex = GetColorForMotor(g.Key)
                });
            }
        }
    }

    private void CalcularStatsCalendario(List<Cita> citas) {
        CalendarioStatsList.Clear();
        var random = new Random();

        for (int i = 0; i < 7; i++) {
            var fecha = DateTime.Today.AddDays(i);
            int cant = citas.Count(c => c.FechaInspeccion.Date == fecha);
            
            // Si no hay datos, inventamos para el gráfico
            if (!citas.Any()) cant = random.Next(2, 12);

            CalendarioStatsList.Add(new CalendarioStatItem {
                Etiqueta = i == 0 ? "HOY" : fecha.ToString("dd/MM"),
                Cantidad = cant,
                AlturaBarra = Math.Min(cant * 15, 180) // Factor de escala
            });
        }
    }

    private string GetColorForMotor(Motor m) => m switch {
        Motor.Gasolina => "#FFB800",
        Motor.Diesel => "#E44D26",
        Motor.Electrico => "#00FF88",
        Motor.Hibrido => "#A0FF00",
        _ => "#00F2FF"
    };
    
    
    
}

// Clases de apoyo (Asegúrate de que estén fuera de la clase principal o en archivos aparte)
public class MotorStatItem {
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
    public string ColorHex { get; set; } = "#FFFFFF";
    public double AnchoBarra => Porcentaje * 2.2; // Multiplicador para que la barra crezca en el UI
}

public class CalendarioStatItem {
    public string Etiqueta { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public double AlturaBarra { get; set; }
}