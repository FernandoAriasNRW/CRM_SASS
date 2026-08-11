using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Application.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IEntityPermissionService permissionService,
    IUserContext userContext,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuthorizeEntity
    where TResponse : class
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var userId = userContext.UserId;
        if (userId == Guid.Empty)
        {
            logger.LogWarning("Unauthorized access attempt. UserId is empty.");
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        }

        bool hasAccess = await permissionService.HasPermissionAsync(
            request.TenantId,
            userId,
            request.EntityType,
            request.EntityId,
            request.RequiredPermission,
            cancellationToken);

        if (!hasAccess)
        {
            logger.LogWarning("Access denied. User {UserId} lacks {RequiredPermission} on {EntityType} {EntityId}.",
                userId, request.RequiredPermission, request.EntityType, request.EntityId);
            
            throw new UnauthorizedAccessException($"No tienes permisos de {request.RequiredPermission} para este recurso.");
        }

        return await next();
    }
}
