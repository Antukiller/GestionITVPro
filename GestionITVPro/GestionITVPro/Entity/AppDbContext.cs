using Microsoft.EntityFrameworkCore;

namespace GestionITVPro.Entity;
public class AppDbContext : DbContext {
    private readonly string? _connectionString;

    // Constructor para uso manual (ej. en tests o herramientas de CLI)
    public AppDbContext(string connectionString) {
        _connectionString = connectionString;
    }

    // Constructor estándar para Inyección de Dependencias
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {
    }

    // CORRECCIÓN: Inicializar con null! silencia el warning de nulidad. 
    // EF Core se encarga de instanciarlo en tiempo de ejecución mediante reflexión.
    public DbSet<CitaEntity> Citas { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        // Solo configuramos si no viene ya configurado desde el ServiceCollection
        if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(_connectionString)) {
            optionsBuilder.UseSqlite(_connectionString);
        }
    }

    public void EnsureCreated() {
        Database.EnsureCreated();
    }
}