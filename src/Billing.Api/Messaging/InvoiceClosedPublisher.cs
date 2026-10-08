using System.Text.Json;
using RabbitMQ.Client;

namespace Billing.Api.Messaging;

public class InvoiceClosedPublisher
{
    public const string QueueName = "invoice.closed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<InvoiceClosedPublisher> _logger;

    public InvoiceClosedPublisher(IConfiguration configuration, ILogger<InvoiceClosedPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublishAsync(InvoiceClosedEvent closedEvent, CancellationToken cancellationToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = _configuration.GetValue("RabbitMQ:Port", 5672),
                UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest"
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(closedEvent, JsonOptions);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                body: body,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish InvoiceClosedEvent for invoice {InvoiceId}. The invoice remains Closed.",
                closedEvent.InvoiceId);
        }
    }
}
