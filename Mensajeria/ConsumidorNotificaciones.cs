using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CreditosApp.Mensajeria;

/// <summary>
/// Consumidor (BackgroundService) de solicitudes.notificaciones con ACK manual:
/// - ACK solo después de guardar la Notificación (o si el MessageId ya existía).
/// - Mensaje inválido: BasicReject sin reencolar (va a la DLQ) + log.
/// - Error de procesamiento: log + NACK. Se reencola una sola vez (si no era redelivery);
///   a la segunda falla va a la DLQ (sin reintentos infinitos) para reenvío manual.
/// </summary>
public class ConsumidorNotificaciones(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<ConsumidorNotificaciones> logger) : BackgroundService
{
    private readonly RabbitMqOptions _opt = options.Value;
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.ConsumerEnabled)
        {
            logger.LogWarning("Consumidor RabbitMQ DESHABILITADO (RabbitMq__ConsumerEnabled=false). Los mensajes quedarán en la cola {Cola}.", _opt.QueueName);
            return;
        }
        if (!_opt.Configurado)
        {
            logger.LogWarning("Consumidor RabbitMQ no iniciado: falta RabbitMq__ConnectionString.");
            return;
        }

        // Reintenta la conexión inicial; luego la librería recupera la conexión automáticamente.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IniciarAsync(stoppingToken);
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "No se pudo conectar el consumidor a RabbitMQ. Reintento en 10 s.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    private async Task IniciarAsync(CancellationToken ct)
    {
        _connection = await TopologiaRabbitMq.CrearFactory(_opt, "CreditosApp-consumidor").CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
        await TopologiaRabbitMq.DeclararAsync(_channel, _opt, ct);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMensajeAsync;

        await _channel.BasicConsumeAsync(queue: _opt.QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);
        logger.LogInformation("Consumidor escuchando la cola {Cola} (ACK manual)", _opt.QueueName);
    }

    private async Task OnMensajeAsync(object sender, BasicDeliverEventArgs ea)
    {
        var channel = ((AsyncEventingBasicConsumer)sender).Channel;
        var cuerpo = Encoding.UTF8.GetString(ea.Body.Span);

        MensajeSolicitudRegistrada? mensaje = null;
        string motivo = "JSON vacío";
        try
        {
            mensaje = JsonSerializer.Deserialize<MensajeSolicitudRegistrada>(cuerpo);
        }
        catch (JsonException ex)
        {
            motivo = "JSON mal formado: " + ex.Message;
        }

        if (mensaje is null || !mensaje.EsValido(out motivo))
        {
            logger.LogWarning("Mensaje INVÁLIDO rechazado sin reencolar (DeliveryTag={Tag}). Motivo: {Motivo}. Cuerpo: {Cuerpo}",
                ea.DeliveryTag, motivo, cuerpo);
            await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var procesador = scope.ServiceProvider.GetRequiredService<ProcesadorNotificaciones>();
            var resultado = await procesador.ProcesarAsync(mensaje, CancellationToken.None);

            // ACK manual SOLO después de guardar (o si ya existía)
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            logger.LogInformation("ACK MessageId={MessageId} ({Resultado})", mensaje.MessageId, resultado);
        }
        catch (MensajeInvalidoException ex)
        {
            logger.LogWarning("Mensaje INVÁLIDO rechazado sin reencolar. MessageId={MessageId}. Motivo: {Motivo}", mensaje.MessageId, ex.Message);
            await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false);
        }
        catch (Exception ex)
        {
            var reencolar = !ea.Redelivered; // un solo reintento, evita bucles infinitos
            logger.LogError(ex, "Error procesando MessageId={MessageId}. NACK requeue={Requeue}. {Accion}",
                mensaje.MessageId, reencolar,
                reencolar ? "Se reintentará una vez." : "Enviado a DLQ: requiere reenvío manual (ver README).");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: reencolar);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            if (_channel is not null) await _channel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error cerrando el consumidor");
        }
    }
}
