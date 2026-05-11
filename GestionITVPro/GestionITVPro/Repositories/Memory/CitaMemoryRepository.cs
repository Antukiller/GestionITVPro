using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Entity;
using GestionITVPro.Enums;
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
            foreach (var vehiculo in CitasFactory.Seed()) Create(vehiculo);
            _logger.Information("SeeData completado...");

        }
    }

    public IEnumerable<Cita> GetAll(int pagina, int tamPagina, bool isDeleteInclude, string campoBusqueda) {
        // 1. Obtenemos todos los valores del almacén interno (_porId)
        // Usamos Select para convertir la CitaEntity a Cita (Model) antes de devolver
        var consulta = _porId.Values.Select(e => e.ToModel()!).AsEnumerable();

        // 2. Filtro de Borrado Lógico
        if (!isDeleteInclude) {
            // Solo incluimos los que NO están marcados como eliminados
            consulta = consulta.Where(v => v.IsDeleted == false);
        }

        // 3. Filtro de Búsqueda General
        if (!string.IsNullOrWhiteSpace(campoBusqueda)) {
            consulta = consulta.Where(v => 
                v.Matricula.Contains(campoBusqueda, StringComparison.OrdinalIgnoreCase) || 
                v.Marca.Contains(campoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                v.Modelo.Contains(campoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                v.DniPropietario.Contains(campoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                v.Cilindrada.ToString().Contains(campoBusqueda) ||
                v.Motor.ToString().Contains(campoBusqueda)
            );
        }

        // 4. Ordenamiento y Paginación
        return consulta
            .OrderBy(v => v.Id) 
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina);
    }

    public Cita? GetById(int id) {
        _logger.Debug("Obteniendo vehiculo con id {Id}", id);
        return _porId.GetValueOrDefault(id).ToModel();
    }

    public Result<IEnumerable<Cita>, DomainError> GetByDateMatricula(
        DateTime inicio, DateTime? fin, int pagina, int tamPagina, 
        string searchText = null, string motor = "TODOS", bool isDeleteInclude = false)
    {
        try
        {
            // 1. Filtrado base desde el diccionario interno _porId
            var consulta = _porId.Values.AsEnumerable();

            // 2. Filtros
            consulta = consulta.Where(c => isDeleteInclude ? c.IsDeleted : !c.IsDeleted);
    
            // Filtro de fechas (solo fecha, sin hora para mayor precisión en búsqueda por día)
            consulta = consulta.Where(c => c.FechaInspeccion.Date >= inicio.Date);
            if (fin.HasValue) 
                consulta = consulta.Where(c => c.FechaInspeccion.Date <= fin.Value.Date);

            // Filtro de Motor
            if (!string.IsNullOrWhiteSpace(motor) && !motor.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
            {
                var motorBusqueda = motor
                    .Replace("DIÉSEL", "Diesel")
                    .Replace("HÍBRIDO", "Hibrido")
                    .Replace("ELÉCTRICO", "Electrico")
                    .Trim();
                if (Enum.TryParse<Motor>(motorBusqueda, true, out var motorEnum))
                    consulta = consulta.Where(c => c.Motor == (int)motorEnum);
            }

            // Filtro de texto (Matrícula, DNI, Marca)
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.ToLower();
                consulta = consulta.Where(c => 
                    (c.Matricula != null && c.Matricula.ToLower().Contains(term)) || 
                    (c.DniPropietario != null && c.DniPropietario.ToLower().Contains(term)) ||
                    (c.Marca != null && c.Marca.ToLower().Contains(term)));
            }

            // 3. Paginación
            var resultado = consulta
                .OrderBy(c => c.FechaInspeccion)
                .Skip((pagina - 1) * tamPagina)
                .Take(tamPagina)
                .Select(c => c.ToModel()!)
                .ToList();

            return Result.Success<IEnumerable<Cita>, DomainError>(resultado);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error en GetByDateMatricula Memoria");
            return Result.Failure<IEnumerable<Cita>, DomainError>(CitaErrors.DatabaseError(ex.Message));
        }
    }

    public Result<Cita, DomainError> Create(Cita entity) {
    // 1. Validación: Máximo 3 citas por propietario en la misma fecha de inspección
    // Usamos FechaItv.Date para comparar solo el día, sin la hora
    if (_porId.Values.Count(v => v.DniPropietario == entity.DniPropietario && 
                                 v.FechaItv.Date == entity.FechaItv.Date) >= 3) {
        _logger.Debug("Límite de citas alcanzado para este DNI en la fecha seleccionada.");
        return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["Límite de 3 citas por día superado"]));
    }

    // 2. Validación: El vehículo (matrícula) no puede tener otra cita el mismo día
    // Usamos el HashSet _citaDiaIndex para que sea ultra rápido (O(1))
    if (_citaDiaIndex.Contains((entity.Matricula, entity.FechaItv.Date))) {
        _logger.Debug("Este vehículo ya tiene una cita programada para este día.");
        return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(entity.Matricula));
    } 

    // 3. Preparación del objeto: Asignamos ID, fechas de auditoría y estado inicial
    var citaModel = entity with { 
        Id = ++_idCounter, 
        CreatedAt = DateTime.Now, 
        IsDeleted = false 
    };

    // Convertimos a Entity para guardarlo en el diccionario
    var citaEntity = citaModel.ToEntity();
    
    // 4. GUARDADO Y ACTUALIZACIÓN DE ÍNDICES (Crucial en memoria)
    _porId.Add(citaEntity.Id, citaEntity); // Guardado principal
    _matriculaIndex[citaEntity.Matricula] = citaEntity.Id; // Índice de matrícula
    _citaDiaIndex.Add((citaEntity.Matricula, citaEntity.FechaItv.Date)); // Índice de fecha

    // Actualizar índice de DNI (Lista de IDs por DNI)
    if (!_dniPropietarioIndex.ContainsKey(citaEntity.DniPropietario)) {
        _dniPropietarioIndex[citaEntity.DniPropietario] = new List<int>();
    }
    _dniPropietarioIndex[citaEntity.DniPropietario].Add(citaEntity.Id);
    
    _logger.Debug($"Cita para {citaEntity.Matricula} creada correctamente con ID {citaEntity.Id}.");
    return Result.Success<Cita, DomainError>(citaEntity.ToModel()!);
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
    
    public int CountCitasFiltradas(string? matricula, DateTime inicio, DateTime? fin, bool isDeleteInclude, string? motor = null) 
    {
        var consulta = _porId.Values.AsEnumerable();

        consulta = consulta.Where(v => isDeleteInclude ? v.IsDeleted : !v.IsDeleted);

        consulta = consulta.Where(c => c.FechaInspeccion >= inicio);
        if (fin.HasValue) consulta = consulta.Where(c => c.FechaInspeccion <= fin.Value);

        if (!string.IsNullOrWhiteSpace(matricula))
        {
            var term = matricula.ToLower();
            consulta = consulta.Where(c =>
                (c.Matricula != null && c.Matricula.ToLower().Contains(term)) ||
                (c.DniPropietario != null && c.DniPropietario.ToLower().Contains(term)) ||
                (c.Marca != null && c.Marca.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(motor) && !motor.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
        {
            var motorBusqueda = motor
                .Replace("DIÉSEL", "Diesel")
                .Replace("HÍBRIDO", "Hibrido")
                .Replace("ELÉCTRICO", "Electrico")
                .Trim();
            if (Enum.TryParse<Motor>(motorBusqueda, true, out var motorEnum))
                consulta = consulta.Where(c => c.Motor == (int)motorEnum);
        }

        return consulta.Count();
    }
}