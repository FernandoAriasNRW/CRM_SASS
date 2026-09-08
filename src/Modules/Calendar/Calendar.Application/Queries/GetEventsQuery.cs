using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Calendar.Application.DTOs;

namespace Calendar.Application.Queries;

public sealed record GetEventsQuery(
    Guid TenantId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Type,
    PaginationRequest Pagination
) : IQuery<PagedResult<CalendarEventDto>>;

/// <summary>
/// Los eventos que están en la papelera.
///
/// La consulta existía en el repositorio —<c>GetDeletedByTenantAsync</c>— desde el principio y
/// <b>no la exponía nadie</b>: se podía mandar un evento a la papelera y no había forma de verlo
/// ni de recuperarlo. Borrar sin poder deshacer es borrar del todo, aunque la fila siga ahí.
/// </summary>
public sealed record GetEventosEnPapeleraQuery(
    Guid TenantId,
    PaginationRequest Pagination
) : IQuery<PagedResult<CalendarEventDto>>;
