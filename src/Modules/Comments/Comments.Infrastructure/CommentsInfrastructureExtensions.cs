using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Comments.Application;
using Comments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comments.Infrastructure;

public static class CommentsInfrastructureExtensions
{
  public static IServiceCollection AddCommentsInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    services.AddDbContext<CommentsDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"),
                         ServerVersion.Parse("8.0.32-mysql")));

    services.AddScoped<IOutboxService, OutboxService>();
    services.AddScoped<ICommentsUnitOfWork, CommentsModuleUnitOfWork>();
    services.AddScoped<ICommentRepository, EfCommentRepository>();

    return services;
  }
}
