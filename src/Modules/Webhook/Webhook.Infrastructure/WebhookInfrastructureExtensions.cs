using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Application.Abstractions;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Infrastructure.Persistence;
using Webhook.Infrastructure.Repositories;
using Webhook.Infrastructure.Services;

namespace Webhook.Infrastructure;

public static class WebhookInfrastructureExtensions
{
    public static IServiceCollection AddWebhookInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WebhookDbContext>(options =>
            options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IUnitOfWork>(sp => new UnitOfWork<WebhookDbContext>(
            sp.GetRequiredService<WebhookDbContext>(),
            sp.GetRequiredService<IOutboxService>()));

        services.AddScoped<IWebhookSubscriptionRepository, EfWebhookSubscriptionRepository>();
        services.AddScoped<IWebhookDispatchService, WebhookDispatchService>();

        services.AddHttpClient("webhook", client =>
            client.DefaultRequestHeaders.Add("User-Agent", "CRM-SaaS-Webhook/1.0"));

        return services;
    }
}
