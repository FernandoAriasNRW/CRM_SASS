using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions.Repositories;
using Tags.Domain.Entities;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure.Repositories;

internal sealed class TagCategoryRepository(TagsDbContext dbContext) : ITagCategoryRepository
{
    public async Task AddAsync(CustomTagCategory category, CancellationToken cancellationToken = default)
    {
        await dbContext.CustomTagCategories.AddAsync(category, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Sin distinguir mayúsculas por la colación de MySQL: «Clientes» y «clientes» son la misma.
    public Task<bool> ExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
        => dbContext.CustomTagCategories.AnyAsync(c => c.TenantId == tenantId && c.Name == name, cancellationToken);
}
