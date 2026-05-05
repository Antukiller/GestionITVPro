using CommunityToolkit.Mvvm.ComponentModel;
using GestionITVPro.Enums;

namespace GestionITVPro.WPF.ViewModels.Citas;


/// <summary>
///     Base reactiva para elementos de personas en listas.
///     Proporciona notificación de cambios para las propiedades comunes.
/// </summary>
public partial class CitaItemViewModel : ObservableObject {
    [ObservableProperty] private int id;
    
    [ObservableProperty] private string _matricula = string.Empty;

    [ObservableProperty] private string _marca = string.Empty;

    [ObservableProperty] private string _modelo = string.Empty;

    [ObservableProperty] private int _cilindrada;

    [ObservableProperty] private DateTime _fechaInspeccion;

    [ObservableProperty] private Motor _motor;

    [ObservableProperty] private DateTime _fechaItv;

    [ObservableProperty] private string _dniPropietario = string.Empty;


    public string Descripcion => $" {Matricula} {Marca} {Modelo} {Motor}";

}