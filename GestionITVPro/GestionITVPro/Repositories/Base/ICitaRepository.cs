using CSharpFunctionalExtensions;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;

namespace GestionITVPro.Repositories.Base;

/// <summary>
///     Contrato especializado para la gestión de vehiculos.
///     Define las operaciones de búsqueda, persistencia y validación de identidad.
///     KISS Principle: Solo Create y Update usan Result porque son las únicas operaciones
///     que tienen restricciones de dominio (Matricula única). Las demás operaciones
///     (GetById, Delete) usan null/bool que es más simple e idiomático en .NET.
/// </summary>
public interface ICitaRepository {
    /// <summary>
    ///     Obtiene todos los vehiculos de forma paginada.
    /// </summary>
    IEnumerable<Cita> GetAll( int pagina, int tamPagina, bool isDeleteInclude, string campoBusqueda);
    

    /// <summary>
    ///     Obtiene un vehiculo por su ID.
    /// </summary>
    Cita? GetById(int id);
    
    Result<IEnumerable<Cita>, DomainError> GetByDateMatricula(DateTime inicio, 
        DateTime? fin, 
        int pagina, 
        int tamPagina, 
        string searchText = null, 
        string motorSeleccionado = "TODOS", 
        bool isDeleteInclude = false);

    /// <summary>
    ///     Crea una nuevo vehiculo en el sistema.
    /// </summary>
    /// <returns>Result con el vehiculo cread o error de dominio.</returns>
    Result<Cita, DomainError> Create(Cita persona);

    /// <summary>
    ///     Actualiza una vehiculo existente.
    /// </summary>
    /// <returns>Result con la vehiculo actualizada o error de dominio.</returns>
    Result<Cita, DomainError> Update(int id, Cita cita);

    /// <summary>
    ///     Elimina una vehiculo.
    /// </summary>
    Cita? Delete(int id, bool isLogical = true);

    /// <summary>
    ///     Realiza una búsqueda por el Documento Nacional de Identidad.
    /// </summary>
    Cita? GetByMatricula(string matricula);

    bool ExistsMatricula(string matricula);

    Cita? GetByDniPropietario(string dniPropietario);

    bool ExistsDniPropietario(string dniPropietario);
    
    /// <summary>
    ///     Elimina todos los vehiculos del sistema.
    /// </summary>
    bool DeleteAll();

    /// <summary>
    ///     Obtiene el número total de vehiculos registrados.
    /// </summary>
    int CountCita(bool includeDeleted = false);

    int CountCitasFiltradas(string? matricula, DateTime inicio, DateTime? fin, bool incluirEliminados);
    
    /// <summary>
    ///     Restaura un vehiculo eliminado lógicamente (IsDeleted = false, DeletedAt = null).
    /// </summary>
    Result<Cita, DomainError> Restore(int id);
}