using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionITVPro.Entity;

/// <summary>
/// Entidad de base de datos para Vehiculos.
/// </summary>

[Table("Vehiculo")]
public class VehiculoEntity {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required] [MaxLength(7)] public string Matricula { get; set; } = string.Empty;

    [Required] [MaxLength(50)] public string Modelo { get; set; } = string.Empty;

    [Required] [MaxLength(50)] public string Marca { get; set; } = string.Empty;
    
    [Required] public int Cilindrada { get; set; }
    
    [Required] public int Motor { get; set; }

    [Required] [MaxLength(50)] public string DniPropietario { get; set; } = string.Empty;

    [Column(TypeName = "datetime2")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "datetime2")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsDeleted { get; set; }
    
    [Column(TypeName = "datetime2")] public DateTime? DeletedAt { get; set; }

}