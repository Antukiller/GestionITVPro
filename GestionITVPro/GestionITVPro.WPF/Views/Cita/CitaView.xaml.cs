using System.Windows.Controls;
using System.Windows.Input;
using GestionITVPro.WPF.ViewModels.Citas;
using Microsoft.Extensions.DependencyInjection;

namespace GestionITVPro.WPF.Views.Cita;

/// <summary>
/// Página de visualizacion de Citas
/// </summary>
public partial class CitaView : Page {
    /// <summary>
    /// Inicializa la vista de citas y configura el ViewModel correspondiente.
    /// </summary>
    public CitaView() {
        InitializeComponent();
        var vm = App.ServiceProvider.GetRequiredService<CitaViewModel>();
        DataContext = vm;
    }
    
    /// <summary>
    /// Maneja el evento de doble clic sobre una cita para mostrar sus detalles. 
    /// </summary>
    
    private void OnCitaDoubleClick(object sender, MouseButtonEventArgs e) {
        if (DataContext is CitaViewModel vm && vm.ViewCommand.CanExecute(null)) vm.ViewCommand.CanExecute(null);
    }
}