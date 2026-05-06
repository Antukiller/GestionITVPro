using System.ComponentModel;
using System.Runtime.InteropServices.JavaScript;
using CommunityToolkit.Mvvm.ComponentModel;
using GestionITVPro.Enums;
using GestionITVPro.Validator;

namespace GestionITVPro.WPF.ViewModels.Form;

/// <summary>
/// FormData para la verificación de CITA en la capa de presentación.
/// Sepata la lógica de validación UI del modelo de dominio puro.
/// IMPORTANTE: Este es el único lugar donde se implementaa IDataErrorInfo para cita.
/// </summary>
public partial class CitaFormData : ObservableObject, IDataErrorInfo {

    [ObservableProperty] private string _matricula = string.Empty;

    [ObservableProperty] private string _marca = string.Empty;

    [ObservableProperty] private string _modelo = string.Empty;

    [ObservableProperty] private int _cilindrada;

    [ObservableProperty] private int id;

    [ObservableProperty] private Motor _motor;
    
    [ObservableProperty] private string _dniPropietario = string.Empty;

    [ObservableProperty] private DateTime _fechaItv = DateTime.UtcNow;

    [ObservableProperty] private DateTime _fechaInspeccion = DateTime.Today.AddDays(+30);
    
    /// <summary>
    /// Marca de tiempo de creación del registro
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Marca de tiempo de la última actualuzación del resgistro.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Indica si el registro está marcado como eliminado (soft delete).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Marca de tiempo de eliminación lógica, si aplica.</summary>
    public DateTime? DeletedAt { get; set; }
    
    /// <summary>Resumen de errores globales del formulario. Requerido por IDataErrorInfo.</summary>
    public string Error { get; }

    /// <summary>
    ///     Validación campo por campo requerida por IDataErrorInfo para el binding WPF con ValidatesOnDataErrors=True.
    /// </summary>
    /// <param name="columnName">Nombre de la propiedad a validar.</param>
    /// <returns>Mensaje de error en español si el campo es inválido; cadena vacía o null si es válido.</returns>
    public string this[string columnName] => columnName switch {
        nameof(Matricula) when !Matricula.IsValidMatricula()
            => "La matrícula es obligatoria y no puede estar vacía",

        nameof(Marca) when !Marca.IsValidMarca()
            => "La marca es obligatoria (2-50 caracteres)",

        nameof(Modelo) when !Modelo.IsValidModelo()
            => "El modelo es obligatorio (2-50 caracteres)",

        nameof(Cilindrada) when !Cilindrada.IsValidCilindrada()
            => "La cilindrada debe de estar entre 0 y 3000",

        nameof(FechaInspeccion) when !FechaInspeccion.IsWithinNext30Days()
            => "La inspeccion debe estar entre hoy y los próximos 30 dias",

        nameof(FechaItv) when !FechaItv.IsValidFechaCita()
            => "La fecha de matriculación no puede ser furtura",
        
        nameof(DniPropietario) when !DniPropietario.IsValidDniPropietario()
            => "El dni del propietario es obligatorio",
        
        

        nameof(DniPropietario) when !DniPropietario.IsValidDniPropietario()
            => "EL dni del propietario no puede estar vacío y es obligatio",

        _ => null!

    };


    /// <summary>
    ///     Verifica que todos los campos del formulario sean válidos antes de persistir.
    /// </summary>
    /// <returns>True si el formulario no tiene errores de validación.</returns>
    public bool IsValid() {
        return string.IsNullOrEmpty(this[nameof(Matricula)]) &&
               string.IsNullOrEmpty(this[nameof(Marca)]) &&
               string.IsNullOrEmpty(this[nameof(Modelo)]) &&
               string.IsNullOrEmpty(this[nameof(Cilindrada)]) &&
               string.IsNullOrEmpty(this[nameof(FechaInspeccion)]) &&
               string.IsNullOrEmpty(this[nameof(FechaItv)]) &&
               string.IsNullOrEmpty(this[nameof(DniPropietario)]);
    }

    /// <summary>
    ///     Devuelve una cadena con todos los errores de validación actuales, uno por línea.
    /// </summary>
    /// <returns>Texto con los errores de validación, o cadena vacía si el formulario es válido.</returns>
    public string GetValidationErrors() {
        var campos = new[] {
            (nameof(Matricula), "Matricula"),
            (nameof(Marca), "Marca"),
            (nameof(Modelo), "Modelo"),
            (nameof(Cilindrada), "Cilindrada"),
            (nameof(FechaInspeccion), "Fecha de Inspección"),
            (nameof(FechaItv), "Fecha de Matriculación"),
            (nameof(DniPropietario), "DNI del Propietario")

        };

        var errores = campos
            .Select(c => (Campo: c.Item2, Error: this[c.Item1]))
            .Where(c => !string.IsNullOrWhiteSpace(c.Error))
            .Select(c => $"• {c.Campo}: {c.Error}");

        return string.Join("\n", errores);
    }
}