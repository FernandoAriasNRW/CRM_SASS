using BuildingBlocks.Application.Abstractions;

namespace Reporting.Application.Tags;

/// <summary>
/// Cambia todas las etiquetas de un panel. Aparte de la edición general (<c>PUT /dashboards/{id}</c>),
/// que manda el panel entero: para cambiar sólo las etiquetas habría que reenviar también los
/// recuadros, y si otra persona los cambió mientras tanto se pisarían.
/// </summary>
/// <param name="IsAdmin">Lo pone el endpoint desde la sesión. Puede quien creó el panel o un administrador.</param>
public sealed record SetDashboardTagsCommand(Guid TenantId, Guid UserId, bool IsAdmin, Guid DashboardId, IReadOnlyList<Guid> TagIds)
    : ICommand<bool>;
