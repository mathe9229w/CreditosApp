using RabbitMQ.Client;

namespace CreditosApp.Mensajeria;

public static class TopologiaRabbitMq
{
    public static ConnectionFactory CrearFactory(RabbitMqOptions options, string nombreCliente) => new()
    {
        // amqps:// => TLS habilitado automáticamente (puerto 5671)
        Uri = new Uri(options.ConnectionString),
        ClientProvidedName = nombreCliente,
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true
    };

    /// <summary>
    /// Declara (idempotente) la cola durable principal y su DLQ.
    /// Los mensajes rechazados sin reencolar (inválidos o fallidos) van a la DLQ para reenvío manual.
    /// </summary>
    public static async Task DeclararAsync(IChannel channel, RabbitMqOptions options, CancellationToken ct = default)
    {
        await channel.QueueDeclareAsync(
            queue: options.DeadLetterQueueName,
            durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: options.QueueName,
            durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = options.DeadLetterQueueName
            },
            cancellationToken: ct);
    }
}
