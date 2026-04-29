using System.Data;
using CSharpFunctionalExtensions;
using Dapper;
using GestionITVPro.Entity;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Serilog;

namespace GestionITVPro.Repositories.Dapper;

public class CitaDapperRepository : ICitaRepository {
    private readonly IDbConnection _connection;
    private readonly ILogger _logger = Log.ForContext<CitaDapperRepository>();
    private Action? _onDispose;


    public CitaDapperRepository(IDbConnection connection, Action? onDispose = null, bool dropData = false,
        bool seeData = false) {
        _connection = connection;
        _onDispose = onDispose;
        EnsureTable(dropData);

        if (seeData && CountTotal() == 0) Seed();
    }


    public IEnumerable<Cita> GetAll(string? marca, string? dniPropietario, string? matricula, 
        DateTime? desde, DateTime? hasta, int page = 1, int pageSize = 10, bool includeDeleted = true) 
    {
        // CAMBIO: Usamos date() para que la comparación de strings funcione como fechas
        const string sql = @"
        SELECT * FROM Citas 
        WHERE (@Marca IS NULL OR Marca LIKE '%' || @Marca || '%')
          AND (@Dni IS NULL OR DniPropietario = @Dni)
          AND (@Matricula IS NULL OR Matricula = @Matricula)
          AND (@Desde IS NULL OR date(FechaItv) >= date(@Desde))
          AND (@Hasta IS NULL OR date(FechaItv) <= date(@Hasta))
          AND (@IncludeDeleted = 1 OR IsDeleted = 0)
        ORDER BY Id 
        LIMIT @Limit OFFSET @Offset";

        try {
            var parameters = new {
                Marca = string.IsNullOrWhiteSpace(marca) ? null : marca,
                Dni = string.IsNullOrWhiteSpace(dniPropietario) ? null : dniPropietario,
                Matricula = string.IsNullOrWhiteSpace(matricula) ? null : matricula,
                Desde = desde?.ToString("yyyy-MM-dd"), // Formato simplificado para date()
                Hasta = hasta?.ToString("yyyy-MM-dd"),
                IncludeDeleted = includeDeleted ? 1 : 0,
                Limit = pageSize,
                Offset = (page - 1) * pageSize
            };

            var entidades = _connection.Query<CitaEntity>(sql, parameters);
            return entidades.Select(e => e.ToModel()!);
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error en GetAll Dapper");
            return Enumerable.Empty<Cita>();
        }
    }

    public Cita? GetById(int id) {
        try {
            var sql = "SELECT * FROM Citas WHERE Id = @Id";
            var entity = _connection.QueryFirstOrDefault<CitaEntity>(sql, new { Id = id });
            return entity.ToModel();
        }
        catch (Exception ex) {
            _logger.Error(ex, "Erro al obtener vehiculo por ID {Id}", id);
            return null;
        }
    }

    public Result<Cita, DomainError> Create(Cita model) {
        var dni = model.DniPropietario ?? "";
        var matricula = model.Matricula ?? "";

        // 1. REGLA: Límite de 3 (Dapper usa tu método privado ContarCitaPorDni)
        if (ContarCitaPorDni(dni) >= 3) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["Límite alcanzado: Este propietario ya tiene 3 vehículos registrados."]));
        }

        // 2. REGLA: Cita mismo día
        if (ExisteCitaMismoDia(matricula, model.FechaItv)) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.MatriculaAlreadyExists(matricula));
        }

        // 3. INTEGRIDAD: Matrícula única
        if (ExistsMatricula(matricula)) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.MatriculaAlreadyExists(matricula));
        }

        const string sql = @"
            INSERT INTO Citas (
                Matricula, Marca, Modelo, Cilindrada, Motor, 
                DniPropietario, FechaItv, FechaInspeccion, CreatedAt, UpdatedAt, IsDeleted
            ) VALUES (
                @Matricula, @Marca, @Modelo, @Cilindrada, @Motor, 
                @DniPropietario, @FechaItv, @FechaInspeccion, @CreatedAt, @UpdatedAt, @IsDeleted
            );
            SELECT last_insert_rowid();";

        try {
            var id = _connection.QuerySingle<int>(sql, new {
                Matricula = model.Matricula,
                model.Marca,
                model.Modelo,
                model.Cilindrada,
                Motor = (int)model.Motor,
                DniPropietario = model.DniPropietario,
                FechaItv = model.FechaItv.ToString("yyyy-MM-dd HH:mm:ss"),
                FechaInspeccion = model.FechaInspeccion.ToString("yyyy-MM-dd HH:mm:ss"), // AÑADIDO
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                IsDeleted = 0
            });

            return GetById(id) is { } creado 
                ? Result.Success<Cita, DomainError>(creado)
                : Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error en Dapper Create");
            return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
        }
    }

    public Result<Cita, DomainError> Update(int id, Cita model) {
        var existing = GetById(id);
        if (existing == null) return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
        
        // 1. Matrícula duplicada
        if (model.Matricula != existing.Matricula && ExistsMatricula(model.Matricula))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula));
        
        // 2. Límite de 3
        if (model.DniPropietario != existing.DniPropietario && ContarCitaPorDni(model.DniPropietario) >= 3)
            return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["El nuevo propietario ya tiene el límite de 3 vehículos."]));

        // 3. Cita mismo día
        if (ExisteCitaMismoDia(model.Matricula, model.FechaItv, id))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(model.Matricula));

        const string sql = @"
            UPDATE Citas SET
                Matricula = @Matricula, Marca = @Marca, Modelo = @Modelo, 
                Cilindrada = @Cilindrada, Motor = @Motor, DniPropietario = @DniPropietario, 
                FechaItv = @FechaItv, FechaInspeccion = @FechaInspeccion, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND IsDeleted = 0";

        _connection.Execute(sql, new {
            Id = id,
            model.Matricula,
            model.Marca,
            model.Modelo,
            model.Cilindrada,
            Motor = (int)model.Motor,
            model.DniPropietario,
            FechaItv = model.FechaItv.ToString("yyyy-MM-dd HH:mm:ss"),
            FechaInspeccion = model.FechaInspeccion.ToString("yyyy-MM-dd HH:mm:ss"), // AÑADIDO
            UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        });

        return Result.Success<Cita, DomainError>(GetById(id)!);
    }

    public Cita? Delete(int id, bool isLogical = true) {
        try {
            var existing = GetById(id);
            if (existing == null)
                return null;

            if (isLogical) {
                var sql =
                    "UPDATE Citas SET IsDeleted = 1, DeletedAt = @DeletedAt, UpdatedAt = @UpdatedAt WHERE Id = @Id";
                _connection.Execute(sql, new { Id = id, DeletedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
                return GetById(id);
            }
            else {
                var sql = "DELETE FROM Citas WHERE Id = @Id";
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
            var sql = "SELECT * FROM Citas WHERE Matricula = @Matricula ";
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
            var sql = "SELECT COUNT(1) FROM Citas WHERE Matricula = @Matricula";
            return _connection.ExecuteScalar<int>(sql, new { Matricula = matricula }) > 0;
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al verificar la Matricula {Matricula}", matricula);
            return false;
        }
    }

    public Cita? GetByDniPropietario(string dniPropietario) {
        var sql = "SELECT * FROM Citas WHERE DniPropietario = @DniPropietario AND IsDeleted = 0 LIMIT 1";
        return _connection.QueryFirstOrDefault<CitaEntity>(sql, new { DniPropietario = dniPropietario })?.ToModel();
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        var sql = "SELECT COUNT(1) FROM Citas WHERE DniPropietario = @DniPropietario AND IsDeleted = 0";
        return _connection.ExecuteScalar<int>(sql, new { DniPropietario = dniPropietario }) > 0;
    }

    public bool DeleteAll() {
        try {
            _connection.Execute("DELETE FROM Citas");
            return true;
        }
        catch (Exception Ex) {
            _logger.Error(Ex, "Error al eliminar todos los vehiculos");
            return false;
        }
    }

    public int CountCita(bool includeDeleted = false) {
        try {
            var sql = includeDeleted
                ? "SELECT COUNT(1) FROM Citas"
                : "SELECT COUNT(1) FROM Citas WHERE IsDeleted = 0";
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
            var sql = "UPDATE Citas SET IsDeleted = 0, DeletedAt = NULL, UpdatedAt = @UpdatedAt WHERE Id = @Id";
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
        if (_connection.State != ConnectionState.Open) _connection.Open();
        if (dropData) _connection.Execute("DROP TABLE IF EXISTS Citas");

        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Citas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Matricula TEXT NOT NULL UNIQUE,
                Marca TEXT NOT NULL,
                Modelo TEXT NOT NULL,
                Cilindrada INTEGER,
                Motor INTEGER,
                DniPropietario TEXT NOT NULL,
                FechaItv TEXT NOT NULL,
                FechaInspeccion TEXT NOT NULL, -- AÑADIDO
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsDeleted INTEGER DEFAULT 0,
                DeletedAt TEXT
        )");
    }
    
    
    private bool ExisteCitaMismoDia(string matricula, DateTime fecha, int? excluirId = null) {
        // CAMBIADO: "Vehiculos" por "Citas"
        var sql = @"SELECT COUNT(1) FROM Citas 
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
        return _connection.ExecuteScalar<int>("SELECT COUNT(1) FROM Citas");
    }

    private int ContarCitaPorDni(string dni) =>
        _connection.ExecuteScalar<int>(
            "SELECT COUNT(1) FROM Citas WHERE DniPropietario = @DniPropietario AND IsDeleted = 0",
            new { DniPropietario = dni });


    private void Seed() {
        foreach (var v in VehiculosFactory.Seed()) Create(v);
    }
}

