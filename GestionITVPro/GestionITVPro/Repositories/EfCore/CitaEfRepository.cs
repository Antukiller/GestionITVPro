using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GestionITVPro.Repositories.EfCore;

/// <summary>
///     Repositorio de personas que utiliza Entity Framework Core con SQLite.
///     Persiste los datos en una base de datos SQLite usando EF Core.
/// </summary>
public class CitaEfRepository : ICitaRepository {
    private readonly AppDbContext _context;
    private readonly ILogger _logger = Log.ForContext<CitaEfRepository>();

    public CitaEfRepository(AppDbContext context, bool dropData = false, bool seedData = false) {
        _context = context;

        if (dropData) _context.Database.EnsureDeleted();

        _context.Database.EnsureCreated();

        if (seedData && !_context.Citas.Any()) {
            _logger.Information("Sembrando datos de vehiculos...");
            foreach (var v in VehiculosFactory.Seed()) {
                Create(v);
            }
        }
    }

    public IEnumerable<Cita> GetAll(string? marca, string? dniPropietario, string? matricula, DateTime? desde, DateTime? hasta,
        int page = 1, int pageSize = 10, bool includeDeleted = true) {
        var query = _context.Citas.AsQueryable();
        if (!includeDeleted) {
            query = query.Where(c => !c.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(marca)) {
            query = query.Where(c => c.Marca.Contains(marca.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(matricula)) {
            query = query.Where(c => c.Matricula.Contains(matricula));
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

        var entidades = query
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList(); // Aquí es donde la consulta viaja a la base de datos
        // 2. Mapeamos a modelo en memoria (C#)
        return entidades.Select(e => e.ToModel()!);
    }

    public Cita? GetById(int id) {
        try {
            var entity = _context.Citas.FirstOrDefault(v => v.Id == id);
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error("Error al obtener vehiculo por ID {Id}", id);
            return null;
        }
    }

    public Result<Cita, DomainError> Create(Cita model) {
        var dni = model.DniPropietario ?? "";
        var matricula = model.Matricula ?? "";

        // 1. REGLA: Límite de 3 (Consistencia con tests)
        if (ContarVehiculosPorDni(dni) >= 3) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["Límite alcanzado: Este propietario ya tiene 3 vehículos registrados."]));
        }

        // 2. REGLA: Cita mismo día
        // Usamos .Date para comparar solo el día, ignorando la hora
        bool citaExiste = _context.Citas.Any(v => 
            v.Matricula == matricula && 
            v.FechaItv.Date == model.FechaItv.Date && 
            !v.IsDeleted);

        if (citaExiste) {
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(matricula));
        }
        
        // 3. INTEGRIDAD: Matrícula única
        if (ExistsMatricula(matricula))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(matricula));

        try {
            var entity = model.ToEntity();
            entity.Id = 0; // Aseguramos que sea una inserción
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.IsDeleted = false;

            _context.Citas.Add(entity);
            _context.SaveChanges();

            // Usamos AsNoTracking para que la entidad devuelta esté limpia
            var creado = _context.Citas.AsNoTracking().FirstOrDefault(v => v.Id == entity.Id);
            return Result.Success<Cita, DomainError>(creado.ToModel()!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al crear el vehiculo en EF");
            return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
        }
    }

 public Result<Cita, DomainError> Update(int id, Cita model)
{
    // 1. Buscar la entidad ORIGINAL (sin AsNoTracking para poder actualizarla luego)
    var entity = _context.Citas.FirstOrDefault(v => v.Id == id);
    if (entity == null)
        return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

    var nuevaMatricula = model.Matricula ?? entity.Matricula;
    var nuevoDni = model.DniPropietario ?? entity.DniPropietario;

    // 2. REGLA: Matrícula duplicada
    // Validamos contra otros registros, excluyendo el actual por ID
    if (nuevaMatricula != entity.Matricula)
    {
        bool matriculaExiste = _context.Citas.Any(v => v.Matricula == nuevaMatricula && v.Id != id && !v.IsDeleted);
        if (matriculaExiste)
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));
    }

    // 3. REGLA: DNI en uso (Específica para tu test)
    // El test espera fallo si el DNI ya está en OTRA cita
    bool dniEnUso = _context.Citas.Any(v => 
        v.Id != id && 
        v.DniPropietario == nuevoDni && 
        !v.IsDeleted);

    if (dniEnUso)
    {
        // Revisa que en CitaErrors NO tenga la 's' de "Propiestario"
        return Result.Failure<Cita, DomainError>(CitaErrors.DniPropiestarioAlreadyExists(nuevoDni));
    }

    // 4. REGLA: Límite de 3
    if (nuevoDni != entity.DniPropietario && ContarVehiculosPorDni(nuevoDni) >= 3)
    {
        return Result.Failure<Cita, DomainError>(
            CitaErrors.Validation(["El nuevo propietario ya tiene el límite de 3 vehículos."]));
    }

    // 5. REGLA: Cita mismo día
    bool otraCitaEseDia = _context.Citas.Any(v => 
        v.Id != id && 
        v.Matricula == nuevaMatricula && 
        v.FechaItv.Date == model.FechaItv.Date && 
        !v.IsDeleted);

    if (otraCitaEseDia)
    {
        return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(nuevaMatricula));
    }

    // 6. Mapeo directo sobre la entidad trackeada
    // NO uses 'entity = model' ni nada parecido, actualiza propiedades:
    entity.Matricula = nuevaMatricula;
    entity.Marca = model.Marca ?? entity.Marca;
    entity.Modelo = model.Modelo ?? entity.Modelo;
    entity.Cilindrada = model.Cilindrada;
    entity.Motor = (int)model.Motor;
    entity.DniPropietario = nuevoDni;
    entity.FechaItv = model.FechaItv;
    entity.UpdatedAt = DateTime.UtcNow;

    try
    {
        // EF Core detecta los cambios automáticamente en 'entity'
        _context.SaveChanges();
        return Result.Success<Cita, DomainError>(entity.ToModel()!);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Error al actualizar vehiculo en EF");
        return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
    }
}

    public Cita? Delete(int id, bool isLogical = true) {
        try {
            var entity = _context.Citas.FirstOrDefault(v => v.Id == id);
            if (entity == null)
                return null;

            if (isLogical) {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                _context.SaveChanges();
                return GetById(id);
            }

            _context.Citas.Remove(entity);
            _context.SaveChanges();
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al eliminar vehiculo");
            return null;
        }
    }

    public Cita? GetByMatricula(string matricula) {
        try {
            var entity = _context.Citas.FirstOrDefault(v => v.Matricula == matricula);
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener vehiculo por Matricula {Matricula}", matricula);
            return null;
        }
    }

    public bool ExistsMatricula(string matricula) {
        // CORRECCIÓN: Normalmente queremos saber si existe una matrícula ACTIVA
        return _context.Citas.Any(v => v.Matricula == matricula && !v.IsDeleted);
    }

    public Cita? GetByDniPropietario(string dniPropietario) {
        try {
            // CORRECCIÓN: Verificar nulidad antes de mapear
            var entity = _context.Citas.FirstOrDefault(v => v.DniPropietario == dniPropietario && !v.IsDeleted);
            return entity?.ToModel(); 
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener el vehiculo por DNI {DniPropietario}", dniPropietario);
            return null;
        }
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        try {
            return _context.Citas.Any(v => v.DniPropietario == dniPropietario);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al verificar el DNI del propietario {DniPropietario}", dniPropietario);
            return false;
        }
    }

    public bool DeleteAll() {
        try {
            _context.Citas.RemoveRange(_context.Citas);
            _context.SaveChanges();
            return true;
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al eliminar todos los vehivulos");
            return false;
        }
    }

    public int CountCita(bool includeDeleted = false) {
        try {
            var query = includeDeleted
                ? _context.Citas
                : _context.Citas.Where(v => !v.IsDeleted);

            return query.Count();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al contar vehículos en EF");
            return 0;
        }
    }

    public Result<Cita, DomainError> Restore(int id) {
        try {
            // CORRECCIÓN: Verificar si la matrícula de la cita a restaurar ya existe en otra cita activa
            var entity = _context.Citas.Find(id);
            if (entity == null) return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
        
            if (_context.Citas.Any(v => v.Matricula == entity.Matricula && !v.IsDeleted && v.Id != id)) {
                return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(entity.Matricula));
            }

            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.SaveChanges();
        
            return Result.Success<Cita, DomainError>(entity.ToModel()!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al restaurar vehiculo");
            return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
        }
    }
    
    // Helper para la regla de los 3
    private int ContarVehiculosPorDni(string dni) => 
        _context.Citas.Count(v => v.DniPropietario == dni && !v.IsDeleted);
}