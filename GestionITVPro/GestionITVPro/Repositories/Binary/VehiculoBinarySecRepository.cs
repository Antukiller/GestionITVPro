
using System.Text;
using GestionITVPro.Repositories.Base;

namespace GestionITVPro.Repositories.Binary;
using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using GestionITVPro.Error.Common;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using Serilog;


public class VehiculoBinRepository : IVehiculoRepository {
    private const string FilePath = "Data/vehiculos.dat";
    private readonly ILogger _logger = Log.ForContext<VehiculoBinRepository>();

    private int _idCounter = 0;
    private readonly Dictionary<int, VehiculoEntity> _porId = [];
    private readonly Dictionary<string, int> _matriculaIndex = [];
    private readonly Dictionary<string, List<int>> _dniPropietarioIndex = new(StringComparer.OrdinalIgnoreCase);

    public VehiculoBinRepository(bool dropData = false) {
        if (!Directory.Exists("Data")) Directory.CreateDirectory("Data");

        if (dropData && File.Exists(FilePath)) {
            _logger.Warning("Borrando archivo de datos binarios...");
            File.Delete(FilePath);
        }

        if (File.Exists(FilePath)) Load();
    }

    // --- FUNCIONES DE CONSULTA ---

    public IEnumerable<Vehiculo> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        var query = includeDeleted
            ? _porId.Values.AsEnumerable()
            : _porId.Values.Where(v => !v.IsDeleted);

        return query
            .OrderBy(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToModel();
    }

    public Vehiculo? GetById(int id) {
        return _porId.TryGetValue(id, out var entity) ? entity.ToModel() : null;
    }

    public Vehiculo? GetByMatricula(string matricula) {
        return _matriculaIndex.TryGetValue(matricula, out var id) ? GetById(id) : null;
    }

    public bool ExistsMatricula(string matricula) => _matriculaIndex.ContainsKey(matricula);

    public Vehiculo? GetByDniPropietario(string dniPropietario) {
        if (_dniPropietarioIndex.TryGetValue(dniPropietario, out var ids) && ids.Count > 0) {
            return GetById(ids[0]);
        }
        return null;
    }

    public bool ExistsDniPropietario(string dniPropietario) => _dniPropietarioIndex.ContainsKey(dniPropietario);

    public int CountVehiculos(bool includeDeleted = false) {
        return includeDeleted ? _porId.Count : _porId.Values.Count(v => !v.IsDeleted);
    }

    // --- FUNCIONES DE ESCRITURA ---

    public Result<Vehiculo, DomainError> Create(Vehiculo model) {
        var matricula = model.Matricula ?? "";
        var dni = model.DniPropietario ?? "";

        if (ExistsMatricula(matricula))
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.MatriculaAlreadyExists(matricula));

        if (_dniPropietarioIndex.TryGetValue(dni, out var lista) && lista.Count >= 3)
            return Result.Failure<Vehiculo, DomainError>(
                VehiculoErrors.Validation(["Límite alcanzado: Este propietario ya tiene 3 vehículos registrados."]));

        var entity = (model with {
            Id = ++_idCounter,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        }).ToEntity();

        _porId[entity.Id] = entity;
        _matriculaIndex[entity.Matricula] = entity.Id;
        
        if (!_dniPropietarioIndex.ContainsKey(dni)) _dniPropietarioIndex[dni] = [];
        _dniPropietarioIndex[dni].Add(entity.Id);

        Save();
        return Result.Success<Vehiculo, DomainError>(entity.ToModel()!);
    }

    public Result<Vehiculo, DomainError> Update(int id, Vehiculo model) {
        if (!_porId.TryGetValue(id, out var actual))
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.NotFound(id.ToString()));

        var nuevaMatricula = string.IsNullOrWhiteSpace(model.Matricula) ? actual.Matricula : model.Matricula;
        var nuevoDni = model.DniPropietario ?? "";

        if (nuevaMatricula != actual.Matricula && ExistsMatricula(nuevaMatricula))
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.MatriculaAlreadyExists(nuevaMatricula));

        if (actual.DniPropietario != nuevoDni) {
            if (_dniPropietarioIndex.TryGetValue(nuevoDni, out var lista) && lista.Count >= 3)
                return Result.Failure<Vehiculo, DomainError>(
                    VehiculoErrors.Validation(["El nuevo propietario ya tiene el límite de 3 vehículos."]));

            // Sincronizar DNI Index
            if (_dniPropietarioIndex.TryGetValue(actual.DniPropietario ?? "", out var listaVieja)) listaVieja.Remove(id);
            if (!_dniPropietarioIndex.ContainsKey(nuevoDni)) _dniPropietarioIndex[nuevoDni] = [];
            _dniPropietarioIndex[nuevoDni].Add(id);
        }

        if (actual.Matricula != nuevaMatricula) {
            _matriculaIndex.Remove(actual.Matricula);
            _matriculaIndex[nuevaMatricula] = id;
        }

        var entity = (model with { 
            Id = id, 
            Matricula = nuevaMatricula, 
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = actual.CreatedAt 
        }).ToEntity();

        _porId[id] = entity;
        Save();
        return Result.Success<Vehiculo, DomainError>(entity.ToModel()!);
    }

    public Vehiculo? Delete(int id, bool isLogical = true) {
        if (!_porId.TryGetValue(id, out var entity)) return null;

        if (isLogical) {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
        } else {
            _porId.Remove(id);
            _matriculaIndex.Remove(entity.Matricula);
            if (_dniPropietarioIndex.TryGetValue(entity.DniPropietario ?? "", out var lista)) lista.Remove(id);
        }

        Save();
        return entity.ToModel();
    }

    public Result<Vehiculo, DomainError> Restore(int id) {
        if (!_porId.TryGetValue(id, out var entity))
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.NotFound(id.ToString()));

        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.UpdatedAt = DateTime.UtcNow;
        
        Save();
        return Result.Success<Vehiculo, DomainError>(entity.ToModel()!);
    }

    public bool DeleteAll() {
        _porId.Clear();
        _matriculaIndex.Clear();
        _dniPropietarioIndex.Clear();
        _idCounter = 0;
        if (File.Exists(FilePath)) File.Delete(FilePath);
        return true;
    }

    // --- PERSISTENCIA SECUENCIAL ---

    private void Save() {
        try {
            using var stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            writer.Write(_porId.Count);
            writer.Write(_idCounter);

            foreach (var v in _porId.Values) {
                writer.Write(v.Id);
                writer.Write(v.Matricula ?? "");
                writer.Write(v.Marca ?? "");
                writer.Write(v.Modelo ?? "");
                writer.Write(v.Cilindrada);
                writer.Write((int)v.Motor); // Guardamos el Enum como entero
                writer.Write(v.DniPropietario ?? "");
                writer.Write(v.IsDeleted);
                // Usamos formato ISO 8601 para fechas
                writer.Write(v.CreatedAt.ToString("O"));
                writer.Write(v.UpdatedAt.ToString("O"));
                writer.Write(v.DeletedAt?.ToString("O") ?? "NULL");
            }
        } catch (Exception ex) {
            _logger.Error(ex, "Error al guardar el archivo binario.");
        }
    }

    private void Load() {
        try {
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            int cantidad = reader.ReadInt32();
            _idCounter = reader.ReadInt32();

            for (int i = 0; i < cantidad; i++) {
                var entity = new VehiculoEntity {
                    Id = reader.ReadInt32(),
                    Matricula = reader.ReadString(),
                    Marca = reader.ReadString(),
                    Modelo = reader.ReadString(),
                    Cilindrada = reader.ReadInt32(),
                    Motor = reader.ReadInt32(),
                    DniPropietario = reader.ReadString(),
                    IsDeleted = reader.ReadBoolean(),
                    CreatedAt = DateTime.Parse(reader.ReadString())
                };
                
                // LeemosUpdatedAt y DeletedAt
                entity.UpdatedAt = DateTime.Parse(reader.ReadString());
                string delAtStr = reader.ReadString();
                entity.DeletedAt = delAtStr == "NULL" ? null : DateTime.Parse(delAtStr);

                // Reconstruir índices
                _porId[entity.Id] = entity;
                _matriculaIndex[entity.Matricula] = entity.Id;
                
                if (!string.IsNullOrWhiteSpace(entity.DniPropietario)) {
                    if (!_dniPropietarioIndex.ContainsKey(entity.DniPropietario)) 
                        _dniPropietarioIndex[entity.DniPropietario] = [];
                    _dniPropietarioIndex[entity.DniPropietario].Add(entity.Id);
                }
            }
        } catch (Exception ex) {
            _logger.Error(ex, "Error al cargar el archivo binario.");
        }
    }
}