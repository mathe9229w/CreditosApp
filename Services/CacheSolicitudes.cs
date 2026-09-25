using System.Text.Json;
using CreditosApp.Models.ViewModels;
using Microsoft.Extensions.Caching.Distributed;

namespace CreditosApp.Services;

public interface ICacheSolicitudes
{
    Task<List<SolicitudResumen>?> ObtenerAsync(string usuarioId);
    Task GuardarAsync(string usuarioId, List<SolicitudResumen> solicitudes);
    Task InvalidarAsync(string usuarioId);
}

/// <summary>
/// Cache del listado "Mis solicitudes" por usuario en Redis (IDistributedCache), TTL 60 s.
/// Si Redis falla, se registra el error y se continúa con la base de datos.
/// </summary>
public class CacheSolicitudes(IDistributedCache cache, ILogger<CacheSolicitudes> logger) : ICacheSolicitudes
{
    public static readonly TimeSpan Duracion = TimeSpan.FromSeconds(60);

    public static string Clave(string usuarioId) => $"solicitudes:usuario:{usuarioId}";

    public async Task<List<SolicitudResumen>?> ObtenerAsync(string usuarioId)
    {
        try
        {
            var json = await cache.GetStringAsync(Clave(usuarioId));
            if (json is null)
            {
                logger.LogInformation("Cache MISS {Clave}", Clave(usuarioId));
                return null;
            }
            logger.LogInformation("Cache HIT {Clave}", Clave(usuarioId));
            return JsonSerializer.Deserialize<List<SolicitudResumen>>(json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error leyendo cache {Clave}", Clave(usuarioId));
            return null;
        }
    }

    public async Task GuardarAsync(string usuarioId, List<SolicitudResumen> solicitudes)
    {
        try
        {
            await cache.SetStringAsync(Clave(usuarioId), JsonSerializer.Serialize(solicitudes),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Duracion });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error escribiendo cache {Clave}", Clave(usuarioId));
        }
    }

    public async Task InvalidarAsync(string usuarioId)
    {
        try
        {
            await cache.RemoveAsync(Clave(usuarioId));
            logger.LogInformation("Cache invalidada {Clave}", Clave(usuarioId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidando cache {Clave}", Clave(usuarioId));
        }
    }
}
