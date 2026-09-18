using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Comments.Application;
using Comments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comments.Infrastructure;

/// <summary>Ata el UnitOfWork del módulo a su propio DbContext.</summary>
public sealed class CommentsModuleUnitOfWork(CommentsDbContext context, IOutboxService outboxService)
    : UnitOfWork<CommentsDbContext>(context, outboxService), ICommentsUnitOfWork
{
}
