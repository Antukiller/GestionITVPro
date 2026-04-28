using GestionITVPro.Errors.Common;

namespace GestionITVPro.Error.Cita;


/// <summary>
/// Contenedor de errores específicos para el dominio de Vehículos.
/// </summary>

public abstract record CitaError(string Message) : DomainError(Message) {
    public sealed record NotFound(string Id)
        // Antes decía 'persona', ahora 'cita' o 'identificador' para ser precisos
        : CitaError($"No se encontró la cita con ID {Id}");

    public sealed record Validation(IEnumerable<string> Errors)
        : CitaError(
            $"Se han detectado errores: {string.Join(", ", Errors)}. alcanzado el límite de 3 vehículos.");

    public sealed record MatriculaAlreadyExists(string Matricula)
        // Cambiamos el orden: "programada una cita" en lugar de "una cita programada"
        : CitaError($"La matrícula {Matricula} ya tiene programada una cita para esa fecha.");

    public sealed record DniPropiestarioAlreadyExists(string DniPropietario)
        : CitaError(
            $"Conflicto de integridad: El DNI del propietario {DniPropietario} ya está registrado en el sistema.");

    public sealed record Database(string Details)
        : CitaError($"Error de base de datos: {Details}");

    public sealed record StorageError(string Details)
        : CitaError($"Error de almacenamiento: {Details}");
}

/// <summary>
/// Factory para crear errores de dominio de Vehículo.
/// </summary>
public static class CitaErrors {
    public static DomainError NotFound(string id) {
        return new CitaError.NotFound(id);
    }

    public static DomainError Validation(IEnumerable<string> errors) {
        return new CitaError.Validation(errors);
    }

    public static DomainError MatriculaAlreadyExists(string matricula) {
        return new CitaError.MatriculaAlreadyExists(matricula);
    }

    public static DomainError DniPropiestarioAlreadyExists(string dniPropietario) {
        return new CitaError.DniPropiestarioAlreadyExists(dniPropietario);
    }

    public static DomainError DatabaseError(string details) {
        return new CitaError.Database(details);
    }

    public static DomainError StorageError(string details) {
        return new CitaError.StorageError(details);
    }
}

