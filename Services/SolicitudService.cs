using CreditosApp.Data;
using CreditosApp.Models;
using CreditosApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Services;

public class SolicitudService(ApplicationDbContext db, ICacheSolicitudes cache, ILogger<SolicitudService> logger) : ISolicitudService
{
    public Task<Cliente?> ObtenerClienteAsync(string usuarioId) =>
        db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

    public async Task<(IReadOnlyList<SolicitudResumen> Items, bool DesdeCache)> ListarDelUsuarioAsync(string usuarioId)
    {
        var cacheadas = await cache.ObtenerAsync(usuarioId);
        if (cacheadas is not null) return (cacheadas, true);

        var desdeBd = await ConsultarDelUsuarioAsync(usuarioId);
        await cache.GuardarAsync(usuarioId, desdeBd);
        return (desdeBd, false);
    }

    public async Task<IReadOnlyList<SolicitudResumen>> ListarEstadosVigentesAsync(string usuarioId) =>
        await ConsultarDelUsuarioAsync(usuarioId);

    public Task<SolicitudCredito?> ObtenerDelUsuarioAsync(int solicitudId, string usuarioId) =>
        db.Solicitudes
            .AsNoTracking()
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == solicitudId && s.Cliente!.UsuarioId == usuarioId);

    public async Task<ResultadoOperacion> CrearAsync(string usuarioId, decimal montoSolicitado)
    {
        if (string.IsNullOrEmpty(usuarioId))
            return ResultadoOperacion.Error("Debe iniciar sesión para registrar una solicitud.");

        if (montoSolicitado <= 0)
            return ResultadoOperacion.Error("El monto solicitado debe ser mayor a 0.");

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        if (cliente is null)
            return ResultadoOperacion.Error("Su usuario no tiene un perfil de cliente. Registre sus ingresos mensuales primero.");

        if (!cliente.Activo)
            return ResultadoOperacion.Error("El cliente está inactivo y no puede registrar solicitudes.");

        var tienePendiente = await db.Solicitudes.AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);
        if (tienePendiente)
            return ResultadoOperacion.Error("Ya tiene una solicitud Pendiente. Espere su evaluación antes de registrar otra.");

        if (!ReglasCredito.PuedeRegistrarse(montoSolicitado, cliente.IngresosMensuales))
            return ResultadoOperacion.Error(
                $"El monto solicitado ({Formato.Soles(montoSolicitado)}) no puede superar 10 veces sus ingresos mensuales " +
                $"(máximo {Formato.Soles(cliente.IngresosMensuales * ReglasCredito.FactorMaximoRegistro)}).");

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = montoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };
        db.Solicitudes.Add(solicitud);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Carrera: el índice único filtrado impide una segunda solicitud Pendiente.
            logger.LogWarning(ex, "No se pudo guardar la solicitud del usuario {UsuarioId}", usuarioId);
            return ResultadoOperacion.Error("No se pudo registrar la solicitud: ya existe una solicitud Pendiente o los datos son inválidos.");
        }

        logger.LogInformation("Solicitud {SolicitudId} registrada para {UsuarioId}", solicitud.Id, usuarioId);
        await cache.InvalidarAsync(usuarioId); // Invalidación: nueva solicitud
        return ResultadoOperacion.Ok($"Solicitud #{solicitud.Id} registrada correctamente por {Formato.Soles(montoSolicitado)}. Estado: Pendiente.", solicitud.Id);
    }

    public async Task<ResultadoOperacion> GuardarPerfilAsync(string usuarioId, decimal ingresosMensuales)
    {
        if (ingresosMensuales <= 0)
            return ResultadoOperacion.Error("Los ingresos mensuales deben ser mayores a 0.");

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        if (cliente is null)
        {
            cliente = new Cliente { UsuarioId = usuarioId, Activo = true };
            db.Clientes.Add(cliente);
        }
        cliente.IngresosMensuales = ingresosMensuales;
        await db.SaveChangesAsync();
        return ResultadoOperacion.Ok("Perfil de cliente guardado.");
    }

    private async Task<List<SolicitudResumen>> ConsultarDelUsuarioAsync(string usuarioId)
    {
        logger.LogDebug("Consultando solicitudes en BD para {UsuarioId}", usuarioId);
        return await db.Solicitudes
            .AsNoTracking()
            .Where(s => s.Cliente!.UsuarioId == usuarioId)
            .OrderByDescending(s => s.Id)
            .Select(s => new SolicitudResumen
            {
                Id = s.Id,
                MontoSolicitado = s.MontoSolicitado,
                FechaSolicitud = s.FechaSolicitud,
                Estado = s.Estado,
                MotivoRechazo = s.MotivoRechazo
            })
            .ToListAsync();
    }
}
