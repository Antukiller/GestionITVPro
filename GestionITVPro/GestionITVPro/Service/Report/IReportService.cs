using CSharpFunctionalExtensions;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;

namespace GestionITVPro.Service.Report;

// <summary>
/// Define el contrato para la generación de informes estadísticos y visuales de las citas de ITV.
/// </summary>
public interface IReportService {
    
    /// <summary>
    /// Genera un objeto de informe con estadísticas consolidadas (Motores, Fechas, Marcas).
    /// </summary>
    /// <param name="citas">Colección de citas a analizar.</param>
    /// <returns>Objeto InformeCita con los datos calculados.</returns>
    InformeCita GenerarInformeEstadistico(IEnumerable<Cita> citas);

    /// <summary>
    /// Genera un informe en formato HTML detallado de las citas.
    /// </summary>
    /// <param name="citas">Colección de citas.</param>
    /// <param name="incluirEliminadas">Indica si se deben procesar citas marcadas como borradas.</param>
    /// <returns>Result con el string HTML o error de generación.</returns>
    Result<string, DomainError> GenerarInformeCitasHtml(
        IEnumerable<Cita> citas, 
        bool incluirEliminadas = false);

    /// <summary>
    /// Genera un informe específico de vehículos por tipo de motor (Eléctricos vs Combustión).
    /// </summary>
    /// <param name="citas">Colección de citas.</param>
    /// <returns>Result con el HTML o error de generación.</returns>
    Result<string, DomainError> GenerarInformeMotoresHtml(IEnumerable<Cita> citas);

    /// <summary>
    /// Guarda el contenido HTML generado en un archivo físico (ej: .html).
    /// </summary>
    /// <param name="html">Contenido del informe.</param>
    /// <param name="fileName">Nombre del archivo de destino.</param>
    /// <returns>Result indicando éxito o error de almacenamiento.</returns>
    Result<bool, DomainError> GuardarInformeHtml(string html, string fileName);
    
    /// <summary>
    ///     Guarda el informe HTML en un archivo.
    /// </summary>
    /// <param name="html">Contenido HTML.</param>
    /// <param name="fileName">Nombre del archivo.</param>
    /// <returns>
    ///     Result con true si se guardó correctamente o error <see cref="Errors.Report.ReportErrors.StorageError(string)" />.
    /// </returns>
    Result<bool, DomainError> GuardarInforme(string html, string fileName);
    /// <summary>
    /// Exporta el informe HTML a formato PDF.
    /// </summary>
    /// <param name="html">Contenido del informe en HTML.</param>
    /// <param name="fileName">Nombre del archivo (ej: informe.pdf).</param>
    /// <returns>Result indicando éxito o error en la conversión/guardado.</returns>
    Result<bool, DomainError> GuardarInformePdf(string html, string fileName);
}