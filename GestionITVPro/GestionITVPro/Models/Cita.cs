using GestionITVPro.Enums;

namespace GestionITVPro.Models;

/// <summary>
/// Clase base inmutable para cualquier vehículo resgistrado en el sistema.
/// </summary>
public record Vehiculo {
    
    public int Id {get; init;}
    public string Matricula { get; init; } = string.Empty;
    public string Marca { get; init; }  = string.Empty;
    public string Modelo { get; init; }  = string.Empty;
    public int Cilindrada { get; init; } 
    public Motor Motor { get; init; } 
    public string DniPropietario { get; init; }  = string.Empty;

    public DateTime FechaCita { get; init; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public bool IsDeleted { get; init; } = false;
    public DateTime? DeletedAt { get; init; } = null;
    
    
    /// <summary>
    /// Retorna una descripción formatrada para la visualización
    /// </summary>
    public string Descripcion => $"{Matricula}, {Marca}, {Modelo}, {Motor.ToString()}";
    
    
    /// <summary>
    /// Determina si dos vehículos son idénticos comparando sus matrículas de forma insensible a mayúsculas.
    /// </summary>
    /// <param name="other">Instancia de vehículo a comparar</param>
    /// <returns>True si las matrículas coinciden</returns>
    public virtual bool Equals(Vehiculo? other) {
        return other != null && string.Equals(Matricula,  other.Matricula, StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Calcula el código hash basado exclusicamente en la matrícula para mantener coherencia con la igualdad.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
        return HashCode.Combine(Matricula.ToLowerInvariant());
    }
}