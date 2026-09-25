namespace CreditosApp.Models.ViewModels;

public class MisSolicitudesViewModel
{
    public FiltroSolicitudes Filtro { get; set; } = new();
    public IReadOnlyList<SolicitudResumen> Solicitudes { get; set; } = [];
    public bool TieneCliente { get; set; }
}
