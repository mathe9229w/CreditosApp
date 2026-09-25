namespace CreditosApp.Models.ViewModels;

/// <summary>DTO serializable (se cachea en Redis en la P4).</summary>
public class SolicitudResumen
{
    public int Id { get; set; }
    public decimal MontoSolicitado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public string? MotivoRechazo { get; set; }
}
