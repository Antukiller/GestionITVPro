
using System.Data;
using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Entity;
using GestionITVPro.Enums;
using GestionITVPro.Error.Common;
using GestionITVPro.Error.Vehiculo;
using GestionITVPro.Factory;
using GestionITVPro.Mapper;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Microsoft.Data.Sqlite;
using Serilog;

namespace GestionITVPro.Repositories.Ado;

public class VehiculoAdoRepository : IVehiculoRepository {
    private static readonly Lazy<VehiculoAdoRepository> Lazy = new(() => new VehiculoAdoRepository());
    public static VehiculoAdoRepository Instance => Lazy.Value;
    
    private readonly ILogger _logger = Log.ForContext<VehiculoAdoRepository>();
    private readonly string _connectionString = AppConfig.ConnectionString;
    
    public VehiculoAdoRepository() : this(AppConfig.DropData, AppConfig.SeedData) { }

    public VehiculoAdoRepository(bool dropData, bool seedData) {
        _logger.Debug("Iniciando Repositorio Ado");
        EnsureDataFolder();
        
        if (dropData) {
            EnsureTable();
        }

        if (seedData && CountVehiculos(true) == 0) {
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
            DROP TABLE IF EXISTS Vehiculos;
            CREATE TABLE Vehiculos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Matricula TEXT NOT NULL UNIQUE,
                Marca TEXT NOT NULL,
                Modelo TEXT NOT NULL,
                Cilindrada INTEGER NOT NULL,
                Motor INTEGER NOT NULL,
                DniPropietario TEXT NOT NULL,
                FechaItv TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT
            );";
        command.ExecuteNonQuery();
    }

    public IEnumerable<Cita> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        _logger.Debug("GetAll: pag {Page}, size {Size}", page, pageSize);
        var entities = new List<CitaEntity>();
        using var connection = CreateConnection();
        connection.Open();
        
        string sql = "SELECT * FROM Vehiculos ";
        if (!includeDeleted) sql += "WHERE IsDeleted = 0 ";
        sql += "ORDER BY Id LIMIT @Limit OFFSET @Offset";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Limit", pageSize);
        command.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);

        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            entities.Add(MapReaderToEntity(reader));
        }
        return entities.ToModel();
    }

    public Cita? GetById(int id) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Vehiculos WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReaderToEntity(reader).ToModel() : null;
    }

    public Result<Cita, DomainError> Create(Cita cita) {
        _logger.Debug("Creando vehículo: {Matricula}", cita.Matricula);

        if (ExisteCitaMismoDia(cita.Matricula, cita.FechaCita)) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El vehiculo ya tiene una cita programada para este día"]));
        }

        if (ExistsMatricula(cita.Matricula))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(cita.Matricula));

        if (!ValidarLimiteVehiculos(cita.DniPropietario))
            return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["El propietario ya tiene 3 vehículos."]));

        var entity = cita.ToEntity();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Vehiculos (Matricula, Marca, Modelo, Cilindrada, Motor, DniPropietario, CreatedAt, UpdatedAt, IsDeleted)
            VALUES (@Matricula, @Marca, @Modelo, @Cilindrada, @Motor, @DniPropietario, @CreatedAt, @UpdatedAt, 0);
            SELECT last_insert_rowid();";
        
        AddParameters(command, entity);
        var id = Convert.ToInt32(command.ExecuteScalar());
        
        return Result.Success<Cita, DomainError>(GetById(id)!);
    }

    public Result<Cita, DomainError> Update(int id, Cita cita) {
        var existing = GetById(id);
        if (existing == null) return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));

        // Validar matrícula si cambia
        if (cita.Matricula != existing.Matricula && ExistsMatricula(cita.Matricula))
            return Result.Failure<Cita, DomainError>(CitaErrors.MatriculaAlreadyExists(cita.Matricula));

        // Validar límite si cambia de dueño
        if (cita.DniPropietario != existing.DniPropietario && !ValidarLimiteVehiculos(cita.DniPropietario))
            return Result.Failure<Cita, DomainError>(CitaErrors.Validation(["El nuevo propietario ya tiene 3 vehículos."]));
        
        // 1. Nueva restricción: Validar que no choque con OTRA cita (excluyendo el ID actual)
        if (ExisteCitaMismoDia(cita.Matricula, cita.FechaCita, id)) {
            return Result.Failure<Cita, DomainError>(
                CitaErrors.Validation(["El vehículo ya tiene otra cita programada para este día."]));
        }

        var entity = cita.ToEntity();
        entity.Id = id;
        entity.UpdatedAt = DateTime.UtcNow;

        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Vehiculos SET 
                Matricula = @Matricula, Marca = @Marca, Modelo = @Modelo, 
                Cilindrada = @Cilindrada, Motor = @Motor, DniPropietario = @DniPropietario, 
                UpdatedAt = @UpdatedAt 
            WHERE Id = @Id";
        
        AddParameters(command, entity);
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
            command.CommandText = "UPDATE Vehiculos SET IsDeleted = 1, DeletedAt = @DeletedAt WHERE Id = @Id";
            command.Parameters.AddWithValue("@DeletedAt", DateTime.UtcNow.ToString("o"));
        } else {
            command.CommandText = "DELETE FROM Vehiculos WHERE Id = @Id";
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
        command.CommandText = "UPDATE Vehiculos SET IsDeleted = 0, DeletedAt = NULL, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("o"));
        command.ExecuteNonQuery();

        return Result.Success<Cita, DomainError>(GetById(id)!);
    }

    public bool ExistsMatricula(string matricula) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Vehiculos WHERE Matricula = @Matricula";
        command.Parameters.AddWithValue("@Matricula", matricula);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public Cita? GetByMatricula(string matricula) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Vehiculos WHERE Matricula = @Matricula";
        command.Parameters.AddWithValue("@Matricula", matricula);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReaderToEntity(reader).ToModel() : null;
    }

    public Cita? GetByDniPropietario(string dniPropietario) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Vehiculos WHERE DniPropietario = @Dni AND IsDeleted = 0 LIMIT 1";
        command.Parameters.AddWithValue("@Dni", dniPropietario);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReaderToEntity(reader).ToModel() : null;
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Vehiculos WHERE DniPropietario = @Dni AND IsDeleted = 0";
        command.Parameters.AddWithValue("@Dni", dniPropietario);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public int CountVehiculos(bool includeDeleted = false) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = includeDeleted ? "SELECT COUNT(*) FROM Vehiculos" : "SELECT COUNT(*) FROM Vehiculos WHERE IsDeleted = 0";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public bool DeleteAll() {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Vehiculos";
        return command.ExecuteNonQuery() >= 0;
    }

    // --- MÉTODOS AUXILIARES ---

    private bool ValidarLimiteVehiculos(string dni) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Vehiculos WHERE DniPropietario = @Dni AND IsDeleted = 0";
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
        command.Parameters.AddWithValue("@DniPropietario", entity.DniPropietario);
        command.Parameters.AddWithValue("@CreatedAt", entity.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@UpdatedAt", entity.UpdatedAt.ToString("o"));
    }

    private CitaEntity MapReaderToEntity(SqliteDataReader reader) {
        return new CitaEntity {
            Id = reader.GetInt32("Id"),
            Matricula = reader.GetString("Matricula"),
            Marca = reader.GetString("Marca"),
            Modelo = reader.GetString("Modelo"),
            Cilindrada = reader.GetInt32("Cilindrada"),
            Motor = reader.GetInt32("Motor"),
            DniPropietario = reader.GetString("DniPropietario"),
            FechaItv = DateTime.Parse(reader.GetString(reader.GetOrdinal("FechaItv"))),
            CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
            UpdatedAt = DateTime.Parse(reader.GetString("UpdatedAt")),
            IsDeleted = reader.GetInt32("IsDeleted") == 1,
            DeletedAt = reader.IsDBNull("DeletedAt") ? null : DateTime.Parse(reader.GetString("DeletedAt"))
        };
    }
    
    private bool ExisteCitaMismoDia(string matricula, DateTime fecha, int? excluirId = null) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
    
        // Comparamos solo la parte de la fecha (YYYY-MM-DD)
        string sql = @"SELECT COUNT(1) FROM Vehiculos 
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