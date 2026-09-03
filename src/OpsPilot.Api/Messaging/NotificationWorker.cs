using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Data;
using OpsPilot.Api.Domain;

namespace OpsPilot.Api.Messaging;

public sealed class NotificationWorker(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMq:HostName"] ?? "localhost"
        };

        await using var connection =
            await factory.CreateConnectionAsync(stoppingToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: "opspilot.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: "opspilot.notifications",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: "opspilot.notifications",
            exchange: "opspilot.events",
            routingKey: "#",
            cancellationToken: stoppingToken);

        var consumer =
            new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var messageIdText =
                eventArgs.BasicProperties.MessageId;

            if (!Guid.TryParse(messageIdText, out var messageId))
            {
                logger.LogWarning(
                "Received RabbitMQ message without a valid MessageId.");

                await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: stoppingToken);

                return;
            }

            using var scope =
                scopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<OpsPilotDbContext>();

            var alreadyProcessed =
                await dbContext.ProcessedMessages
                    .AnyAsync(
                        message => message.MessageId == messageId,
                        stoppingToken);

            if (alreadyProcessed)
            {
                logger.LogInformation(
                "Skipping duplicate message {MessageId}.",
                messageId);

                await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken);

                return;
            }

            logger.LogInformation(
                "Notification worker received message {MessageId} of type {EventType}.",
                messageId,
                eventArgs.BasicProperties.Type);
            dbContext.ProcessedMessages.Add(
                new ProcessedMessage
                {
                    MessageId = messageId,
                    ProcessedAtUtc = DateTime.UtcNow
                });

            await dbContext.SaveChangesAsync(
                stoppingToken);

            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: "opspilot.notifications",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }
}