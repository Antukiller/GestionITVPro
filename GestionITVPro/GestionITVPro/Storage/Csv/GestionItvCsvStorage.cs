using System.Text;
using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Dto;
using GestionITVPro.Errors.Common;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using Serilog;

namespace GestionITVPro.Storage.Csv;

public class GestionItvCsvStorage : IGestionItvCsvStorage {

    private readonly ILogger _logger = Log.ForContext<GestionItvCsvStorage>();
    
    
    public GestionItvCsvStorage() : this(AppConfig.DataFolder) { }

    public GestionItvCsvStorage(string dataFolder) {
        _logger.Debug("Iniiando la clase GestionCsvStorage con carpetas: {DataFolder}", dataFolder);
        InitStorage(dataFolder);
    }

    public Result<bool, DomainError> Salvar(IEnumerable<Cita> items, string path) {
        try {
            _logger.Debug("Guardando los tems en el archivo '{path}'", path);
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine(
                "Id;Matricula;Modelo;Marca;Cilindrada;Motor;DniPropietario;FechaItv;FechaInspeccion;CreatedAt;UpdatedAt;IsDeleted;DeletedAt");

            foreach (var v in items ) {
                var dto = v.ToDto();
                writer.WriteLine(
                    $"{dto.Id};{EscapeCsvField(dto.Matricula)};{EscapeCsvField(dto.Marca)};{EscapeCsvField(dto.Modelo)};{dto.Cilindrada};{EscapeCsvField(dto.Motor)};{EscapeCsvField(dto.DniPropietario)};{dto.FechaItv};{dto.FechaInspeccion};{dto.CreatedAt};{dto.UpdatedAt};{dto.IsDeleted};{EscapeCsvField(dto.DeletedAt ?? "")}");
            }

            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al guardar los items en el archivo '{path}'", path);
            return Result.Failure<bool, DomainError>(StorageErrors.WriteError(ex.Message));
        }
    }
    

    public Result<IEnumerable<Cita>, DomainError> Cargar(string path) {
        _logger.Debug("Cargando los items del archivo '{path}'", path);

        if (!Path.Exists(path)) {
            _logger.Warning("El archivo '{path}' no existe.", path);
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.FileNotFound(path));
        }

        try {
            var v = File.ReadLines(path, Encoding.UTF8)
                .Skip(1)
                .Select(linea => linea.Split(";"))
                .Select(campo => new CitaDto(
                    int.Parse(campo[0]),
                    campo[1],
                    campo[2],
                    campo[3],
                    int.Parse(campo[4]),
                    campo[5],
                    campo[6],
                    campo[7],
                    campo[8],
                    campo[9],
                    campo[10],
                    bool.TryParse(campo[11], out var isDele) && isDele,
                    string.IsNullOrEmpty(campo[12]) ? null : campo[12]
                ).ToModel());
            return Result.Success<IEnumerable<Cita>, DomainError>(v);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al cargar los items del archivo '{path}'", path);
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.InvalidFormat(ex.Message));
            
        }
    }

    private static string EscapeCsvField(string field) {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains(';') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

    private void InitStorage(string folder) {
        if (Directory.Exists(folder))
            return;
        _logger.Debug("El directorio '{Folder}' no existe. Creándolo...", folder);
        Directory.CreateDirectory(folder);
    }
    
}