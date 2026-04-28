using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using GestionITVPro.Error.Common;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Serilog;

namespace GestionITVPro.Repositories.EfCore;

/// <summary>
///     Repositorio de personas que utiliza Entity Framework Core con SQLite.
///     Persiste los datos en una base de datos SQLite usando EF Core.
/// </summary>
public class VehiculoEfRepository : IVehiculoRepository {
    private readonly AppDbContext _context;
    private readonly ILogger _logger = Log.ForContext<VehiculoEfRepository>();

    public VehiculoEfRepository(AppDbContext context, bool dropData = false, bool seedData = false) {
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

    public IEnumerable<Cita> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        try {
            var query = includeDeleted
                ? _context.Citas.AsQueryable()
                : _context.Citas.Where(v => !v.IsDeleted);

            var entities = query
                .OrderBy(v => v.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return entities.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener vehiculos");
            return Enumerable.Empty<Cita>();
        }
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
        
        // 1. REGLA: No permitir dos citas el mismo día para el mismo vehículo
        bool citaExiste = _context.Citas.Any(v => 
            v.Matricula == model.Matricula && 
            v.FechaItv.Date == model.FechaCita.Date && 
            !v.IsDeleted);

        if (citaExiste) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El vehículo ya tiene una cita programada para esa fecha."]));
        }
        
        if (ExistsMatricula(model.Matricula ?? ""))
            return Result.Failure<Cita, DomainError>(
                CitaErrors.MatriculaAlreadyExists(model.Matricula ?? ""));


        if (ContarVehiculosPorDni(model.DniPropietario ?? "") >= 3)
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El propietario ya tiene el límite de 3 vehículos"]));


        model = model with {
            Id = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

        try {
            var entity = model.ToEntity();
            _context.Citas.Add(entity);
            _context.SaveChanges();

            return Result.Success<Cita, DomainError>(GetById(entity.Id));
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al crear el vehiculo");
            return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
        }
    }

    public Result<Cita, DomainError> Update(int id, Cita model) {
        var entity = _context.Citas.FirstOrDefault(v => v.Id == id);
        if (entity == null)
            return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

        var existingModel = entity.ToModel();
        if (existingModel == null)
            return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
        
        // 1. REGLA: Validar fecha excluyendo la cita actual
        bool otraCitaEseDia = _context.Citas.Any(v => 
            v.Id != id && 
            v.Matricula == (model.Matricula ?? entity.Matricula) && 
            v.FechaItv.Date == model.FechaCita.Date && 
            !v.IsDeleted);

        if (otraCitaEseDia) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El vehículo ya tiene otra cita programada para ese día."]));
        }

        if ((model.Matricula ?? "") != (existingModel.Matricula ?? "") &&
            _context.Citas.Any(v => v.Matricula == (model.Matricula ?? "") && v.Id != id))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula ?? ""));

        var newDniPropietario = string.IsNullOrWhiteSpace(model.DniPropietario)
            ? existingModel.DniPropietario ?? ""
            : model.DniPropietario;
        if (newDniPropietario != (existingModel.DniPropietario ?? "") && _context.Citas.Any(v => v.DniPropietario == newDniPropietario && v.Id != id))
            return Result.Failure<Cita, DomainError>(CitaErrors.DniPropiestarioAlreadyExists(newDniPropietario));

        entity.Matricula = model.Matricula ?? "";
        entity.Marca = model.Marca ?? "";
        entity.Modelo = model.Modelo ?? "";
        entity.Cilindrada = model.Cilindrada;
        entity.Motor = (int)model.Motor;
        entity.DniPropietario = newDniPropietario;
        entity.FechaItv = model.FechaCita;
        entity.UpdatedAt = DateTime.UtcNow;

        try {
            _context.SaveChanges();
            return Result.Success<Cita, DomainError>(GetById(id)!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al actualizar vehiculo");
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
        try {
            return _context.Citas.Any(v => v.Matricula == matricula);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al verificar la Matricula {Matricula}", matricula);
            return false;
        }
    }

    public Cita? GetByDniPropietario(string dniPropietario) {
        try {
            var entities = _context.Citas.FirstOrDefault(v => v.DniPropietario == dniPropietario);
            return entities.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener el vehiculo por el Dni del propietario {DniPropietario}", dniPropietario);
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

    public int CountVehiculos(bool includeDeleted = false) {
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
            var entity = _context.Citas.Find(id);
            if (entity == null)
                return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.SaveChanges();
            
            _logger.Information("Vehiculo con Id {Id} restaurada correctamente",id);
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