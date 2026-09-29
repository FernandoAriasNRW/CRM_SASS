namespace Reporting.Domain.Definitions;

/// <param name="Optional">
/// Si el campo puede no tener valor. Decide si «está vacío» tiene sentido sobre él: una fecha de
/// resolución puede faltar —el ticket sigue abierto—, una de vencimiento no.
/// </param>
public sealed record AvailableField(string Key, string Name, FieldType Type, bool Optional = false);
