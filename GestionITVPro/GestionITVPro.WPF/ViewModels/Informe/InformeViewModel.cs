using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Enums;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.Service.Report;
using Microsoft.Win32;
using Serilog;
using CSharpFunctionalExtensions;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GestionITVPro.WPF.ViewModels.Informe;

public partial class InformeViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly IReportService _reportService;
    private readonly ICitasService _citasService;

    // --- PROPIEDADES PARA INFORMES ---
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _mostrarEliminados;
    [ObservableProperty] private bool _mostrarVehiculosEletricos;
    [ObservableProperty] private Motor? _selectedMotor;
    [ObservableProperty] private string _statusMessage = "";

    // --- PROPIEDADES PARA GRÁFICOS Y TARJETAS (Faltaban estas) ---
    [ObservableProperty] private int _totalCitas;
    [ObservableProperty] private int _citasParaHoy;
    [ObservableProperty] private double _cilindradaMedia;
    [ObservableProperty] private double _porcentajeVehiculosEco;
    [ObservableProperty] private DateTime _ultimaCitaProgramada;
    
    // Las listas deben ser de las clases de apoyo, no de Motor o Cita directamente
    [ObservableProperty] private List<MotorStat> _motorStatsList = new();
    [ObservableProperty] private List<CitaDiaStat> _calendarioStatsList = new();

    public IEnumerable<Motor> Motors => Enum.GetValues<Motor>();

    public InformeViewModel(
        ICitasService citasService,
        IReportService reportService,
        IDialogService dialogService
    )
    {
        _citasService = citasService;
        _reportService = reportService;
        _dialogService = dialogService;
        _logger = Log.ForContext<InformeViewModel>();
        
        // Cargar estadísticas al iniciar
        LoadStatistics();
    }

    // --- COMANDOS INFORMES ---
    [RelayCommand] private void GenerarInformeCitasPdf() => EjecutarGenerarCitas("PDF");
    [RelayCommand] private void GenerarInformeCitasHtml() => EjecutarGenerarCitas("HTML");
    [RelayCommand] private void GenerarInformeVehiculosPdf() => EjecutarGenerarVehiculos("PDF");
    [RelayCommand] private void GenerarInformeVehiculosHtml() => EjecutarGenerarVehiculos("HTML");
    [RelayCommand] private void GenerarInformeRendimientoPdf() => EjecutarGenerarRendimiento("PDF");
    [RelayCommand] private void GenerarInformeRendimientoHtml() => EjecutarGenerarRendimiento("HTML");

    // --- LÓGICA DE ESTADÍSTICAS (GRÁFICOS) ---
    [RelayCommand]
    public void LoadStatistics() 
    {
        try 
        {
            // Usamos GetAll con parámetros por defecto para traer todo
            var citas = _citasService.GetAll(1, 2000, true).ToList();
            int total = citas.Count;
            
            // 1. Cálculos de Tarjetas
            TotalCitas = total;
            CitasParaHoy = citas.Count(c => c.FechaInspeccion.Date == DateTime.Today);
            CilindradaMedia = citas.Any() ? citas.Average(c => c.Cilindrada) : 0;
            
            int vehiculosEco = citas.Count(c => c.Motor == Motor.Electrico || c.Motor == Motor.Hibrido);
            PorcentajeVehiculosEco = total > 0 ? (double)vehiculosEco / total : 0;
            UltimaCitaProgramada = citas.Any() ? citas.Max(c => c.FechaInspeccion) : DateTime.Today;

            // 2. Gráfico de Motores
            MotorStatsList = Enum.GetValues(typeof(Motor)).Cast<Motor>()
                .Select(m => {
                    int cant = citas.Count(c => c.Motor == m);
                    double porc = total > 0 ? (double)cant / total * 100 : 0;
                    return new MotorStat {
                        Nombre = m.ToString(),
                        Cantidad = cant,
                        Porcentaje = porc,
                        AnchoBarra = porc * 2.2,
                        ColorHex = GetColorForMotor(m)
                    };
                }).ToList();

            // 3. Gráfico de Calendario
            var proximosDias = Enumerable.Range(0, 7).Select(d => DateTime.Today.AddDays(d)).ToList();
            int maxCitasEnUnDia = proximosDias.Any() 
                ? proximosDias.Max(d => citas.Count(c => c.FechaInspeccion.Date == d.Date)) 
                : 1;

            CalendarioStatsList = proximosDias.Select(fecha => {
                int count = citas.Count(c => c.FechaInspeccion.Date == fecha.Date);
                return new CitaDiaStat {
                    Etiqueta = fecha.ToString("dd/MM"),
                    Cantidad = count,
                    AlturaBarra = maxCitasEnUnDia > 0 ? ((double)count / Math.Max(maxCitasEnUnDia, 1)) * 150 : 0
                };
            }).ToList();

            StatusMessage = $"Actualizado: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) 
        {
            _logger.Error(ex, "Error al cargar estadísticas");
            StatusMessage = "Error de conexión";
        }
    }

    private string GetColorForMotor(Motor m) => m switch {
        Motor.Diesel => "#FF5252",
        Motor.Gasolina => "#FFD740",
        Motor.Hibrido => "#00FF88",
        _ => "#00F2FF"
    };

    // --- LÓGICA DE INFORMES (Omitida por brevedad, mantenla igual que la tenías) ---
    private void EjecutarGenerarCitas(string formato) { /* Tu código anterior... */ }
    private void EjecutarGenerarVehiculos(string formato) { /* Tu código anterior... */ }
    private void EjecutarGenerarRendimiento(string formato) { /* Tu código anterior... */ }
    private void ProcesarGuardado(Result<string, DomainError> result, string tipo, string formato) { /* Tu código anterior... */ }
}

// CLASES DE APOYO (Fuera de la clase principal)
public class MotorStat {
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
    public double AnchoBarra { get; set; }
    public string ColorHex { get; set; } = "#FFFFFF";
}

public class CitaDiaStat {
    public string Etiqueta { get; set; } = "";
    public int Cantidad { get; set; }
    public double AlturaBarra { get; set; }
}