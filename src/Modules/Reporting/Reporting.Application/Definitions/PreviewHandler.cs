using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

public sealed class PreviewHandler(IReportResolver resolutor)
    : IQueryHandler<PreviewQuery, PreviewDto>
{
    /// <summary>Cuántas filas se enseñan. Suficiente para reconocer el informe, no para leerlo entero.</summary>
    private const int SampleRows = 25;

    public async Task<Result<PreviewDto>> Handle(PreviewQuery request, CancellationToken ct)
    {
        var validacion = request.Definition.Validate();
        if (validacion.IsFailure)
            return Result<PreviewDto>.Failure(validacion.Error!);

        try
        {
            var table = await resolutor.ResolveAsync(request.Title, request.TenantId, request.Definition, ct);

            return Result<PreviewDto>.Success(new PreviewDto(
                table.Title, table.Subtitle, table.Columns,
                table.Rows.Take(SampleRows).ToList(),
                table.Rows.Count));
        }
        catch (InvalidOperationException ex)
        {
            // El motor lanza con un mensaje que dice qué pieza no sabe traducir. Se devuelve tal
            // cual en vez de un «error al generar la vista previa»: en un constructor, saber qué
            // combinación no funciona es la mitad del trabajo.
            return Result<PreviewDto>.Failure(ex.Message);
        }
    }
}
