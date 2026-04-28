using CSharpFunctionalExtensions;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;
using GestionITVPro.Storage.Common;
using Serilog;


namespace GestionITVPro.Service.ImportExport;

public class ImportExportService(
    IStorage<Cita> storage
) : IImportExportService {
    private readonly ILogger _logger = Log.ForContext<ImportExportService>();


    public Result<int, DomainError> ExportarDatos(IEnumerable<Cita> citas, string path) {
        _logger.Information("Exportando datps a {Path}", path);
        var list = citas.ToList();
        return storage.Salvar(list, path)
            .Map(_ => list.Count);
    }

    public Result<IEnumerable<Cita>, DomainError> ImportarDatos(string path) {
        return storage.Cargar(path);
    }

    public Result<int, DomainError> ExportarDatosSistema(IEnumerable<Cita> citas) {
        return ExportarDatos(citas, string.Empty);
    }

    public Result<IEnumerable<Cita>, DomainError> ImportarDatosSistema(string path) {
        return ImportarDatos(path);
    }
}