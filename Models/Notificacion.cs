using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

/// <summary>Notificación de recepción generada por el consumidor de la cola.</summary>
public class Notificacion
{
    public int Id { get; set; }

    /// <summary>Id del mensaje (UUID). Único: evita duplicados por redelivery o reenvío.</summary>
    public Guid MessageId { get; set; }

    public int SolicitudId { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Texto { get; set; } = string.Empty;

    public DateTime FechaProcesamientoUtc { get; set; }
}
