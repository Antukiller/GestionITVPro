using System.Windows.Controls;
using GestionITVPro.WPF.ViewModels.Informe;
using Microsoft.Extensions.DependencyInjection;


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
        DataContext = App.ServiceProvider.GetRequiredService<InformeViewModel>();
    }
}