using System.IO;
using System.Text.Json;
using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Serilog;


namespace GestionITVPro.Repositories.Json;

public class CitaJsonRepository : ICitaRepository {
    private readonly Dictionary<string, int> _matriculaIndex = new();
    private readonly Dictionary<string, List<int>> _dniPropietarioIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string matricula, DateTime fecha)> _citaDiaIndex = [];
    private readonly string _filePath;


    private readonly JsonSerializerOptions _jsonOptions = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger _logger = Log.ForContext<CitaJsonRepository>();
    private readonly Dictionary<int, CitaEntity> _porId = new();
    private int _idCounter;

    public CitaJsonRepository(string filePath, bool dropData = false, bool seeData = false) {
        _filePath = filePath;
        EnsureDirectory();
        
        if (dropData && File.Exists(_filePath)) File.Delete(_filePath);

        if (File.Exists(filePath)) Load();
        
        if (seeData && _porId.Count == 0)
            foreach (var v in CitasFactory.Seed()) {
                Create(v);
            }
    }

    /// <inheritdoc/>
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
            if (!isDeleteInclude) consulta = consulta.Where(c => !c.IsDeleted);
    
            // Filtro de fechas (solo fecha, sin hora para mayor precisión en búsqueda por día)
            consulta = consulta.Where(c => c.FechaInspeccion.Date >= inicio.Date);
            if (fin.HasValue) 
                consulta = consulta.Where(c => c.FechaInspeccion.Date <= fin.Value.Date);

            // Filtro de Motor
            if (!string.IsNullOrWhiteSpace(motor) && motor.ToUpper() != "TODOS")
                consulta = consulta.Where(c => c.Motor.ToString().ToUpper() == motor.ToUpper());

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
        if (!_porId.TryGetValue(id, out var actual)) 
            return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

        var nuevaMatricula = model.Matricula ?? actual.Matricula;
        var nuevoDni = model.DniPropietario ?? actual.DniPropietario;

        // 1. Matrícula duplicada
        if (nuevaMatricula != actual.Matricula && ExistsMatricula(nuevaMatricula))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));

        // 2. Cita mismo día
        bool otraCitaEseDia = _porId.Values.Any(v => 
            v.Id != id && 
            v.Matricula == nuevaMatricula && 
            v.FechaItv.Date == model.FechaItv.Date && 
            !v.IsDeleted);

        if (otraCitaEseDia)
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));

        // 3. Límite de 3 (Si cambia de dueño)
        if (nuevoDni != actual.DniPropietario) {
            if (_dniPropietarioIndex.TryGetValue(nuevoDni, out var lista) && lista.Count >= 3)
                return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["límite de 3 vehículos"]));
        }

        // Actualizar datos y sincronizar índices...
        var entity = model.ToEntity();
        entity.Id = id;
        entity.UpdatedAt = DateTime.UtcNow;

        // Sincronización de índices (Matrícula)
        if (actual.Matricula != entity.Matricula) {
            _matriculaIndex.Remove(actual.Matricula);
            _matriculaIndex[entity.Matricula] = id;
        }

        // Sincronización de índices (DNI)
        if (actual.DniPropietario != entity.DniPropietario) {
            if (_dniPropietarioIndex.TryGetValue(actual.DniPropietario, out var vieja)) vieja.Remove(id);
            if (!_dniPropietarioIndex.ContainsKey(entity.DniPropietario)) _dniPropietarioIndex[entity.DniPropietario] = new List<int>();
            _dniPropietarioIndex[entity.DniPropietario].Add(id);
        }

        _porId[id] = entity;
        Save();
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

        if (_dniPropietarioIndex.TryGetValue(entity.DniPropietario, out var lista)) {
            lista.Remove(id);
            if (lista.Count == 0) _dniPropietarioIndex.Remove(entity.DniPropietario);
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
        try {
            _logger.Debug("Eliminando de forma permanente a todos los vehiculos");
            _porId.Clear();
            _matriculaIndex.Clear();
            _dniPropietarioIndex.Clear();
            _idCounter = 0;
            if (File.Exists(_filePath)) File.Delete(_filePath);
            return true;
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al eliminar a todos los vehículos...");
            return false;
        }
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
        Save();
        _logger.Information("Vehiculo con Id {Id} restaurada correctamente", id);
        return Result.Success<Cita, DomainError>(restored.ToModel()!);
    }
    
    public int CountCitasFiltradas(string? matricula, DateTime inicio, DateTime? fin, bool isDeleteInclude) 
    {
        var consulta = _porId.Values.AsEnumerable();

        if (!isDeleteInclude) consulta = consulta.Where(v => !v.IsDeleted);

        consulta = consulta.Where(c => c.FechaInspeccion >= inicio);
        if (fin.HasValue) consulta = consulta.Where(c => c.FechaInspeccion <= fin.Value);

        if (!string.IsNullOrWhiteSpace(matricula))
            consulta = consulta.Where(c => c.Matricula.Contains(matricula, StringComparison.OrdinalIgnoreCase));

        return consulta.Count();
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
                    if (!_dniPropietarioIndex.ContainsKey(e.DniPropietario)) 
                        _dniPropietarioIndex[e.DniPropietario] = new List<int>();
                    _dniPropietarioIndex[e.DniPropietario].Add(e.Id);
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