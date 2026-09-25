using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CreditosApp.Mensajeria;

public interface IPublicadorSolicitudes
{
    /// <summary>Publica y espera la confirmación del broker (publisher confirm). Lanza excepción si falla.</summary>
    Task PublicarAsync(MensajeSolicitudRegistrada mensaje, CancellationToken ct = default);
}

/// <summary>Productor: conexión AMQPS compartida, canal con publisher confirms y mensajes persistentes.</summary>
public sealed class PublicadorRabbitMq(IOptions<RabbitMqOptions> options, ILogger<PublicadorRabbitMq> logger)
    : IPublicadorSolicitudes, IAsyncDisposable
{
    private readonly RabbitMqOptions _opt = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublicarAsync(MensajeSolicitudRegistrada mensaje, CancellationToken ct = default)
    {
        if (!_opt.Configurado)
            throw new InvalidOperationException("RabbitMq__ConnectionString no está configurado.");

        await _lock.WaitAsync(ct);
        try
        {
            var channel = await ObtenerCanalAsync(ct);

            var props = new BasicProperties
            {
                Persistent = true, // delivery-mode 2: sobrevive a reinicios del broker
                MessageId = mensaje.MessageId.ToString(),
                Type = MensajeSolicitudRegistrada.TipoEvento,
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(mensaje);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            // Con publisherConfirmationTrackingEnabled=true este await espera el ACK del broker;
            // si el broker responde NACK o basic.return (mandatory) lanza PublishException.
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _opt.QueueName,
                mandatory: true,
                basicProperties: props,
                body: body,
                cancellationToken: timeout.Token);

            logger.LogInformation("Publicado y confirmado por el broker: {Tipo} MessageId={MessageId} SolicitudId={SolicitudId}",
                mensaje.Tipo, mensaje.MessageId, mensaje.SolicitudId);
        }
        catch
        {
            await ReiniciarAsync(); // la próxima publicación abre un canal nuevo
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> ObtenerCanalAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;

        if (_connection is null || !_connection.IsOpen)
        {
            if (_connection is not null) await _connection.DisposeAsync();
            _connection = await TopologiaRabbitMq.CrearFactory(_opt, "CreditosApp-publicador").CreateConnectionAsync(ct);
        }

        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        await TopologiaRabbitMq.DeclararAsync(_channel, _opt, ct);
        return _channel;
    }

    private async Task ReiniciarAsync()
    {
        try
        {
            if (_channel is not null) await _channel.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error cerrando canal del publicador");
        }
        _channel = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
