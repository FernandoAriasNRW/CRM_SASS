using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

public sealed class GetCatalogHandler(IReportResolver resolutor, ICustomFieldReader customFields)
    : IQueryHandler<GetCatalogQuery, CatalogDto>
{
    /// <summary>El tipo de elemento de cada origen, como lo nombran los campos personalizados.</summary>
    private static readonly Dictionary<string, string> EntityTypeOf = new()
    {
        ["Tasks"] = BuildingBlocks.Domain.EntityTypes.Task,
        ["Tickets"] = BuildingBlocks.Domain.EntityTypes.Ticket,
        ["Projects"] = BuildingBlocks.Domain.EntityTypes.Project,
    };

    public async Task<Result<CatalogDto>> Handle(GetCatalogQuery request, CancellationToken ct)
    {
        var dataSources = new List<DataSourceDto>();

        foreach (var o in ReportCatalog.DataSources())
        {
            var fields = o.Fields.Select(c => new FieldDto(
                c.Key, c.Name, c.Type.ToString(),
                ReportCatalog.OperatorsFor(c).Select(op => op.Key).ToList(),
                resolutor.ValuesOf(o.Key, c.Key))).ToList();

            // Los campos personalizados de la organización, para agrupar y filtrar por ellos:
            // «tickets por área», «solo los del área Ventas». Las opciones de un campo de selección
            // van como valores, para que el filtro sea un desplegable y no texto libre.
            if (EntityTypeOf.TryGetValue(o.Key, out var entityType))
            {
                foreach (var field in await customFields.GetDefinitionsAsync(request.TenantId, entityType, ct))
                {
                    var key = ReportCatalog.CustomFieldKey(field.Id);
                    fields.Add(new FieldDto(key, field.Name, "Text",
                        ReportCatalog.OperatorsFor(o.Field(key)!).Select(op => op.Key).ToList(),
                        field.Options));
                }
            }

            dataSources.Add(new DataSourceDto(
                o.Key, o.Name, fields, o.Measures.Select(m => new OptionDto(m.Key, m.Name)).ToList()));
        }

        var catalog = new CatalogDto(
            DataSources: dataSources,

            Operators: ReportCatalog.Operators()
                .Select(o => new OperatorDto(
                    o.Key, o.Name, o.Types.Select(t => t.ToString()).ToList(), o.NeedsValue))
                .ToList(),

            Visualizations: ReportCatalog.Visualizations().Select(f => new OptionDto(f.Key, f.Name)).ToList(),

            Granularities: ReportCatalog.DateGranularities()
                .Select(g => new OptionDto(g.Key, g.Name)).ToList());

        return Result<CatalogDto>.Success(catalog);
    }
}
