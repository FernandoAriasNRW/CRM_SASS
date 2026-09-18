using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed record EditCommentCommand(
    Guid TenantId, Guid Id, Guid EditedBy, string Text) : ICommand<bool>;
