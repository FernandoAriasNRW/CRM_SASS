using BuildingBlocks.Application.Abstractions;
using Projects.Domain.Entities;

namespace Projects.Application.Commands;

public sealed record CreateFolderCommand(Guid TenantId, Guid SpaceId, string Name) : ICommand<Folder>;
public sealed record UpdateFolderCommand(Guid TenantId, Guid FolderId, string Name) : ICommand<bool>;
public sealed record DeleteFolderCommand(Guid TenantId, Guid FolderId, Guid DeletedBy) : ICommand<bool>;
