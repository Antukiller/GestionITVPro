using System.IO;
using GestionITVPro.Cache;
using GestionITVPro.Config;
using GestionITVPro.Entity;
using GestionITVPro.Models;
using GestionITVPro.Repositories.Base;
using GestionITVPro.Repositories.Binary;
using GestionITVPro.Repositories.Dapper;
using GestionITVPro.Repositories.EfCore;
using GestionITVPro.Repositories.Json;
using GestionITVPro.Repositories.Memory;
using GestionITVPro.Service.Backup;
using GestionITVPro.Service.Citas;
using GestionITVPro.Service.Dialogs;
using GestionITVPro.Service.ImportExport;
using GestionITVPro.Service.Report;
using GestionITVPro.Storage.Binary;
using GestionITVPro.Storage.Common;
using GestionITVPro.Storage.Csv;
using GestionITVPro.Storage.Json;
using GestionITVPro.Storage.Xml;
using GestionITVPro.Validator;
using GestionITVPro.Validator.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
namespace GestionITVPro.Infrastructure;

public static class DependenciesProvider {
    public static IServiceProvider BuildServiceProvider(Action<IServiceCollection>? configureAdditional = null) {
        var services = new ServiceCollection();

        CleanData();

        RegisterCaches(services);
        RegisterValidators(services);
        RegisterStorages(services);
        RegisterRepositories(services);
        RegisterServices(services);
        
        // Permitir extensión con servicios adicionales
        configureAdditional?.Invoke(services);
        
        // Construir el proveedpr de servicios y devolverlo
        return services.BuildServiceProvider();
    }

    private static void RegisterStorages(IServiceCollection services) {
        // Registrar almacenamiento para vehiculos según configuracion
        services.AddTransient<IStorage<Cita>>(sp => {
            var storageType = AppConfig.StorageType.ToLower();
            return storageType switch {
                "json" => new GestionItvJsonStorage(),
                "csv" => new GestionItvCsvStorage(),
                "binary" or "bin" => new GestionItvBinaryStorage(),
                "xml" => new GestionItvXmlStorage(),
                _ => new GestionItvJsonStorage()
            };
        });
    }

    private static void RegisterRepositories(IServiceCollection services) {
        services.AddSingleton<ICitaRepository>(sp => {
            var repoType = AppConfig.RepositoryType.ToLower();
            return repoType switch {
                "memory" => new CitaMemoryRepository(AppConfig.DropData, AppConfig.SeedData),
                "json" => new CitaJsonRepository(
                    Path.Combine(AppConfig.DataFolder, "gestionitv.json"),
                    AppConfig.DropData,
                    AppConfig.SeedData),
                
                "binary" or "bin" => new CitaBinRepository(
                    Path.Combine(AppConfig.DataFolder, "gestionitv.bin"), // O la extensión que uses (.dat, .bin)
                    AppConfig.DropData,
                    AppConfig.SeedData),
                // ---------------------------
                "ado" => CreateDapperRepository(AppConfig.DropData, AppConfig.SeedData),
                "dapper" => CreateDapperRepository(AppConfig.DropData, AppConfig.SeedData),
                "efcore" => CreateEfRepository(AppConfig.DropData, AppConfig.SeedData),
                _ => new CitaMemoryRepository(AppConfig.DropData, AppConfig.SeedData)
            };
        });
    }

    private static CitaDapperRepository CreateDapperRepository(bool dropData, bool seedData) {
        // Crear carpeta de datos si no existe
        var dataFolder = AppConfig.DataFolder;
        if (!Directory.Exists(dataFolder))
            Directory.CreateDirectory(dataFolder);
        
        // Crear base de datos si no existe
        var dbPath = Path.Combine(dataFolder, "gestionitv.db");
        var connection = new SqliteConnection($"Data Source={dbPath}");
        // Abrir conexión para crear la base de datps si no existe
        connection.Open();
        // Devolver repositorio con conexión abierta, el repositorio se encargará de cerrala
        return new CitaDapperRepository(connection, () => connection.Close(), dropData, seedData);
    }

    private static CitaEfRepository CreateEfRepository(bool dropData, bool seedData) {
        // Crear carpeta de datos si no existe
        var dataFolder = AppConfig.DataFolder;
        if (!Directory.Exists(dataFolder))
        Directory.CreateDirectory(dataFolder);
        
        // Crear base de datos si no existe
        var dbPath = Path.Combine(dataFolder, "gestionitv");
        var context = new AppDbContext($"Data Source={dbPath}");
        
        // Crear la base de datos si no existe
        return new CitaEfRepository(context, dropData, seedData);
    }

    private static void RegisterValidators(IServiceCollection services) {
        services.AddTransient<IValidador<Cita>, ValidadorCita>();
    }

    private static void RegisterCaches(IServiceCollection services) {
        // Registrar LRU Cache para vehiculos
        services.AddSingleton<ICache<int, Cita>>(sp =>
            new LruCache<int, Cita>(AppConfig.CacheSize));
    }

    private static void RegisterServices(IServiceCollection services) {
        // 1. DialogService (Asegúrate de que la clase se llame DialogService o lo que corresponda)
        services.AddSingleton<IDialogService, DialogService>();
    
        // 2. BackupService (Corregido el acceso al storage)
        services.AddTransient<IBackupService, BackupService>(sp =>
            new BackupService(
                sp.GetRequiredService<IStorage<Cita>>(), 
                AppConfig.BackupDirectory));
    
        // 3. ReportService (¡Corregido! Se usa AddTransient o AddScoped, NO AddDbContext)
        services.AddTransient<IReportService, ReportService>();
    
        // 4. ImportExportService
        services.AddTransient<IImportExportService, ImportExportService>();
    
        // 5. CitaService (Simplificado: el contenedor ya sabe resolver los parámetros del constructor)
        services.AddScoped<ICitasService, CitasService>();
    }
    

    private static void CleanData() {
        // Limpiar directporios de reports o SeedData están activos
        if (AppConfig.DropData || AppConfig.SeedData) {
            CleanDirectory(AppConfig.ReportDirectory);
        }
    }

    private static void CleanDirectory(string path) {
        try {
            if (Directory.Exists(path)) {
                foreach (var file in Directory.GetFiles(path)) {
                    try {
                        File.Delete(path);
                    }
                    catch {
                        /* Ignorar archivos en uso */
                    }

                    foreach (var dir in Directory.GetDirectories(path)) {
                        try {
                            Directory.Delete(dir, true);
                        }
                        catch {
                            /* Ignorar directorios en uso */
                        }
                    }
                    Directory.CreateDirectory(path);
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Warning: No se pudo limpiar directorio {path}: {ex.Message}");
        }
    }
}