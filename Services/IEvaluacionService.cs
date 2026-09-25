using CreditosApp.Models;

namespace CreditosApp.Services;

public interface IEvaluacionService
{
    Task<List<SolicitudCredito>> ListarPendientesAsync();
    Task<ResultadoOperacion> AprobarAsync(int solicitudId);
    Task<ResultadoOperacion> RechazarAsync(int solicitudId, string? motivo);
    Task<List<SolicitudCredito>> ListarNoEncoladasAsync();
    Task<ResultadoOperacion> ReenviarNotificacionAsync(int solicitudId);
}
