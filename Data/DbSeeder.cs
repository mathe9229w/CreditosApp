using CreditosApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Data;

/// <summary>
/// Aplica migraciones y crea los datos iniciales:
/// rol Analista + 1 analista, 2 clientes, 2 solicitudes (una Pendiente y una Aprobada).
/// </summary>
public static class DbSeeder
{
    public const string RolAnalista = "Analista";
    public const string PasswordDemo = "Examen2026!";

    public static async Task InicializarAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<ApplicationDbContext>();

        AsegurarDirectorioSqlite(db.Database.GetConnectionString());
        await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roleManager.RoleExistsAsync(RolAnalista))
            await roleManager.CreateAsync(new IdentityRole(RolAnalista));

        var analista = await CrearUsuarioAsync(userManager, "analista@creditos.pe", logger);
        if (!await userManager.IsInRoleAsync(analista, RolAnalista))
            await userManager.AddToRoleAsync(analista, RolAnalista);

        var usuario1 = await CrearUsuarioAsync(userManager, "cliente1@creditos.pe", logger);
        var usuario2 = await CrearUsuarioAsync(userManager, "cliente2@creditos.pe", logger);

        if (!await db.Clientes.AnyAsync())
        {
            var cliente1 = new Cliente { UsuarioId = usuario1.Id, IngresosMensuales = 3000m, Activo = true };
            var cliente2 = new Cliente { UsuarioId = usuario2.Id, IngresosMensuales = 5000m, Activo = true };
            db.Clientes.AddRange(cliente1, cliente2);
            await db.SaveChangesAsync();

            db.Solicitudes.AddRange(
                new SolicitudCredito
                {
                    ClienteId = cliente1.Id,
                    MontoSolicitado = 12000m, // 4x ingresos -> aprobable
                    FechaSolicitud = DateTime.UtcNow.AddDays(-2),
                    Estado = EstadoSolicitud.Pendiente
                },
                new SolicitudCredito
                {
                    ClienteId = cliente2.Id,
                    MontoSolicitado = 20000m,
                    FechaSolicitud = DateTime.UtcNow.AddDays(-10),
                    Estado = EstadoSolicitud.Aprobado
                });
            await db.SaveChangesAsync();
            logger.LogInformation("Datos iniciales creados.");
        }
    }

    private static async Task<IdentityUser> CrearUsuarioAsync(UserManager<IdentityUser> userManager, string email, ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, PasswordDemo);
        if (!result.Succeeded)
        {
            var errores = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogError("No se pudo crear el usuario {Email}: {Errores}", email, errores);
            throw new InvalidOperationException($"No se pudo crear {email}: {errores}");
        }
        return user;
    }

    /// <summary>SQLite no crea carpetas: si la BD está en un disco persistente (/var/data) se crea la carpeta.</summary>
    private static void AsegurarDirectorioSqlite(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }
}
