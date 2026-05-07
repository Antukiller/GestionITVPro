
using System.IO;
using System.Text;
using GestionITVPro.Errors.Common;
using GestionITVPro.Repositories.Base;

namespace GestionITVPro.Repositories.Binary;
using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using Error.Cita;
using GestionITVPro.Mapper;
using Models;
using Serilog;


public class CitaBinRepository : ICitaRepository {
    private const string FilePath = "Data/vehiculos.dat";
    private readonly ILogger _logger = Log.ForContext<CitaBinRepository>();

    private int _idCounter = 0;
    private readonly Dictionary<int, CitaEntity> _porId = [];
    private readonly Dictionary<string, int> _matriculaIndex = [];
    private readonly Dictionary<string, List<int>> _dniPropietarioIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string matricula, DateTime fecha)> _citaDiaIndex = [];

    public CitaBinRepository(string path, bool dropData = false, bool seedData = false) {
        if (!Directory.Exists("Data")) Directory.CreateDirectory("Data");

        if (dropData && File.Exists(FilePath)) {
            _logger.Warning("Borrando archivo de datos binarios...");
            File.Delete(FilePath);
        }

        if (File.Exists(FilePath)) Load();
    }

    // --- FUNCIONES DE CONSULTA ---

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
        return _porId.TryGetValue(id, out var entity) ? entity.ToModel() : null;
    }
    

    public bool ExistsMatricula(string matricula) => _matriculaIndex.ContainsKey(matricula);

    public Cita? GetByMatricula(string matricula) {
        // CORRECCIÓN: Usar _porId y filtrar por !IsDeleted
        return _porId.Values
            .FirstOrDefault(c => c.Matricula.Equals(matricula, StringComparison.OrdinalIgnoreCase) && !c.IsDeleted)
            ?.ToModel();
    }
    
    public Cita? GetByDniPropietario(string dniPropietario) {
        if (_dniPropietarioIndex.TryGetValue(dniPropietario, out var ids)) {
            // CORRECCIÓN: Buscar el primer ID de la lista que NO esté borrado
            var idNoBorrado = ids.FirstOrDefault(id => _porId.ContainsKey(id) && !_porId[id].IsDeleted);
        
            // Si FirstOrDefault no encuentra nada (devuelve 0), retornamos null
            return idNoBorrado != 0 ? GetById(idNoBorrado) : null;
        }
        return null;
    }

    public bool ExistsDniPropietario(string dniPropietario) => _dniPropietarioIndex.ContainsKey(dniPropietario);

    public int CountCita(bool includeDeleted = false) {
        return includeDeleted ? _porId.Count : _porId.Values.Count(v => !v.IsDeleted);
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


    // --- FUNCIONES DE ESCRITURA ---

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
    
    Save();
    
    _logger.Debug($"Cita para {citaEntity.Matricula} creada correctamente con ID {citaEntity.Id}.");
    return Result.Success<Cita, DomainError>(citaEntity.ToModel()!);
}

    public Result<Cita, DomainError> Update(int id, Cita model) {
        if (!_porId.TryGetValue(id, out var actual))
            return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
    
        var nuevaMatricula = string.IsNullOrWhiteSpace(model.Matricula) ? (actual.Matricula ?? "") : model.Matricula;
        var nuevoDni = model.DniPropietario ?? "";
        // ... dentro del if (actual.Matricula != nuevaMatricula)
        _matriculaIndex.Remove(actual.Matricula); // <-- ASEGÚRATE DE QUE ESTO SE EJECUTE
        _matriculaIndex[nuevaMatricula] = id;

        // 1. REGLA: Cita mismo día (¡CORREGIDO TIPO DE ERROR!)
        var citaMismoDia = _porId.Values.Any(c =>
            c.Id != id &&
            c.Matricula == nuevaMatricula &&
            c.FechaItv.Date == model.FechaItv.Date &&
            !c.IsDeleted);

        if (citaMismoDia) {
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));
        }

        

        // Sincronizar DNI si cambió (IMPORTANTE: actual.DniPropietario puede ser null)
        if (actual.DniPropietario != nuevoDni) {
            if (!string.IsNullOrEmpty(actual.DniPropietario) && _dniPropietarioIndex.TryGetValue(actual.DniPropietario, out var listaVieja)) 
                listaVieja.Remove(id);
        
            if (!_dniPropietarioIndex.ContainsKey(nuevoDni)) _dniPropietarioIndex[nuevoDni] = [];
            _dniPropietarioIndex[nuevoDni].Add(id);
        }

        var entity = (model with { 
            Id = id, 
            Matricula = nuevaMatricula, 
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = actual.CreatedAt 
        }).ToEntity();

        _porId[id] = entity;
        Save();
        return Result.Success<Cita, DomainError>(entity.ToModel()!);
    }

    public Cita? Delete(int id, bool isLogical = true) {
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

    public Result<Cita, DomainError> Restore(int id) {
        if (!_porId.TryGetValue(id, out var entity))
            return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.UpdatedAt = DateTime.UtcNow;
    
        Save(); // Persistir el cambio de flag
    
        var model = entity.ToModel();
        return model != null 
            ? Result.Success<Cita, DomainError>(model)
            : Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError("Error al mapear tras restaurar"));
    }
    public bool DeleteAll() {
        _porId.Clear();
        _matriculaIndex.Clear();
        _dniPropietarioIndex.Clear();
        _idCounter = 0;
        if (File.Exists(FilePath)) File.Delete(FilePath);
        return true;
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
                writer.Write((int)v.Motor);
                writer.Write(v.DniPropietario ?? "");
                writer.Write(v.IsDeleted);
                // IMPORTANTE: Guardar FechaItv
                writer.Write(v.FechaItv.ToString("O")); 
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
            if (!File.Exists(FilePath)) return;
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            int cantidad = reader.ReadInt32();
            _idCounter = reader.ReadInt32();

            _porId.Clear();
            _matriculaIndex.Clear();
            _dniPropietarioIndex.Clear();

            for (int i = 0; i < cantidad; i++) {
                var entity = new CitaEntity {
                    Id = reader.ReadInt32(),
                    Matricula = reader.ReadString(),
                    Marca = reader.ReadString(),
                    Modelo = reader.ReadString(),
                    Cilindrada = reader.ReadInt32(),
                    Motor = reader.ReadInt32(),
                    DniPropietario = reader.ReadString(),
                    IsDeleted = reader.ReadBoolean(),
                    // LEER FECHA ITV
                    FechaItv = DateTime.Parse(reader.ReadString()),
                    CreatedAt = DateTime.Parse(reader.ReadString()),
                    UpdatedAt = DateTime.Parse(reader.ReadString())
                };
                string delAtStr = reader.ReadString();
                entity.DeletedAt = delAtStr == "NULL" ? null : DateTime.Parse(delAtStr);

                // Reconstruir memoria
                _porId[entity.Id] = entity;
                if (!string.IsNullOrEmpty(entity.Matricula)) _matriculaIndex[entity.Matricula] = entity.Id;
                if (!string.IsNullOrEmpty(entity.DniPropietario)) {
                    if (!_dniPropietarioIndex.ContainsKey(entity.DniPropietario))
                        _dniPropietarioIndex[entity.DniPropietario] = [];
                    _dniPropietarioIndex[entity.DniPropietario].Add(entity.Id);
                }
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al cargar el archivo binario.");
        }
    }
}