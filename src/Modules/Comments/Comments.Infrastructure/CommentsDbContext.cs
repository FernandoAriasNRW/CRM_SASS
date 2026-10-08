using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Comments.Application;
using Comments.Domain.Entities;
using Comments.Domain.Mentions;
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

      // Las menciones, en su propia tabla y propiedad del comentario: se reescriben al editar y
      // se van con él al borrarlo.
      e.OwnsMany(c => c.Mentions, m =>
      {
        m.ToTable("CommentMentions");
        m.WithOwner().HasForeignKey("CommentId");
        m.HasKey("CommentId", nameof(CommentMention.Type), nameof(CommentMention.EntityId));
        m.Property(x => x.Type).HasMaxLength(20).IsRequired();
        m.Property(x => x.Label).HasMaxLength(200).IsRequired();

        // Por aquí pregunta una tarea, un ticket o un documento qué comentarios lo mencionan.
        m.HasIndex(x => new { x.Type, x.EntityId }).HasDatabaseName("IX_CommentMentions_Type_EntityId");
      });
      e.Navigation(c => c.Mentions).UsePropertyAccessMode(PropertyAccessMode.Field);
    });

    ApplyTenantFilters(modelBuilder);
  }
}
