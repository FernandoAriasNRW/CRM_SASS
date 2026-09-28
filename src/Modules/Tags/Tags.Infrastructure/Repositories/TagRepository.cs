using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions.Repositories;
using Tags.Domain.Entities;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure.Repositories;

internal sealed class TagRepository : ITagRepository
{
    private readonly TagsDbContext _dbContext;

    public TagRepository(TagsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        await _dbContext.Tags.AddAsync(tag, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
        => _dbContext.Tags.AnyAsync(t => t.TenantId == tenantId && t.Name == name, cancellationToken);
}
