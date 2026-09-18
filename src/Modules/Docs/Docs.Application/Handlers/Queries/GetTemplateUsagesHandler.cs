using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Queries;
using MediatR;

namespace Docs.Application.Handlers.Queries;

public class GetTemplateUsagesHandler(IDocumentRepository repository)
    : IRequestHandler<GetTemplateUsagesQuery, Result<List<TemplateUsageDto>>>
{
    public async Task<Result<List<TemplateUsageDto>>> Handle(
        GetTemplateUsagesQuery request, CancellationToken cancellationToken)
    {
        var usages = await repository.GetTemplateUsagesAsync(request.TenantId, cancellationToken);

        return Result<List<TemplateUsageDto>>.Success(
            usages.Select(u => new TemplateUsageDto(u.Key, u.Count, u.LastUsedAtUtc)).ToList());
    }
}
