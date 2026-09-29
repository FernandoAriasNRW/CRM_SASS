using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

public sealed class GetCatalogHandler(IReportResolver resolutor)
    : IQueryHandler<GetCatalogQuery, CatalogDto>
{
    public Task<Result<CatalogDto>> Handle(GetCatalogQuery request, CancellationToken ct)
    {
        var catalog = new CatalogDto(
            DataSources: ReportCatalog.DataSources()
                .Select(o => new DataSourceDto(
                    o.Key, o.Name,
                    o.Fields.Select(c => new FieldDto(
                        c.Key, c.Name, c.Type.ToString(),
                        ReportCatalog.OperatorsFor(c).Select(op => op.Key).ToList(),
                        resolutor.ValuesOf(o.Key, c.Key))).ToList(),
                    o.Measures.Select(m => new OptionDto(m.Key, m.Name)).ToList()))
                .ToList(),

            Operators: ReportCatalog.Operators()
                .Select(o => new OperatorDto(
                    o.Key, o.Name, o.Types.Select(t => t.ToString()).ToList(), o.NeedsValue))
                .ToList(),

            Visualizations: ReportCatalog.Visualizations().Select(f => new OptionDto(f.Key, f.Name)).ToList(),

            Granularities: ReportCatalog.DateGranularities()
                .Select(g => new OptionDto(g.Key, g.Name)).ToList());

        return Task.FromResult(Result<CatalogDto>.Success(catalog));
    }
}
