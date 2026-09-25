using System.Globalization;

namespace CreditosApp.Models;

public static class Formato
{
    public static string Soles(decimal monto) => "S/ " + monto.ToString("N2", CultureInfo.InvariantCulture);

    public static string Fecha(DateTime utc) => utc.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";

    public static string BadgeEstado(EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.Aprobado => "bg-success",
        EstadoSolicitud.Rechazado => "bg-danger",
        _ => "bg-warning text-dark"
    };
}
