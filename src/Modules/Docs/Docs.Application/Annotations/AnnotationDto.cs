using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

/// <summary>Una anotación tal como la lee la pantalla. El hilo se pide aparte, a Comments.</summary>
public sealed record AnnotationDto(
    Guid Id,
    Guid DocumentId,
    Guid PageId,
    string QuotedText,
    Guid CreatedBy,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);
