using System.Windows.Controls;
using GestionITVPro.WPF.ViewModels;

namespace GestionITVPro.WPF.Views.Grafico;

public partial class GraficoView : Page {
    public GraficoView(GraficosViewModel viewModel) {
        InitializeComponent();
        this.DataContext = viewModel;
    }
}