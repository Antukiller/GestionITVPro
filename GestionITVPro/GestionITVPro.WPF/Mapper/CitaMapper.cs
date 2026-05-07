using GestionITVPro.Models;
using GestionITVPro.WPF.ViewModels.Citas;
using GestionITVPro.WPF.ViewModels.Form;

namespace GestionITVPro.WPF.Mapper;

public static class CitaMapper
{
    // --- De Modelo de Dominio a Formulario (Edición) ---
    public static CitaFormData ToFormData(this Cita model)
    {
        return new CitaFormData
        {
            Id = model.Id,
            DniPropietario = model.DniPropietario,
            Matricula = model.Matricula,
            Marca = model.Marca,
            Modelo = model.Modelo,
            Motor = model.Motor,
            FechaItv = model.FechaItv,
            FechaInspeccion = model.FechaInspeccion,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            IsDeleted = model.IsDeleted,
            DeletedAt = model.DeletedAt
        };
    }

    // --- De Formulario a Modelo de Dominio (Guardar) ---
    public static Cita ToModel(this CitaFormData formData)
    {
        return new Cita
        {
            Id = formData.Id,
            DniPropietario = formData.DniPropietario ?? string.Empty,
            Matricula = formData.Matricula ?? string.Empty,
            Marca = formData.Marca ?? string.Empty,
            Modelo = formData.Modelo ?? string.Empty,
            Cilindrada = formData.Cilindrada,
            Motor = formData.Motor,
            FechaItv = formData.FechaItv,
            FechaInspeccion = formData.FechaInspeccion,
            CreatedAt = formData.CreatedAt,
            UpdatedAt = formData.UpdatedAt,
            IsDeleted = formData.IsDeleted,
            DeletedAt = formData.DeletedAt
        };
    }

    // --- De Modelo de Dominio a Item de Lista (Visualización en DataGrid/ListView) ---
    public static CitaItemViewModel ToItemViewModel(this Cita model)
    {
        return new CitaItemViewModel
        {
            Id = model.Id,
            Matricula = model.Matricula,
            DniPropietario = model.DniPropietario,
            Marca = model.Marca,
            Modelo = model.Modelo,
            FechaItv = model.FechaItv,
            FechaInspeccion = model.FechaInspeccion,
            Motor = model.Motor,
            Cilindrada = model.Cilindrada,
            IsDeleted = model.IsDeleted
        };
    }

    // --- Actualizar un Item de la lista sin recargar todo desde la DB ---
    public static void UpdateFromFormData(this CitaItemViewModel item, CitaFormData form)
    {
        item.Matricula = form.Matricula;
        item.DniPropietario = form.DniPropietario;
        item.Marca = form.Marca;
        item.Modelo = form.Modelo;
        item.FechaInspeccion = form.FechaInspeccion;
        item.Motor = form.Motor;
        item.IsDeleted = form.IsDeleted;
    }

    // --- De Item de Lista a Modelo de Dominio ---
    public static Cita ToModel(this CitaItemViewModel item)
    {
        return new Cita
        {
            Id = item.Id,
            Matricula = item.Matricula,
            DniPropietario = item.DniPropietario,
            Marca = item.Marca,
            Cilindrada = item.Cilindrada,
            Modelo = item.Modelo,
            FechaInspeccion = item.FechaInspeccion,
            FechaItv = item.FechaItv,
            Motor = item.Motor,
            IsDeleted = item.IsDeleted
        };
    }
}