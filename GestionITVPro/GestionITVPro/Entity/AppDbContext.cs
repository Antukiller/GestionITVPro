using Microsoft.EntityFrameworkCore;

namespace GestionITVPro.Entity;

public class AppDbContext : DbContext {
    private readonly string _connectionString;

    public AppDbContext(string connectionString) {
        _connectionString = connectionString;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {
        _connectionString = "";
    }

    public DbSet<VehiculoEntity> Vehiculos { get; set; } = null;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        if (!optionsBuilder.IsConfigured) optionsBuilder.UseSqlite(_connectionString);
    }

    public void EnsureCreated() {
        Database.EnsureCreated();
    }
}