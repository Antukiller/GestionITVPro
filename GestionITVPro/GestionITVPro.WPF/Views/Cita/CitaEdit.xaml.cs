using System.Windows;
using GestionITVPro.WPF.ViewModels.Citas;

namespace GestionITVPro.WPF.Views.Cita;

/// <summary>
/// Ventana modal para crear o editar una Cita.
/// </summary>
public partial class CitaEditWindow : Window {
    /// <summary>
    /// Inicializa la ventana de edición de citas.
    /// </summary>
    public CitaEditWindow() {
        InitializeComponent();
    } 
    
    /// <summary>
    /// Configura la acción de cierre del Viewmodel cuando el contenido se renderiza.
    /// </summary>
    protected override void OnContentRendered(EventArgs e) {
        base.OnContentRendered(e);
        if (DataContext is CitaEditViewModel vm)
            vm.CloseAction = result => {
                DialogResult = result;
                Close();
            };
    }
    
}