using CSharpFunctionalExtensions;
using GestionITVPro.Enums;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;

namespace GestionITVPro.Service.Citas;


/// <summary>
///     Define el contrato para la gestión de citas en el sistema.
/// </summary>
public interface ICitasService {
    int TotalCitas { get; }
    
    // 1. GetAll evolucionado con filtros de búsqueda
    IEnumerable<Cita> GetAll(
        string? marca = null, 
        string? dniPropietario = null, 
        string? matricula = null, 
        DateTime? desde = null, 
        DateTime? hasta = null,
        int page = 1, 
        int pageSize = 10, 
        bool includeDeleted = true);
    
    IEnumerable<Cita> GetCitasOrderBy(
        TipoOrdenamiento ordenamiento, 
        int page = 1, 
        int pageSize = 10, 
        bool includeDeleted = true);

    Result<Cita, DomainError> GetById(int id);

    Result<Cita, DomainError> GetByMatricula(string matricula);

    Result<Cita, DomainError> GetByDniPropietario(string dni);

    Result<Cita, DomainError> Save(Cita cita);

    Result<Cita, DomainError> Update(int id, Cita cita);

    Result<Cita, DomainError> Delete(int id, bool isLogical = true);

    bool DeleteAll();

    Result<Cita, DomainError> Restore(int id);

    int CountCitas(bool includeDeleted = false);




}