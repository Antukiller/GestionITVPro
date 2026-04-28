using CSharpFunctionalExtensions;
using GestionITVPro.Errors.Common;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Models;

namespace GestionITVPro.Service.ImportExport;

/// <summary>
///     Define el contrato para importar y exportar datos del sistema.
/// </summary>
public interface IImportExportService {
    /// <summary>
    ///     Exporta personas a un archivo en el formato configurado.
    /// </summary>
    /// <param name="citas">Enumerable de personas a exportar.</param>
    /// <param name="path">Ruta del archivo de destino.</param>
    /// <returns>
    ///     Result con el número de personas exportadas o error:
    ///     <see cref="StorageError.InvalidFormat" /> o
    ///     <see cref="StorageError.WriteError" />.
    /// </returns>
    Result<int, DomainError> ExportarDatos(IEnumerable<Cita> citas, string path);

    /// <summary>
    ///     Importa personas desde un archivo.
    /// </summary>
    /// <param name="path">Ruta del archivo a importar.</param>
    /// <returns>
    ///     Result con la lista de personas importadas o error:
    ///     <see cref="StorageErrors.FileNotFound(string)" />,
    ///     <see cref="StorageError.InvalidFormat" /> o
    ///     <see cref="StorageError.ReadError" />.
    /// </returns>
    Result<IEnumerable<Cita>, DomainError> ImportarDatos(string path);

    /// <summary>
    ///     Exporta personas usando el directorio configurado en el sistema.
    /// </summary>
    /// <param name="citas">Enumerable de personas a exportar.</param>
    /// <returns>
    ///     Result con el número de personas exportadas o error:
    ///     <see cref="StorageError.InvalidFormat" /> o
    ///     <see cref="StorageError.WriteError" />.
    /// </returns>
    Result<int, DomainError> ExportarDatosSistema(IEnumerable<Cita> citas);

    /// <summary>
    ///     Importa personas desde el directorio configurado en el sistema.
    /// </summary>
    /// <param name="path">Ruta del archivo a importar.</param>
    /// <returns>
    ///     Result con la lista de personas importadas o error:
    ///     <see cref="StorageErrors.FileNotFound(string)" />,
    ///     <see cref="StorageError.InvalidFormat" /> o
    ///     <see cref="StorageError.ReadError" />.
    /// </returns>
    Result<IEnumerable<Cita>, DomainError> ImportarDatosSistema(string path);
}