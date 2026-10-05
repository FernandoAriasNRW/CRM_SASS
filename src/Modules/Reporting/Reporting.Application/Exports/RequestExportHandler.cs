using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

public sealed class RequestExportHandler(
    TimeProvider timeProvider,
    IReportRepository reports,
    IExportRepository exports,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<RequestExportCommand, ExportDto>
{
    public async Task<Result<ExportDto>> Handle(RequestExportCommand request, CancellationToken ct)
    {
        var format = ReportFormat.FromName<ReportFormat>(request.Format);
        if (format is null)
        {
            // Se dice cuáles valen. El mensaje genérico anterior de este módulo —«Invalid report
            // type or format»— dejaba a quien lo recibía sin saber qué cambiar.
            return Result<ExportDto>.Failure(
                $"El formato «{request.Format}» no existe. Los que hay: "
                + string.Join(", ", ReportFormat.All().Select(f => f.Name)));
        }

        var report = await reports.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (report is null)
            return Result<ExportDto>.Failure("El informe no existe");

        // Si ya hay una igual en marcha, se devuelve **esa** en vez de encolar otra.
        //
        // Sin esto, pulsar dos veces el botón genera el mismo fichero dos veces y manda dos
        // avisos. Se compara por formato porque pedir el mismo informe en PDF y en Excel sí son
        // dos trabajos distintos.
        var inProgress = await exports.InProgressAsync(
            request.TenantId, request.ReportId, format.Value, request.RequestedById, ct);

        if (inProgress is not null)
            return Result<ExportDto>.Success(ToDto(inProgress));

        var created = Export.Request(timeProvider.GetUtcNow().UtcDateTime, request.TenantId, request.ReportId, request.RequestedById, format);
        if (created.IsFailure)
            return Result<ExportDto>.Failure(created.Error!);

        await exports.AddAsync(created.Value!, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<ExportDto>.Success(ToDto(created.Value!));
    }

    internal static ExportDto ToDto(Export e) => new(
        e.Id, e.ReportId, e.Format.Name, e.Status.Name,
        e.RequestedAtUtc, e.FinishedAtUtc, e.FileName, e.SizeBytes, e.Error, e.Attempts);
}
