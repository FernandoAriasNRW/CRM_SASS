using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Identity.Application.Abstractions.Queries;
using Identity.Application.Abstractions.Repositories;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Queries;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Identity.Application.Abstractions.Services;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // 1. DbContext
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

        // 2. OutboxService - usa CrmDbContext centralizado
        services.AddScoped<IOutboxService, OutboxService>();

        // 3. UnitOfWork con OutboxService inyectado
        services.AddScoped<IIdentityUnitOfWork, IdentityModuleUnitOfWork>();

        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<ISavedViewRepository, EfSavedViewRepository>();
        services.AddScoped<IEntityPermissionRepository, EfEntityPermissionRepository>();
        services.AddScoped<Identity.Application.Favorites.IFavoriteRepository,
                           Identity.Infrastructure.Persistence.FavoriteRepository>();

        // El puerto que consumen los demás módulos para filtrar por favoritos sin conocer a
        // Identity. Ver IUserFavorites.
        services.AddScoped<BuildingBlocks.Application.Abstractions.IUserFavorites,
                           Identity.Infrastructure.Persistence.UserFavorites>();

        // Y el de visibilidad, para «compartido conmigo» y «privado». Misma razón.
        services.AddScoped<Identity.Application.Sharing.ISharingRepository,
                           Identity.Infrastructure.Persistence.SharingRepository>();
        services.AddScoped<BuildingBlocks.Application.Abstractions.IEntityVisibility,
                           Identity.Infrastructure.Persistence.EntityVisibility>();
        services.AddScoped<BuildingBlocks.Domain.IUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<BuildingBlocks.Application.Authorization.IEntityPermissionService, EntityPermissionService>();
        return services;
    }
}
