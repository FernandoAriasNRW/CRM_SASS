using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed record RemoveCommentCommand(
    Guid TenantId, Guid Id, Guid DeletedBy, string Role) : ICommand<bool>;
