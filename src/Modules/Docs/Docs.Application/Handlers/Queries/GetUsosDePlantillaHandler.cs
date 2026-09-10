using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Queries;
using MediatR;

namespace Docs.Application.Handlers.Queries;

public class GetUsosDePlantillaHandler(IDocumentRepository repository)
    : IRequestHandler<GetUsosDePlantillaQuery, Result<List<UsoDePlantillaDto>>>
{
    public async Task<Result<List<UsoDePlantillaDto>>> Handle(
        GetUsosDePlantillaQuery request, CancellationToken cancellationToken)
    {
        var usos = await repository.GetUsosDePlantillaAsync(request.TenantId, cancellationToken);

        return Result<List<UsoDePlantillaDto>>.Success(
            usos.Select(u => new UsoDePlantillaDto(u.Clave, u.Veces, u.UltimoUsoUtc)).ToList());
    }
}
