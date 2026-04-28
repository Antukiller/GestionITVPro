
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

    public CitaBinRepository(string path, bool dropData = false, bool seedData = false) {
        if (!Directory.Exists("Data")) Directory.CreateDirectory("Data");

        if (dropData && File.Exists(FilePath)) {
            _logger.Warning("Borrando archivo de datos binarios...");
            File.Delete(FilePath);
        }

        if (File.Exists(FilePath)) Load();
    }

    // --- FUNCIONES DE CONSULTA ---

    public IEnumerable<Cita> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        var query = includeDeleted
            ? _porId.Values.AsEnumerable()
            : _porId.Values.Where(v => !v.IsDeleted);

        return query
            .OrderBy(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToModel();
    }

    public Cita? GetById(int id) {
        return _porId.TryGetValue(id, out var entity) ? entity.ToModel() : null;
    }

    public Cita? GetByMatricula(string matricula) {
        return _matriculaIndex.TryGetValue(matricula, out var id) ? GetById(id) : null;
    }

    public bool ExistsMatricula(string matricula) => _matriculaIndex.ContainsKey(matricula);

    public Cita? GetByDniPropietario(string dniPropietario) {
        if (_dniPropietarioIndex.TryGetValue(dniPropietario, out var ids) && ids.Count > 0) {
            return GetById(ids[0]);
        }
        return null;
    }

    public bool ExistsDniPropietario(string dniPropietario) => _dniPropietarioIndex.ContainsKey(dniPropietario);

    public int CountCita(bool includeDeleted = false) {
        return includeDeleted ? _porId.Count : _porId.Values.Count(v => !v.IsDeleted);
    }

    // --- FUNCIONES DE ESCRITURA ---

    public Result<Cita, DomainError> Create(Cita model) {
        var matricula = model.Matricula ?? "";
        var dni = model.DniPropietario ?? "";

        // 1. REGLA: Cita mismo día (¡CAMBIO DE TIPO DE ERROR!)
        var citaMismoDia = _porId.Values.Any(c =>
            c.Matricula == matricula &&
            c.FechaItv.Date == model.FechaItv.Date && 
            !c.IsDeleted);

        if (citaMismoDia) {
            // El test espera MatriculaAlreadyExists para el caso de duplicado mismo día
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(matricula));
        }

        // 2. INTEGRIDAD: Matrícula única global
        if (ExistsMatricula(matricula))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(matricula));

        // 3. LÍMITE DE 3
        if (_dniPropietarioIndex.TryGetValue(dni, out var lista) && lista.Count >= 3)
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["Límite alcanzado"]));

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
        return Result.Success<Cita, DomainError>(entity.ToModel()!);
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