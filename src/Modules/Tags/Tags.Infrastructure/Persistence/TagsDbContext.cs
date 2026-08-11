using Microsoft.EntityFrameworkCore;
using Tags.Domain.Entities;

namespace Tags.Infrastructure.Persistence;

public class TagsDbContext : DbContext
{
    public TagsDbContext(DbContextOptions<TagsDbContext> options) : base(options) { }

    public DbSet<Tag> Tags { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TagsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
