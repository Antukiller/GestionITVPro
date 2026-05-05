using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Config;
using GestionITVPro.Models;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.Service.ImportExport;
using Microsoft.Win32;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.ImportExport;


/// <summary>
/// ViewModel para la importación y exportación de datos.
/// Permite exportar e importar en formato CSV y JSON.
///  </summary>
public partial class ImportExportViewModel(
    ICitasService citasService,
    IImportExportService importExportService,
    IDialogService dialogService
) : ObservableObject {
    private readonly IDialogService _dialogService = dialogService;
    private readonly IImportExportService _importExportService = importExportService;
    private readonly ILogger _logger = Log.ForContext<ImportExportViewModel>();
    private readonly ICitasService _citasService = citasService;


    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private bool _sustituirDatos;


    [RelayCommand]
    private void ExportCsv() {
        try {
            IsLoading = true;
            StatusMessage = "Exportando datos...";

            var dialog = new SaveFileDialog {
                Filter = "CSV|*.csv",
                FileName = $"Exportación_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true) {
                // Usamos null para los filtros que no queremos aplicar en la exportación
                // y nombres de parámetros para ir directamente a la paginación
                var citas = _citasService.GetAll(
                    marca: null, 
                    dniPropietario: null, 
                    matricula: null, 
                    desde: null, 
                    hasta: null, 
                    page: 1, 
                    pageSize: 1000, 
                    includeDeleted: false
                );
                var csvPath = Path.Combine(AppConfig.DataFolder, "citas.csv");
                var result = _importExportService.ExportarDatos(citas, csvPath);

                if (result.IsSuccess) {
                    File.Copy(Path.Combine(AppConfig.DataFolder, "citas.csv"), dialog.FileName, true);
                    StatusMessage = $"Exportados {result.Value} registros";
                    _dialogService.ShowSuccess($"Exportación completada\n{result.Value} registros");
                }
                else {
                    _dialogService.ShowError(result.Error.Message);
                    StatusMessage = "Error al exportar";
                }
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al exportar");
            StatusMessage = "Error al exportar";
        }
        finally {
            IsLoading = false;
        }
    }
    
    [RelayCommand]
    private void ImportarCsv() {
        try {
            var dialog = new OpenFileDialog {
                Filter = "CSV|*.csv",
                Title = "Seleccionar archivo CSV"
            };

            if (dialog.ShowDialog() != true) return;

            IsLoading = true;
            StatusMessage = "Importando datos...";

            if (SustituirDatos) _citasService.DeleteAll();

            var result = _importExportService.ImportarDatosSistema(dialog.FileName);

            if (result.IsSuccess) {
                var count = result.Value.Count();
                StatusMessage = $"Importados {count} registros";
                _dialogService.ShowSuccess($"Importación completada\n{count} registros");
            }
            else {
                _dialogService.ShowError(result.Error.Message);
                StatusMessage = "Error al importar";
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al importar");
            _dialogService.ShowError($"Error al importar: {ex.Message}");
            StatusMessage = "Error al importar";
        }
        finally {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ExportarJson() {
        try {
            IsLoading = true;
            StatusMessage = "Exportando JSON...";

            var dialog = new SaveFileDialog {
                Filter = "JSON|*.json",
                FileName = $"Exportacion_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true) {
                // Usamos null para los filtros que no queremos aplicar en la exportación
                // y nombres de parámetros para ir directamente a la paginación
                var citas = _citasService.GetAll(
                    marca: null, 
                    dniPropietario: null, 
                    matricula: null, 
                    desde: null, 
                    hasta: null, 
                    page: 1, 
                    pageSize: 1000, 
                    includeDeleted: false
                );
                var options = new JsonSerializerOptions {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(citas, options);
                File.WriteAllText(dialog.FileName, json);

                StatusMessage = "Exportación JSON completada";
                _dialogService.ShowSuccess("Exportación JSON completada");
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al exportar JSON");
            StatusMessage = "Error al exportar";
        }
        finally {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ImportarJson() {
        try {
            var dialog = new OpenFileDialog {
                Filter = "JSON|*.json",
                Title = "Seleccionar archivo JSON"
            };

            if (dialog.ShowDialog() != true) return;

            IsLoading = true;
            StatusMessage = "Importando JSON...";

            if (SustituirDatos) _citasService.DeleteAll();

            var json = File.ReadAllText(dialog.FileName);
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var citas = JsonSerializer.Deserialize<IEnumerable<Cita>>(json, options);

            if (citas != null) {
                var count = 0;
                foreach (var v in citas) {
                    var result = _citasService.Save(v);
                    if (result.IsSuccess) count++;
                }

                StatusMessage = $"Importados {count} registros";
                _dialogService.ShowSuccess($"Importación completada\n{count} registros");
            }
            else {
                _dialogService.ShowError("El archivo JSON no tiene un formato válido");
                StatusMessage = "Error al importar";
            }
        }
        catch (JsonException ex) {
            _logger.Error(ex, "Error al importar JSON - formato inválido");
            _dialogService.ShowError($"Error al importar JSON: El formato del archivo no es válido.\n\nDetalles: {ex.Message}");
            StatusMessage = "Error al importar";
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al importar JSON");
            _dialogService.ShowError($"Error al importar: {ex.Message}");
            StatusMessage = "Error al importar";
        }
        finally {
            IsLoading = false;
        }
    }
    
}