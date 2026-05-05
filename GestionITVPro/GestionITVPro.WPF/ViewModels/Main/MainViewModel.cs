using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestionITVPro.Service.Backup;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.Service.ImportExport;
using GestionITVPro.Service.Report;
using GestionITVPro.WPF.Views.AcercaDe;
using GestionITVPro.WPF.Views.Backup;
using GestionITVPro.WPF.Views.Cita;
using GestionITVPro.WPF.Views.Dashboard;
using GestionITVPro.WPF.Views.Grafico;
using GestionITVPro.WPF.Views.ImportExport;
using GestionITVPro.WPF.Views.Informe;
using Serilog;

namespace GestionITVPro.WPF.ViewModels.Main;

/// <summary>
///     ViewModel principal de la aplicación.
///     Maneja la navegación entre vistas y acciones del menú.
/// </summary>
public partial class MainViewModel(
    ICitasService citasService,
    IBackupService backupService,
    IReportService reportService,
    IImportExportService importExportService,
    IDialogService dialogService
) : ObservableObject {
    // ====================================================================
    // EVENTO DE NAVEGACIÓN
    // ====================================================================

    public delegate void NavigateDelegate(Page page);

    private readonly IBackupService _backupService = backupService;
    private readonly IDialogService _dialogService = dialogService;
    private readonly IImportExportService _importExportService = importExportService;

    private readonly ILogger _logger = Log.ForContext<MainViewModel>();
    
    
    // ====================================================================
    // DEPENDENCIAS - Servicios inyectados
    // ====================================================================

    private readonly ICitasService _citasService = citasService;
    private readonly IReportService _reportService = reportService;
    
    
    // ====================================================================
    // PROPIEDADES OBSERVABLES
    // ====================================================================
    [ObservableProperty] private bool _isDarkTheme = true;
    
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private string _statusMessage = "Listo";
    
    // ====================================================================
    // INICIALIZACIÓN
    // ====================================================================

    private void OnInitialized() {
        _logger.Information("✅ MainViewModel inicializado");
    }

    public event NavigateDelegate? OnNavigateRequested;

    // ====================================================================
    // COMANDOS DE NAVEGACIÓN
    // ====================================================================

    [RelayCommand]
    private void NavigateToDashboard() {
        OnNavigateRequested?.Invoke(new DashboardView());
    }

    [RelayCommand]
    private void NavigateToCitas() {
        OnNavigateRequested?.Invoke(new CitaView());
    }
    
    [RelayCommand]
    private void NavigateToInformes() {
        OnNavigateRequested?.Invoke(new InformeView());
    }

    [RelayCommand]
    private void NavigateToGraficos() {
        OnNavigateRequested?.Invoke(new GraficoView());
    }

    [RelayCommand]
    private void NavigateToBackup() {
        OnNavigateRequested?.Invoke(new BackupView());
    }

    [RelayCommand]
    private void NavigateToImportExport() {
        OnNavigateRequested?.Invoke(new ImportExportView());
    }
    
    // ====================================================================
    // COMANDOS DEL MENÚ
    // ====================================================================

    [RelayCommand]
    private void CambiarTema() {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme(IsDarkTheme ? "Dark" : "Light");
    }

    [RelayCommand]
    private void Salir() {
        if (_dialogService.ShowConfirmation("¿Estás seguro de que quieres salir?", "Confirmar salida")) {
            _logger.Information("👋 Usuario cerró la aplicación");
            Application.Current.Shutdown();
        }
    }

    [RelayCommand]
    private void MostrarAcercaDe() {
        var aboutWindow = new AcercaDe();
        aboutWindow.ShowDialog();
    }

    // ====================================================================
    // MÉTODOS AUXILIARES
    // ====================================================================

    private void ApplyTheme(string themeName) {
        try {
            var themeUri = new Uri($"../Themes/{themeName}Theme.xaml", UriKind.Relative);
            var themeDictionary = new ResourceDictionary { Source = themeUri };

            var appResources = Application.Current.Resources.MergedDictionaries;

            for (var i = appResources.Count - 1; i >= 0; i--) {
                var dict = appResources[i];
                if (dict.Source != null && dict.Source.OriginalString.Contains("Theme")) appResources.RemoveAt(i);
            }

            appResources.Add(themeDictionary);

            _logger.Information("✅ Tema cambiado a {Theme}", themeName);
        }
        catch (Exception ex) {
            _logger.Error(ex, "❌ Error al aplicar el tema");
        }
    }

}