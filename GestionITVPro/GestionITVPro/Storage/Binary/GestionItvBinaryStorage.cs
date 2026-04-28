using System.Text;
using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Dto;
using GestionITVPro.Errors.Common;
using GestionITVPro.Errors.Storage;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using Serilog;


namespace GestionITVPro.Storage.Binary;

public class GestionItvBinaryStorage : IGestionItvBinaryStorage {
    private readonly ILogger _logger = Log.ForContext<GestionItvBinaryStorage>();

    public GestionItvBinaryStorage() {
        _logger.Debug("Iniciando la clase GestionItvBinaryStorage");
        InitStorage();
    }
    
    public Result<bool, DomainError> Salvar(IEnumerable<Cita> items, string path) {
        try {
            _logger.Debug("Guardando los items en archivo binario '{path0}'", path);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            var dtos = items.Select(v => v.ToDto()).ToList();
            writer.Write(dtos.Count);

            foreach (var dto in dtos) {
                writer.Write(dto.Id);
                writer.Write(dto.Matricula);
                writer.Write(dto.Marca);
                writer.Write(dto.Modelo);
                writer.Write(dto.Cilindrada);
                writer.Write(dto.Motor);
                writer.Write(dto.DniPropietario);
                writer.Write(dto.FechaItv);
                writer.Write(dto.CreatedAt);
                writer.Write(dto.UpdatedAt);
                writer.Write(dto.IsDeleted);
                writer.Write(dto.DeletedAt ?? "");
            }

            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al guarsar los items en el archivo binario '{path}'", path);
            return Result.Failure<bool, DomainError>(StorageErrors.WriteError(ex.Message));
        }
    }

    public Result<IEnumerable<Cita>, DomainError> Cargar(string path) {
        _logger.Debug("Cargando los items del archivo binario '{path}'", path);

        if (!File.Exists(path)) {
            _logger.Warning("El archivo '{path}' no existe.", path);
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.FileNotFound(path));
        }

        try {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8);


            var count = reader.ReadInt32();
            var vehiculos = new List<Cita>();

            for (var i = 0; i < count; i++) {
                var dto = new CitaDto(
                    reader.ReadInt32(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadInt32(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadBoolean(),
                    string.IsNullOrEmpty(reader.ReadString()) ? null : reader.ReadString()
                );
                vehiculos.Add(dto.ToModel());
            }

            return Result.Success<IEnumerable<Cita>, DomainError>(vehiculos);
        }
    
        catch (Exception ex) {
            _logger.Error(ex, "Error al cargar los items del archivo binario '{path}'", path);
            return Result.Failure<IEnumerable<Cita>, DomainError>(StorageErrors.InvalidFormat(ex.Message));
        }
    }

    private void InitStorage() {
        if (Directory.Exists(AppConfig.DataFolder))
            return;
        _logger.Debug("El directorio 'data' no existe. Creándolo");
        Directory.CreateDirectory(AppConfig.DataFolder);
    }
}