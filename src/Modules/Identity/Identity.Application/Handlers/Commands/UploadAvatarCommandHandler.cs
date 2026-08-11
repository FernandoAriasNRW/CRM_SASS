using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Commands;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Handlers.Commands;

public sealed class UploadAvatarCommandHandler : ICommandHandler<UploadAvatarCommand, string>
{
    private readonly IUserRepository _userRepository;
    private readonly IStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;

    public UploadAvatarCommandHandler(IUserRepository userRepository, IStorageService storageService, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _storageService = storageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<string>> Handle(UploadAvatarCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, false, ct);
        if (user is null)
            return Result<string>.Failure("User not found");

        var extension = Path.GetExtension(request.FileName);
        var fileName = $"avatars/{user.Id}{extension}";

        try
        {
            var avatarUrl = await _storageService.UploadFileAsync(request.Content, fileName, request.ContentType, ct);
            
            user.UpdateAvatarUrl(avatarUrl);
            await _userRepository.UpdateAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            
            return Result<string>.Success(avatarUrl);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Error uploading avatar: {ex.Message}");
        }
    }
}
