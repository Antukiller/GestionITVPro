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
using System.Text;

namespace GestionITVPro.WPF.ViewModels.Informe;

public partial class InformeViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly IReportService _reportService;
    private readonly ICitasService _citasService;

    // --- PROPIEDADES PARA FILTROS (Vinculadas al XAML) ---
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _incluirCanceladas; // Antes era _mostrarEliminados
    [ObservableProperty] private bool _soloVehiculosEco;  // Antes era _mostrarVehiculosEletricos
    [ObservableProperty] private string _statusMessage = "";

    // --- PROPIEDADES PARA GRÁFICOS Y ESTADÍSTICAS ---
    [ObservableProperty] private int _totalCitas;
    [ObservableProperty] private int _citasParaHoy;
    [ObservableProperty] private double _cilindradaMedia;
    [ObservableProperty] private double _porcentajeVehiculosEco;
    [ObservableProperty] private DateTime _ultimaCitaProgramada;
    
    [ObservableProperty] private List<MotorStat> _motorStatsList = new();
    [ObservableProperty] private List<CitaDiaStat> _calendarioStatsList = new();

    public InformeViewModel(ICitasService citasService, IReportService reportService, IDialogService dialogService)
    {
        _citasService = citasService;
        _reportService = reportService;
        _dialogService = dialogService;
        _logger = Log.ForContext<InformeViewModel>();
        
        LoadStatistics();
    }

    // --- COMANDOS PARA LOS BOTONES DEL XAML ---
    [RelayCommand] private void GenerarInformeCitasPdf() => EjecutarGenerarCitas("PDF");
    [RelayCommand] private void GenerarInformeCitasHtml() => EjecutarGenerarCitas("HTML");
    [RelayCommand] private void GenerarInformeVehiculosPdf() => EjecutarGenerarVehiculos("PDF");
    [RelayCommand] private void GenerarInformeVehiculosHtml() => EjecutarGenerarVehiculos("HTML");
    [RelayCommand] private void GenerarInformeRendimientoPdf() => EjecutarGenerarRendimiento("PDF");
    [RelayCommand] private void GenerarInformeRendimientoHtml() => EjecutarGenerarRendimiento("HTML");

    // --- CARGA DE ESTADÍSTICAS (GRÁFICOS) ---
    [RelayCommand]
    public void LoadStatistics() 
    {
        try 
        {
            var citas = _citasService.GetAll(1, 2000, true).ToList();
            if (!citas.Any()) return;

            TotalCitas = citas.Count;
            CitasParaHoy = citas.Count(c => c.FechaInspeccion.Date == DateTime.Today);
            CilindradaMedia = citas.Average(c => c.Cilindrada);
            
            int vehiculosEco = citas.Count(c => c.Motor == Motor.Electrico || c.Motor == Motor.Hibrido);
            PorcentajeVehiculosEco = (double)vehiculosEco / TotalCitas;

            // Gráfico de Motores
            MotorStatsList = Enum.GetValues(typeof(Motor)).Cast<Motor>()
                .Select(m => {
                    int cant = citas.Count(c => c.Motor == m);
                    double porc = TotalCitas > 0 ? (double)cant / TotalCitas * 100 : 0;
                    return new MotorStat {
                        Nombre = m.ToString(),
                        Cantidad = cant,
                        Porcentaje = porc,
                        AnchoBarra = porc * 2.2,
                        ColorHex = m switch {
                            Motor.Diesel => "#FF5252",
                            Motor.Gasolina => "#FFD740",
                            Motor.Hibrido => "#00FF88",
                            _ => "#00F2FF"
                        }
                    };
                }).ToList();

            StatusMessage = $"Datos actualizados: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) 
        {
            _logger.Error(ex, "Error en estadísticas");
            StatusMessage = "Error al cargar gráficos";
        }
    }

    // --- LÓGICA DE EXPORTACIÓN ---
    private void EjecutarGenerarCitas(string formato)
    {
        ProcesarGeneracion("Informe de Citas", formato, IncluirCanceladas, SoloVehiculosEco);
    }

    private void EjecutarGenerarVehiculos(string formato)
    {
        ProcesarGeneracion("Listado de Vehículos", formato, false, SoloVehiculosEco);
    }

    private void ProcesarGeneracion(string titulo, string formato, bool canceladas, bool eco)
    {
        try
        {
            IsGenerating = true;
            StatusMessage = $"Generando {titulo}...";

            var dialog = new SaveFileDialog {
                Filter = formato == "PDF" ? "Archivo PDF|*.pdf" : "Archivo HTML|*.html",
                FileName = $"{titulo.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                var citas = _citasService.GetAll(1, 2000, canceladas)
                    .Where(c => !eco || (c.Motor == Motor.Electrico || c.Motor == Motor.Hibrido))
                    .ToList();

                string htmlBody = ConvertirDatosAHtml(titulo, citas);
                Result<bool, DomainError> result;

                if (formato == "PDF")
                    result = _reportService.GuardarInformePdf(htmlBody, dialog.FileName);
                else
                    result = _reportService.GuardarInformeHtml(htmlBody, dialog.FileName);

                if (result.IsSuccess)
                    _dialogService.ShowSuccess($"{titulo} exportado con éxito.");
                else
                    _dialogService.ShowError(result.Error.Message);
            }
        }
        finally
        {
            IsGenerating = false;
            StatusMessage = "Listo";
        }
    }

    private void EjecutarGenerarRendimiento(string formato)
    {
        try
        {
            IsGenerating = true;
            StatusMessage = $"Generando Informe de Rendimiento en {formato}...";

            var dialog = new SaveFileDialog {
                Filter = formato == "PDF" ? "Archivo PDF|*.pdf" : "Archivo HTML|*.html",
                FileName = $"Informe_Rendimiento_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                // Generamos el HTML basado en las propiedades de estadísticas que ya tienes
                string htmlBody = ConvertirEstadisticasAHtml();
                
                Result<bool, DomainError> result;

                if (formato == "PDF")
                    result = _reportService.GuardarInformePdf(htmlBody, dialog.FileName);
                else
                    result = _reportService.GuardarInformeHtml(htmlBody, dialog.FileName);

                if (result.IsSuccess)
                    _dialogService.ShowSuccess("Informe de Rendimiento exportado con éxito.");
                else
                    _dialogService.ShowError(result.Error.Message);
            }
        }
        finally
        {
            IsGenerating = false;
            StatusMessage = "Listo";
        }
    }

    // --- GENERADOR DE HTML ---
    private string ConvertirDatosAHtml(string titulo, List<Cita> citas)
    {
        StringBuilder sb = new();
        sb.Append("<html><head><style>");
        sb.Append("body { font-family: 'Segoe UI', sans-serif; background-color: #f4f4f4; padding: 30px; }");
        sb.Append("table { width: 100%; border-collapse: collapse; background: white; }");
        sb.Append("th { background: #00F2FF; color: black; padding: 10px; border: 1px solid #ddd; }");
        sb.Append("td { padding: 8px; border: 1px solid #ddd; text-align: center; }");
        sb.Append("h1 { color: #0A0A0C; border-bottom: 3px solid #00F2FF; }");
        sb.Append("</style></head><body>");
        sb.Append($"<h1>{titulo.ToUpper()} - GESTIÓN ITV PRO</h1>");
        sb.Append("<p>Reporte generado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "</p>");
        sb.Append("<table><tr><th>DNI</th><th>Matrícula</th><th>Marca</th><th>Motor</th><th>Próxima ITV</th></tr>");

        foreach (var c in citas)
        {
            sb.Append($"<tr><td>{c.DniPropietario}</td><td>{c.Matricula}</td><td>{c.Marca}</td><td>{c.Motor}</td><td>{c.FechaInspeccion:dd/MM/yyyy}</td></tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }
    
    
    private string ConvertirEstadisticasAHtml()
    {
        StringBuilder sb = new();
        sb.Append("<html><head><style>");
        sb.Append("body { font-family: 'Segoe UI', sans-serif; padding: 40px; color: #333; }");
        sb.Append(".card { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; border-left: 5px solid #00F2FF; }");
        sb.Append("h1 { color: #0A0A0C; border-bottom: 2px solid #00F2FF; }");
        sb.Append(".stat-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }");
        sb.Append("</style></head><body>");

        sb.Append("<h1>INFORME DE RENDIMIENTO Y ESTADÍSTICAS</h1>");
        sb.Append($"<p>Fecha de reporte: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");

        sb.Append("<div class='stat-grid'>");
        
        // Tarjetas de Resumen
        sb.Append($"<div class='card'><h3>Total Citas</h3><p style='font-size:24px;'>{TotalCitas}</p></div>");
        sb.Append($"<div class='card'><h3>Citas para Hoy</h3><p style='font-size:24px;'>{CitasParaHoy}</p></div>");
        sb.Append($"<div class='card'><h3>Cilindrada Media</h3><p style='font-size:24px;'>{CilindradaMedia:F2} cc</p></div>");
        sb.Append($"<div class='card'><h3>Vehículos ECO</h3><p style='font-size:24px;'>{PorcentajeVehiculosEco:P0}</p></div>");
        
        sb.Append("</div>");

        // Desglose de Motores
        sb.Append("<h2>Desglose por tipo de Motor</h2>");
        sb.Append("<table style='width:100%; border-collapse: collapse;'>");
        sb.Append("<tr style='background:#f4f4f4;'><th>Motor</th><th>Cantidad</th><th>Porcentaje</th></tr>");
        
        foreach (var stat in MotorStatsList)
        {
            sb.Append($"<tr><td>{stat.Nombre}</td><td>{stat.Cantidad}</td><td>{stat.Porcentaje:F1}%</td></tr>");
        }
        
        sb.Append("</table>");

        sb.Append("</body></html>");
        return sb.ToString();
    }
    
    
}

// --- CLASES DE APOYO ---
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