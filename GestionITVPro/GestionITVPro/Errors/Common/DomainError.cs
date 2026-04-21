namespace GestionITVPro.Error.Common;


/// <summary>
/// Clase base abstracta para todos los errores del dominio académico.
/// </summary>
public abstract record DomainError(string Message);