using System.Data;
using CSharpFunctionalExtensions;
using Dapper;
using GestionITVPro.Entity;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Microsoft.Data.Sqlite;
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


    public IEnumerable<Cita> GetAll(int pagina, int tamPagina, bool isDeleteInclude, string campoBusqueda) 
    {
        // Usamos '%' || @Busqueda || '%' para concatenar comodines en SQLite
        const string sql = @"
    SELECT * FROM Citas 
    WHERE (@IncludeDeleted = 1 OR IsDeleted = 0)
      AND (@Busqueda IS NULL OR (
          Matricula LIKE '%' || @Busqueda || '%' OR
          Marca LIKE '%' || @Busqueda || '%' OR
          Modelo LIKE '%' || @Busqueda || '%' OR
          DniPropietario LIKE '%' || @Busqueda || '%'
      ))
    ORDER BY Id 
    LIMIT @Limit OFFSET @Offset";

        try {
            var parameters = new {
                Busqueda = string.IsNullOrWhiteSpace(campoBusqueda) ? null : campoBusqueda,
                IncludeDeleted = isDeleteInclude ? 1 : 0,
                Limit = tamPagina,
                Offset = (pagina - 1) * tamPagina
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

   public Result<IEnumerable<Cita>, DomainError> GetByDateMatricula(
    DateTime inicio, DateTime? fin, int pagina, int tamPagina, 
    string searchText = null, string motor = "TODOS", bool isDeleteInclude = false)
{
    try
    {
        // SQL corregido para usar las funciones de fecha de SQLite y parámetros consistentes
        var sql = @"
        SELECT * FROM Citas 
        WHERE date(FechaInspeccion) >= date(@inicio) 
        AND (@fin IS NULL OR date(FechaInspeccion) <= date(@fin))
        AND (@isDeleteInclude = 1 OR IsDeleted = 0)
        AND (@motor = 'TODOS' OR Motor = @motorValue)
        AND (@search IS NULL OR (
            LOWER(Matricula) LIKE @search OR 
            LOWER(DniPropietario) LIKE @search OR 
            LOWER(Marca) LIKE @search))
        ORDER BY FechaInspeccion ASC
        LIMIT @limit OFFSET @offset";

        // Mapeo de Motor: Si en DB es INTEGER (como en tu EnsureTable), 
        // necesitamos convertir el string "GASOLINA" a su valor int del Enum.
        int motorValue = 0;
        if (motor != "TODOS")
        {
            // Intentamos parsear el string al Enum MotorType (o como se llame en tu modelo)
            if (Enum.TryParse<Motor>(motor, true, out var resultadoEnum))
            {
                motorValue = (int)resultadoEnum;
            }
        }

        var parametros = new {
            inicio = inicio.ToString("yyyy-MM-dd"),
            fin = fin?.ToString("yyyy-MM-dd"),
            isDeleteInclude = isDeleteInclude ? 1 : 0,
            motor = motor.ToUpper(),
            motorValue = motorValue,
            search = string.IsNullOrWhiteSpace(searchText) ? null : $"%{searchText.ToLower()}%",
            limit = tamPagina,
            offset = (pagina - 1) * tamPagina
        };

        // Ejecutamos sobre _connection directamente (la que recibe el constructor)
        var entidades = _connection.Query<CitaEntity>(sql, parametros);
        
        var modelos = entidades.Select(e => e.ToModel()!).ToList();

        return Result.Success<IEnumerable<Cita>, DomainError>(modelos);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Error en GetByDateMatricula Dapper");
        return Result.Failure<IEnumerable<Cita>, DomainError>(CitaErrors.DatabaseError(ex.Message));
    }
}

    public Result<Cita, DomainError> Create(Cita model) {
    // 1. REGLA: Límite de 3 por DNI el mismo día
    // Nota: He ajustado la lógica para que cuente citas en ese DNI para ESA fecha específica
    var citasDniFecha = _connection.ExecuteScalar<int>(
        "SELECT COUNT(1) FROM Citas WHERE DniPropietario = @Dni AND date(FechaItv) = date(@Fecha) AND IsDeleted = 0",
        new { Dni = model.DniPropietario, Fecha = model.FechaItv.ToString("yyyy-MM-dd") });

    if (citasDniFecha >= 3) {
        return Result.Failure<Cita, DomainError>(
            CitaErrors.Validation(["Límite alcanzado: Máximo 3 citas por propietario al día."]));
    }

    // 2. REGLA: El vehículo no puede repetir el mismo día
    if (ExisteCitaMismoDia(model.Matricula, model.FechaItv)) {
        return Result.Failure<Cita, DomainError>(
            CitaErrors.MatriculaAlreadyExists(model.Matricula));
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
            model.Matricula,
            model.Marca,
            model.Modelo,
            model.Cilindrada,
            Motor = (int)model.Motor,
            model.DniPropietario,
            // Guardamos las fechas como strings ISO para que SQLite pueda filtrarlas con date()
            FechaItv = model.FechaItv.ToString("yyyy-MM-dd HH:mm:ss"),
            FechaInspeccion = model.FechaInspeccion.ToString("yyyy-MM-dd HH:mm:ss"),
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            IsDeleted = 0
        });

        var creado = GetById(id);
        return creado != null 
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
    
    public int CountCitasFiltradas(string? matricula, DateTime inicio, DateTime? fin, bool isDeleteInclude) 
    {
        // La consulta es casi igual a la de búsqueda, pero solo pedimos el COUNT
        const string sql = @"
        SELECT COUNT(1) FROM Citas 
        WHERE (@IncludeDeleted = 1 OR IsDeleted = 0)
          AND date(FechaInspeccion) >= date(@Inicio)
          AND (@Fin IS NULL OR date(FechaInspeccion) <= date(@Fin))
          AND (@Matricula IS NULL OR Matricula LIKE @MatriculaLike)";

        try 
        {
            var parameters = new {
                Inicio = inicio.ToString("yyyy-MM-dd"),
                Fin = fin?.ToString("yyyy-MM-dd"),
                IncludeDeleted = isDeleteInclude ? 1 : 0,
                Matricula = string.IsNullOrWhiteSpace(matricula) ? null : matricula,
                MatriculaLike = string.IsNullOrWhiteSpace(matricula) ? null : $"%{matricula}%"
            };

            // ExecuteScalar devuelve el primer valor de la primera fila (el COUNT)
            return _connection.ExecuteScalar<int>(sql, parameters);
        }
        catch (Exception ex) 
        {
            _logger.Error(ex, "Error al contar citas filtradas con Dapper");
            return 0;
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
                Cilindrada INTEGER NOT NULL,
                Motor INTEGER NOT NULL,
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
        foreach (var v in CitasFactory.Seed()) Create(v);
    }
}

