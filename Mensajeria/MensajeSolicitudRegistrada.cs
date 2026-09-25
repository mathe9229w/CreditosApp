namespace CreditosApp.Mensajeria;

/// <summary>Mensaje JSON publicado en la cola solicitudes.notificaciones.</summary>
public record MensajeSolicitudRegistrada
{
    public const string TipoEvento = "SolicitudRegistrada";

    public string Tipo { get; init; } = TipoEvento;
    public Guid MessageId { get; init; }
    public int SolicitudId { get; init; }
    public string UsuarioId { get; init; } = string.Empty;
    public DateTime FechaEventoUtc { get; init; }

    public bool EsValido(out string motivo)
    {
        if (Tipo != TipoEvento) { motivo = $"Tipo desconocido '{Tipo}'"; return false; }
        if (MessageId == Guid.Empty) { motivo = "MessageId vacío"; return false; }
        if (SolicitudId <= 0) { motivo = "SolicitudId inválido"; return false; }
        if (string.IsNullOrWhiteSpace(UsuarioId)) { motivo = "UsuarioId vacío"; return false; }
        if (FechaEventoUtc == default) { motivo = "FechaEventoUtc vacía"; return false; }
        motivo = string.Empty;
        return true;
    }
}
