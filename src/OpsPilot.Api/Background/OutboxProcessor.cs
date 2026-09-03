using Microsoft.EntityFrameworkCore;
using OpsPilot.Api.Data;

namespace OpsPilot.Api.Background;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
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
                    logger.LogInformation(
                        "Processing outbox message {MessageId} of type {MessageType}.",
                        message.Id,
                        message.Type);
                }
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