using GestionITVPro.Entity;

namespace GestionITVPro.Mapper;

using System.Globalization;
using GestionITVPro.Dto;
using GestionITVPro.Enums;
using Models;


public static class CitaMapper {
    // Formato fecha con hora para CreateAt, UpdateAt y DeleteAt.
    private const string DateFormat = "d";
    private const string DateTimeFormat = "s";
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;


    public static Cita ToModel(this CitaDto dto) {
        var createdAt = DateTime.Parse(dto.CreatedAt, InvariantCulture);
        var updateAt = DateTime.Parse(dto.UpdatedAt, InvariantCulture);
        DateTime? deleteAt = string.IsNullOrEmpty(dto.DeletedAt)
            ? null
            : DateTime.Parse(dto.DeletedAt, InvariantCulture);
        var fechaItv = DateTime.Parse(dto.FechaItv, InvariantCulture);
        var fechaInspeccion = DateTime.Parse(dto.FechaInspeccion, InvariantCulture);
        return new Cita {
            Id = dto.Id,
            Matricula = dto.Matricula,
            Marca = dto.Marca,
            Modelo = dto.Modelo,
            Cilindrada = dto.Cilindrada,
            Motor = Enum.TryParse(dto.Motor, out Motor tipo) ? tipo : Motor.Gasolina,
            DniPropietario = dto.DniPropietario,
            FechaItv = fechaItv,
            FechaInspeccion = fechaInspeccion,
            CreatedAt = createdAt,
            UpdatedAt = updateAt,
            IsDeleted = dto.IsDeleted,
            DeletedAt = deleteAt
        };
    }
    
    
    
    public static CitaDto ToDto(this Cita cita) {
        // 1. Añadimos el 'new VehiculoDto'
        // 2. Usamos el formato ISO que definiste arriba para las fechas
        return new CitaDto(
            cita.Id,
            cita.Matricula,
            cita.Marca,
            cita.Modelo,
            cita.Cilindrada,
            cita.Motor.ToString(),
            cita.DniPropietario,
            cita.FechaItv.ToString(DateFormat, InvariantCulture),
            cita.FechaInspeccion.ToString(DateFormat, InvariantCulture),
            cita.CreatedAt.ToString(DateTimeFormat, InvariantCulture), // Usa tu constante IsoFormat
            cita.UpdatedAt.ToString(DateTimeFormat, InvariantCulture),
            cita.IsDeleted,
            cita.DeletedAt.HasValue 
                ? cita.DeletedAt.Value.ToString(DateTimeFormat, InvariantCulture) 
                : string.Empty
        );
    }

    public static Cita? ToModel(this CitaEntity? entity) {
        if (entity == null) return null;

        return new Cita {
            Id = entity.Id,
            Matricula = entity.Matricula,
            Marca = entity.Marca,
            Modelo = entity.Modelo,
            Cilindrada = entity.Cilindrada,
            Motor = (Motor)entity.Motor,
            DniPropietario = entity.DniPropietario,
            FechaItv = entity.FechaItv,
            FechaInspeccion = entity.FechaInspeccion,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt
        };
    }
    
    
    /// <summary>
    // Conviente en una lista de entidades a modelos de domonio.
    /// </summary>
    
    public static IEnumerable<Cita> ToModel(this IEnumerable<CitaEntity> entities) {
        return entities.Select(ToModel).OfType<Cita>();
    }


    public static CitaEntity ToEntity(this Cita model) {
        return new CitaEntity {
            Id = model.Id,
            Matricula = model.Matricula,
            Marca = model.Marca,
            Modelo = model.Modelo,
            Cilindrada = model.Cilindrada,
            Motor = (int)model.Motor,
            DniPropietario = model.DniPropietario,
            FechaItv = model.FechaItv,
            FechaInspeccion = model.FechaInspeccion,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            IsDeleted = model.IsDeleted,
            DeletedAt = model.DeletedAt
            
        };
    }
}