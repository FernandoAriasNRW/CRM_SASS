using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Comments.Application;
using Comments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comments.Infrastructure;

public sealed class CommentsDbContext(DbContextOptions<CommentsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<Comment> Comments => Set<Comment>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Comment>(e =>
    {
      e.ToTable("Comments");
      e.Property(c => c.EntidadDestino).HasMaxLength(30).IsRequired();

      // Sin longitud sería TEXT, y MySQL no admite DEFAULT en TEXT (error 1101). Cinco mil
      // caracteres dan de sobra para un comentario y dejan la columna indexable si hiciera falta.
      e.Property(c => c.Texto).HasMaxLength(Comment.LargoMaximo).IsRequired();

      // Es la consulta del hilo, y ocurre cada vez que se abre un detalle.
      e.HasIndex(c => new { c.TenantId, c.EntidadDestino, c.EntityId, c.CreadoUtc })
       .HasDatabaseName("IX_Comments_Tenant_Entidad_Creado");
    });

    ApplyTenantFilters(modelBuilder);
  }
}

/// <summary>Ata el UnitOfWork del módulo a su propio DbContext.</summary>
public sealed class CommentsModuleUnitOfWork(CommentsDbContext context, IOutboxService outboxService)
    : UnitOfWork<CommentsDbContext>(context, outboxService), ICommentsUnitOfWork
{
}

public sealed class EfCommentRepository(CommentsDbContext context) : ICommentRepository
{
  public async Task<Comment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Comments.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

  public async Task<IReadOnlyList<Comment>> GetHiloAsync(
      Guid tenantId, string entidadDestino, Guid entityId, CancellationToken ct = default)
      => await context.Comments.AsNoTracking()
          .Where(c => c.TenantId == tenantId && c.EntidadDestino == entidadDestino && c.EntityId == entityId)
          .OrderBy(c => c.CreadoUtc)
          .ToListAsync(ct);

  public async Task AddAsync(Comment comentario, CancellationToken ct = default)
      => await context.Comments.AddAsync(comentario, ct);

  public Task UpdateAsync(Comment comentario, CancellationToken ct = default)
  {
    context.Comments.Update(comentario);
    return Task.CompletedTask;
  }

  public Task RemoveAsync(Comment comentario, CancellationToken ct = default)
  {
    context.Comments.Remove(comentario);
    return Task.CompletedTask;
  }
}

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
