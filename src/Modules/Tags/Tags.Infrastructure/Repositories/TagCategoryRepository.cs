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

    public Task<CustomTagCategory?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => dbContext.CustomTagCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, cancellationToken);

    // Sin distinguir mayúsculas por la colación de MySQL: «Clientes» y «clientes» son la misma.
    public Task<bool> ExistsAsync(Guid tenantId, string name, Guid? exceptId = null, CancellationToken cancellationToken = default)
    {
        var except = exceptId ?? Guid.Empty;
        return dbContext.CustomTagCategories.AnyAsync(
            c => c.TenantId == tenantId && c.Name == name && c.Id != except, cancellationToken);
    }

    public Task<int> CountTagsAsync(Guid tenantId, string categoryName, CancellationToken cancellationToken = default)
        => dbContext.Tags.CountAsync(t => t.TenantId == tenantId && t.Category == categoryName, cancellationToken);

    public async Task RenameAsync(CustomTagCategory category, string newName, CancellationToken cancellationToken = default)
    {
        var oldName = category.Name;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Tags
            .Where(t => t.TenantId == category.TenantId && t.Category == oldName)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.Category, newName.Trim()), cancellationToken);

        category.Rename(newName);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RemoveAsync(CustomTagCategory category, CancellationToken cancellationToken = default)
    {
        dbContext.CustomTagCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
