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
/// motivo que <see cref="ApiHost.Services.AutomationsBridge"/>.
///
/// La alternativa habría sido escuchar los eventos de integración de `BuildingBlocks.Contracts`,
/// que ya declaran un `ProjectCreatedEvent` con estos mismos campos. Se descartó porque **nadie
/// los publica ni los consume**: están declarados y sin usar. Apoyarse en ellos habría exigido
/// construir antes el camino de publicación, y habría convertido un arreglo de aislamiento en un
/// proyecto aparte. Queda anotado como la evolución natural de esto.
/// </summary>
public sealed class AutomaticTags(ITagRepository tags)
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
    private static string RandomColor() => "#" + Random.Shared.Next(0x1000000).ToString("X6");

    public Task Handle(DomainEventNotification<ProjectCreatedEvent> notification, CancellationToken ct)
    {
        var domainEvent = notification.DomainEvent;
        return CreateIfMissingAsync(domainEvent.TenantId, domainEvent.Name, TagCategory.Project, domainEvent.ProjectId, ct);
    }

    public Task Handle(DomainEventNotification<TeamCreatedEvent> notification, CancellationToken ct)
    {
        var domainEvent = notification.DomainEvent;
        return CreateIfMissingAsync(domainEvent.TenantId, domainEvent.Name, TagCategory.Team, domainEvent.TeamId, ct);
    }

    /// <summary>
    /// Dos proyectos con el mismo nombre no pueden tener dos etiquetas iguales: el índice único
    /// (inquilino, categoría, nombre) lo impide, y chocar contra él lanzaría una excepción en mitad
    /// del alta del proyecto por culpa de una etiqueta. El segundo se queda sin la suya.
    /// </summary>
    private async Task CreateIfMissingAsync(Guid tenantId, string name, string category, Guid referenceId, CancellationToken ct)
    {
        if (await tags.ExistsByNameAsync(tenantId, category, name, ct))
            return;

        await tags.AddAsync(Tag.Create(
            tenantId: tenantId,
            name: name,
            colorHex: RandomColor(),
            category: category,
            externalReferenceId: referenceId), ct);
    }
}
