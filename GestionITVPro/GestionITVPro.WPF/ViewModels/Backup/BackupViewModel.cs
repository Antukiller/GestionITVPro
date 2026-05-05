using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Service.Backup;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using LiveChartsCore.Defaults;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Backup;

/// <summary>
/// ViewModel para la gestión de copias de seguridad.
/// Permite crear, resturar, y eliminar backups del sistema.
/// </summary>
public partial class BackupViewModel : ObservableObject {
    private readonly IBackupService _backupService;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger = Log.ForContext<BackupViewModel>();
    private readonly ICitasService _citasService;


    [ObservableProperty] private ObservableCollection<string> _backups = new();

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private string? _selectedBackup;

    [ObservableProperty] private string _statusMessage = "";

    public BackupViewModel(
        ICitasService citasService,
        IBackupService backupService,
        IDialogService dialogService) {
        _citasService = citasService;
        _backupService = backupService;
        _dialogService = dialogService;
        LoadBackups();
    }
    
    
    /// <summary>
    /// Carga la lista de archvios de backup disponibles en el directorio de backup.
    /// </summary>
    private void LoadBackups() {
        try {
            var backupList = _backupService.ListarBackups();
            Backups = new ObservableCollection<string>(backupList);
            StatusMessage = $"Encontrados {Backups.Count} backups";
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al cargar backups");
            StatusMessage = "Error al cargar backups";
        }
    }


    [RelayCommand]
    private void RealizarBackup() {
        try {
            IsLoading = true;
            StatusMessage = "Realizando backups...";

            var citas = _citasService.GetAll();
            var result = _backupService.RealizarBackup(citas);

            if (result.IsSuccess) {
                LoadBackups();
                StatusMessage = $"Backup creado: {Path.GetFileName(result.Value)}";
                _dialogService.ShowSuccess($"Backup creado correctamente: \n{result.Value}");
            }
            else {
                _dialogService.ShowError(result.Error.Message);
                StatusMessage = "Error al crear el backup";
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al realizar backup");
            StatusMessage = "Error al crear backup";
        }
        finally {
            IsLoading = false;
        }
    }

    private void RestaurarBackup() {
        if (string.IsNullOrEmpty(SelectedBackup)) {
            _dialogService.ShowWarning("Selecciona un bcakup para restaurar");
            return;
        }
        
        if (!_dialogService.ShowConfirmation(
                $"¿Restaurar el backup {Path.GetFileName(SelectedBackup)}?\n\n" +
                $"⚠️ Advertencia: Se borrarán todos los datos actuales (citas)\n" +
                $"y se reemplazará por el contenido de la copia de seguridad.\n\n" +
                $"Esta acción no se puede deshacer.",
                "Confimar restauración"))
            return;

        try {
            IsLoading = true;
            StatusMessage = "Restaurando backup...";

            var restoreResult = _backupService.RestaurarBackupSistema(
                SelectedBackup,
                () => _citasService.DeleteAll(),
                c => _citasService.Save(c));

            if (restoreResult.IsSuccess) {
                StatusMessage = $"Restaurados {restoreResult.Value} registros";
                _dialogService.ShowSuccess($"Backup restaurado correctamente\n{restoreResult.Value} registros");
            }
            else {
                _dialogService.ShowError(restoreResult.Error.Message);
                StatusMessage = "Error al restaurar";
            }
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al restaurar backup");
            StatusMessage = "Error al restaurar";
        }
        finally {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Refresh() {
        LoadBackups();
    }
    
    [RelayCommand]
    private void EliminarBackups() {
        if (string.IsNullOrEmpty(SelectedBackup)) return;

        if (!_dialogService.ShowConfirmation($"¿Eliminar el backup {Path.GetFileName(SelectedBackup)}"))
            return;

        try {
            File.Delete(SelectedBackup);
            LoadBackups();
            StatusMessage = "Backup eliminado";
        }
        catch (Exception ex) {
            _logger.Error(ex, "Error al eliminar backup");
            StatusMessage = "Error al eliminar";
        }
    }
}