using CreditosApp.Data;
using CreditosApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Mensajeria;

public enum ResultadoProcesamiento { Creada, Duplicado }

public class MensajeInvalidoException(string mensaje) : Exception(mensaje);

/// <summary>Lógica del consumidor (scoped): guarda la Notificación de forma idempotente por MessageId.</summary>
public class ProcesadorNotificaciones(ApplicationDbContext db, ILogger<ProcesadorNotificaciones> logger)
{
    public const string TextoRecepcion = "Recibimos tu solicitud de crédito y está pendiente de evaluación";

    public async Task<ResultadoProcesamiento> ProcesarAsync(MensajeSolicitudRegistrada mensaje, CancellationToken ct)
    {
        // Idempotencia: si el MessageId ya fue procesado se confirma sin insertar.
        if (await db.Notificaciones.AnyAsync(n => n.MessageId == mensaje.MessageId, ct))
        {
            logger.LogWarning("MessageId {MessageId} ya procesado: se confirma sin duplicar", mensaje.MessageId);
            return ResultadoProcesamiento.Duplicado;
        }

        // Integridad: la solicitud debe existir y pertenecer al usuario indicado.
        var propietario = await db.Solicitudes
            .Where(s => s.Id == mensaje.SolicitudId)
            .Select(s => s.Cliente!.UsuarioId)
            .FirstOrDefaultAsync(ct);
        if (propietario is null)
            throw new MensajeInvalidoException($"La solicitud {mensaje.SolicitudId} no existe");
        if (propietario != mensaje.UsuarioId)
            throw new MensajeInvalidoException($"UsuarioId no corresponde al propietario de la solicitud {mensaje.SolicitudId}");

        db.Notificaciones.Add(new Notificacion
        {
            MessageId = mensaje.MessageId,
            SolicitudId = mensaje.SolicitudId,
            UsuarioId = mensaje.UsuarioId,
            Texto = TextoRecepcion,
            FechaProcesamientoUtc = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Carrera con otra entrega del mismo mensaje: el índice único lo impidió.
            if (await db.Notificaciones.AsNoTracking().AnyAsync(n => n.MessageId == mensaje.MessageId, ct))
            {
                logger.LogWarning("MessageId {MessageId} insertado en paralelo: se confirma sin duplicar", mensaje.MessageId);
                return ResultadoProcesamiento.Duplicado;
            }
            throw;
        }

        logger.LogInformation("Notificación creada para SolicitudId={SolicitudId} MessageId={MessageId}", mensaje.SolicitudId, mensaje.MessageId);
        return ResultadoProcesamiento.Creada;
    }
}
