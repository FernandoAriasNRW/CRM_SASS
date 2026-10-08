using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Authorization;
using Docs.Application.Commands;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class DeletePageHandler(
    IDocumentRepository repository, IUserContext userContext, IEntityPermissionService permissions)
    : IRequestHandler<DeletePageCommand, Result>
{
    public async Task<Result> Handle(DeletePageCommand request, CancellationToken cancellationToken)
    {
        var page = await repository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (page == null)
            return Result.Failure("Page not found");

        await permissions.EnsureCanWriteAsync(userContext, page, cancellationToken);

        page.Delete();
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
