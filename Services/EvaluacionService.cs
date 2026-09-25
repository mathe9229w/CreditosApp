using CreditosApp.Data;
using CreditosApp.Mensajeria;
using CreditosApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Services;

/// <summary>Reglas del panel Analista. Todas las validaciones se hacen en servidor.</summary>
public class EvaluacionService(
    ApplicationDbContext db,
    ICacheSolicitudes cache,
    INotificadorSolicitudes notificador,
    IPublicadorSolicitudes publicador,
    ILogger<EvaluacionService> logger) : IEvaluacionService
{
    public Task<List<SolicitudCredito>> ListarPendientesAsync() =>
        db.Solicitudes
            .AsNoTracking()
            .Include(s => s.Cliente)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

    public async Task<ResultadoOperacion> AprobarAsync(int solicitudId)
    {
        var solicitud = await CargarAsync(solicitudId);
        if (solicitud is null) return ResultadoOperacion.Error($"La solicitud #{solicitudId} no existe.");

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            return ResultadoOperacion.Error($"La solicitud #{solicitudId} ya fue procesada ({solicitud.Estado}).");

        if (!ReglasCredito.PuedeAprobarse(solicitud.MontoSolicitado, solicitud.Cliente!.IngresosMensuales))
            return ResultadoOperacion.Error(
                $"No se puede aprobar la solicitud #{solicitudId}: el monto {Formato.Soles(solicitud.MontoSolicitado)} excede 5 veces " +
                $"los ingresos mensuales ({Formato.Soles(solicitud.Cliente.IngresosMensuales * ReglasCredito.FactorMaximoAprobacion)}).");

        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;
        return await GuardarAsync(solicitud, $"Solicitud #{solicitudId} aprobada.");
    }

    public async Task<ResultadoOperacion> RechazarAsync(int solicitudId, string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return ResultadoOperacion.Error("El motivo de rechazo es obligatorio.");
        motivo = motivo.Trim();
        if (motivo.Length > 500)
            return ResultadoOperacion.Error("El motivo de rechazo no puede superar 500 caracteres.");

        var solicitud = await CargarAsync(solicitudId);
        if (solicitud is null) return ResultadoOperacion.Error($"La solicitud #{solicitudId} no existe.");

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            return ResultadoOperacion.Error($"La solicitud #{solicitudId} ya fue procesada ({solicitud.Estado}).");

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivo;
        return await GuardarAsync(solicitud, $"Solicitud #{solicitudId} rechazada.");
    }

    public Task<List<SolicitudCredito>> ListarNoEncoladasAsync() =>
        db.Solicitudes.AsNoTracking()
            .Where(s => s.NotificacionMessageId != null && !s.NotificacionEncolada)
            .OrderBy(s => s.Id)
            .ToListAsync();

    /// <summary>
    /// Reenvío manual del evento SolicitudRegistrada con el MISMO MessageId guardado.
    /// El consumidor es idempotente: si ya se procesó, confirma sin duplicar.
    /// </summary>
    public async Task<ResultadoOperacion> ReenviarNotificacionAsync(int solicitudId)
    {
        var solicitud = await CargarAsync(solicitudId);
        if (solicitud is null) return ResultadoOperacion.Error($"La solicitud #{solicitudId} no existe.");

        solicitud.NotificacionMessageId ??= Guid.NewGuid(); // solicitudes semilla sin MessageId
        var mensaje = new MensajeSolicitudRegistrada
        {
            MessageId = solicitud.NotificacionMessageId.Value,
            SolicitudId = solicitud.Id,
            UsuarioId = solicitud.Cliente!.UsuarioId,
            FechaEventoUtc = DateTime.UtcNow
        };
        try
        {
            await publicador.PublicarAsync(mensaje);
            solicitud.NotificacionEncolada = true;
            await db.SaveChangesAsync();
            return ResultadoOperacion.Ok($"Notificación de la solicitud #{solicitudId} reenviada (MessageId {mensaje.MessageId}).");
        }
        catch (Exception ex)
        {
            await db.SaveChangesAsync(); // conserva el MessageId generado
            logger.LogError(ex, "Falló el reenvío de SolicitudId={SolicitudId} MessageId={MessageId}", solicitudId, mensaje.MessageId);
            return ResultadoOperacion.Error($"No se pudo reenviar la notificación: {ex.Message}");
        }
    }

    private Task<SolicitudCredito?> CargarAsync(int id) =>
        db.Solicitudes.Include(s => s.Cliente).FirstOrDefaultAsync(s => s.Id == id);

    private async Task<ResultadoOperacion> GuardarAsync(SolicitudCredito solicitud, string mensaje)
    {
        // 1) Persistir el nuevo estado
        await db.SaveChangesAsync();
        logger.LogInformation("Solicitud {SolicitudId} -> {Estado}", solicitud.Id, solicitud.Estado);

        // 2) Invalidar cache Redis del propietario
        var propietario = solicitud.Cliente!.UsuarioId; // identidad obtenida desde la BD (servidor)
        await cache.InvalidarAsync(propietario);

        // 3) Recién después emitir el evento WebSocket, solo al propietario
        await notificador.NotificarEstadoAsync(propietario, solicitud);

        return ResultadoOperacion.Ok(mensaje, solicitud.Id);
    }
}
