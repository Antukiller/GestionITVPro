using GestionITVPro.Error.Common;

namespace GestionITVPro.Error.Vehiculo;


/// <summary>
/// Contenedor de errores específicos para el dominio de Vehículos.
/// </summary>

public abstract record VehiculoError(string Message) : DomainError(Message) {
    public sealed record NotFound(string Id)
        : VehiculoError($"No se ha encontrado ninguna persona con el idetificador: {Id}");

    public sealed record Validation(IEnumerable<string> Errors)
        : VehiculoError(
            $"Se han detectado errores de validdación en la entidad: {Environment.NewLine} {string.Join($"{Environment.NewLine}", Errors)}");

    public sealed record MatriculaAlreadyExists(string Matricula)
        : VehiculoError($"Conflicto de integridad: La matrícula {Matricula} ya está registrado en el sistema.");

    public sealed record DniPropiestarioAlreadyExists(string DniPropietario)
        : VehiculoError(
            $"Conflicto de integridad: El DNI del propietario {DniPropietario} ya está registrado en el sistema.");

    public sealed record Database(string Details)
        : VehiculoError($"Error de base de datos: {Details}");

    public sealed record StorageError(string Details)
        : VehiculoError($"Error de almacenamiento: {Details}");
}

/// <summary>
/// Factory para crear errores de dominio de Vehículo.
/// </summary>
public static class VehiculoErrors {
    public static DomainError NotFound(string id) {
        return new VehiculoError.NotFound(id);
    }

    public static DomainError Validation(IEnumerable<string> errors) {
        return new VehiculoError.Validation(errors);
    }

    public static DomainError MatriculaAlreadyExists(string matricula) {
        return new VehiculoError.MatriculaAlreadyExists(matricula);
    }

    public static DomainError DniPropiestarioAlreadyExists(string dniPropietario) {
        return new VehiculoError.DniPropiestarioAlreadyExists(dniPropietario);
    }

    public static DomainError DatabaseError(string details) {
        return new VehiculoError.Database(details);
    }

    public static DomainError StorageError(string details) {
        return new VehiculoError.StorageError(details);
    }
}

