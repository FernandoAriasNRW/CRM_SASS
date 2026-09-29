using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.BuiltIn;
using Tags.Domain.Entities;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure.Repositories;

internal sealed class TagRepository(TagsDbContext dbContext) : ITagRepository
{
    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        await dbContext.Tags.AddAsync(tag, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Tag?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => dbContext.Tags.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, cancellationToken);

    public async Task RemoveAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        dbContext.Tags.Remove(tag);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public Task<bool> ExistsByNameAsync(Guid tenantId, string category, string name, CancellationToken cancellationToken = default)
        => dbContext.Tags.AnyAsync(t => t.TenantId == tenantId && t.Category == category && t.Name == name, cancellationToken);

    public async Task<bool> NameIsTakenAsync(
        Guid tenantId, string category, string name, Tag? exceptTag, CancellationToken cancellationToken = default)
    {
        var exceptId = exceptTag?.Id ?? Guid.Empty;

        if (await dbContext.Tags.AnyAsync(
                t => t.TenantId == tenantId && t.Category == category && t.Name == name && t.Id != exceptId,
                cancellationToken))
            return true;

        // Una predefinida en la otra lengua sólo choca si la organización la tiene: si la borró,
        // crear «Cliente VIP» a mano es legítimo.
        var builtIn = BuiltInTags.All.FirstOrDefault(b => b.Category == category
            && (string.Equals(b.SpanishName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(b.EnglishName, name, StringComparison.OrdinalIgnoreCase)));

        return builtIn is not null
            && await dbContext.Tags.AnyAsync(
                t => t.TenantId == tenantId && t.BuiltInKey == builtIn.Key && t.Id != exceptId, cancellationToken);
    }
}
