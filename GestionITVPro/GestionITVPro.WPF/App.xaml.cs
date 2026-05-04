using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GestionITVPro.Config;
using GestionITVPro.Infrastructure;
using GestionITVPro.Views;
using GestionITVPro.WPF.Views.Splash;
using Serilog;
using Serilog.Debugging;

namespace GestionITVPro.WPF;


/// <summary>
/// Clase principal de la aplicacion WPF.
/// Controla el ciclo de vida de la aplicación.
/// </summary>
public partial class App {
    /// <summary>
    /// Proveedor  de servicios para inyeccion de dependencias.
    /// Acceso global desde cualquier parte de la app:
    ///   var service = App.ServiceProvider.GetRequiredService<IPersonasService>();
    /// </summary>
    public static IServiceProvider ServiceProvider { get; set; } = null!;

    // ====================================================================
    // OnStartup - Se ejecuta al iniciar la aplicación
    // ====================================================================
    protected override void OnStartup(StartupEventArgs e) {
        // Forzamos el directorio actual al del ejecutable para que las 
        // rutas relativas de appsettings.json (log/, data/, etc.) funcionen.
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        ConfigureSerilog();

        Log.Information("🚀☠️ Aplicación WPF iniciada 🙈🙊");
        ;

        ServiceProvider = DependenciesProvider.BuildServiceProvider();

        Log.Information("✅ ServiceProvider creado con todos los servicios");

        ConfigureExceptionHandling();

        var splah = new SplashWindow();
        Log.Information("Mostrndo SplashWindow");
        splah.ShowDialog();
        Log.Information("SplashWindow cerrado");

        // Crear y mostrar ventana principal
        var mainWindow = new MainWindow();
        Log.Information("Llamando a mainWindow.Show");
        mainWindow.Show();


        base.OnStartup(e);

        Log.Information("mainWindow.Show() completado");

    }
    
    
    /// <summary>
    ///  Configura Serilog leyendo la configuracion de appsettings.json
    /// </summary>
    private void ConfigureSerilog() {
        // Habilitar SelfLog para depuración de Serilog
        SelfLog.Enable(msg => Debug.WriteLine($"SERILOG DIAG: {msg}"));
        
        // Configurar logger desde JSON
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(AppConfig.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();
        Log.Information("Serilog inicializado desde JSON");
    }
    
    
    /// <summary>
    ///  Configura los manejadores de excepciones para loggin y recuperacion.
    /// </summary>
    private void ConfigureExceptionHandling() {
        // Excepciones en el hilo de UI
        DispatcherUnhandledException +=  OnDispatcherUnhandledException;
        // Excepciones en el hilo principal
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        // Excepciones en tareas asíncronas 
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }
    
    /// <summary>
    /// Maneja excepciones no controladas en el hilo de UI.
    /// Muestra un MessageBox y marca la excepcion como manejada.
    /// </summary>
    private void  OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
        Log.Fatal(e.Exception, "Exception no manejada");
        MessageBox.Show(
            $"Erro: {e.Exception.Message}",
            "Error",
            MessageBoxButton.OK, 
            MessageBoxImage.Error);
        e.Handled = true;
    }
    
    /// <summary>
    /// Maneja excepciones no contrladas en el hilo principal.
    /// </summary>
    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e) {
        Log.Fatal(e.ExceptionObject as Exception, "Exposicion no manejada");
    }
    
    /// <summary>
    /// Maneja exceptciones no observadas en tareas asíncronas.
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        Log.Error(e.Exception, "Exception en tarea");
        e.SetObserved();
    }
    
    /// <summary>
    /// Método ejecutado al cerrar la aplicación.
    /// Libera recursos y cierra los logs. 
    /// </summary>
    
    protected override void OnExit(ExitEventArgs e) {
        Log.Information("Aplicacion cerrandose");
        Log.CloseAndFlush();
        
        // Disponer el ServiceProvider si implementa IDisposable
        if (ServiceProvider is IDisposable disposable) disposable.Dispose();
        
        base.OnExit(e);
    }
}