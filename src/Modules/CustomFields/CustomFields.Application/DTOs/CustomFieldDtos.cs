namespace CustomFields.Application.DTOs;

public sealed record CustomFieldDefinitionDto(
    Guid Id,
    string Nombre,
    string Tipo,
    string EntidadDestino,
    bool Obligatorio,
    IReadOnlyList<string> Opciones,
    int Posicion
);

/// <summary>
/// El valor de un campo para una entidad, con lo justo de la definición para poder pintarlo sin
/// una segunda consulta.
/// </summary>
public sealed record CustomFieldValueDto(
    Guid DefinitionId,
    string Nombre,
    string Tipo,
    bool Obligatorio,
    IReadOnlyList<string> Opciones,
    int Posicion,
    string? Valor
);
