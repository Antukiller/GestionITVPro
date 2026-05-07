using GestionITVPro.Views.Main;
using GestionITVPro.WPF.ViewModels;
using GestionITVPro.WPF.ViewModels.Backup;
using GestionITVPro.WPF.ViewModels.Citas;
using GestionITVPro.WPF.ViewModels.Dashboard;
using GestionITVPro.WPF.ViewModels.ImportExport;
using GestionITVPro.WPF.ViewModels.Informe;
using GestionITVPro.WPF.ViewModels.Main;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GestionITVPro.WPF.Infrastructure;

/// <summary>
///     Proveedor de dependencias para el Frontend WPF.
///     Extiende el Back con los ViewModels específicos de presentación.
/// </summary>
public static class FrontDependenciesProvider {
    /// <summary>
    ///     Construye el proveedor de servicios combinando Back + Front.
    ///     El Back se extiende con los ViewModels del Front mediante callback.
    /// </summary>
    /// <returns>Proveedor de servicios con todos los servicios registrados.</returns>
    public static IServiceProvider BuildServiceProvider() {
        Log.Information("Configurando servicios (Back + Front)...");

        // Usar el DependenciesProvider del Back y extender con ViewModels del Front
        var serviceProvider = GestionITVPro.Infrastructure.DependenciesProvider.BuildServiceProvider(services => {
            RegisterViewModels(services);
            Log.Information("ViewModels registradas desde Front");
        });

        Log.Information("Servicios configurados correctamente");

        return serviceProvider;
    }

    /// <summary>
    ///     Registra todos los ViewModels del Frontend como servicios transientes.
    /// </summary>
    private static void RegisterViewModels(IServiceCollection services) {
        // ViewModel principal de la aplicación
        services.AddTransient<MainViewModel>();
        // ViewModel del Dashboard (página de inicio con estadísticas)
        services.AddTransient<DashboardViewModel>();
        // ViewModels de Citas (listado y edición)
        services.AddTransient<CitaViewModel>();
        services.AddTransient<CitaEditViewModel>();
        // ViewModel de Backup (gestión de copias de seguridad)
        services.AddTransient<BackupViewModel>();
        // ViewModel de Gráficos (visualización de estadísticas)
        services.AddTransient<GraficosViewModel>();
        // ViewModel de Informes (generación de reportes)
        services.AddTransient<InformeViewModel>();
        // ViewModel de Import/Export (importación y exportación de datos)
        services.AddTransient<ImportExportViewModel>();
    }
}