using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [StringLength(500)]
    public string? MotivoRechazo { get; set; }

    /// <summary>MessageId (UUID) del evento SolicitudRegistrada. Se reutiliza en reenvíos manuales.</summary>
    public Guid? NotificacionMessageId { get; set; }

    /// <summary>true cuando el broker confirmó (publisher confirm) la publicación.</summary>
    public bool NotificacionEncolada { get; set; }
}
