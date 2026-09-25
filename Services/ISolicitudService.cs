using CreditosApp.Models;
using CreditosApp.Models.ViewModels;

namespace CreditosApp.Services;

public interface ISolicitudService
{
    Task<Cliente?> ObtenerClienteAsync(string usuarioId);

    /// <summary>Solicitudes del usuario autenticado.</summary>
    Task<IReadOnlyList<SolicitudResumen>> ListarDelUsuarioAsync(string usuarioId);

    /// <summary>Detalle de una solicitud, solo si pertenece al usuario.</summary>
    Task<SolicitudCredito?> ObtenerDelUsuarioAsync(int solicitudId, string usuarioId);
}
