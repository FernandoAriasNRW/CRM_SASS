using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Commands;
using Identity.Application.DTOs;
using Identity.Domain.Entities;

namespace Identity.Application.Handlers.Commands;

public sealed class SaveViewCommandHandler(ISavedViewRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<SaveViewCommand, SavedViewDto>
{
    public async Task<Result<SavedViewDto>> Handle(SaveViewCommand request, CancellationToken cancellationToken)
    {
        var savedView = SavedView.Create(request.UserId, request.TenantId, request.ModuleName, request.ViewName, request.StateJson, request.IsDefault);
        
        await repository.AddAsync(savedView, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SavedViewDto>.Success(new SavedViewDto(
            savedView.Id, savedView.UserId, savedView.TenantId, savedView.ModuleName, savedView.ViewName, savedView.StateJson, savedView.IsDefault));
    }
}

public sealed class DeleteSavedViewCommandHandler(ISavedViewRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteSavedViewCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteSavedViewCommand request, CancellationToken cancellationToken)
    {
        var savedView = await repository.GetByIdAsync(request.TenantId, request.ViewId, cancellationToken);
        if (savedView is null || savedView.UserId != request.UserId)
            return Result<bool>.Failure("View not found or unauthorized.");

        await repository.DeleteAsync(savedView, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
