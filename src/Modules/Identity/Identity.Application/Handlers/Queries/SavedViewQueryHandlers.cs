using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.DTOs;
using Identity.Application.Queries;

namespace Identity.Application.Handlers.Queries;

public sealed class GetSavedViewsQueryHandler(ISavedViewRepository repository)
    : IQueryHandler<GetSavedViewsQuery, IReadOnlyList<SavedViewDto>>
{
    public async Task<Result<IReadOnlyList<SavedViewDto>>> Handle(GetSavedViewsQuery request, CancellationToken cancellationToken)
    {
        var views = await repository.GetByUserIdAsync(request.TenantId, request.UserId, request.ModuleName, cancellationToken);
        
        var dtos = views.Select(v => new SavedViewDto(v.Id, v.UserId, v.TenantId, v.ModuleName, v.ViewName, v.StateJson, v.IsDefault)).ToList();
        
        return Result<IReadOnlyList<SavedViewDto>>.Success(dtos);
    }
}
