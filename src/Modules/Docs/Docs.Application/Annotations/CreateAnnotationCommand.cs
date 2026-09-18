using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

public sealed record CreateAnnotationCommand(
    Guid TenantId, Guid DocumentId, Guid PageId, Guid CreatedBy, string QuotedText)
    : IRequest<Result<Guid>>;
