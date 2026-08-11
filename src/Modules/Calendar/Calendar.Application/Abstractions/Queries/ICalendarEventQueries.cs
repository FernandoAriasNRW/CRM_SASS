using BuildingBlocks.Domain;
using Calendar.Application.DTOs;

namespace Calendar.Application.Abstractions.Queries;

/// <summary>
/// Query Service para operaciones de lectura del módulo Calendar.
/// Separación CQRS: Este servicio SOLO retorna DTOs, nunca entidades de dominio.
/// </summary>
public interface ICalendarEventQueries
{
    /// <summary>
    /// Obtiene un evento de calendario por su ID.
    /// </summary>
    Task<CalendarEventDto?> GetByIdAsync(Guid tenantId, Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene eventos de calendario con paginación y filtros.
    /// </summary>
    Task<PagedResult<CalendarEventDto>> GetByTenantAsync(
        Guid tenantId,
        DateTime? startDate,
        DateTime? endDate,
        string? type,
        PaginationRequest pagination,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene eventos eliminados (soft deleted) para un tenant.
    /// Usado para recuperación o auditoría.
    /// </summary>
    Task<PagedResult<CalendarEventDto>> GetDeletedByTenantAsync(
        Guid tenantId,
        PaginationRequest pagination,
        CancellationToken ct = default);
}