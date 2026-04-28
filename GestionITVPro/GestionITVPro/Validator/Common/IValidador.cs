using CSharpFunctionalExtensions;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;

namespace GestionITVPro.Validator.Common;


/// <summary>
/// Contrato para validar entidades del dominio
/// </summary>
/// <typeparam name="T">Tipo </typeparam>
public interface IValidador<T> {
    /// <summary>
    /// Valida una entidad según las reglas de dominio.
    /// </summary>
    /// <param name="entidad">Entidad a validadr</param>
    /// <returns>Result con la entidad validad o error <see cref="Cita.VehiculoError.Validation(string)"/>si no es válido</returns>
    Result<T, DomainError> Validar(T entidad);
}