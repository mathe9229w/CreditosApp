using CreditosApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> Solicitudes => Set<SolicitudCredito>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(e =>
        {
            e.ToTable("Clientes", t =>
                t.HasCheckConstraint("CK_Clientes_IngresosMensuales", "\"IngresosMensuales\" > 0"));
            e.Property(c => c.UsuarioId).IsRequired();
            e.HasIndex(c => c.UsuarioId).IsUnique();
            e.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
            // SQLite no soporta decimal nativo: se guarda como REAL para comparar/ordenar correctamente.
            e.Property(c => c.IngresosMensuales).HasConversion<double>();
        });

        builder.Entity<SolicitudCredito>(e =>
        {
            e.ToTable("SolicitudesCredito", t =>
                t.HasCheckConstraint("CK_SolicitudesCredito_MontoSolicitado", "\"MontoSolicitado\" > 0"));
            e.Property(s => s.MontoSolicitado).HasConversion<double>();
            e.Property(s => s.MotivoRechazo).HasMaxLength(500);
            e.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
            // Regla: un cliente solo puede tener UNA solicitud Pendiente (índice único filtrado).
            e.HasIndex(s => s.ClienteId)
                .IsUnique()
                .HasFilter("\"Estado\" = 0")
                .HasDatabaseName("IX_SolicitudesCredito_ClienteId_Pendiente");
        });
    }
}
