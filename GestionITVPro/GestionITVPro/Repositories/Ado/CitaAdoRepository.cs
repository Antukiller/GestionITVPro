
using System.Data;
using System.IO;
using CSharpFunctionalExtensions;
using GestionITVPro.Config;
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
            foreach (var vehiculo in CitasFactory.Seed()) {
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

    public IEnumerable<Cita> GetAll(int pagina, int tamPagina, bool isDeleteInclude, string campoBusqueda) {
        var lista = new List<Cita>();

        try {
            using var connection = CreateConnection();
            connection.Open();

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

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            // Si campoBusqueda es nulo o vacío, pasamos DBNull para que el SQL ignore el filtro
            command.Parameters.AddWithValue("@Busqueda", string.IsNullOrWhiteSpace(campoBusqueda) ? DBNull.Value : campoBusqueda);
            command.Parameters.AddWithValue("@IncludeDeleted", isDeleteInclude ? 1 : 0);
            command.Parameters.AddWithValue("@Limit", tamPagina);
            command.Parameters.AddWithValue("@Offset", (pagina - 1) * tamPagina);

            using var reader = command.ExecuteReader();
            while (reader.Read()) {
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

   public Result<IEnumerable<Cita>, DomainError> GetByDateMatricula(
    DateTime inicio, DateTime? fin, int pagina, int tamPagina, 
    string searchText = null, string motor = "TODOS", bool isDeleteInclude = false)
{
    var lista = new List<Cita>();

    try {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        
        // 1. Lógica para el Motor (Convertir Texto de la UI a Entero de la DB)
        int motorValue = -1;
        if (!motor.Equals("TODOS", StringComparison.OrdinalIgnoreCase)) {
            var motorBusqueda = motor
                .Replace("DIÉSEL", "Diesel")
                .Replace("HÍBRIDO", "Hibrido")
                .Replace("ELÉCTRICO", "Electrico")
                .Trim();
            if (Enum.TryParse<Motor>(motorBusqueda, true, out var resultadoEnum)) {
                motorValue = (int)resultadoEnum;
            }
        }

        // 2. Query SQL corregida
        command.CommandText = @"
            SELECT * FROM Citas 
            WHERE date(FechaInspeccion) >= date(@inicio)
            AND (@fin_val IS NULL OR date(FechaInspeccion) <= date(@fin_val))
            AND IsDeleted = @inc_del
            AND (@motor_text = 'TODOS' OR Motor = @motor_int) -- Comparación numérica
            AND (@search_val IS NULL OR (
                LOWER(Matricula) LIKE @search_val OR 
                LOWER(DniPropietario) LIKE @search_val OR 
                LOWER(Marca) LIKE @search_val
            ))
            ORDER BY FechaInspeccion ASC
            LIMIT @limit OFFSET @offset";

        // 3. Parámetros limpios
        command.Parameters.AddWithValue("@inicio", inicio.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@fin_val", fin.HasValue ? fin.Value.ToString("yyyy-MM-dd") : DBNull.Value);
        command.Parameters.AddWithValue("@inc_del", isDeleteInclude ? 1 : 0);
        command.Parameters.AddWithValue("@motor_text", motor);
        command.Parameters.AddWithValue("@motor_int", motorValue);
        command.Parameters.AddWithValue("@search_val", string.IsNullOrWhiteSpace(searchText) ? DBNull.Value : $"%{searchText.ToLower()}%");
        command.Parameters.AddWithValue("@limit", tamPagina);
        command.Parameters.AddWithValue("@offset", (pagina - 1) * tamPagina);

        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            var entity = new CitaEntity {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Matricula = reader.GetString(reader.GetOrdinal("Matricula")),
                Marca = reader.GetString(reader.GetOrdinal("Marca")),
                Modelo = reader.GetString(reader.GetOrdinal("Modelo")),
                DniPropietario = reader.GetString(reader.GetOrdinal("DniPropietario")),
                // IMPORTANTE: Si en DB es INTEGER, leemos GetInt32
                Motor = reader.GetInt32(reader.GetOrdinal("Motor")), 
                FechaInspeccion = DateTime.Parse(reader.GetString(reader.GetOrdinal("FechaInspeccion"))),
                IsDeleted = reader.GetInt32(reader.GetOrdinal("IsDeleted")) == 1
            };
            lista.Add(entity.ToModel()!);
        }

        return Result.Success<IEnumerable<Cita>, DomainError>(lista);
    }
    catch (Exception ex) {
        return Result.Failure<IEnumerable<Cita>, DomainError>(CitaErrors.DatabaseError(ex.Message));
    }
}

    public Result<Cita, DomainError> Create(Cita model) {
    var dni = model.DniPropietario ?? "";
    var matricula = model.Matricula ?? "";

    // 1. REGLA: Límite de 3 citas por propietario el mismo día
    // Reutilizamos ExisteLimiteDniDia (método auxiliar que definiremos abajo)
    if (ExisteLimiteDniDia(dni, model.FechaItv)) {
        return Result.Failure<Cita, DomainError>(
            CitaErrors.Validation(["Límite alcanzado: Máximo 3 citas por propietario al día."]));
    }

    // 2. REGLA: El vehículo no puede repetir el mismo día
    if (ExisteCitaMismoDia(matricula, model.FechaItv)) {
        return Result.Failure<Cita, DomainError>(
            CitaErrors.MatriculaAlreadyExists(matricula));
    }

    // 3. INTEGRIDAD: Matrícula única global (si no ha sido borrada)
    if (ExistsMatricula(matricula)) {
        return Result.Failure<Cita, DomainError>(
            CitaErrors.MatriculaAlreadyExists(matricula));
    }

    using var connection = CreateConnection();
    connection.Open();

    const string sql = @"
        INSERT INTO Citas (
            Matricula, Marca, Modelo, Cilindrada, Motor, 
            DniPropietario, FechaItv, FechaInspeccion, CreatedAt, UpdatedAt, 
            IsDeleted
        ) VALUES (
            @Matricula, @Marca, @Modelo, @Cilindrada, @Motor, 
            @DniPropietario, @FechaItv, @FechaInspeccion, @CreatedAt, @UpdatedAt, 
            @IsDeleted
        );
        SELECT last_insert_rowid();";

    using var command = connection.CreateCommand();
    command.CommandText = sql;

    command.Parameters.AddWithValue("@Matricula", matricula);
    command.Parameters.AddWithValue("@Marca", model.Marca ?? "");
    command.Parameters.AddWithValue("@Modelo", model.Modelo ?? "");
    command.Parameters.AddWithValue("@Cilindrada", model.Cilindrada);
    command.Parameters.AddWithValue("@Motor", (int)model.Motor);
    command.Parameters.AddWithValue("@DniPropietario", dni);
    command.Parameters.AddWithValue("@FechaItv", model.FechaItv.ToString("yyyy-MM-dd HH:mm:ss"));
    command.Parameters.AddWithValue("@FechaInspeccion", model.FechaInspeccion.ToString("yyyy-MM-dd HH:mm:ss"));
    command.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    command.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    command.Parameters.AddWithValue("@IsDeleted", 0);

    try {
        var id = Convert.ToInt32(command.ExecuteScalar());
        var creado = GetById(id);
        return creado != null 
            ? Result.Success<Cita, DomainError>(creado)
            : Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
    }
    catch (Exception ex) {
        _logger.Error(ex, "Error al crear cita en ADO");
        return Result.Failure<Cita, DomainError>(CitaErrors.DatabaseError(ex.Message));
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
    
    
    private bool ExisteLimiteDniDia(string dni, DateTime fecha) {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT COUNT(*) FROM Citas 
        WHERE DniPropietario = @Dni 
          AND date(FechaItv) = date(@Fecha) 
          AND IsDeleted = 0";
        command.Parameters.AddWithValue("@Dni", dni);
        command.Parameters.AddWithValue("@Fecha", fecha.ToString("yyyy-MM-dd"));
        return Convert.ToInt32(command.ExecuteScalar()) >= 3;
    }
    
    public int CountCitasFiltradas(string? matricula, DateTime inicio, DateTime? fin, bool isDeleteInclude, string? motor = null) 
    {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();

        command.CommandText = @"
        SELECT COUNT(*) FROM Citas 
        WHERE IsDeleted = @IncludeDeleted
          AND date(FechaInspeccion) >= date(@Inicio)
          AND (@Fin IS NULL OR date(FechaInspeccion) <= date(@Fin))
          AND (@Matricula IS NULL OR Matricula LIKE @MatriculaLike)
          AND (@Motor IS NULL OR @Motor = '' OR Motor = @MotorValue)";

        command.Parameters.AddWithValue("@Inicio", inicio.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@Fin", (object?)fin?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("@IncludeDeleted", isDeleteInclude ? 1 : 0);
        command.Parameters.AddWithValue("@Matricula", (object?)matricula ?? DBNull.Value);
        command.Parameters.AddWithValue("@MatriculaLike", string.IsNullOrWhiteSpace(matricula) ? DBNull.Value : $"%{matricula}%");

    if (!string.IsNullOrWhiteSpace(motor) && !motor.Equals("TODOS", StringComparison.OrdinalIgnoreCase))
    {
        var motorBusqueda = motor
            .Replace("DIÉSEL", "Diesel")
            .Replace("HÍBRIDO", "Hibrido")
            .Replace("ELÉCTRICO", "Electrico")
            .Trim();
        if (Enum.TryParse<Motor>(motorBusqueda, true, out var motorEnum))
        {
            command.Parameters.AddWithValue("@Motor", 1);
            command.Parameters.AddWithValue("@MotorValue", (int)motorEnum);
        }
        else
        {
            command.Parameters.AddWithValue("@Motor", DBNull.Value);
            command.Parameters.AddWithValue("@MotorValue", DBNull.Value);
        }
    }
    else
    {
        command.Parameters.AddWithValue("@Motor", DBNull.Value);
        command.Parameters.AddWithValue("@MotorValue", DBNull.Value);
    }

    return Convert.ToInt32(command.ExecuteScalar());
    }
}