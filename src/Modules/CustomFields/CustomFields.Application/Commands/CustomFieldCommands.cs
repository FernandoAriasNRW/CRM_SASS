using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using CustomFields.Application.DTOs;

namespace CustomFields.Application.Commands;

public sealed record DefineCustomFieldCommand(
    Guid TenantId,
    string Nombre,
    string Tipo,
    string EntidadDestino,
    bool Obligatorio,
    IReadOnlyList<string>? Opciones,
    int Posicion
) : ICommand<CustomFieldDefinitionDto>;

public sealed record UpdateCustomFieldCommand(
    Guid TenantId,
    Guid Id,
    string Nombre,
    bool Obligatorio,
    IReadOnlyList<string>? Opciones,
    int Posicion
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
    string? Valor
) : ICommand<bool>;
