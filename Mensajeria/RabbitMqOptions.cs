namespace CreditosApp.Mensajeria;

/// <summary>Sección "RabbitMq" (variables de entorno RabbitMq__*).</summary>
public class RabbitMqOptions
{
    public const string Seccion = "RabbitMq";

    /// <summary>URI amqps://usuario:clave@host/vhost de CloudAMQP. Nunca se sube al repo.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string QueueName { get; set; } = "solicitudes.notificaciones";

    /// <summary>Cola de mensajes muertos (inválidos o fallidos) para revisión/reenvío manual.</summary>
    public string DeadLetterQueueName { get; set; } = "solicitudes.notificaciones.dlq";

    public bool ConsumerEnabled { get; set; } = true;

    public bool Configurado => !string.IsNullOrWhiteSpace(ConnectionString);
}
