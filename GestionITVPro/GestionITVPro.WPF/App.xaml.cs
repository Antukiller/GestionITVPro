using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GestionITVPro.Config;
using GestionITVPro.Views.Main;
using GestionITVPro.WPF.Infrastructure;
using GestionITVPro.WPF.Views.Splash;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Debugging;

namespace GestionITVPro.WPF;


/// <summary>
/// Clase principal de la aplicacion WPF.
/// Controla el ciclo de vida de la aplicación.
/// </summary>
public partial class App : Application {
    /// <summary>
    /// Proveedor  de servicios para inyeccion de dependencias.
    /// Acceso global desde cualquier parte de la app:
    ///   var service = App.ServiceProvider.GetRequiredService<IPersonasService>();
    /// </summary>
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    // ====================================================================
    // OnStartup - Se ejecuta al iniciar la aplicación
    // ====================================================================
    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. Configuración básica inicial
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        ConfigureSerilog();
        Log.Information("🚀 Aplicación WPF iniciada");

        // 2. INICIALIZAR EL PROVIDER (¡Esto debe ir antes de cualquier uso!)
        // Asegúrate de que esta línea se ejecute ANTES de llamar al Splash o a la MainWindow
        ServiceProvider = FrontDependenciesProvider.BuildServiceProvider();
    
        if (ServiceProvider == null)
        {
            Log.Fatal("❌ El ServiceProvider no se pudo crear.");
            return;
        }
        Log.Information("✅ ServiceProvider creado correctamente");

        ConfigureExceptionHandling();

        // 3. Mostrar SplashWindow
        // Lo mostramos como Dialog para que bloquee el hilo hasta que termine la carga
        var splash = new SplashWindow();
        Log.Information("Mostrando SplashWindow");
        splash.ShowDialog(); 
        Log.Information("SplashWindow cerrado, procediendo a cargar MainWindow");

        // Crear y mostrar ventana principal
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        
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