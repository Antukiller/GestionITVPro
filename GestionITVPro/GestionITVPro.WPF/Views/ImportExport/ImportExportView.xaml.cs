using System.Windows.Controls;
using GestionITVPro.WPF.ViewModels.ImportExport;
using Microsoft.Testing.Platform.Services;

namespace GestionITVPro.WPF.Views.ImportExport;

/// <summary>
///     Página de importación y exportación de datos.
/// </summary>
public partial class ImportExportView : Page {
    /// <summary>
    ///     Inicializa la vista de importación/exportación y configura el ViewModel correspondiente.
    /// </summary>
    public ImportExportView() {
        InitializeComponent();
        DataContext = App.ServiceProvider.GetRequiredService<ImportExportViewModel>();
    }
}