using BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Webhooks;

/// <summary>
/// Background service that processes pending webhook deliveries. Similar to OutboxDispatcherWorker pattern already in
/// the codebase.
/// </summary>
public sealed class WebhookDispatcherWorker : BackgroundService
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<WebhookDispatcherWorker> _logger;

  public WebhookDispatcherWorker(
      IServiceScopeFactory scopeFactory,
      ILogger<WebhookDispatcherWorker> logger)
  {
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _logger.LogInformation("Webhook dispatcher worker started");

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>();

        // Process deliveries every 10 seconds
        await dispatcher.ProcessDeliveriesAsync(batchSize: 20, stoppingToken);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Webhook dispatcher iteration failed");
      }

      await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    }

    _logger.LogInformation("Webhook dispatcher worker stopped");
  }
}

/// <summary>
/// Extension methods for webhook registration.
/// </summary>
public static class WebhookExtensions
{
  /// <summary>
  /// Adds webhook infrastructure to the service collection.
  /// </summary>
  public static IServiceCollection AddWebhookInfrastructure(this IServiceCollection services)
  {
    services.AddHttpClient("webhook", client =>
    {
      client.DefaultRequestHeaders.Add("User-Agent", "CRM-SaaS-Webhook/1.0");
    });

    services.AddScoped<IWebhookRepository, EfWebhookRepository>();
    //services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();
    services.AddHostedService<WebhookDispatcherWorker>();

    return services;
  }
}