using BuildingBlocks.Domain;
using Communication.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Communication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Communication.Application.Abstractions.Repositories;
using Communication.Infrastructure.Repositories;

namespace Communication.Infrastructure;

public static class CommunicationInfrastructureExtensions
{
    public static IServiceCollection AddCommunicationInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // 1. DbContext
        services.AddDbContext<CommunicationsDbContext>(options =>
            options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

        // 2. Primero OutboxService
        services.AddScoped<IOutboxService, OutboxService>();

        // 3. UnitOfWork con OutboxService inyectado
        services.AddScoped<ICommunicationUnitOfWork, CommunicationModuleUnitOfWork>();

        services.AddScoped<IConversationRepository, EfConversationRepository>();
        services.AddScoped<IMessageRepository, EfMessageRepository>();
        return services;
    }
}
