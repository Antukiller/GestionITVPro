using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Entity;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
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
public class CitaMemoryRepository : ICitaRepository {
    // Indexa la matrícula (1 a 1): Matrícula -> ID del Vehículo
    private readonly Dictionary<string, int> _matriculaIndex = [];
    
    // Nuevo índice para validación rápida de fecha: Matrícula + Fecha(string) -> ID
    private readonly HashSet<(string matricula, DateTime fecha)> _citaDiaIndex = [];
    
    // Indexa el propietario (1 a muchos): DNI -> Lista de IDs de sus Vehículos
    private readonly Dictionary<string, List<int>> _dniPropietarioIndex = new(StringComparer.OrdinalIgnoreCase);
    
    private readonly ILogger _logger = Log.ForContext<CitaMemoryRepository>();
    private readonly Dictionary<int, CitaEntity> _porId = [];
    private int _idCounter;
    
    /// <summary>
    ///     Constuctor delegado que usa la configuracion de la aplicacion.
    /// </summary>
    public CitaMemoryRepository() : this(AppConfig.DropData, AppConfig.SeedData) { }


    public CitaMemoryRepository(bool dropData, bool seedData) {
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

    public IEnumerable<Cita> GetAll(string? marca, string? dniPropietario, string? matricula, DateTime? desde, DateTime? hasta,
        int page = 1, int pageSize = 10, bool includeDeleted = true) {
        var query = _porId.Values.AsQueryable();
        if (!includeDeleted) {
            query = query.Where(c => !c.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(marca)) {
            query = query.Where(c => c.Marca.Contains(marca, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(dniPropietario)) {
            query = query.Where(c => c.DniPropietario == dniPropietario);
        }

        if (desde.HasValue) {
            query = query.Where(c => c.FechaInspeccion >= desde.Value);
        }

        if (hasta.HasValue) {
            query = query.Where(c => c.FechaInspeccion <= hasta.Value);
        }

        return query
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => c.ToModel()!);
    }

    public Cita? GetById(int id) {
        _logger.Debug("Obteniendo vehiculo con id {Id}", id);
        return _porId.GetValueOrDefault(id).ToModel();
    }

    public Result<Cita, DomainError> Create(Cita model) {
    var dni = model.DniPropietario ?? "";
    var matricula = model.Matricula ?? "";
    var fechaCita = model.FechaItv.Date;

    // 1. REGLA DE NEGOCIO: Máximo 3 vehículos por DNI (Validar PRIMERO para los tests)
    if (_dniPropietarioIndex.TryGetValue(dni, out var listaVehiculos) && listaVehiculos.Count >= 3) {
        _logger.Warning("Límite alcanzado para DNI {Dni}", dni);
        return Result.Failure<Cita, DomainError>(
             CitaErrors.Validation(["Límite alcanzado: Este propietario ya tiene 3 vehículos registrados."]));
    }

    // 2. REGLA DE NEGOCIO: No duplicar cita para el mismo vehículo el mismo día
    // Usamos el HashSet para que sea O(1) en lugar de recorrer toda la lista
    if (_citaDiaIndex.Contains((matricula, fechaCita))) {
        _logger.Warning("Cita ya existente para {Matricula} en fecha {Fecha}", matricula, fechaCita);
        return Result.Failure<Cita, DomainError>(
            CitaErrors.MatriculaAlreadyExists(matricula)); // O el error específico de Fecha si lo tienes
    }

    // 3. INTEGRIDAD: Validar Matrícula única global
    if (ExistsMatricula(matricula)) {
        _logger.Warning("Matricula {Matricula} ya existe en el sistema", matricula);
        return Result.Failure<Cita, DomainError>(
            CitaErrors.MatriculaAlreadyExists(matricula));
    }
    
    // 4. Preparar la entidad
    model = model with {
        Id = ++_idCounter,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false,
        DeletedAt = null
    };
    
    // 5. Persistencia y actualización de índices
    var entity = model.ToEntity();
    _porId[entity.Id] = entity;
    _matriculaIndex[entity.Matricula] = entity.Id;
    _citaDiaIndex.Add((entity.Matricula, entity.FechaItv.Date)); // <--- ¡No olvides actualizar el índice de fechas!
    
    if (!_dniPropietarioIndex.ContainsKey(dni)) {
        _dniPropietarioIndex[dni] = new List<int>();
    }
    _dniPropietarioIndex[dni].Add(entity.Id);
    
    _logger.Information("Vehiculo creado con Matricula {Matricula}", entity.Matricula);
    return Result.Success<Cita, DomainError>(entity.ToModel()!);
}

   public Result<Cita, DomainError> Update(int id, Cita model) {
    _logger.Debug("Actualizando vehiculo con Id {Id}", id);

    if (!_porId.TryGetValue(id, out var actual)) {
        return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
    }

    var nuevoDni = model.DniPropietario ?? "";
    var nuevaMatricula = string.IsNullOrWhiteSpace(model.Matricula) ? (actual.Matricula ?? "") : model.Matricula;
    var nuevaFecha = model.FechaItv.Date;

    // 1. REGLA: Límite de 3 (Si cambia de dueño)
    if (actual.DniPropietario != nuevoDni) {
        if (_dniPropietarioIndex.TryGetValue(nuevoDni, out var listaDestino) && listaDestino.Count >= 3) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["límite de 3 vehículos"]));
        }
    }

    // 2. REGLA: Cita mismo día (Si cambia matrícula o fecha)
    if ((actual.Matricula != nuevaMatricula || actual.FechaItv.Date != nuevaFecha) &&
        _citaDiaIndex.Contains((nuevaMatricula, nuevaFecha))) {
        return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));
    }

    // 3. INTEGRIDAD: Matrícula duplicada en otro registro
    if (nuevaMatricula != actual.Matricula && ExistsMatricula(nuevaMatricula)) {
        return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));
    }

    // --- Sincronización de Índices ---
    
    // Limpiar índice de fecha anterior
    _citaDiaIndex.Remove((actual.Matricula ?? "", actual.FechaItv.Date));

    // DNI: Mover de lista si cambió el dueño
    if (actual.DniPropietario != nuevoDni) {
        if (_dniPropietarioIndex.TryGetValue(actual.DniPropietario ?? "", out var listaVieja)) {
            listaVieja.Remove(id);
        }
        if (!_dniPropietarioIndex.ContainsKey(nuevoDni)) _dniPropietarioIndex[nuevoDni] = [];
        _dniPropietarioIndex[nuevoDni].Add(id);
    }

    // Matrícula: Actualizar índice si cambió
    if (actual.Matricula != nuevaMatricula) {
        _matriculaIndex.Remove(actual.Matricula ?? "");
        _matriculaIndex[nuevaMatricula] = id;
    }

    // 4. Guardar
    var modelActualizado = model with {
        Id = id,
        Matricula = nuevaMatricula,
        CreatedAt = actual.CreatedAt,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = actual.IsDeleted
    };

    var entity = modelActualizado.ToEntity();
    _porId[id] = entity;
    _citaDiaIndex.Add((entity.Matricula, entity.FechaItv.Date)); // Añadir nueva fecha

    return Result.Success<Cita, DomainError>(entity.ToModel()!);
}

public Cita? Delete(int id, bool isLogical = true) {
    if (!_porId.TryGetValue(id, out var entity)) return null;

    if (isLogical) {
        // Borrado Lógico: Solo marcamos, pero mantenemos los índices para evitar duplicados "fantasma"
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        _logger.Information("Borrado lógico de cita {Id}", id);
    }
    else {
        // Borrado Físico: Limpieza total
        _porId.Remove(id);
        _matriculaIndex.Remove(entity.Matricula ?? "");
        _citaDiaIndex.Remove((entity.Matricula ?? "", entity.FechaItv.Date)); // Limpiar índice de fecha
        
        if (_dniPropietarioIndex.TryGetValue(entity.DniPropietario ?? "", out var lista)) {
            lista.Remove(id);
        }
        _logger.Information("Borrado físico de cita {Id}", id);
    }

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
        _citaDiaIndex.Clear();
        _idCounter = 0;
        return true;
    }

    public int CountCita(bool includeDeleted = false) {
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