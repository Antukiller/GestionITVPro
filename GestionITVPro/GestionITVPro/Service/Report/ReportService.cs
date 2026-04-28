using CSharpFunctionalExtensions;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
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
        var proximaSemana = hoy.AddDays(7);

        return new InformeCita
        {
            ListadoCitas = list.OrderBy(c => c.FechaItv),
            TotalCitas = list.Count,

            // Conteo por tipos de motor
            Gasolina = list.Count(c => c.Motor == Motor.Gasolina),
            Diesel = list.Count(c => c.Motor == Motor.Diesel),
            Hibrido = list.Count(c => c.Motor == Motor.Hibrido),
            Electrico = list.Count(c => c.Motor == Motor.Electrico),

            // Métricas de fechas
            CitasParaHoy = list.Count(c => c.FechaItv.Date == hoy),
            CitasAtrasadas = list.Count(c => c.FechaItv.Date < hoy && !c.IsDeleted),
            CitasProximaSemana = list.Count(c => c.FechaItv.Date > hoy && c.FechaItv.Date <= proximaSemana),
            UltimaCitaProgramada = list.Any() ? list.Max(c => c.FechaItv) : null,

            // Otras métricas
            CilindradaMedia = list.Any() ? list.Average(c => c.Cilindrada) : 0,
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
        <h3>Total Combustión: {stats.TotalCombustion}</h3>
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

    public Result<bool, DomainError> GuardarInformePdf(string html, string fileName) {
        try {
            // NOTA: Para generar un PDF real necesitas una librería externa.
            // Aquí te dejo el placeholder donde invocarías a tu conversor (ej: DinkToPdf o iText)
            _logger.Information("Iniciando conversión de HTML a PDF para {FileName}", fileName);
        
            // Simulación:
            // byte[] pdfBytes = _pdfConverter.Convert(html);
            // File.WriteAllBytes(fileName, pdfBytes);
        
            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            return Result.Failure<bool, DomainError>(CitaErrors.DatabaseError($"Error en conversión PDF: {ex.Message}"));
        }
    }
}