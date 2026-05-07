using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using GestionITVPro.Enums;
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
            foreach (var v in CitasFactory.Seed()) {
                Create(v);
            }
        }
    }

    public IEnumerable<Cita> GetAll(int pagina, int tamPagina, bool isDeleteInclude, string campoBusqueda) {
        // 1. Empezamos la consulta sobre la tabla de Citas
        var consulta = _context.Citas.AsQueryable();

        // 2. Filtro de Borrado Lógico
        if (!isDeleteInclude) {
            consulta = consulta.Where(v => v.IsDeleted == false);
        }

        // 3. Filtro de Búsqueda General (Traducido a SQL)
        if (!string.IsNullOrWhiteSpace(campoBusqueda)) {
            // EF Core traduce Contains a un LIKE '%valor%' de SQL
            consulta = consulta.Where(v => 
                v.Matricula.Contains(campoBusqueda) || 
                v.Marca.Contains(campoBusqueda) ||
                v.Modelo.Contains(campoBusqueda) ||
                v.DniPropietario.Contains(campoBusqueda) ||
                v.Cilindrada.ToString().Contains(campoBusqueda)
            );
        }

        // 4. Ordenamiento, Paginación y Mapeo
        return consulta
            .OrderBy(v => v.Id) 
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .AsEnumerable() // Traemos a memoria solo la página solicitada
            .Select(e => e.ToModel()!);
    }

    public Cita? GetById(int id) {
        try {
            var entity = _context.Citas.FirstOrDefault(v => v.Id == id);
            return entity?.ToModel();
        }
        catch (Exception ex) {
            _logger.Error("Error al obtener vehiculo por ID {Id}", id);
            return null;
        }
    }

   public Result<IEnumerable<Cita>, DomainError> GetByDateMatricula(
    DateTime inicio, 
    DateTime? fin, 
    int pagina, 
    int tamPagina, 
    string searchText = null, 
    string motorSeleccionado = "TODOS", 
    bool isDeleteInclude = false) { 
       try {
        var consulta = _context.Citas.AsQueryable();

        // 1. IMPORTANTE: Corregir el filtro de borrado (solo los NO borrados)
        if (!isDeleteInclude) 
        {
            consulta = consulta.Where(v => !v.IsDeleted); 
        }

        // 2. Filtro por Rango de Fecha de Inspección
        consulta = consulta.Where(c => c.FechaInspeccion >= inicio);
        if (fin.HasValue) 
        {
            var finDia = fin.Value.Date.AddDays(1).AddTicks(-1);
            consulta = consulta.Where(c => c.FechaInspeccion <= finDia);
        }

        // 3. Filtro por Motor (ComboBox)
        if (!string.IsNullOrWhiteSpace(motorSeleccionado) && !motorSeleccionado.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
        {
            // Limpiamos el texto para que coincida con los nombres de la Enum (sin tildes si tu Enum no las tiene)
            string motorBusqueda = motorSeleccionado
                .Replace("DIÉSEL", "Diesel")
                .Replace("HÍBRIDO", "Hibrido")
                .Replace("ELÉCTRICO", "Electrico")
                .Trim();

            if (Enum.TryParse<Motor>(motorBusqueda, true, out var motorEnum))
            {
                int motorValue = (int)motorEnum; // Convertimos a int porque tu entidad usa int
                consulta = consulta.Where(c => c.Motor == motorValue);
            }
        }

        // 4. Filtro Combinado (DNI, Matrícula, Marca)
        if (!string.IsNullOrWhiteSpace(searchText)) 
        {
            var term = searchText.ToLower();
            consulta = consulta.Where(c => 
                c.Matricula.ToLower().Contains(term) || 
                c.DniPropietario.ToLower().Contains(term) || 
                c.Marca.ToLower().Contains(term)
            );
        }

        // 5. Paginación eficiente en Base de Datos
        var entidades = consulta
            .OrderBy(c => c.FechaInspeccion)
            .Skip((pagina - 1) * tamPagina)
            .Take(tamPagina)
            .ToList();

        return Result.Success<IEnumerable<Cita>, DomainError>(entidades.Select(e => e.ToModel()!));
    }
    catch (Exception ex) 
    {
        _logger.Error(ex, "Error en búsqueda avanzada de citas");
        return Result.Failure<IEnumerable<Cita>, DomainError>(CitaErrors.DatabaseError(ex.Message));
    }
}


    public Result<Cita, DomainError> Create(Cita model) {
        try {
            // 1. REGLA: Máximo 3 citas por propietario en la misma fecha (Query a DB)
            var countDniDia = _context.Citas.Count(v => 
                v.DniPropietario == model.DniPropietario && 
                v.FechaItv.Date == model.FechaItv.Date && 
                !v.IsDeleted);

            if (countDniDia >= 3) {
                return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["Límite de 3 citas por día superado"]));
            }

            // 2. REGLA: El vehículo no puede repetir el mismo día (Query a DB)
            var matriculaDuplicada = _context.Citas.Any(v => 
                v.Matricula == model.Matricula && 
                v.FechaItv.Date == model.FechaItv.Date && 
                !v.IsDeleted);

            if (matriculaDuplicada) {
                return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula));
            }

            // 3. Conversión y Auditoría
            // IMPORTANTE: No asignes ID manualmente si es Autoincremental en la DB
            var entity = model.ToEntity();
            entity.CreatedAt = DateTime.Now;
            entity.IsDeleted = false;

            // 4. Persistencia
            _context.Citas.Add(entity);
            _context.SaveChanges(); // Aquí la DB asigna el ID automáticamente

            _logger.Debug("Cita para {Matricula} creada en DB con ID {Id}", entity.Matricula, entity.Id);
            return Result.Success<Cita, DomainError>(entity.ToModel()!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al crear cita en EF Core");
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
                return entity.ToModel();
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
    
    public int CountCitasFiltradas(string? matricula, DateTime inicio, DateTime? fin, bool isDeleteInclude) 
    {
        var consulta = _context.Citas.AsQueryable();

        if (!isDeleteInclude) consulta = consulta.Where(v => !v.IsDeleted);

        consulta = consulta.Where(c => c.FechaInspeccion >= inicio);
        if (fin.HasValue) consulta = consulta.Where(c => c.FechaInspeccion <= fin.Value);

        if (!string.IsNullOrWhiteSpace(matricula))
            consulta = consulta.Where(c => c.Matricula.Contains(matricula));

        return consulta.Count(); // EF traduce esto a SELECT COUNT(*) en SQL
    }
}