using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using GestionITVPro.Enums;
using GestionITVPro.Error.Cita;
using GestionITVPro.Errors.Common;
using GestionITVPro.Models;
using GestionITVPro.Validator.Common;

namespace GestionITVPro.Validator;

public static class ValidadorVehiculoExtensions {
    public static bool IsValidModelo(this string modelo) {
        return !string.IsNullOrWhiteSpace(modelo) && modelo.Trim().Length >= 2 && modelo.Trim().Length <= 50;
    }

    public static bool IsValidMarca(this string marca) {
        return !string.IsNullOrWhiteSpace(marca) && marca.Trim().Trim().Length >= 2 && marca.Trim().Length <= 50;
    }

    public static bool IsValidCilindrada(this int cilindrada) {
        return cilindrada >= 0 && cilindrada <= 3000;
    }
    

    public static bool IsValidMotor(this Motor motor) => motor switch {
        Motor.Diesel or Motor.Electrico or Motor.Gasolina or Motor.Hibrido => true,
        _ => false // Cualquier otra cosa (o valor nulo/extraño) es falso
    };
    
    public static bool IsValidFechaCita(this DateTime fecha) {
        // La cita no puede ser anterior a "hoy" (comparando solo la fecha sin hora)
        // O si permites citas para el mismo día, que no sea anterior a DateTime.Today
        return fecha.Date >= DateTime.Today;
    }

    public static bool IsWithinNext30Days(this DateTime fechaInspeccion) {
        var hoy = DateTime.Today;
        var limite = hoy.AddDays(30);

        return fechaInspeccion.Date >= hoy && fechaInspeccion.Date <= limite;
    }
    
    // NUEVO - PARA LA MATRICULACIÓN: Debe ser hoy o en el PASADO
    public static bool IsValidFechaPasada(this DateTime fecha) {
        return fecha.Date <= DateTime.Today; 
    }
    
    /*
    public static bool IsValidMotor2(this Vehiculo Entity) {
        return !Enum.IsDefined(typeof(Motor), Entity.Motor);
    }*/

    public static bool IsValidDniPropietario(this string dniPropietario) {
        if (string.IsNullOrWhiteSpace(dniPropietario)) return false;

        dniPropietario = dniPropietario.Trim().ToUpper().Replace(" ", "").Replace("-", "");
        
        //if (dniPropietario.Length != 9) return false;

        if (!Regex.IsMatch(dniPropietario, @"^[0-9]{8}[A-Z]$")) return false;

        var numero = dniPropietario.Substring(0, 8);
        var letra = dniPropietario[8];
        
        const string letrasValidas = "TRWAGMYFPDXBNJZSQVHLCKE";
        if (!int.TryParse(numero, out var dniNumerico)) return false;

        var resto = dniNumerico % 23;
        var letraClaculada = letrasValidas[resto];

        return letra == letraClaculada;
    }
    

    public static bool IsValidMatricula(this string matricula) 
    {
        // 1. Limpieza básica
        if (string.IsNullOrWhiteSpace(matricula)) return false;

        // Quitamos espacios y guiones, y pasamos a mayúsculas
        matricula = matricula.Trim().ToUpper().Replace(" ", "").Replace("-", "");

        // 2. Comprobación de formato con Regex (4 números y 3 letras)
        // El formato estándar actual es: ^[0-9]{4}[A-Z]{3}$
        if (!Regex.IsMatch(matricula, @"^[0-9]{4}[B-DF-HJ-NP-TV-Z]{3}$")) return false;

        // 3. Validación de letras prohibidas (Regla de la DGT)
        // Las matrículas españolas NO usan vocales (A, E, I, O, U) 
        // para evitar palabras malsonantes ni las letras Ñ o Q por confusión.
    
        var letras = matricula.Substring(4, 3);
        const string letrasProhibidas = "AEIOUÑQ";

        foreach (char c in letras)
        {
            if (letrasProhibidas.Contains(c)) return false;
        }

        return true;
    }
    
}

public class ValidadorCita : IValidador<Cita> {
    public Result<Cita, DomainError> Validar(Cita v) {
        var errores = new List<string>();
        
        if (!v.Modelo.IsValidModelo())
            errores.Add("El modelo es obliogatorio y no puede estar vacío(2-50 car.)");
        
        if (!v.Marca.IsValidMarca())
            errores.Add("La marca es obligatorio y no puede estar vacía(2-50 car.)");
        
        if (!v.Cilindrada.IsValidCilindrada())
            errores.Add("La cilindrada debe de estar entre 0 y 3000");
        
        if (!v.Motor.IsValidMotor())
            errores.Add("El motor debe ser acorde a la base de datos.");
        
        if (!v.DniPropietario.IsValidDniPropietario())
            errores.Add("El DNI no es válido (8 números y letra correcta)");
        
        if (v.FechaItv.Date > DateTime.Today)
            errores.Add("La fecha de matriculación no puede ser futura.");

        // 2. Para la Fecha de la Cita (FechaInspeccion):
        // El enunciado dice: Entre hoy y +30 días.
        if (!v.FechaInspeccion.IsWithinNext30Days())
            errores.Add("La fecha de inspección debe estar entre hoy y los próximos 30 días.");
        
        if (!v.Matricula.IsValidMatricula())
            errores.Add("La matrícula no es válida (4 números-3 letras)");
        
        if (errores.Any()) {
            return Result.Failure<Cita, DomainError>(new CitaError.Validation(errores));
        }

        return Result.Success<Cita, DomainError>(v);

    }
}