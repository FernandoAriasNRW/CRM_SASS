using BuildingBlocks.Application.Events;
using MediatR;
using Projects.Domain.Events;
using Tags.Application.Abstractions.Repositories;
using Tags.Domain.Entities;
using Tags.Domain.ValueObjects;

namespace Tags.Application.Handlers.Events;

public class ProjectCreatedEventHandler : INotificationHandler<DomainEventNotification<ProjectCreatedEvent>>
{
    private readonly ITagRepository _tagRepository;

    public ProjectCreatedEventHandler(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task Handle(DomainEventNotification<ProjectCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var eventData = notification.DomainEvent;
        var colorHex = "#" + new Random().Next(0x1000000).ToString("X6"); // Random color
        
        var tag = Tag.Create(
            tenantId: eventData.TenantId,
            name: eventData.Name,
            colorHex: colorHex,
            category: TagCategory.Project,
            externalReferenceId: eventData.ProjectId
        );

        await _tagRepository.AddAsync(tag, cancellationToken);
    }
}
