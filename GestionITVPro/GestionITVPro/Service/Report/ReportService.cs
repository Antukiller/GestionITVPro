using System.IO;
using System.Text;
using CSharpFunctionalExtensions;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Errors.Report;
using GestionITVPro.Models;
using Serilog;

namespace GestionITVPro.Service.Report;

public class ReportService : IReportService {
    private const string DateFormat = "dd/MM/yyyy";
    private readonly ILogger _logger = Log.ForContext<ReportService>();
    private readonly string _reportDirectory;

    public ReportService(string reportDirectory) {
        _reportDirectory = reportDirectory;
        _logger.Debug("Inicualizando la clase ReportService con directorio {Directory}", _reportDirectory);
        
    }

    public InformeCita GenerarInformeEstadistico(IEnumerable<Cita> citas)
    {
        var list = citas.ToList();
        var hoy = DateTime.Today;

        // 1. Separamos las activas (no borradas) de las completadas (borradas)
        var activas = list.Where(c => !c.IsDeleted).ToList();
        var completadas = list.Where(c => c.IsDeleted).ToList();

        return new InformeCita
        {
            // Total absoluto (debe dar 80)
            TotalCitas = list.Count,
        
            // Basado en IsDeleted = true (debe dar 40)
            CitasCompletadas = completadas.Count,
    
            // Basado en IsDeleted = false y Fecha anterior a hoy (debe dar 10)
            CitasAtrasadas = activas.Count(c => c.FechaInspeccion.Date < hoy),
        
            // Basado en IsDeleted = false y Fecha exactamente hoy (debe dar 20)
            CitasParaHoy = activas.Count(c => c.FechaInspeccion.Date == hoy),

            // Conteo de motores (sobre el total de 80)
            Gasolina = list.Count(c => c.Motor == Motor.Gasolina),
            Diesel = list.Count(c => c.Motor == Motor.Diesel),
            Electrico = list.Count(c => c.Motor == Motor.Electrico),
            Hibrido = list.Count(c => c.Motor == Motor.Hibrido),
    
            // Fecha más lejana en el calendario
            UltimaCitaProgramada = list.Any() ? list.Max(c => c.FechaInspeccion) : null
        };
    }

    public Result<string, DomainError> GenerarInformeCitasHtml(IEnumerable<Cita> citas, bool incluirEliminadas = false) {
    try {
        var lista = incluirEliminadas ? citas : citas.Where(c => !c.IsDeleted);
        var stats = GenerarInformeEstadistico(lista);

        var html = $@"
        <html>
        <head>
            <style>
                body {{ font-family: sans-serif; margin: 40px; color: #333; }}
                h1 {{ color: #2c3e50; border-bottom: 2px solid #3498db; }}
                .summary {{ background: #f8f9fa; padding: 15px; border-radius: 8px; margin-bottom: 20px; }}
                table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
                th {{ background: #3498db; color: white; padding: 12px; text-align: left; }}
                td {{ padding: 10px; border-bottom: 1px solid #ddd; }}
                tr:nth-child(even) {{ background: #f2f2f2; }}
                .badge-eco {{ background: #2ecc71; color: white; padding: 4px 8px; border-radius: 4px; font-size: 0.8em; }}
            </style>
        </head>
        <body>
            <h1>Resumen de Citas ITV</h1>
            <div class='summary'>
                <p><strong>Total de Citas:</strong> {stats.TotalCitas}</p>
                <p><strong>Citas para Hoy:</strong> {stats.CitasParaHoy}</p>
                <p><strong>Citas Atrasadas:</strong> <span style='color:red'>{stats.CitasAtrasadas}</span></p>
                <p><strong>Vehículos ECO:</strong> {stats.TotalEco} ({stats.PorcentajeVehiculosEco:F2}%)</p>
            </div>
            <table>
                <thead>
                    <tr>
                        <th>Fecha</th>
                        <th>Matrícula</th>
                        <th>Vehículo</th>
                        <th>Motor</th>
                        <th>DNI Propietario</th>
                    </tr>
                </thead>
                <tbody>
                    {string.Join("", lista.Select(c => $@"
                    <tr>
                        <td>{c.FechaItv:dd/MM/yyyy HH:mm}</td>
                        <td><strong>{c.Matricula}</strong></td>
                        <td>{c.Marca} {c.Modelo}</td>
                        <td>{c.Motor} {(c.Motor == Motor.Electrico || c.Motor == Motor.Hibrido ? "<span class='badge-eco'>ECO</span>" : "")}</td>
                        <td>{c.DniPropietario}</td>
                    </tr>"))}
                </tbody>
            </table>
        </body>
        </html>";

        return Result.Success<string, DomainError>(html);
    }
    catch (Exception ex) {
        return Result.Failure<string, DomainError>(CitaErrors.DatabaseError($"Error al generar HTML: {ex.Message}"));
    }
}
    public Result<string, DomainError> GenerarInformeMotoresHtml(IEnumerable<Cita> citas) {
        var stats = GenerarInformeEstadistico(citas);
    
        var html = $@"
    <html>
    <head><style>/* Estilos similares al anterior */</style></head>
    <body>
        <h1>Estadísticas de Motorización</h1>
        <ul>
            <li><strong>Gasolina:</strong> {stats.Gasolina}</li>
            <li><strong>Diesel:</strong> {stats.Diesel}</li>
            <li><strong>Híbrido:</strong> {stats.Hibrido}</li>
            <li><strong>Eléctrico:</strong> {stats.Electrico}</li>
        </ul>
        <hr>
        <h3>Total ECO: {stats.TotalEco}</h3>
    </body>
    </html>";

        return Result.Success<string, DomainError>(html);
    }

    public Result<bool, DomainError> GuardarInformeHtml(string html, string fileName) {
        try {
            File.WriteAllText(fileName, html);
            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            return Result.Failure<bool, DomainError>(CitaErrors.DatabaseError($"No se pudo guardar el archivo: {ex.Message}"));
        }
    }

    public Result<bool, DomainError> GuardarInforme(string html, string fileName) {
        var directory = _reportDirectory;
        _logger.Information("Guardando informe en directorio {Directory}", directory);

        try {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, html, Encoding.UTF8);

            _logger.Information("Informe guardado correctamente en {FilePath}", filePath);
            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al guardar informe");
            return Result.Failure<bool, DomainError>(
                ReportErrors.SaveError(ex.Message));
        }
    }

    public Result<bool, DomainError> GuardarInformePdf(string html, string fileName) {
        try {
            _logger.Information("Iniciando conversión de HTML a PDF para {FileName}", fileName);

            // 1. Crear el objeto convertidor de SelectPdf
            SelectPdf.HtmlToPdf converter = new SelectPdf.HtmlToPdf();

            // 2. Opciones de configuración (Opcional, para que se vea profesional)
            converter.Options.PdfPageSize = SelectPdf.PdfPageSize.A4;
            converter.Options.PdfPageOrientation = SelectPdf.PdfPageOrientation.Portrait;
            converter.Options.MarginTop = 20;
            converter.Options.MarginBottom = 20;

            // 3. Convertir el string HTML que recibimos a un documento PDF
            SelectPdf.PdfDocument doc = converter.ConvertHtmlString(html);

            // 4. Guardar el archivo físicamente en la ruta que eligió el usuario
            doc.Save(fileName);

            // 5. IMPORTANTE: Cerrar el documento para liberar el archivo y que aparezca en el PC
            doc.Close();

            _logger.Information("PDF generado y guardado con éxito en {Path}", fileName);
            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error crítico al convertir a PDF");
            return Result.Failure<bool, DomainError>(CitaErrors.DatabaseError($"Error en conversión PDF: {ex.Message}"));
        }
    }
}