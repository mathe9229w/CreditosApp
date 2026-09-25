using CreditosApp.Hubs;
using CreditosApp.Models;
using Microsoft.AspNetCore.SignalR;

namespace CreditosApp.Services;

public record SolicitudEstadoActualizado(int SolicitudId, string Estado, string? MotivoRechazo);

public interface INotificadorSolicitudes
{
    Task NotificarEstadoAsync(string usuarioIdPropietario, SolicitudCredito solicitud);
}

/// <summary>Emite el evento SolicitudEstadoActualizado solo al usuario propietario.</summary>
public class NotificadorSolicitudes(IHubContext<SolicitudesHub> hub, ILogger<NotificadorSolicitudes> logger) : INotificadorSolicitudes
{
    public async Task NotificarEstadoAsync(string usuarioIdPropietario, SolicitudCredito solicitud)
    {
        var evento = new SolicitudEstadoActualizado(solicitud.Id, solicitud.Estado.ToString(), solicitud.MotivoRechazo);
        try
        {
            await hub.Clients.User(usuarioIdPropietario).SendAsync(SolicitudesHub.EventoEstadoActualizado, evento);
            logger.LogInformation("Evento {Evento} enviado a {UsuarioId}: {@Payload}",
                SolicitudesHub.EventoEstadoActualizado, usuarioIdPropietario, evento);
        }
        catch (Exception ex)
        {
            // El estado ya está guardado; el cliente lo recupera al reconectar/recargar.
            logger.LogError(ex, "No se pudo emitir el evento de la solicitud {SolicitudId}", solicitud.Id);
        }
    }
}
