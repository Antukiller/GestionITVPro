using GestionITVPro.Error.Common;

namespace GestionITVPro.Error.Storage;

/// <summary>
/// Contenedor de errores específicos para el subdominio de Almacenamiento (Storage).
/// </summary>
public abstract record StrorageError(string Message) : DomainError(Message) {
    public sealed record FileNotFound(string FilePath)
        : StrorageError($"No se ha encontrado el archivo en la ruta: {FilePath}");

    public sealed record InvalidFormat(string Details)
        : StrorageError($"El formatp del archivo es inválido o incompatible: {Details}");

    public sealed record WriteErrors(string Details)
        : StrorageError($"Error al escribir en el almacenamiento: {Details}");

    public sealed record ReadErrors(string Details)
        : StrorageError($"Error al leer del almacenamiento: {Details}");

    public sealed record AccessErrors(string Details)
        : StrorageError($"Error de acceso al almacenamiento: {Details}");
}


public static class StorageErrors {
    public static DomainError FileNotFound(string filePath) {
        return new StrorageError.FileNotFound(filePath);
    }

    public static DomainError InvalidFormat(string details) {
        return new StrorageError.InvalidFormat(details);
    }

    public static DomainError WriteError(string details) {
        return new StrorageError.WriteErrors(details);
    }

    public static DomainError ReadError(string details) {
        return new StrorageError.ReadErrors(details);
    }

    public static DomainError AccessError(string details) {
        return new StrorageError.AccessErrors(details);
    }
}