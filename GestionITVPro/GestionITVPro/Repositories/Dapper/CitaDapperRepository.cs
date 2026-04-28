using System.Data;
using CSharpFunctionalExtensions;
using Dapper;
using GestionITVPro.Entity;
using GestionITVPro.Error.Common;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Serilog;

namespace GestionITVPro.Storage.Dapper;

public class VehiculoDapperRepository : IVehiculoRepository {
    private readonly IDbConnection _connection;
    private readonly ILogger _logger = Log.ForContext<VehiculoDapperRepository>();
    private Action? _onDispose;


    public VehiculoDapperRepository(IDbConnection connection, Action? onDispose = null, bool dropData = false,
        bool seeData = false) {
        _connection = connection;
        _onDispose = _onDispose;
        EnsureTable(dropData);

        if (seeData && CountTotal() == 0) Seed();
    }

    public IEnumerable<Cita> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        try {
            var sql = includeDeleted
                ? "SELECT * FROM Vehiculos ORDER BY Id LIMIT @PageSize OFFSET @Offset"
                : "SELECT * FROM Vehiculos WHERE IsDeleted = 0 ORDER BY Id LIMIT @PageSize OFFSET @Offset";
            var entities = _connection
                .Query<CitaEntity>(sql, new { PageSize = pageSize, Offset = (page - 1) * pageSize }).ToList();
            return entities.Select(e => e.ToModel()!).ToList();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al obtener vehiculos");
            return [];
        }
    }

    public Cita? GetById(int id) {
        try {
            var sql = "SELECT * FROM Vehiculos WHERE Id = @Id";
            var entity = _connection.QueryFirstOrDefault<CitaEntity>(sql, new { Id = id });
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Erro al obtener vehiculo por ID {Id}", id);
            return null;
        }
    }

    public Result<Cita, DomainError> Create(Cita model) {
        
        if (ExisteCitaMismoDia(model.Matricula ?? "", model.FechaCita)) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El vehículo ya tiene una cita programada para esa fecha."]));
        }
        if (ExistsMatricula(model.Matricula ?? ""))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula ?? ""));
        if (ContarVehiculosPorDni(model.DniPropietario ?? "") >= 3 )
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El propietario ya tiene el límite de 3 vehículos"]));

        model = model with {
            Id = 0,
            Marca = string.IsNullOrWhiteSpace(model.Marca)
                ? $"{(model.Matricula ?? "").ToLower()}@BMW"
                : model.Marca,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        var entity = model.ToEntity();

        try {
            var sql =
                @"INSERT INTO Vehiculos (Matricula, Marca, Modelo, Cilindrada, Motor, DniPropietario, FechaItv, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
                VALUES (@Matricula, @Marca, @Modelo, @Cilindrada, @Motor, @DniPropietario,@FechaItv, @CreatedAt, @UpdatedAt, @IsDeleted, @DeletedAt);
                SELECT last_insert_rowid();";

            entity.Id = _connection.ExecuteScalar<int>(sql, new {
                Matricula = entity.Matricula ?? "",
                entity.Marca,
                entity.Modelo,
                entity.Cilindrada,
                entity.Motor,
                DniPropietario = entity.DniPropietario ?? "",
                FechaItv = entity.FechaItv.ToString("yyyy-MM-dd"),
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.IsDeleted,
                entity.DeletedAt
            });
            return Result.Success<Cita, DomainError>(GetById(entity.Id)!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Erro al crear el vehículo");
            return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
            throw;
        }
    }

    public Result<Cita, DomainError> Update(int id, Cita model) {
        var existing = GetById(id);
        if (existing == null)
            return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
        
        if ((model.Matricula ?? "") != (existing.Matricula ?? "") && ExistsMatricula(model.Matricula ?? ""))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula ?? ""));
        
        var newDniPropietario = string.IsNullOrWhiteSpace(model.DniPropietario) ? existing.DniPropietario ?? "" : model.DniPropietario;
        if (newDniPropietario != (existing.DniPropietario ?? "") && ExistsDniPropietario(newDniPropietario))
            return Result.Failure<Cita, DomainError>(CitaErrors.DniPropiestarioAlreadyExists(newDniPropietario));
        
        model = model with {
            Id = 0,
            Marca = string.IsNullOrWhiteSpace(model.Marca)
                ? $"{(model.Matricula ?? "").ToLower()}@BMW"
                : model.Marca,
            DniPropietario = newDniPropietario,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false,
            DeletedAt = null
        };
        var entity = model.ToEntity();
        entity.Id = id;
        entity.UpdatedAt = DateTime.UtcNow;

        try {
            var sql =
                @"UPDATE Vehiculos SET
                  Matricula = @Matricula, Marca = @Marca, Modelo = @Modelo, Cilindrada = @Cilindrada, Motor = @Motor, DniPropietario = @DniPropietario, 
                  FechaItv = @FechaItv, CreatedAt = @CreatedAt, UpdatedAt = @UpdatedAt, IsDeleted = @IsDeleted, DeletedAt = @DeletedAt
                WHERE Id = @Id";

            entity.Id = _connection.ExecuteScalar<int>(sql, new {
                Id = id,
                Matricula = entity.Matricula ?? "",
                entity.Marca,
                entity.Modelo,
                entity.Cilindrada,
                entity.Motor,
                DniPropietario = entity.DniPropietario ?? "",
                FechaItv = entity.FechaItv.ToString("yyyy-MM-dd"),
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.IsDeleted,
                entity.DeletedAt
            });
            var a = GetById(id);
            return a != null
                ? Result.Success<Cita, DomainError>(a)
                : Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError("Error al recuperar tras actualizar"));

        }
        catch (Exception ex) {
            _logger.Error(ex, "Erro al crear el vehículo");
            return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
            
        }
    }

    public Cita? Delete(int id, bool isLogical = true) {
        try {
            var existing = GetById(id);
            if (existing == null)
                return null;

            if (isLogical) {
                var sql =
                    "UPDATE Vehiculos SET IsDeleted = 1, DeletedAt = @DeletedAt, UpdatedAt = @UpdatedAt WHERE Id = @Id";
                _connection.Execute(sql, new { Id = id, DeletedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
                return GetById(id);
            }
            else {
                var sql = "DELETE FROM Vehiculos WHERE Id = @Id";
                _connection.Execute(sql, new { Id = id });
                return existing;
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al eliminar un vehiculo...");
            return null;
        }
    }

    public Cita? GetByMatricula(string matricula) {
        try {
            var sql = "SELECT * FROM Vehiculos WHERE Matricula = @Matricula ";
            var entity = _connection.QueryFirstOrDefault<CitaEntity>(sql, new { Matricula = matricula });
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Errpr al obtener vehiculo por Matricula {Matricula}", matricula);
            return null;
        }
    }

    public bool ExistsMatricula(string matricula) {
        try {
            var sql = "SELECT COUNT(1) FROM Vehiculos WHERE Matricula = @Matricula";
            return _connection.ExecuteScalar<int>(sql, new { Matricula = matricula }) > 0;
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al verificar la Matricula {Matricula}", matricula);
            return false;
        }
    }

    public Cita? GetByDniPropietario(string dniPropietario) {
        var sql = "SELECT * FROM Vehiculos WHERE DniPropietario = @DniPropietario AND IsDeleted = 0 LIMIT 1";
        return _connection.QueryFirstOrDefault<CitaEntity>(sql, new { DniPropietario = dniPropietario })?.ToModel();
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        var sql = "SELECT COUNT(1) FROM Vehiculos WHERE DniPropietario = @DniPropietario AND IsDeleted = 0";
        return _connection.ExecuteScalar<int>(sql, new { DniPropietario = dniPropietario }) > 0;
    }

    public bool DeleteAll() {
        try {
            _connection.Execute("DELETE FROM Vehiculos");
            return true;
        }
        catch (Exception Ex) {
            _logger.Error(Ex, "Error al eliminar todos los vehiculos");
            return false;
        }
    }

    public int CountVehiculos(bool includeDeleted = false) {
        try {
            var sql = includeDeleted
                ? "SELECT COUNT(1) FROM Vehiculos"
                : "SELECT COUNT(1) FROM Vehiculos WHERE IsDeleted = 0";
            return _connection.ExecuteScalar<int>(sql);
        }
        catch {
            return 0;
        }
    }

    public Result<Cita, DomainError> Restore(int id) {
        try {
            var existing = GetById(id);
            if (existing == null)
                return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
            var sql = "UPDATE Vehiculos SET IsDeleted = 0, DeletedAt = NULL, UpdatedAt = @UpdatedAt WHERE Id = @Id";
            _connection.Execute(sql, new { Id = id, UpdatedAt = DateTime.UtcNow });
            
            _logger.Information("Vehiculo con Id {Id} restaurada correctamente", id);
            return Result.Success<Cita, DomainError>(GetById(id)!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al restaurar vehiculo");
            return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
        }
    }

    private void EnsureTable(bool dropData) {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        if (dropData) _connection.Execute("DROP TABLE IF EXISTS Vehiculos");

        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Vehiculos (
                Id INTEGER PRIMARY KEY,
                Matricula TEXT NOT NULL UNIQUE,
                Marca TEXT NOT NULL,
                Modelo TEXT NOT NULl,
                Cilindrada INTEGER,
                Motor INTEGER,
                DniPropietario TEXT NOT NULL,
                FechaItv TEXT NOT NULl,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsDeleted INTEGER DEFAULT 0,
                DeletedAt TEXT
        )");
    }
    
    
    private bool ExisteCitaMismoDia(string matricula, DateTime fecha, int? excluirId = null) {
        var sql = @"SELECT COUNT(1) FROM Vehiculos 
                WHERE Matricula = @Matricula 
                AND date(FechaItv) = date(@Fecha) 
                AND IsDeleted = 0";
    
        if (excluirId.HasValue) sql += " AND Id <> @Id";

        return _connection.ExecuteScalar<int>(sql, new { 
            Matricula = matricula, 
            Fecha = fecha.ToString("yyyy-MM-dd"), 
            Id = excluirId 
        }) > 0;
    }

    private int CountTotal() {
        return _connection.ExecuteScalar<int>("SELECT COUNT(1) FROM Vehiculos");
    }

    private int ContarVehiculosPorDni(string dni) =>
        _connection.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM Vehiculos WHERE DniPropietario = @DniPropietario AND IsDeleted = 0",
            new { DniPropietario = dni });


    private void Seed() {
        foreach (var v in VehiculosFactory.Seed()) Create(v);
    }
}

