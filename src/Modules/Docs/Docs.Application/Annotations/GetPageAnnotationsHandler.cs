using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

public sealed class GetPageAnnotationsHandler(IDocumentRepository repository)
    : IRequestHandler<GetPageAnnotationsQuery, Result<List<AnnotationDto>>>
{
    public async Task<Result<List<AnnotationDto>>> Handle(
        GetPageAnnotationsQuery request, CancellationToken cancellationToken)
    {
        var annotations = await repository.GetPageAnnotationsAsync(request.PageId, cancellationToken);

        return Result<List<AnnotationDto>>.Success(annotations
            .Select(a => new AnnotationDto(
                a.Id, a.DocumentId, a.PageId, a.QuotedText, a.CreatedBy, a.CreatedAtUtc, a.ResolvedAtUtc))
            .ToList());
    }
}
