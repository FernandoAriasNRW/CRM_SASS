using Tags.Domain.Entities;

namespace Tags.Application.Abstractions.Repositories;

/// <summary>Las categorías personalizadas; las predefinidas viven en código (<c>TagCategory</c>).</summary>
public interface ITagCategoryRepository
{
    Task AddAsync(CustomTagCategory category, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
}
