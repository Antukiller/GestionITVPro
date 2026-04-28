using System.Runtime.CompilerServices;
using System.Text.Json;
using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using GestionITVPro.Error.Common;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Serilog;


namespace GestionITVPro.Repositories.Json;

public class VehiculoJsonRepository : IVehiculoRepository {
    private readonly Dictionary<string, int> _matriculaIndex = new();
    private readonly Dictionary<string, List<int>> _dniPropietario = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _filePath;


    private readonly JsonSerializerOptions _jsonOptions = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger _logger = Log.ForContext<VehiculoJsonRepository>();
    private readonly Dictionary<int, CitaEntity> _porId = new();
    private int _idCounter;

    public VehiculoJsonRepository(string filePath, bool dropData = false, bool seeData = false) {
        _filePath = filePath;
        EnsureDirectory();
        
        if (dropData && File.Exists(_filePath)) File.Delete(_filePath);

        if (File.Exists(filePath)) Load();
        
        if (seeData && _porId.Count == 0)
            foreach (var v in VehiculosFactory.Seed()) {
                Create(v);
            }
    }

    /// <inheritdoc/>
    public IEnumerable<Cita> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        _logger.Debug("Obteniendo vehiculos con paginación: página {Page}, tamaño {PageSize}, incluir borrados: {IncludeDeleted}",page, pageSize, includeDeleted);

        var query = includeDeleted
            ? _porId.Values.AsEnumerable()
            : _porId.Values.Where(a => !a.IsDeleted);

        return query
            .OrderBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToModel();
    }
    
    
    public Cita? GetById(int id) {
        _logger.Debug("Obteniendo vehiculo con id {Id}", id);
        return _porId.GetValueOrDefault(id).ToModel();
    }

    public Result<Cita, DomainError> Create(Cita model) {
        _logger.Debug("Creando nuevo vehiculo {Matricula}", model.Matricula);
        
        var citaMismoDia = _porId.Values.Any(c =>
            c.Matricula == model.Matricula &&
            c.FechaItv.Date == model.FechaCita.Date && 
            !c.IsDeleted);

        if (citaMismoDia) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["EL vehiculo ya tiene programada una cita para esa fecha"]));
        }
        var dni = model.DniPropietario ?? "";
        // 1. Validar Matrícula (Existencia única)
        if (ExistsMatricula(model.Matricula ?? "")) {
            _logger.Warning("No se puede crear: Matricula {Matricula} ya existe", model.Matricula);
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula ?? ""));
        }
        
        // 2. REGLA DE NEGOCIO: Máximo 3 vehículos por DNI
        // Accedemos directamente al "cajón" del DNI en el diccionario
        if (_dniPropietario.TryGetValue(dni, out var listaVehiculos) && listaVehiculos.Count >= 3) {
            return Result.Failure<Cita, DomainError>(
                 CitaErrors.Validation(["Límite alcanzado: Este propietario ya tiene 3 vehículos registrados."]));
        }
        
        // 3. Preparar la entidad
        model = model with {
            Id = ++_idCounter,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        
        // 4. Persistencia en Memoria
        var entity = model.ToEntity();
        _porId[entity.Id] = entity;
        _matriculaIndex[entity.Matricula] = entity.Id;
        
        // Actualizar el índice de DNI (la lista dentro del diccionario)
        if (!_dniPropietario.ContainsKey(dni)) {
            _dniPropietario[dni] = new List<int>();
        }
        _dniPropietario[dni].Add(entity.Id);
        
        Save();
        _logger.Information("Vehiculo creado con Matricula {Matricula}", entity.Matricula);
        return Result.Success<Cita, DomainError>(entity.ToModel()!);
    }

    public Result<Cita, DomainError> Update(int id, Cita model) {
        if (!_porId.TryGetValue(id, out var actual)) return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

        var nuevoDni = model.DniPropietario ?? "";
        var nuevaMatricula = string.IsNullOrWhiteSpace(model.Matricula) ? (actual.Matricula ?? "") : model.Matricula;

        // Validación Matrícula
        if (nuevaMatricula != actual.Matricula && ExistsMatricula(nuevaMatricula)) 
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));

        // REGLA DE NEGOCIO: Límite de 3 (USANDO _dniPropietario, NO _matriculaIndex)
        if (actual.DniPropietario != nuevoDni) {
            if (_dniPropietario.TryGetValue(nuevoDni, out var listaDestino) && listaDestino.Count >= 3) {
                return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["Límite de 3 vehículos alcanzado"]));
            }
        }
        
        var citaMismoDia = _porId.Values.Any(c =>
            c.Id != id &&
            c.Matricula == model.Matricula &&
            c.FechaItv.Date == model.FechaCita.Date &&
            !c.IsDeleted);

        if (citaMismoDia) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El vehiculo ya tiene otra cita prgramada para ese día."]));
        }

        var entity = (model with { Id = id, Matricula = nuevaMatricula, UpdatedAt = DateTime.UtcNow }).ToEntity();

        // Sincronizar índices
        if (actual.Matricula != entity.Matricula) {
            _matriculaIndex.Remove(actual.Matricula ?? "");
            _matriculaIndex[entity.Matricula] = id;
        }

        if (actual.DniPropietario != entity.DniPropietario) {
            if (_dniPropietario.TryGetValue(actual.DniPropietario ?? "", out var listaVieja)) listaVieja.Remove(id);
            if (!_dniPropietario.ContainsKey(entity.DniPropietario)) _dniPropietario[entity.DniPropietario] = new List<int>();
            _dniPropietario[entity.DniPropietario].Add(id);
        }

        _porId[id] = entity;
        Save(); // <--- CRUCIAL
        return Result.Success<Cita, DomainError>(entity.ToModel()!);
    }

    public Cita? Delete(int id, bool isLogical = true) {
        _logger.Debug("Eliminando vehiculo con id {id} (borrado lógico: {IsLogical})", id, isLogical);

        if (!_porId.TryGetValue(id, out var entity)) {
            _logger.Warning("No se puede eliminar: vehiculo con id {Id} no encontrado", id);
            return null;
        }

        if (isLogical) {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            Save();
            _logger.Information("Borrado logico de vehiculo con id {Id}", id);
            return entity.ToModel();
        }

        if (!isLogical) {
            _porId.Remove(id);
            _matriculaIndex.Remove(entity.Matricula ?? "");
        }

        if (_dniPropietario.TryGetValue(entity.DniPropietario, out var lista)) {
            lista.Remove(id);
            if (lista.Count == 0) _dniPropietario.Remove(entity.DniPropietario);
        }
        
        Save();
        _logger.Information("Vehículo {Id} eliminado físicamente", id);
        return entity.ToModel();
    }

    public Cita? GetByMatricula(string matricula) {
        _logger.Debug("Obteniendo vehiculo con matricula {Matricula}", matricula);
        return _matriculaIndex.TryGetValue(matricula, out var id) && _porId.TryGetValue(id, out var entity)
            ? entity.ToModel()
            : null;
    }

    public bool ExistsMatricula(string matricula) {
        return _matriculaIndex.ContainsKey(matricula);
    }

    public Cita? GetByDniPropietario(string dniPropietario) {
        _logger.Debug("Obteniendo vehiculo con DniPropietarip {DniPropietario}", dniPropietario);
        if (_dniPropietario.TryGetValue(dniPropietario, out var listaIds) && listaIds.Count > 0) {
            var id = listaIds[0];
            return _porId.TryGetValue(id, out var entity) ? entity.ToModel() : null;
        }

        return null;
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        return _dniPropietario.ContainsKey(dniPropietario);
    }

    public bool DeleteAll() {
        try {
            _logger.Debug("Eliminando de forma permanente a todos los vehiculos");
            _porId.Clear();
            _matriculaIndex.Clear();
            _dniPropietario.Clear();
            _idCounter = 0;
            if (File.Exists(_filePath)) File.Delete(_filePath);
            return true;
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al eliminar a todos los vehículos...");
            return false;
        }
    }

    public int CountVehiculos(bool includeDeleted = false) {
        var query = includeDeleted
            ? _porId.Values.AsEnumerable()
            : _porId.Values.Where(a => !a.IsDeleted);
        return query.Count();
    }

    public Result<Cita, DomainError> Restore(int id) {
        if (!_porId.TryGetValue(id, out var entity)) {
            _logger.Warning("No se puede restaurar: vehiculo con id {id} no encontrado", id);
            return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
        }

        var restored = new CitaEntity {
            Id = entity.Id,
            Matricula = entity.Matricula,
            Marca = entity.Marca,
            Modelo = entity.Modelo,
            Cilindrada = entity.Cilindrada,
            Motor = entity.Motor,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        _porId[id] = restored;

        _matriculaIndex[restored.Matricula ?? ""] = id;
        if (!string.IsNullOrWhiteSpace(restored.DniPropietario)) {
            if (!_dniPropietario.ContainsKey(restored.DniPropietario)) {
                _dniPropietario[restored.DniPropietario] = new List<int>();
            }

            if (!_dniPropietario[restored.DniPropietario].Contains(id)) {
                _dniPropietario[restored.DniPropietario].Add(id);
            }
        }
        Save();
        _logger.Information("Vehiculo con Id {Id} restaurada correctamente", id);
        return Result.Success<Cita, DomainError>(restored.ToModel()!);
    }
    
    private void EnsureDirectory() {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
    }

    private void Load() {
        try {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var entities = JsonSerializer.Deserialize<List<CitaEntity>>(json, _jsonOptions);
            if (entities == null) return;

            foreach (var e in entities) {
                _porId[e.Id] = e;
                _matriculaIndex[e.Matricula ?? ""] = e.Id;
                if (!string.IsNullOrWhiteSpace(e.DniPropietario)) {
                    if (!_dniPropietario.ContainsKey(e.DniPropietario)) 
                        _dniPropietario[e.DniPropietario] = new List<int>();
                    _dniPropietario[e.DniPropietario].Add(e.Id);
                }
                if (e.Id > _idCounter) _idCounter = e.Id;
            }
        } catch (Exception ex) { _logger.Error(ex, "Error al cargar JSON"); }
    }

    private void Save() {
        try {
            var json = JsonSerializer.Serialize(_porId.Values.ToList(), _jsonOptions);
            File.WriteAllText(_filePath, json);
        } catch (Exception ex) { _logger.Error(ex, "Error al guardar JSON"); }
    }
}