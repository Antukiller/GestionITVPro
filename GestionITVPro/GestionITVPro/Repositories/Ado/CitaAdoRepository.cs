
using System.Data;
using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Entity;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Microsoft.Data.Sqlite;
using Serilog;

namespace GestionITVPro.Repositories.Ado;

public class CitaAdoRepository : ICitaRepository {
    private static readonly Lazy<CitaAdoRepository> Lazy = new(() => new CitaAdoRepository());
    public static CitaAdoRepository Instance => Lazy.Value;
    
    private readonly ILogger _logger = Log.ForContext<CitaAdoRepository>();
    private readonly string _connectionString = AppConfig.ConnectionString;
    
    public CitaAdoRepository() : this(AppConfig.DropData, AppConfig.SeedData) { }

    public CitaAdoRepository(bool dropData, bool seedData) {
        _logger.Debug("Iniciando Repositorio Ado");
        EnsureDataFolder();
        
        if (dropData) {
            EnsureTable();
        }

        if (seedData && CountCita(true) == 0) {
            _logger.Information("Sembrando datos iniciales...");
            foreach (var vehiculo in VehiculosFactory.Seed()) {
                Create(vehiculo);
            }
        }
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private void EnsureDataFolder() {
        var path = Path.GetDirectoryName(AppConfig.DataFolder);
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }
    }

    private void EnsureTable() {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            DROP TABLE IF EXISTS Citas;
            CREATE TABLE Citas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Matricula TEXT NOT NULL UNIQUE,
                Marca TEXT NOT NULL,
                Modelo TEXT NOT NULL,
                Cilindrada INTEGER NOT NULL,
                Motor INTEGER NOT NULL,
                DniPropietario TEXT NOT NULL,
                FechaItv TEXT NOT NULL,
                FechaInspeccion TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT
            );";
        command.ExecuteNonQuery();
    }

   public IEnumerable<Cita> GetAll(string? marca, string? dniPropietario, string? matricula, 
    DateTime? desde, DateTime? hasta, int page = 1, int pageSize = 10, bool includeDeleted = true) {
    var lista = new List<Cita>();

    try {
        using var connection = CreateConnection(); // Corregido: Crear conexión local
        connection.Open(); // IMPORTANTE: Abrir conexión

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

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        // Parámetros
        command.Parameters.AddWithValue("@Marca", (object?)marca ?? DBNull.Value);
        command.Parameters.AddWithValue("@Dni", (object?)dniPropietario ?? DBNull.Value);
        command.Parameters.AddWithValue("@Matricula", (object?)matricula ?? DBNull.Value);
        command.Parameters.AddWithValue("@Desde", desde?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Hasta", hasta?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IncludeDeleted", includeDeleted ? 1 : 0);
        command.Parameters.AddWithValue("@Limit", pageSize);
        command.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);

        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            // Usamos el método MapReaderToEntity que ya tienes abajo para no repetir código
            lista.Add(MapReaderToEntity(reader).ToModel()!);
        }
    }
    catch (Exception ex) {
        _logger.Error(ex, "Error en GetAll ADO.NET");
    }

    return lista;
}

    public Cita? GetById(int id) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Citas WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReaderToEntity(reader).ToModel() : null;
    }

   public Result<Cita, DomainError> Create(Cita model)
    {
        var dni = model.DniPropietario ?? "";
        var matricula = model.Matricula ?? "";

        // 1. REGLA: Límite de 3 (Validar PRIMERO para los tests)
        if (!ValidarLimiteVehiculos(dni))
        {
            _logger.Warning("Límite alcanzado para DNI {Dni}", dni);
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["Límite alcanzado: Este propietario ya tiene 3 vehículos registrados."]));
        }

        // 2. REGLA: Cita mismo día
        if (ExisteCitaMismoDia(matricula, model.FechaItv))
        {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.MatriculaAlreadyExists(matricula));
        }

        // 3. INTEGRIDAD: Matrícula única
        if (ExistsMatricula(matricula))
        {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.MatriculaAlreadyExists(matricula));
        }

        using var connection = CreateConnection(); // Usamos tu método local
        connection.Open();

        const string sql = @"
            INSERT INTO Citas (
                Matricula, Marca, Modelo, Cilindrada, Motor, 
                DniPropietario, FechaItv, FechaInspeccion, CreatedAt, UpdatedAt, 
                IsDeleted, DeletedAt
            ) VALUES (
                @Matricula, @Marca, @Modelo, @Cilindrada, @Motor, 
                @DniPropietario, @FechaItv, @FechaInspeccion, @CreatedAt, @UpdatedAt, 
                @IsDeleted, @DeletedAt
            );
            SELECT last_insert_rowid();";

        using var command = new SqliteCommand(sql, connection);

        // Añadimos TODOS los parámetros para evitar el error "Must add values..."
        command.Parameters.AddWithValue("@Matricula", matricula);
        command.Parameters.AddWithValue("@Marca", model.Marca ?? "");
        command.Parameters.AddWithValue("@Modelo", model.Modelo ?? "");
        command.Parameters.AddWithValue("@Cilindrada", model.Cilindrada);
        command.Parameters.AddWithValue("@Motor", (int)model.Motor);
        command.Parameters.AddWithValue("@DniPropietario", dni);
        command.Parameters.AddWithValue("@FechaItv", model.FechaItv.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@FechaInspeccion", model.FechaInspeccion.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@IsDeleted", 0);
        command.Parameters.AddWithValue("@DeletedAt", DBNull.Value);

        try
        {
            var id = Convert.ToInt32(command.ExecuteScalar());
            var creado = GetById(id);
            return creado != null 
                ? Result.Success<Cita, DomainError>(creado)
                : Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error al crear cita en ADO");
            // Si salta un error de restricción de base de datos que no pillamos antes
            if (ex.Message.Contains("UNIQUE")) 
                return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(matricula));
                
            return Result.Failure<Cita, DomainError>(CitaErrors.Validation([ex.Message]));
        }
    }

    public Result<Cita, DomainError> Update(int id, Cita cita) {
    var existing = GetById(id);
    if (existing == null) return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

    // 1. Validar matrícula si cambia
    if (cita.Matricula != existing.Matricula && ExistsMatricula(cita.Matricula))
        return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(cita.Matricula));

    // 2. Validar límite si cambia de dueño
    if (cita.DniPropietario != existing.DniPropietario && !ValidarLimiteVehiculos(cita.DniPropietario))
        return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["El nuevo propietario ya tiene el límite de 3 vehículos."]));
    
    // 3. Validar cita mismo día (Cambiamos el tipo de error a MatriculaAlreadyExists para consistencia)
    if (ExisteCitaMismoDia(cita.Matricula, cita.FechaItv, id)) {
        return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(cita.Matricula));
    }

    using var connection = CreateConnection();
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = @"
        UPDATE Citas SET 
            Matricula = @Matricula, 
            Marca = @Marca, 
            Modelo = @Modelo, 
            Cilindrada = @Cilindrada, 
            Motor = @Motor, 
            DniPropietario = @DniPropietario, 
            FechaItv = @FechaItv,
            FechaInspeccion = @FechaInspeccion,
            UpdatedAt = @UpdatedAt 
        WHERE Id = @Id AND IsDeleted = 0"; // Aseguramos que no actualizamos algo borrado
    
    // En lugar de AddParameters genérico, ponemos los específicos del UPDATE
    command.Parameters.AddWithValue("@Id", id);
    command.Parameters.AddWithValue("@Matricula", cita.Matricula);
    command.Parameters.AddWithValue("@Marca", cita.Marca);
    command.Parameters.AddWithValue("@Modelo", cita.Modelo);
    command.Parameters.AddWithValue("@Cilindrada", cita.Cilindrada);
    command.Parameters.AddWithValue("@Motor", (int)cita.Motor);
    command.Parameters.AddWithValue("@DniPropietario", cita.DniPropietario);
    command.Parameters.AddWithValue("@FechaItv", cita.FechaItv.ToString("yyyy-MM-dd HH:mm:ss"));
    command.Parameters.AddWithValue("@FechaInspeccion", cita.FechaInspeccion.ToString("yyyy-MM-dd HH:mm:ss"));
    command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

    command.ExecuteNonQuery();
    return Result.Success<Cita, DomainError>(GetById(id)!);
}

    public Cita? Delete(int id, bool isLogical = true) {
        var existing = GetById(id);
        if (existing == null) return null;

        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();

        if (isLogical) {
            command.CommandText = "UPDATE Citas SET IsDeleted = 1, DeletedAt = @DeletedAt WHERE Id = @Id";
            command.Parameters.AddWithValue("@DeletedAt", DateTime.UtcNow.ToString("o"));
        } else {
            command.CommandText = "DELETE FROM Citas WHERE Id = @Id";
        }
        
        command.Parameters.AddWithValue("@Id", id);
        command.ExecuteNonQuery();
        
        return isLogical ? GetById(id) : existing;
    }

    public Result<Cita, DomainError> Restore(int id) {
        var existing = GetById(id);
        if (existing == null) return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Citas SET IsDeleted = 0, DeletedAt = NULL, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("o"));
        command.ExecuteNonQuery();

        return Result.Success<Cita, DomainError>(GetById(id)!);
    }

    public bool ExistsMatricula(string matricula) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Citas WHERE Matricula = @Matricula";
        command.Parameters.AddWithValue("@Matricula", matricula);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public Cita? GetByMatricula(string matricula) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Citas WHERE Matricula = @Matricula";
        command.Parameters.AddWithValue("@Matricula", matricula);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReaderToEntity(reader).ToModel() : null;
    }

    public Cita? GetByDniPropietario(string dniPropietario) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Citas WHERE DniPropietario = @Dni AND IsDeleted = 0 LIMIT 1";
        command.Parameters.AddWithValue("@Dni", dniPropietario);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReaderToEntity(reader).ToModel() : null;
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Citas WHERE DniPropietario = @Dni AND IsDeleted = 0";
        command.Parameters.AddWithValue("@Dni", dniPropietario);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public int CountCita(bool includeDeleted = false) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = includeDeleted ? "SELECT COUNT(*) FROM Citas" : "SELECT COUNT(*) FROM Citas WHERE IsDeleted = 0";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public bool DeleteAll() {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Citas";
        return command.ExecuteNonQuery() >= 0;
    }

    // --- MÉTODOS AUXILIARES ---

    private bool ValidarLimiteVehiculos(string dni) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Citas WHERE DniPropietario = @Dni AND IsDeleted = 0";
        command.Parameters.AddWithValue("@Dni", dni);
        return Convert.ToInt32(command.ExecuteScalar()) < 3;
    }

    private void AddParameters(SqliteCommand command, CitaEntity entity) {
        command.Parameters.AddWithValue("@Id", entity.Id);
        command.Parameters.AddWithValue("@Matricula", entity.Matricula);
        command.Parameters.AddWithValue("@Marca", entity.Marca);
        command.Parameters.AddWithValue("@Modelo", entity.Modelo);
        command.Parameters.AddWithValue("@Cilindrada", entity.Cilindrada);
        command.Parameters.AddWithValue("@Motor", (int)entity.Motor);
        command.Parameters.AddWithValue("@FechaItv", entity.FechaItv.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@FechaInspeccion", entity.FechaInspeccion.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@DniPropietario", entity.DniPropietario);
        command.Parameters.AddWithValue("@CreatedAt", entity.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@UpdatedAt", entity.UpdatedAt.ToString("o"));
    }

    private CitaEntity MapReaderToEntity(SqliteDataReader reader) {
        return new CitaEntity {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Matricula = reader.GetString(reader.GetOrdinal("Matricula")),
            Marca = reader.GetString(reader.GetOrdinal("Marca")),
            Modelo = reader.GetString(reader.GetOrdinal("Modelo")),
            Cilindrada = reader.GetInt32(reader.GetOrdinal("Cilindrada")),
            Motor = reader.GetInt32(reader.GetOrdinal("Motor")),
            DniPropietario = reader.GetString(reader.GetOrdinal("DniPropietario")),
            FechaItv = DateTime.Parse(reader.GetString(reader.GetOrdinal("FechaItv"))),
            FechaInspeccion = DateTime.Parse(reader.GetString(reader.GetOrdinal("FechaInspeccion"))),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            IsDeleted = reader.GetInt32(reader.GetOrdinal("IsDeleted")) == 1,
            DeletedAt = reader.IsDBNull(reader.GetOrdinal("DeletedAt")) 
                ? null 
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("DeletedAt")))
        };
    }
    
    private bool ExisteCitaMismoDia(string matricula, DateTime fecha, int? excluirId = null) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
    
        // Comparamos solo la parte de la fecha (YYYY-MM-DD)
        string sql = @"SELECT COUNT(1) FROM Citas 
                   WHERE Matricula = @Matricula 
                   AND date(FechaItv) = date(@Fecha) 
                   AND IsDeleted = 0";
    
        if (excluirId.HasValue) sql += " AND Id <> @Id";

        command.CommandText = sql;
        command.Parameters.AddWithValue("@Matricula", matricula);
        command.Parameters.AddWithValue("@Fecha", fecha.ToString("yyyy-MM-dd"));
        if (excluirId.HasValue) command.Parameters.AddWithValue("@Id", excluirId.Value);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }
}