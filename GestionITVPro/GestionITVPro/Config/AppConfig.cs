using System.IO;

namespace GestionITVPro.Config;
using System.Globalization;
using Microsoft.Extensions.Configuration;


/// <summary>
/// Clase de configuración global para la gestión de ITV.
/// Lee los valores desde appsettings.json.
/// </summary>
public class AppConfig
{
    static AppConfig()
    {
        Configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .Build();
    }

    // Exponemos la configuración para que otros servicios (como Serilog) la usen
    public static IConfiguration Configuration { get; }

    public static CultureInfo Locale => CultureInfo.GetCultureInfo("es-ES");

    // ====================================================================
    // CONFIGURACIÓN DE REPOSITORIO Y DATOS
    // ====================================================================

    public static string DataFolder => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        Configuration.GetValue<string>("Repository:Directory") ?? "data");

    public static string ConnectionString => Configuration.GetValue<string>("Repository:ConnectionString") ??
                                             "Data Source=data/gestionITV.db";

    public static string StorageType => Configuration.GetValue<string>("Storage:Type") ?? "json";

    public static string RepositoryType {
        get {
            var type = Configuration.GetValue<string>("Repository:Type") ?? "memory";
            return type.ToLower() switch {
                "memory" => "memory",
                "json" => "json",
                "binary" => "binary",
                "ado" => "ado",
                "dapper" => "dapper",
                "efcore" => "efcore",
                _ => "memory"
            };
        }
    }

    public static string GestionItv {
        get {
            var extension = StorageType.ToLower() switch {
                "json" => "json",
                "csv" => "csv",
                "xml" => "xml",
                "bin" or "binary" => "bin",
                _ => "json"
            };
            return Path.Combine(DataFolder, $"gestionITV.{extension}");
        }
    }

    public static int CacheSize => Configuration.GetValue("Cache:Size", 15);
    public static bool DropData => Configuration.GetValue("Repository:DropData", false);
    public static bool SeedData => Configuration.GetValue("Repository:SeedData", true);
    public static bool UseLogicalDelete => Configuration.GetValue("Repository:UseLogicalDelete", true);

    // ====================================================================
    // CONFIGURACIÓN DE BACKUP Y REPORTES
    // ====================================================================

    public static string BackupDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        Configuration.GetValue<string>("Backup:Directory") ?? "backup");

    public static string BackupFormat {
        get {
            var format = Configuration.GetValue<string>("Backup:Format") ?? "json";
            return format.ToLower() switch {
                "json" => "json",
                "csv" => "csv",
                "xml" => "xml",
                "bin" => "bin",
                _ => "json"
            };
        }
    }

    public static bool IsDevelopment => Configuration.GetValue("Development:Enabled", false);

    public static string ReportDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        Configuration.GetValue<string>("Reports:Directory") ?? "reports");

    // ====================================================================
    // CONFIGURACIÓN DE LOGGING
    // ====================================================================

    public static bool LogToFile => Configuration.GetValue("Logging:File:Enabled", true);

    public static string LogDirectory => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        Configuration.GetValue<string>("Logging:File:Directory") ?? "log");

    public static int LogRetainDays => Configuration.GetValue("Logging:File:RetainDays", 7);

    public static string LogLevel => Configuration.GetValue<string>("Logging:File:Level") ?? "Error";

    public static string LogOutTemplate => Configuration.GetValue<string>("Logging:File:OutputTemplate")
                                           ?? "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";
    
}