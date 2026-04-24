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

        if (seedData && !_context.Vehiculos.Any()) {
            _logger.Information("Sembrando datos de vehiculos...");
            foreach (var v in VehiculosFactory.Seed()) {
                Create(v);
            }
        }
    }

    public IEnumerable<Vehiculo> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        try {
            var query = includeDeleted
                ? _context.Vehiculos.AsQueryable()
                : _context.Vehiculos.Where(v => !v.IsDeleted);

            var entities = query
                .OrderBy(v => v.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            return entities.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener vehiculos");
            return Enumerable.Empty<Vehiculo>();
        }
    }

    public Vehiculo? GetById(int id) {
        try {
            var entity = _context.Vehiculos.FirstOrDefault(v => v.Id == id);
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error("Error al obtener vehiculo por ID {Id}", id);
            return null;
        }
    }

    public Result<Vehiculo, DomainError> Create(Vehiculo model) {
        if (ExistsMatricula(model.Matricula ?? ""))
            return Result.Failure<Vehiculo, DomainError>(
                VehiculoErrors.MatriculaAlreadyExists(model.Matricula ?? ""));


        if (ContarVehiculosPorDni(model.DniPropietario ?? "") >= 3)
            return Result.Failure<Vehiculo, DomainError>(
                VehiculoErrors.Validation(["El propietario ya tiene el límite de 3 vehículos"]));


        model = model with {
            Id = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };

        try {
            var entity = model.ToEntity();
            _context.Vehiculos.Add(entity);
            _context.SaveChanges();

            return Result.Success<Vehiculo, DomainError>(GetById(entity.Id));
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al crear el vehiculo");
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.DatabaseError(ex.Message));
        }
    }

    public Result<Vehiculo, DomainError> Update(int id, Vehiculo model) {
        var entity = _context.Vehiculos.FirstOrDefault(v => v.Id == id);
        if (entity == null)
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.NotFound(id.ToString()));

        var existingModel = entity.ToModel();
        if (existingModel == null)
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.NotFound(id.ToString()));

        if ((model.Matricula ?? "") != (existingModel.Matricula ?? "") &&
            _context.Vehiculos.Any(v => v.Matricula == (model.Matricula ?? "") && v.Id != id))
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.MatriculaAlreadyExists(model.Matricula ?? ""));

        var newDniPropietario = string.IsNullOrWhiteSpace(model.DniPropietario)
            ? existingModel.DniPropietario ?? ""
            : model.DniPropietario;
        if (newDniPropietario != (existingModel.DniPropietario ?? "") && _context.Vehiculos.Any(v => v.DniPropietario == newDniPropietario && v.Id != id))
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.DniPropiestarioAlreadyExists(newDniPropietario));

        entity.Matricula = model.Matricula ?? "";
        entity.Marca = model.Marca ?? "";
        entity.Modelo = model.Modelo ?? "";
        entity.Cilindrada = model.Cilindrada;
        entity.Motor = (int)model.Motor;
        entity.DniPropietario = newDniPropietario;
        entity.UpdatedAt = DateTime.UtcNow;

        try {
            _context.SaveChanges();
            return Result.Success<Vehiculo, DomainError>(GetById(id)!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al actualizar vehiculo");
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.DatabaseError(ex.Message));
        }

    }

    public Vehiculo? Delete(int id, bool isLogical = true) {
        try {
            var entity = _context.Vehiculos.FirstOrDefault(v => v.Id == id);
            if (entity == null)
                return null;

            if (isLogical) {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                _context.SaveChanges();
                return GetById(id);
            }

            _context.Vehiculos.Remove(entity);
            _context.SaveChanges();
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al eliminar vehiculo");
            return null;
        }
    }

    public Vehiculo? GetByMatricula(string matricula) {
        try {
            var entity = _context.Vehiculos.FirstOrDefault(v => v.Matricula == matricula);
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener vehiculo por Matricula {Matricula}", matricula);
            return null;
        }
    }

    public bool ExistsMatricula(string matricula) {
        try {
            return _context.Vehiculos.Any(v => v.Matricula == matricula);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al verificar la Matricula {Matricula}", matricula);
            return false;
        }
    }

    public Vehiculo? GetByDniPropietario(string dniPropietario) {
        try {
            var entities = _context.Vehiculos.FirstOrDefault(v => v.DniPropietario == dniPropietario);
            return entities.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener el vehiculo por el Dni del propietario {DniPropietario}", dniPropietario);
            return null;
        }
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        try {
            return _context.Vehiculos.Any(v => v.DniPropietario == dniPropietario);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al verificar el DNI del propietario {DniPropietario}", dniPropietario);
            return false;
        }
    }

    public bool DeleteAll() {
        try {
            _context.Vehiculos.RemoveRange(_context.Vehiculos);
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
                ? _context.Vehiculos
                : _context.Vehiculos.Where(v => !v.IsDeleted);

            return query.Count();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al contar vehículos en EF");
            return 0;
        }
    }

    public Result<Vehiculo, DomainError> Restore(int id) {
        try {
            var entity = _context.Vehiculos.Find(id);
            if (entity == null)
                return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.NotFound(id.ToString()));
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.UpdatedAt = DateTime.UtcNow;
            _context.SaveChanges();
            
            _logger.Information("Vehiculo con Id {Id} restaurada correctamente",id);
            return Result.Success<Vehiculo, DomainError>(entity.ToModel()!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al restaurar vehiculo");
            return Result.Failure<Vehiculo, DomainError>(VehiculoErrors.DatabaseError(ex.Message));
        }
    }
    
    // Helper para la regla de los 3
    private int ContarVehiculosPorDni(string dni) => 
        _context.Vehiculos.Count(v => v.DniPropietario == dni && !v.IsDeleted);
}