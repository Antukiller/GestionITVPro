using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Dto;
using GestionITVPro.Error.Common;
using GestionITVPro.Error.Storage;
using GestionITVPro.Mapper;
using GestionITVPro.Models;

namespace GestionITVPro.Storage.Xml;

using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Serilog;

public class GestionItvXmlStorage : IGestionItvXmlStorage {
    private readonly ILogger _logger = Log.ForContext<GestionItvXmlStorage>();
    
    private readonly XmlSerializerNamespaces _xmlSerializerNamespaces = new();

    private readonly XmlWriterSettings _xmlWriterSettings = new() {
        Indent = true,
        Encoding = new UTF8Encoding(false), // UTF-8 sin BOM para consistencia
    };

    public GestionItvXmlStorage() {
        _logger.Debug("Inicializando la clase ItvXmlStorage");
        InitStorage();
    }

    public Result<bool, DomainError> Salvar(IEnumerable<Vehiculo> items, string path) {
        try {
            _logger.Debug("Guardando los items en el archivo XML '{path}'", path);
            
            var dtos = items.Select(v => v.ToDto()).ToList();
            var serializer = new XmlSerializer(typeof(List<VehiculoDto>));

            using var streamWriter = new StreamWriter(path, false, new UTF8Encoding(false));
            using var xmlWriter = XmlWriter.Create(streamWriter, _xmlWriterSettings);
            
            serializer.Serialize(xmlWriter, dtos, _xmlSerializerNamespaces);
            
            return Result.Success<bool, DomainError>(true);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al guardar los items en el archivo XML '{path}'", path);
            return Result.Failure<bool, DomainError>(StorageErrors.WriteError(ex.Message));
        }
    }

    public Result<IEnumerable<Vehiculo>, DomainError> Cargar(string path) {
        _logger.Debug("Cargando los items del archivo XML '{path}'", path);

        if (!File.Exists(path)) {
            _logger.Warning("El archivo XML '{path}' no existe", path);
            return Result.Failure<IEnumerable<Vehiculo>, DomainError>(StorageErrors.FileNotFound(path));
        }

        try {
            var serializer = new XmlSerializer(typeof(List<VehiculoDto>));
            
            using var stream = File.OpenRead(path);
            var dtos = serializer.Deserialize(stream) as List<VehiculoDto>;

            if (dtos == null) {
                return Result.Failure<IEnumerable<Vehiculo>, DomainError>(
                    StorageErrors.InvalidFormat("No se pudieron deserializar los DTOs desde XML."));
            }

            return Result.Success<IEnumerable<Vehiculo>, DomainError>(dtos.Select(dto => dto.ToModel()));
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al cargar los items del archivo XML '{path}'", path);
            return Result.Failure<IEnumerable<Vehiculo>, DomainError>(StorageErrors.ReadError(ex.Message));
        }
    }

    private void InitStorage() {
        // Asumo el uso de AppConfig.DataFolder para mantener coherencia con tu ejemplo de JSON
        if (Directory.Exists(AppConfig.DataFolder))
            return;
            
        _logger.Debug("El directorio '{path}' no existe. Creándolo...", AppConfig.DataFolder);
        Directory.CreateDirectory(AppConfig.DataFolder);
    }
}