namespace CreditosApp.Models;

public record ResultadoOperacion(bool Exito, string Mensaje, int? SolicitudId = null)
{
    public static ResultadoOperacion Ok(string mensaje, int? id = null) => new(true, mensaje, id);
    public static ResultadoOperacion Error(string mensaje) => new(false, mensaje);
}
