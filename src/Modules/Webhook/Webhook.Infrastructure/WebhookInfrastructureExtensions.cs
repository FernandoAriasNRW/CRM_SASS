using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Webhook.Application.Abstractions;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Infrastructure.Delivery;
using Webhook.Infrastructure.Persistence;
using Webhook.Infrastructure.Repositories;

namespace Webhook.Infrastructure;

public static class WebhookInfrastructureExtensions
{
    public static IServiceCollection AddWebhookInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WebhookDbContext>(options =>
            options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IWebhookUnitOfWork, WebhookModuleUnitOfWork>();

        services.AddScoped<IWebhookSubscriptionRepository, EfWebhookSubscriptionRepository>();
        services.AddScoped<IWebhookDeliveryRepository, EfWebhookDeliveryRepository>();

        // El envío: opciones, qué direcciones se aceptan, el aviso al trabajo y el propio trabajo.
        services.Configure<WebhookOptions>(configuration.GetSection(WebhookOptions.Section));
        services.AddSingleton<IWebhookUrlPolicy, WebhookUrlPolicy>();
        services.AddSingleton<WebhookDeliverySignal>();
        services.AddSingleton<IWebhookDeliverySignal>(sp => sp.GetRequiredService<WebhookDeliverySignal>());
        services.AddHostedService<WebhookDeliveryWorker>();

        services.AddHttpClient(WebhookHttpClient.Name, client =>
                client.DefaultRequestHeaders.Add("User-Agent", "CRM-SaaS-Webhook/1.0"))
            .ConfigurePrimaryHttpMessageHandler(sp =>
                WebhookHttpClient.CreateHandler(sp.GetRequiredService<IOptions<WebhookOptions>>().Value));

        return services;
    }
}
