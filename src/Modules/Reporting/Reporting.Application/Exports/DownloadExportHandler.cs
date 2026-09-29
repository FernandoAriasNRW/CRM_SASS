using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

public sealed class DownloadExportHandler(IExportRepository exports)
    : IQueryHandler<DownloadExportQuery, ExportedFile>
{
    public async Task<Result<ExportedFile>> Handle(DownloadExportQuery request, CancellationToken ct)
    {
        var export = await exports.GetByIdAsync(request.TenantId, request.ExportId, ct);
        if (export is null)
            return Result<ExportedFile>.Failure("Esa exportación no existe");

        // Se distingue «todavía no» de «no salió». Son dos respuestas distintas para quien
        // pregunta, y juntarlas en un «no disponible» deja a la pantalla adivinando si merece la
        // pena reintentar.
        if (export.Status != ExportStatus.Ready)
        {
            return Result<ExportedFile>.Failure(
                export.Status == ExportStatus.Failed
                    ? $"La exportación falló: {export.Error}"
                    : $"La exportación todavía no está lista (está {export.Status.Name.ToLowerInvariant()})");
        }

        var content = await exports.ContentAsync(request.TenantId, request.ExportId, ct);

        // Estado «Lista» sin bytes es una incoherencia, no un caso de uso. Se dice tal cual en
        // vez de devolver un fichero vacío, que en el navegador parece un fichero corrupto.
        if (content is null)
            return Result<ExportedFile>.Failure("La exportación consta como lista pero no tiene fichero");

        return Result<ExportedFile>.Success(
            new ExportedFile(export.FileName ?? "informe", content.ContentType, content.Bytes));
    }
}
