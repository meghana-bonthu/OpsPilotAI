using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Data;
using OpsPilot.Api.Messaging;

namespace OpsPilot.Api.Background;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    RabbitMqEventPublisher eventPublisher,
    ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var dbContext = scope.ServiceProvider
                    .GetRequiredService<OpsPilotDbContext>();

                var pendingMessages = await dbContext.OutboxMessages
                    .Where(message => message.ProcessedAtUtc == null)
                    .OrderBy(message => message.OccurredAtUtc)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var message in pendingMessages)
                {
                    try
                    {
                        await eventPublisher.PublishAsync(
                        message.Id,
                        message.Type,
                        message.Payload,
                        stoppingToken);

                        message.ProcessedAtUtc = DateTime.UtcNow;
                        message.Error = null;

                        logger.LogInformation(
                        "Published outbox message {MessageId} of type {MessageType}.",
                        message.Id,
                        message.Type);
                    }
                    catch (Exception exception)
                    {
                        message.Error = exception.Message.Length > 2000
                        ? exception.Message[..2000]
                        : exception.Message;

                        logger.LogError(
                        exception,
                        "Failed to publish outbox message {MessageId} of type {MessageType}.",
                        message.Id,
                        message.Type);
                    }
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Outbox processing failed.");
            }

            try
            {
                await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
            }
            catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}