using System.Data;
using CSharpFunctionalExtensions;
using GestionITVPro.Entity;
using GestionITVPro.Error.Common;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using Serilog;

namespace GestionITVPro.Storage.Dapper;

public class VehiculoDapperRepository : IVehiculoRepository {
    private readonly IDbConnection _connection;
    private readonly ILogger _logger = Log.ForContext<VehiculoDapperRepository>();
    private Action? _onDispose;


    private VehiculoDapperRepository(IDbConnection connection, Action? onDispose = null, bool dropData = false,
        bool seeData = false) {
        _connection = connection;
        _onDispose = _onDispose;
        EnsureTable(dropData);

        if (seeData && CountTotal() == 0) Seed();
    }

    public IEnumerable<Vehiculo> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        try {
            var sql = includeDeleted
                ? "SELECT * FROM Vehiculos ORDER BY Id LIMIT @PageSize OFFSET"
                : "SELECT * FROM Vehiculos WHERE IsDeleted = 0 ORDER BY Id LIMIT @PageSize OFFSET";
            var entities = _connection
                .Query<VehiculoEntity>(sql, { PageSize = pageSize, Offset = (page - 1) * pageSize }).ToList();
        }
        catch (Exception e) {
            Console.WriteLine(e);
            throw;
        }
    }

    public Vehiculo? GetById(int id) {
        throw new NotImplementedException();
    }

    public Result<Vehiculo, DomainError> Create(Vehiculo persona) {
        throw new NotImplementedException();
    }

    public Result<Vehiculo, DomainError> Update(int id, Vehiculo vehiculo) {
        throw new NotImplementedException();
    }

    public Vehiculo? Delete(int id, bool isLogical = true) {
        throw new NotImplementedException();
    }

    public Vehiculo? GetByMatricula(string matricula) {
        throw new NotImplementedException();
    }

    public bool ExistsMatricula(string matricula) {
        throw new NotImplementedException();
    }

    public Vehiculo? GetByDniPropietario(string dniPropietario) {
        throw new NotImplementedException();
    }

    public bool ExistsDniPropietario(string dniPropietario) {
        throw new NotImplementedException();
    }

    public bool DeleteAll() {
        throw new NotImplementedException();
    }

    public int CountVehiculos(bool includeDeleted = false) {
        throw new NotImplementedException();
    }

    public Result<Vehiculo, DomainError> Restore(int id) {
        throw new NotImplementedException();
    }

    private void EnsureTabla(bool dropData) {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        if (dropData) _connection.Execute("DROP TABLE IF EXISTS Vehiculos");
        
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Vehiculos (
                Id INTERGER PRIMARY KEY,
                Matricula TEXT NOT NULL UNIQUE,
                
            )")
    }
}

