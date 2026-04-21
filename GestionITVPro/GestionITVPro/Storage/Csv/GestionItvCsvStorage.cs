using CSharpFunctionalExtensions;
using GestionITVPro.Config;
using GestionITVPro.Error.Common;
using GestionITVPro.Models;
using Serilog;

namespace GestionITVPro.Storage.Csv;

public class GestionItvCsv : IGestionItvCsvStorage {

    private readonly ILogger _logger = Log.ForContext<GestionItvCsv>();
    
    
    public GestionItvCsv() : this(AppConfig.DataFolder) { }
    public Result<bool, DomainError> Salvar(IEnumerable<Vehiculo> items, string path) {
        throw new NotImplementedException();
    }

    public Result<IEnumerable<Vehiculo>, DomainError> Cargar(string dataFolder) {
        throw new NotImplementedException();
    }
}