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
      e.Property(c => c.EntityType).HasMaxLength(30).IsRequired();

      // Sin longitud sería TEXT, y MySQL no admite DEFAULT en TEXT (error 1101). Cinco mil
      // caracteres dan de sobra para un comentario y dejan la columna indexable si hiciera falta.
      e.Property(c => c.Text).HasMaxLength(Comment.MaxLength).IsRequired();

      // Es la consulta del hilo, y ocurre cada vez que se abre un detalle.
      e.HasIndex(c => new { c.TenantId, c.EntityType, c.EntityId, c.CreatedAtUtc })
       .HasDatabaseName("IX_Comments_TenantId_EntityType_EntityId_CreatedAtUtc");
    });

    ApplyTenantFilters(modelBuilder);
  }
}
