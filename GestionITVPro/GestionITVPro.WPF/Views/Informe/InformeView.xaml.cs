using System.Windows.Controls;
using Microsoft.Testing.Platform.Services;

namespace GestionITVPro.WPF.Views.Informe;


/// <summary>
///     Página de generación de informes.
/// </summary>
public partial class InformeView : Page {
    /// <summary>
    ///     Inicializa la vista de informes y configura el ViewModel correspondiente.
    /// </summary>
    public InformeView() {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<InformesViewModel>();
    }
}