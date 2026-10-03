using DetectorApi.Entidades;
using Microsoft.EntityFrameworkCore;

namespace DetectorApi.Datos;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opciones) : base(opciones)
    {
    }

    public DbSet<Captura> Capturas => Set<Captura>();
    public DbSet<ConfiguracionAvisos> ConfiguracionAvisos => Set<ConfiguracionAvisos>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Captura>(entidad =>
        {
            entidad.Property(c => c.NombreArchivo).HasMaxLength(100).IsRequired();
            entidad.HasIndex(c => c.FechaHora);
        });
        modelBuilder.Entity<ConfiguracionAvisos>(entidad =>
        {
            entidad.Property(c => c.Id).ValueGeneratedNever();
            entidad.Property(c => c.Email).HasMaxLength(200);
            entidad.Property(c => c.WhatsappTelefono).HasMaxLength(20);
            entidad.Property(c => c.WhatsappApiKey).HasMaxLength(100);
        });
    }
}
