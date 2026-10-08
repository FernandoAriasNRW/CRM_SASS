using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Comments.Application;
using Comments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comments.Infrastructure;

/// <summary>
/// Ata el UnitOfWork del módulo a su propio DbContext. Pide el repartidor de eventos: sin él, la
/// clase base guarda y no reparte nada en proceso, en silencio.
/// </summary>
public sealed class CommentsModuleUnitOfWork(CommentsDbContext context, IOutboxService outboxService, TimeProvider timeProvider,
    BuildingBlocks.Infrastructure.DomainEvents.IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<CommentsDbContext>(context, outboxService, timeProvider, domainEventDispatcher), ICommentsUnitOfWork
{
}
