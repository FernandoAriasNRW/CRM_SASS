using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using CustomFields.Application.DTOs;

namespace CustomFields.Application.Commands;

public sealed record DefineCustomFieldCommand(
    Guid TenantId,
    string Name,
    string Type,
    string TargetEntity,
    bool IsRequired,
    IReadOnlyList<string>? Options,
    int Position,
    /// <summary>Sólo para los campos de tipo Formula; en los demás se ignora.</summary>
    string? Formula = null
) : ICommand<CustomFieldDefinitionDto>;

public sealed record UpdateCustomFieldCommand(
    Guid TenantId,
    Guid Id,
    string Name,
    bool IsRequired,
    IReadOnlyList<string>? Options,
    int Position,
    string? Formula = null
) : ICommand<bool>;

public sealed record RemoveCustomFieldCommand(
    Guid TenantId,
    Guid Id
) : ICommand<bool>;

/// <summary>
/// Guarda el valor de un campo para una entidad. Sin valor, se borra el que hubiera.
/// </summary>
public sealed record SetCustomFieldValueCommand(
    Guid TenantId,
    Guid DefinitionId,
    Guid EntityId,
    string? Value
) : ICommand<bool>;
