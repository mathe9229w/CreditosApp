using CreditosApp.Data;
using CreditosApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Services;

/// <summary>Reglas del panel Analista. Todas las validaciones se hacen en servidor.</summary>
public class EvaluacionService(
    ApplicationDbContext db,
    ICacheSolicitudes cache,
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

    private Task<SolicitudCredito?> CargarAsync(int id) =>
        db.Solicitudes.Include(s => s.Cliente).FirstOrDefaultAsync(s => s.Id == id);

    private async Task<ResultadoOperacion> GuardarAsync(SolicitudCredito solicitud, string mensaje)
    {
        // 1) Persistir el nuevo estado
        await db.SaveChangesAsync();
        logger.LogInformation("Solicitud {SolicitudId} -> {Estado}", solicitud.Id, solicitud.Estado);

        // 2) Invalidar cache Redis del propietario
        await cache.InvalidarAsync(solicitud.Cliente!.UsuarioId);

        return ResultadoOperacion.Ok(mensaje, solicitud.Id);
    }
}
