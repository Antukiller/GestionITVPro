using CSharpFunctionalExtensions;
using GestionITVPro.Errors.Backup;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;

namespace GestionITVPro.Service.Backup;

/// <summary>
///     Define el contrato para la gestión de backups del sistema.
/// </summary>
public interface IBackupService {
    /// <summary>
    ///     Realiza un backup de las personas en un archivo ZIP.
    /// </summary>
    /// <param name="vehiculos">Lista de personas a respaldar.</param>
    /// <param name="customBackupDirectory">Directorio personalizado para guardar el backup (opcional).</param>
    /// <returns>
    ///     Result con la ruta del archivo ZIP creado o error:
    ///     <see cref="BackupError.CreationError" />,
    ///     <see cref="BackupErrors.DirectoryError(string)" /> o
    ///     <see cref="BackupErrors.InvalidBackupFile(string)" />.
    /// </returns>
    Result<string, DomainError> RealizarBackup(
        IEnumerable<Cita> vehiculos,
        string? customBackupDirectory = null);

    /// <summary>
    ///     Restaura un backup desde un archivo ZIP.
    /// </summary>
    /// <param name="archivoBackup">Ruta del archivo ZIP.</param>
    /// <param name="customImagesDirectory">Directorio personalizado para restaurar imágenes (opcional).</param>
    /// <returns>
    ///     Result con la lista de personas restauradas o error:
    ///     <see cref="BackupErrors.FileNotFound(string)" />,
    ///     <see cref="BackupErrors.InvalidBackupFile(string)" /> o
    ///     <see cref="BackupErrors.RestorationError(string)" />.
    /// </returns>
    Result<IEnumerable<Cita>, DomainError> RestaurarBackup(
        string archivoBackup,
        string? customImagesDirectory = null);

    /// <summary>
    ///     Lista los archivos de backup disponibles en el directorio.
    /// </summary>
    /// <param name="customBackupDirectory">Directorio personalizado (opcional).</param>
    /// <returns>Enumerable con las rutas de los archivos de backup.</returns>
    IEnumerable<string> ListarBackups(string? customBackupDirectory = null);

    /// <summary>
    ///     Realiza un backup completo del sistema incluyendo datos y metadatos.
    /// </summary>
    /// <param name="citas">Lista de personas a respaldar.</param>
    /// <returns>
    ///     Result con la ruta del archivo ZIP o error:
    ///     <see cref="BackupErrors.CreationError(string)" />,
    ///     <see cref="BackupErrors.DirectoryError(string)" /> o
    ///     <see cref="BackupErrors.InvalidBackupFile(string)" />.
    /// </returns>
    Result<string, DomainError> RealizarBackupSistema(IEnumerable<Cita> citas);

    /// <summary>
    ///     Restaura un backup completo del sistema.
    ///     Borra todos los datos e imágenes existentes antes de restaurar.
    /// </summary>
    /// <param name="archivoBackup">Ruta del archivo ZIP.</param>
    /// <param name="deleteAllCallback">Función para borrar todos los datos existentes.</param>
    /// <param name="createCallback">Función para crear cada persona en el repositorio.</param>
    /// <returns>
    ///     Result con el número de personas restauradas o error:
    ///     <see cref="BackupErrors.FileNotFound(string)" />,
    ///     <see cref="BackupErrors.InvalidBackupFile(string)" /> o
    ///     <see cref="BackupErrors.RestorationError(string)" />.
    /// </returns>
    Result<int, DomainError> RestaurarBackupSistema(string archivoBackup,
        Func<bool> deleteAllCallback,
        Func<Cita, Result<Cita, DomainError>> createCallback);
}