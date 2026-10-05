using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Schedules;

public sealed class ScheduleReportHandler(
    TimeProvider timeProvider,
    IReportRepository reports,
    IScheduleRepository schedules,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<ScheduleReportCommand, ScheduleDto>
{
    public async Task<Result<ScheduleDto>> Handle(ScheduleReportCommand request, CancellationToken ct)
    {
        var frequency = ScheduleFrequency.FromName<ScheduleFrequency>(request.Frequency);
        if (frequency is null)
        {
            return Result<ScheduleDto>.Failure(
                $"La frecuencia «{request.Frequency}» no existe. Las que hay: "
                + string.Join(", ", ScheduleFrequency.All().Select(f => f.Name)));
        }

        var format = ReportFormat.FromName<ReportFormat>(request.Format);
        if (format is null)
        {
            return Result<ScheduleDto>.Failure(
                $"El formato «{request.Format}» no existe. Los que hay: "
                + string.Join(", ", ReportFormat.All().Select(f => f.Name)));
        }

        if (!TimeOnly.TryParse(request.Time, out var time))
            return Result<ScheduleDto>.Failure($"«{request.Time}» no es una hora; se espera algo como 08:00");

        var report = await reports.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (report is null)
            return Result<ScheduleDto>.Failure("El informe no existe");

        var created = ReportSchedule.Create(
            timeProvider.GetUtcNow().UtcDateTime,
            request.TenantId, request.ReportId, request.RecipientId, frequency, format, time, request.Day);

        if (created.IsFailure)
            return Result<ScheduleDto>.Failure(created.Error!);

        await schedules.AddAsync(created.Value!, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<ScheduleDto>.Success(ToDto(created.Value!));
    }

    internal static ScheduleDto ToDto(ReportSchedule p) => new(
        p.Id, p.ReportId, p.Frequency.Name, p.Format.Name,
        p.Time.ToString("HH\\:mm"), p.Day, p.IsActive, p.LastGeneratedDay);
}
