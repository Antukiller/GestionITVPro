using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Entity;
using GestionITVPro.Error.Common;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Serilog;

namespace GestionITVPro.Repositories.Memory;

/// <summary>
///     Repositorio en memoria para la gestión de Vehiculos.
///     Utiliza diccionarios para almacenamiento rápido.
/// </summary>
public class VehiculoMemoryRepository : IVehiculoRepository {
    // Indexa la matrícula (1 a 1): Matrícula -> ID del Vehículo
    private readonly Dictionary<string, int> _matriculaIndex = [];
    
    // Nuevo índice para validación rápida de fecha: Matrícula + Fecha(string) -> ID
    private readonly HashSet<(string matricula, DateTime fecha)> _citaDiaIndex = [];
    
    // Indexa el propietario (1 a muchos): DNI -> Lista de IDs de sus Vehículos
    private readonly Dictionary<string, List<int>> _dniPropietarioIndex = new(StringComparer.OrdinalIgnoreCase);
    
    private readonly ILogger _logger = Log.ForContext<VehiculoMemoryRepository>();
    private readonly Dictionary<int, CitaEntity> _porId = [];
    private int _idCounter;
    
    /// <summary>
    ///     Constuctor delegado que usa la configuracion de la aplicacion.
    /// </summary>
    public VehiculoMemoryRepository() : this(AppConfig.DropData, AppConfig.SeedData) { }


    public VehiculoMemoryRepository(bool dropData, bool seedData) {
        if (dropData) {
            _logger.Warning("Borrando datos en memoria...");
            DeleteAll();
        }

        if (seedData) {
            _logger.Information("Cargando datos de semilla...");
            foreach (var vehiculo in VehiculosFactory.Seed()) Create(vehiculo);
            _logger.Information("SeeData completado...");

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

        var citaMismoDia = _porId.Values.Any(c =>
            c.Matricula == model.Matricula &&
            c.FechaItv.Date == model.FechaCita.Date && 
            !c.IsDeleted);

        if (citaMismoDia) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["EL vehiculo ya tiene programada una cita para esa fecha"]));
        }
        
        _logger.Debug("Creando nuevo vehiculo {Matricula}", model.Matricula);
        var dni = model.DniPropietario ?? "";
        // 1. Validar Matrícula (Existencia única)
        if (ExistsMatricula(model.Matricula ?? "")) {
            _logger.Warning("No se puede crear: Matricula {Matricula} ya existe", model.Matricula);
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula ?? ""));
        }
        
        // 2. REGLA DE NEGOCIO: Máximo 3 vehículos por DNI
        // Accedemos directamente al "cajón" del DNI en el diccionario
        if (_dniPropietarioIndex.TryGetValue(dni, out var listaVehiculos) && listaVehiculos.Count >= 3) {
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
        if (!_dniPropietarioIndex.ContainsKey(dni)) {
            _dniPropietarioIndex[dni] = new List<int>();
        }
        _dniPropietarioIndex[dni].Add(entity.Id);
        
        
        _logger.Information("Vehiculo creado con Matricula {Matricula}", entity.Matricula);
        return Result.Success<Cita, DomainError>(entity.ToModel()!);
    }

   public Result<Cita, DomainError> Update(int id, Cita model) {
    _logger.Debug("Actualizando vehiculo con Id {Id}", id);

    // 1. Verificar existencia
    if (!_porId.TryGetValue(id, out var actual)) {
        _logger.Warning("No se puede actualizar: vehiculo con id {Id} no encontrado", id);
        return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
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

    var nuevoDni = model.DniPropietario ?? "";
    var nuevaMatricula = string.IsNullOrWhiteSpace(model.Matricula) ? (actual.Matricula ?? "") : model.Matricula;

    // 2. Validación de Matrícula (Si cambia, que no exista ya)
    if (nuevaMatricula != actual.Matricula && ExistsMatricula(nuevaMatricula)) {
        _logger.Warning("No se puede actualizar: Matricula {Matricula} ya está en uso", nuevaMatricula);
        return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));
    }

    // 3. REGLA DE NEGOCIO: Si cambia de dueño, validar límite de 3 en el destino
    if (actual.DniPropietario != nuevoDni) {
        if (_dniPropietarioIndex.TryGetValue(nuevoDni, out var listaDestino) && listaDestino.Count >= 3) {
            _logger.Warning("El nuevo dueño {Dni} ya tiene 3 vehículos", nuevoDni);
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El nuevo propietario ya tiene el límite de 3 vehículos."]));
        }
    }

    // 4. Actualizar objeto (Inmutabilidad)
    var modelActualizado = model with {
        Id = id,
        Matricula = nuevaMatricula,
        CreatedAt = actual.CreatedAt, // Mantener fecha original
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = actual.IsDeleted,
        DeletedAt = actual.DeletedAt
    };

    var entity = modelActualizado.ToEntity();

    // 5. Sincronizar Diccionarios e Índices
    
    // Matrícula
    if (actual.Matricula != entity.Matricula) {
        _matriculaIndex.Remove(actual.Matricula ?? "");
        _matriculaIndex[entity.Matricula] = id;
    }

    // DNI (Solo si ha cambiado el dueño)
    if (actual.DniPropietario != entity.DniPropietario) {
        // Quitar del antiguo
        if (_dniPropietarioIndex.TryGetValue(actual.DniPropietario ?? "", out var listaVieja)) {
            listaVieja.Remove(id);
            if (listaVieja.Count == 0) _dniPropietarioIndex.Remove(actual.DniPropietario ?? "");
        }

        // Añadir al nuevo
        if (!_dniPropietarioIndex.ContainsKey(entity.DniPropietario)) {
            _dniPropietarioIndex[entity.DniPropietario] = new List<int>();
        }
        _dniPropietarioIndex[entity.DniPropietario].Add(id);
    }

    _porId[id] = entity;
    
    _logger.Information("Vehiculo con Id {id} actualizado correctamente", id);
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
            _logger.Information("Borrado logico de vehiculo con id {Id}", id);
            return entity.ToModel();
        }

        if (!isLogical) {
            _porId.Remove(id);
            _matriculaIndex.Remove(entity.Matricula ?? "");
        }

        if (_dniPropietarioIndex.TryGetValue(entity.DniPropietario, out var lista)) {
            lista.Remove(id);
            if (lista.Count == 0) _dniPropietarioIndex.Remove(entity.DniPropietario);
        }
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
        if (_dniPropietarioIndex.TryGetValue(dniPropietario, out var listaIds) && listaIds.Count > 0) {
            var id = listaIds[0];
            return _porId.TryGetValue(id, out var entity) ? entity.ToModel() : null;
        }

        return null;
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        return _dniPropietarioIndex.ContainsKey(dniPropietario);
    }

    public bool DeleteAll() {
        _logger.Debug("Eliminando de forma permanente a todos los vehiculos");
        _porId.Clear();
        _matriculaIndex.Clear();
        _dniPropietarioIndex.Clear();
        _idCounter = 0;
        return true;
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
            FechaItv = entity.FechaItv,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        _porId[id] = restored;

        _matriculaIndex[restored.Matricula ?? ""] = id;
        if (!string.IsNullOrWhiteSpace(restored.DniPropietario)) {
            if (!_dniPropietarioIndex.ContainsKey(restored.DniPropietario)) {
                _dniPropietarioIndex[restored.DniPropietario] = new List<int>();
            }

            if (!_dniPropietarioIndex[restored.DniPropietario].Contains(id)) {
                _dniPropietarioIndex[restored.DniPropietario].Add(id);
            }
        }
        
        _logger.Information("Vehiculo con Id {Id} restaurada correctamente", id);
        return Result.Success<Cita, DomainError>(restored.ToModel()!);
    }
}