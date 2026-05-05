using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Dto;
using GestionITVPro.Errors.Common;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using Serilog;

namespace GestionITVPro.Storage.Json;

public class GestionItvJsonStorage : IGestionItvJsonStorage {
    private readonly ILogger _logger = Log.ForContext<GestionItvJsonStorage>();

    private readonly JsonSerializerOptions _options = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public GestionItvJsonStorage() {
        _logger.Debug("Inicializando la clase GestionItvJsonStorage");
        InitStorage();
    }
    
    
        
    public Result<bool, DomainError> Salvar(IEnumerable<Cita> items, string path) {
        try {
            _logger.Debug("Guardando los items en el archivo '{path}'", path);
            var dtos = items.Select(v => v.ToDto()).ToList();
            var json = JsonSerializer.Serialize(dtos, _options);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            _logger.Error(ex, $"Error al guardar los items en el archivo '{path}'", path);
            return Result.Failure<bool, DomainError>(StorageErrors.WriteError(ex.Message));
        }
    }
    

    public Result<IEnumerable<Cita>, DomainError> Cargar(string path) {
        _logger.Debug("Cargando los items del archivo '{path}'", path);

        if (!Path.Exists(path)) {
            _logger.Warning("El archivo '{path}' no existe", path);
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.FileNotFound(path));
        }

        try {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var dtos = JsonSerializer.Deserialize<List<CitaDto>>(json, _options);

            if (dtos == null)
                return Result.Failure<IEnumerable<Cita>, DomainError>(
                    StorageErrors.InvalidFormat("No se pudieron deserializar los DTOs."));

            return Result.Success<IEnumerable<Cita>, DomainError>(dtos.Select(dtos => dtos.ToModel()));
            
        }
        catch (Exception ex) {
            _logger.Debug(ex, "Error al cargar los items del archivo '{path}'", path);
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.ReadError(ex.Message));
        }
    }

    private void InitStorage() {
        if (Directory.Exists(AppConfig.DataFolder))
            return;
        _logger.Debug("El directorio 'data', no existe. Creándolo....");
        Directory.CreateDirectory("data");
    }
}