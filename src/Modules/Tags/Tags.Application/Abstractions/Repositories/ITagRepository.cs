using Tags.Domain.Entities;

namespace Tags.Application.Abstractions.Repositories;

public interface ITagRepository
{
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);
    Task<List<Tag>> GetAllAsync(CancellationToken cancellationToken = default);
}
