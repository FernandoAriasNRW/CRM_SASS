namespace CustomFields.Application.DTOs;

public sealed record CustomFieldDefinitionDto(
    Guid Id,
    string Name,
    string Type,
    string TargetEntity,
    bool IsRequired,
    IReadOnlyList<string> Options,
    int Position,
    /// <summary>La expresión, si es un campo calculado.</summary>
    string? Formula
);

/// <summary>
/// El valor de un campo para una entidad, con lo justo de la definición para poder pintarlo sin
/// una segunda consulta.
/// </summary>
public sealed record CustomFieldValueDto(
    Guid DefinitionId,
    string Name,
    string Type,
    bool IsRequired,
    IReadOnlyList<string> Options,
    int Position,
    string? Value,
    /// <summary>La expresión, si es un campo calculado. La pantalla la enseña como ayuda.</summary>
    string? Formula = null,
    /// <summary>
    /// Por qué este campo calculado no tiene valor, si es el caso. Un hueco por falta de datos
    /// NO llena esto: eso es normal y se distingue de una fórmula rota.
    /// </summary>
    string? Error = null
);
