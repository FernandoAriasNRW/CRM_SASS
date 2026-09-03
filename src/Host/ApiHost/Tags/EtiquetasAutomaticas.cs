using BuildingBlocks.Application.Events;
using MediatR;
using Projects.Domain.Events;
using Tags.Application.Abstractions.Repositories;
using Tags.Domain.Entities;
using Tags.Domain.ValueObjects;
using Teams.Domain.Events;

namespace ApiHost.Tags;

/// <summary>
/// Crea una etiqueta automáticamente cuando nace un proyecto o un equipo.
///
/// **Vive en el host porque cruza módulos.** Estaba dentro de `Tags.Application`, que para
/// escuchar esos eventos referenciaba `Projects.Domain` y `Teams.Domain`: dos módulos alcanzando
/// a otros dos, exactamente lo que la arquitectura prohíbe. Aquí es legítimo, porque el host es
/// quien conoce a todos y compone lo que ninguno puede componer solo — el mismo sitio y el mismo
/// motivo que <see cref="ApiHost.Services.PuenteDeAutomatizaciones"/>.
///
/// La alternativa habría sido escuchar los eventos de integración de `BuildingBlocks.Contracts`,
/// que ya declaran un `ProjectCreatedEvent` con estos mismos campos. Se descartó porque **nadie
/// los publica ni los consume**: están declarados y sin usar. Apoyarse en ellos habría exigido
/// construir antes el camino de publicación, y habría convertido un arreglo de aislamiento en un
/// proyecto aparte. Queda anotado como la evolución natural de esto.
/// </summary>
public sealed class EtiquetasAutomaticas(ITagRepository etiquetas)
    : INotificationHandler<DomainEventNotification<ProjectCreatedEvent>>,
      INotificationHandler<DomainEventNotification<TeamCreatedEvent>>
{
    /// <summary>
    /// Un color al azar para que la etiqueta nazca distinguible.
    ///
    /// `Random.Shared` y no `new Random()`: el original creaba una instancia nueva en cada
    /// evento, y hasta .NET 6 eso las sembraba con el reloj, así que varios proyectos creados
    /// en el mismo instante —una importación, o el sembrado inicial— salían todos del mismo
    /// color. `Random.Shared` no tiene ese problema y además es seguro entre hilos.
    /// </summary>
    private static string ColorAlAzar() => "#" + Random.Shared.Next(0x1000000).ToString("X6");

    public Task Handle(DomainEventNotification<ProjectCreatedEvent> notificacion, CancellationToken ct)
    {
        var evento = notificacion.DomainEvent;

        return etiquetas.AddAsync(Tag.Create(
            tenantId: evento.TenantId,
            name: evento.Name,
            colorHex: ColorAlAzar(),
            category: TagCategory.Project,
            externalReferenceId: evento.ProjectId), ct);
    }

    public Task Handle(DomainEventNotification<TeamCreatedEvent> notificacion, CancellationToken ct)
    {
        var evento = notificacion.DomainEvent;

        return etiquetas.AddAsync(Tag.Create(
            tenantId: evento.TenantId,
            name: evento.Name,
            colorHex: ColorAlAzar(),
            category: TagCategory.Team,
            externalReferenceId: evento.TeamId), ct);
    }
}
