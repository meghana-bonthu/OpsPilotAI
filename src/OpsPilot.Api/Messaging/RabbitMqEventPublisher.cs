using System.Text;
using RabbitMQ.Client;

namespace OpsPilot.Api.Messaging;

public sealed class RabbitMqEventPublisher(
    IConfiguration configuration)
{
    public async Task PublishAsync(
        Guid messageId,
        string type,
        string payload,
        CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMq:HostName"]
                ?? "localhost"
        };

        await using var connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: "opspilot.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(payload);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId.ToString(),
            Type = type,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: "opspilot.events",
            routingKey: type,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}