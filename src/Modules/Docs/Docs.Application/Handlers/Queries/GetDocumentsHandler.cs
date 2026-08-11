using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Queries;
using MediatR;

namespace Docs.Application.Handlers.Queries;

public class GetDocumentsHandler(IDocumentRepository repository) 
    : IRequestHandler<GetDocumentsQuery, Result<List<DocumentDto>>>
{
    public async Task<Result<List<DocumentDto>>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await repository.GetByTenantAsync(request.TenantId, cancellationToken);
        
        var dtoList = docs.Select(d => new DocumentDto(d.Id, d.Title, d.Description, (int)d.Type, d.OwnerId, d.CreatedAtUtc, d.UpdatedAtUtc)).ToList();

        return Result<List<DocumentDto>>.Success(dtoList);
    }
}
