using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Schedules;

public interface IScheduleRepository
{
    Task<ReportSchedule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ReportSchedule>> ForReportAsync(Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// Todas las activas, <b>de todos los inquilinos</b>.
    ///
    /// El trabajador corre sin petición y por tanto sin inquilino, así que esta consulta cruza el
    /// filtro global a propósito. Es la excepción que <c>TenantDbContext</c> documenta, y está
    /// aquí —en un método con nombre— para que el trabajador no escriba consultas por su cuenta.
    /// </summary>
    Task<IReadOnlyList<ReportSchedule>> ActiveAsync(CancellationToken ct = default);

    Task AddAsync(ReportSchedule schedule, CancellationToken ct = default);

    void Remove(ReportSchedule schedule);

    Task SaveAsync(CancellationToken ct = default);
}
