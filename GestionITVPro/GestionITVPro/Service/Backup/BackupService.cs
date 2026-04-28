using System.IO.Compression;
using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Errors.Common;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Models;
using GestionITVPro.Storage.Common;
using Serilog;

namespace GestionITVPro.Service.Backup;
public class BackupService(
    IStorage<Cita> storage,
    string? defaultBackupDirectory = null
) : IBackupService {
    private readonly string _defaultBackupDirectory = defaultBackupDirectory ?? Path.Combine(AppConfig.DataFolder, "backups");
    private readonly ILogger _logger = Log.ForContext<BackupService>();

    public Result<string, DomainError> RealizarBackup(IEnumerable<Cita> citas) {
        // Llamamos a la sobrecarga que acepta el directorio por defecto para no repetir código
        return RealizarBackup(citas, _defaultBackupDirectory);
    }

    public Result<string, DomainError> RealizarBackup(IEnumerable<Cita> citas, string? customBackupDirectory = null) {
        var backDirectory = customBackupDirectory ?? _defaultBackupDirectory;
        _logger.Information("Iniciando backup de ITV en: {dir}", backDirectory);

        var citasList = citas.ToList();
        if (citasList.Count == 0) {
            return Result.Failure<string, DomainError>(StorageErrors.WriteError("No hay citas para respaldar."));
        }

        try {
            Directory.CreateDirectory(backDirectory);
        } catch (Exception ex) {
            return Result.Failure<string, DomainError>(StorageErrors.WriteError($"Error al crear directorio: {ex.Message}"));
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"itv-backup-{Guid.NewGuid()}");
        var dataDir = Path.Combine(tempDir, "data");

        try {
            Directory.CreateDirectory(dataDir);

            // 1. Guardar datos en data/citas.json
            var jsonPath = Path.Combine(dataDir, "citas.json");
            var salvarResult = storage.Salvar(citasList, jsonPath);
            
            if (salvarResult.IsFailure) return Result.Failure<string, DomainError>(salvarResult.Error);

            // 2. Crear el archivo comprimido .zip
            var fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var zipPath = Path.Combine(backDirectory, $"Backup_ITV_{fecha}.zip");

            ZipFile.CreateFromDirectory(tempDir, zipPath);

            _logger.Information("Backup ITV completado: {path}", zipPath);
            return Result.Success<string, DomainError>(zipPath);

        } catch (Exception ex) {
            _logger.Error(ex, "Fallo crítico en el proceso de backup");
            return Result.Failure<string, DomainError>(StorageErrors.WriteError(ex.Message));
        } finally {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    public Result<IEnumerable<Cita>, DomainError> RestaurarBackup(string archivoZip) {
        return RestaurarBackup(archivoZip, null);
    }

    public Result<IEnumerable<Cita>, DomainError> RestaurarBackup(string archivoZip, string? customImagesDirectory = null) {
        if (!File.Exists(archivoZip)) 
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.FileNotFound(archivoZip));

        var tempDir = Path.Combine(Path.GetTempPath(), $"itv-restore-{Guid.NewGuid()}");
        
        try {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(archivoZip, tempDir);

            var jsonPath = Path.Combine(tempDir, "data", "citas.json"); // Cambiado vehiculos.json a citas.json
            if (!File.Exists(jsonPath))
                return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.InvalidFormat("El backup no contiene citas.json"));

            return storage.Cargar(jsonPath);

        } catch (Exception ex) {
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.ReadError(ex.Message));
        } finally {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    public IEnumerable<string> ListarBackups(string? customBackupDirectory = null) {
        var path = customBackupDirectory ?? _defaultBackupDirectory;
        if (!Directory.Exists(path)) return Enumerable.Empty<string>();
        
        return Directory.GetFiles(path, "*.zip")
                        .OrderByDescending(f => File.GetCreationTime(f));
    }

    public Result<string, DomainError> RealizarBackupSistema(IEnumerable<Cita> citas) {
        // En una implementación básica, es lo mismo que RealizarBackup
        return RealizarBackup(citas);
    }

    public Result<int, DomainError> RestaurarBackupSistema(string archivoBackup, Func<bool> deleteAllCallback, Func<Cita, Result<Cita, DomainError>> createCallback, string? customImagesDirectory = null) {
        return RestaurarBackup(archivoBackup)
            .Bind(citas => {
                _logger.Warning("Iniciando restauración de sistema. Eliminando datos actuales...");
                
                if (!deleteAllCallback()) 
                    return Result.Failure<int, DomainError>(StorageErrors.WriteError("Error al limpiar la base de datos antes de restaurar."));

                int restaurados = 0;
                foreach (var cita in citas) {
                    var res = createCallback(cita);
                    if (res.IsSuccess) restaurados++;
                }

                _logger.Information("Restauración completada. {n} citas importadas.", restaurados);
                return Result.Success<int, DomainError>(restaurados);
            });
    }
}