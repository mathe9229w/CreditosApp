using CreditosApp.Data;
using CreditosApp.Models;
using CreditosApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Services;

public class SolicitudService(ApplicationDbContext db, ILogger<SolicitudService> logger) : ISolicitudService
{
    public Task<Cliente?> ObtenerClienteAsync(string usuarioId) =>
        db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

    public async Task<IReadOnlyList<SolicitudResumen>> ListarDelUsuarioAsync(string usuarioId)
    {
        return await ConsultarDelUsuarioAsync(usuarioId);
    }

    public Task<SolicitudCredito?> ObtenerDelUsuarioAsync(int solicitudId, string usuarioId) =>
        db.Solicitudes
            .AsNoTracking()
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == solicitudId && s.Cliente!.UsuarioId == usuarioId);

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
