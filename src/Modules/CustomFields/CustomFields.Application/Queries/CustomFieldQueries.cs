using BuildingBlocks.Application.Abstractions;
using CustomFields.Application.DTOs;

namespace CustomFields.Application.Queries;

public sealed record GetCustomFieldsQuery(
    Guid TenantId,
    string? TargetEntity
) : IQuery<IReadOnlyList<CustomFieldDefinitionDto>>;

public sealed record GetCustomFieldValuesQuery(
    Guid TenantId,
    string TargetEntity,
    Guid EntityId
) : IQuery<IReadOnlyList<CustomFieldValueDto>>;
