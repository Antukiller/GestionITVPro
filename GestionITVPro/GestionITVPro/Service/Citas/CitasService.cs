
using CSharpFunctionalExtensions;
using GestionITVPro.Cache;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using GestionITVPro.Validator.Common;
using Serilog;

namespace GestionITVPro.Service.Citas;

public class CitasService(
    ICitaRepository repository,
    IValidador<Cita> valCita,
    ICache<int, Cita> cache
) : ICitasService {
    private readonly ILogger _logger = Log.ForContext<CitasService>();


    public int TotalCitas => repository.GetAll(1, int.MaxValue).Count();
    
    public IEnumerable<Cita> GetAll(int page = 1, int pageSize = 10, bool includeDeleted = true) {
        return repository.GetAll(page, pageSize, includeDeleted);
    }

    public IEnumerable<Cita> GetCitasOrderBy(TipoOrdenamiento ordenamiento, int page = 1, int pageSize = 10, bool includeDeleted = true) 
    {
        // 1. Obtenemos todas las citas del repositorio (sin paginar aún)
        // Usamos int.MaxValue para traer todas y poder ordenar el conjunto completo
        var citas = repository.GetAll(1, int.MaxValue, includeDeleted);

        // 2. Aplicamos el ordenamiento según el Enum
        var listaOrdenada = AplicarOrdenamientoCitas(citas, ordenamiento);

        // 3. Ahora aplicamos la paginación sobre la lista ya ordenada
        return listaOrdenada
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }


    public Result<Cita, DomainError> GetById(int id) {
        if (cache.Get(id) is { } cached)
            return Result.Success<Cita, DomainError>(cached);

        if (repository.GetById(id) is { } cita) {
            cache.Add(id, cita);
            return Result.Success<Cita, DomainError>(cita);
        }

        return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
    }

    public Result<Cita, DomainError> GetByMatricula(string matricula) {
        if (repository.GetByMatricula(matricula) is { } cita)
            return Result.Success<Cita, DomainError>(cita);

        return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(matricula));
    }

    public Result<Cita, DomainError> GetByDniPropietario(string dni) {
        if (repository.GetByDniPropietario(dni) is { } cita)
            return Result.Success<Cita, DomainError>(cita);

        return Result.Failure<Cita, DomainError>(CitaErrors.NotFound(dni));
    }

    public Result<Cita, DomainError> Save(Cita cita) {
        return ValidarCita(cita)
            .Ensure(c => !repository.ExistsMatricula(c.Matricula ?? ""),
                c => CitaErrors.MatriculaAlreadyExists(c.Matricula ?? ""))
            .Ensure(c => !repository.ExistsDniPropietario(c.DniPropietario ?? ""),
                c => CitaErrors.DniPropiestarioAlreadyExists(c.DniPropietario))
            .Bind(c => repository.Create(c));
    }

    public Result<Cita, DomainError> Update(int id, Cita cita) {
        return CheckExists(id)
            .Bind(_ => ValidarCita(cita))
            .Ensure(c => IsMatriculaValidForUpdate(id, c.Matricula),
                c => CitaErrors.MatriculaAlreadyExists(c.Matricula ?? ""))
            .Ensure(c => IsDniPropietarioValidForUpdate(id, c.DniPropietario),
                c => CitaErrors.DniPropiestarioAlreadyExists(c.DniPropietario ?? ""))
            .Tap(_ => cache.Remove(id))
            .Bind(c => repository.Update(id, c));
    }

    public Result<Cita, DomainError> Delete(int id, bool isLogical = true) {
        return CheckExists(id)
            .Tap(_ => cache.Remove(id)) // Solo una vez es suficiente
            .Map(cita => {
                repository.Delete(id, isLogical);
                return cita; 
            }); // <-- Asegúrate de que este punto y coma esté aquí
    }

    public bool DeleteAll() {
        _logger.Warning("Eliminando todas las citas del sistema");
        return repository.DeleteAll();
    }

    public Result<Cita, DomainError> Restore(int id) {
        _logger.Information("Restaurando cita con ID {Id}", id);
        return repository.Restore(id);
    }

    public int CountCitas(bool includeDeleted = false) {
        return repository.CountCita(includeDeleted);
    }
    
    
    // Funciones Auxiliares


    private IEnumerable<Cita> AplicarOrdenamientoCitas(IEnumerable<Cita> lista,
        TipoOrdenamiento orden) {
        return orden switch {
            TipoOrdenamiento.Matricula => lista.OrderBy(c => c.Matricula),
            TipoOrdenamiento.DniPropietario => lista.OrderBy(c => c.DniPropietario),
            TipoOrdenamiento.Marca => lista.OrderBy(c => c.Marca),
            TipoOrdenamiento.Modelo => lista.OrderBy(c => c.Modelo),
            TipoOrdenamiento.FechaItv => lista.OrderBy(c => c.FechaItv),
            TipoOrdenamiento.Cilindrada => lista.OrderBy(c => c.Cilindrada),
            _ => lista.OrderBy(c => c.Id)
        };
    }


    private bool IsMatriculaValidForUpdate(int id, string matricula) {
        var c = repository.GetByMatricula(matricula);
        return c == null || c.Id == id;
    }

    private bool IsDniPropietarioValidForUpdate(int id, string dni) {
        var c = repository.GetByDniPropietario(dni);
        return c == null || c.Id == id;
    }
    
    
    
    private Result<Cita, DomainError> ValidarCita(Cita cita) {
        _logger.Debug("Validando cita");
        var validation = valCita.Validar(cita);
        
        return validation.IsFailure 
            ? Result.Failure<Cita, DomainError>(validation.Error) 
            : Result.Success<Cita, DomainError>(cita);
    }
    
    private Result<Cita, DomainError> CheckExists(int id) {
        return repository.GetById(id) is { } cita
            ? Result.Success<Cita, DomainError>(cita)
            : Result.Failure<Cita, DomainError>(CitaErrors.NotFound(id.ToString()));
    }
}