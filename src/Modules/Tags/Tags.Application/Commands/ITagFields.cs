namespace Tags.Application.Commands;

/// <summary>Lo que se rellena al crear o editar una etiqueta, para validarlo con las mismas reglas.</summary>
public interface ITagFields
{
    Guid TenantId { get; }
    string Name { get; }
    string? ColorHex { get; }
    string Category { get; }
}
