using BuildingBlocks.Application.Events;
using MediatR;
using Tags.Application.Abstractions.Repositories;
using Tags.Domain.Entities;
using Tags.Domain.ValueObjects;
using Teams.Domain.Events;

namespace Tags.Application.Handlers.Events;

public class TeamCreatedEventHandler : INotificationHandler<DomainEventNotification<TeamCreatedEvent>>
{
    private readonly ITagRepository _tagRepository;

    public TeamCreatedEventHandler(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task Handle(DomainEventNotification<TeamCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var eventData = notification.DomainEvent;
        var colorHex = "#" + new Random().Next(0x1000000).ToString("X6"); // Random color
        
        var tag = Tag.Create(
            tenantId: eventData.TenantId,
            name: eventData.Name,
            colorHex: colorHex,
            category: TagCategory.Team,
            externalReferenceId: eventData.TeamId
        );

        await _tagRepository.AddAsync(tag, cancellationToken);
    }
}
