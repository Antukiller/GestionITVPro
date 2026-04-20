using GestionITVPro.Entity;

namespace GestionITVPro.Mapper;

using System.Globalization;
using GestionITVPro.Dto;
using GestionITVPro.Enums;
using GestionITVPro.Models;


public static class VehiculoMapper {
    // Formato fecha con hora para CreateAt, UpdateAt y DeleteAt.
    private const string DateTimeFormat = "s";
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;


    public static Vehiculo ToModel(this VehiculoDto dto) {
        var createdAt = DateTime.Parse(dto.CreateAt, InvariantCulture);
        var updateAt = DateTime.Parse(dto.UpdateAt, InvariantCulture);
        DateTime? deleteAt = string.IsNullOrEmpty(dto.DeletedAt)
            ? null
            : DateTime.Parse(dto.DeletedAt, InvariantCulture);

        return new Vehiculo {
            Id = dto.Id,
            Matricula = dto.Matricula,
            Marca = dto.Marca,
            Modelo = dto.Modelo,
            Cilindrada = dto.Cilindrada,
            Motor = Enum.TryParse(dto.Motor, out Motor tipo) ? tipo : Motor.Gasolina,
            DniPropietario = dto.DniPropietario,
            CreatedAt = createdAt,
            UpdatedAt = updateAt,
            IsDeleted = dto.IsDeleted,
            DeletedAt = deleteAt
        };
    }
    
    
    
    public static VehiculoDto ToDto(this Vehiculo vehiculo) {
        // 1. Añadimos el 'new VehiculoDto'
        // 2. Usamos el formato ISO que definiste arriba para las fechas
        return new VehiculoDto(
            vehiculo.Id,
            vehiculo.Matricula,
            vehiculo.Marca,
            vehiculo.Modelo,
            vehiculo.Cilindrada,
            vehiculo.Motor.ToString(),
            vehiculo.DniPropietario,
            vehiculo.CreatedAt.ToString(DateTimeFormat, InvariantCulture), // Usa tu constante IsoFormat
            vehiculo.UpdatedAt.ToString(DateTimeFormat, InvariantCulture),
            vehiculo.IsDeleted,
            vehiculo.DeletedAt?.ToString(DateTimeFormat, InvariantCulture)
        );
    }

    public static Vehiculo? ToModel(this VehiculoEntity? entity) {
        if (entity == null) return null;

        return new Vehiculo {
            Id = entity.Id,
            Matricula = entity.Matricula,
            Marca = entity.Marca,
            Modelo = entity.Modelo,
            Cilindrada = entity.Cilindrada,
            Motor = (Motor)entity.Motor,
            DniPropietario = entity.DniPropietario,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt
        };
    }
    
    
    /// <summary>
    // Conviente en una lista de entidades a modelos de domonio.
    /// </summary>
    
    public static IEnumerable<Vehiculo> ToModel(this IEnumerable<VehiculoEntity> entities) {
        return entities.Select(ToModel).OfType<Vehiculo>();
    }


    public static VehiculoEntity ToEntity(this Vehiculo model) {
        return new VehiculoEntity {
            Id = model.Id,
            Matricula = model.Matricula,
            Marca = model.Marca,
            Modelo = model.Modelo,
            Cilindrada = model.Cilindrada,
            Motor = (int)model.Motor,
            DniPropietario = model.DniPropietario,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            IsDeleted = model.IsDeleted,
            DeletedAt = model.DeletedAt

        };
    }
}