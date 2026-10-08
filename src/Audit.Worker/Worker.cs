using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Audit.Worker;

public class Worker : BackgroundService
{
    public const string QueueName = "invoice.closed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;

    public Worker(IConfiguration configuration, ILogger<Worker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = _configuration.GetValue("RabbitMQ:Port", 5672),
            UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        IConnection? connection = null;
        IChannel? channel = null;

        try
        {
            connection = await factory.CreateConnectionAsync(stoppingToken);
            channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(delivery.Body.ToArray());
                    var closedEvent = JsonSerializer.Deserialize<InvoiceClosedEvent>(json, JsonOptions);
                    if (closedEvent is null)
                    {
                        _logger.LogWarning("Received empty InvoiceClosedEvent payload.");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Invoice {InvoiceNumber} closed at {ClosedAt}",
                            closedEvent.InvoiceNumber,
                            closedEvent.ClosedAt);
                    }

                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process InvoiceClosedEvent.");
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                }
            };

            await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
            _logger.LogInformation("Audit.Worker listening on queue {Queue}.", QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Encerramento do host.
        }
        finally
        {
            if (channel is not null)
            {
                await channel.CloseAsync();
            }

            if (connection is not null)
            {
                await connection.CloseAsync();
            }
        }
    }
}
