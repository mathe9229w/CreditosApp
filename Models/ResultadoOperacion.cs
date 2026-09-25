namespace CreditosApp.Models;

public record ResultadoOperacion(bool Exito, string Mensaje, int? SolicitudId = null, string? Advertencia = null)
{
    public static ResultadoOperacion Ok(string mensaje, int? id = null, string? advertencia = null) => new(true, mensaje, id, advertencia);
    public static ResultadoOperacion Error(string mensaje) => new(false, mensaje);
}
