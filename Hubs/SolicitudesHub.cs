using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CreditosApp.Hubs;

/// <summary>
/// Hub en /hubs/solicitudes. Solo usuarios autenticados con Identity (conexión anónima => 401).
/// El navegador NO puede elegir destinatarios: el hub no expone métodos invocables;
/// el servidor envía a Clients.User(usuarioId) usando el usuario dueño obtenido de la BD.
/// </summary>
[Authorize]
public class SolicitudesHub(ILogger<SolicitudesHub> logger) : Hub
{
    public const string Ruta = "/hubs/solicitudes";
    public const string EventoEstadoActualizado = "SolicitudEstadoActualizado";

    public override Task OnConnectedAsync()
    {
        var transporte = Context.Features.Get<Microsoft.AspNetCore.Http.Connections.Features.IHttpTransportFeature>()?.TransportType;
        logger.LogInformation("Hub conectado: usuario {UsuarioId}, conexión {ConnectionId}, transporte {Transporte}",
            Context.UserIdentifier, Context.ConnectionId, transporte);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Hub desconectado: usuario {UsuarioId}, conexión {ConnectionId}", Context.UserIdentifier, Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
