using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Enums;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.Service.Report;
using Microsoft.Win32;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Informe;

public partial class InformeViewModel(
    ICitasService citasService,
    IReportService reportService,
    IDialogService dialogService
) : ObservableObject {
    private readonly IDialogService _dialogService = dialogService;
    private readonly ILogger _logger = Log.ForContext<InformeViewModel>();
    private readonly IReportService _reportService = reportService;
    private readonly ICitasService _citasService = citasService;

    [ObservableProperty] private bool _isGenerating;

    [ObservableProperty] private bool _mostrarEliminados;

    [ObservableProperty] private bool _mostrarVehiculosEletricos;

    [ObservableProperty] private Motor? _selectedMotor;

    [ObservableProperty] private string _statusMessage = "";

    public IEnumerable<Motor> Motors => Enum.GetValues<Motor>();


    [RelayCommand]
    private void GenerarInformeCitasPdf() {
        try {
            IsGenerating = true;
            StatusMessage = "Generando informe de citas...";

            var citas = _citasService.GetCitasOrderBy(
                TipoOrdenamiento.Matricula,
                1,
                1000,
                MostrarEliminados);

            var informeHtml =
                _reportService.GenerarInformeCitasHtml(citas, MostrarEliminados);
            if (informeHtml.IsFailure) {
                _dialogService.ShowError(informeHtml.Error.Message);
                return;
            }

            var saveDialog = new SaveFileDialog {
                Filter = "PDF|*.pdf",
                FileName = $"Informe_Citas_{DateTime.Now:yyyyMMdd}"
            };

            if (saveDialog.ShowDialog() == true) {
                var result = _reportService.GuardarInformeHtml(informeHtml.Value, saveDialog.FileName);
                if (result.IsSuccess) {
                    StatusMessage = "Informe PDF generado";
                    _dialogService.ShowSuccess("Informe PDF generado correctamente");
                }
                else {
                    _dialogService.ShowError(result.Error.Message);
                }
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al genera informe de citas PDF");
            StatusMessage = "Error al generar";
        }
        finally {
            IsGenerating = false;
        }
    }
    

    [RelayCommand]
    private void GenerarInformeCitasHtml() {
        try {
            IsGenerating = true;
            StatusMessage = "Generando informe HTML...";

            var citas = _citasService.GetCitasOrderBy(
                TipoOrdenamiento.Matricula,
                1,
                1000,
                MostrarEliminados
            );

            var result = _reportService.GenerarInformeCitasHtml(citas, MostrarEliminados);
            if (result.IsFailure) {
                _dialogService.ShowError(result.Error.Message);
                return;
            }

            var saveDialog = new SaveFileDialog {
                Filter = "HTML|*.html",
                FileName = $"Informe_Citas_{DateTime.Now:yyyyMMdd}"
            };

            if (saveDialog.ShowDialog() == true) {
                var saveResult = _reportService.GuardarInforme(result.Value, saveDialog.FileName);
                if (saveResult.IsSuccess) {
                    StatusMessage = "Informe HTML guardado";
                    _dialogService.ShowSuccess("Informe HTML generado correctamente");
                }
                else {
                    _dialogService.ShowError(saveResult.Error.Message);
                }
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al generar el informe de citas HTML");
            StatusMessage = "Error al generar";
        }
        finally {
            IsGenerating = false;
        }
    }
    
}