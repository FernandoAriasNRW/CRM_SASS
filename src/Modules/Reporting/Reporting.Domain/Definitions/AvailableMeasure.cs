namespace Reporting.Domain.Definitions;

/// <summary>
/// Qué se calcula por cada grupo.
///
/// <paramref name="OnField"/> es el campo del que se saca el número; en el conteo es nulo
/// porque contar no necesita ningún campo.
/// </summary>
public sealed record AvailableMeasure(string Key, string Name, string? OnField)
{
    public static readonly AvailableMeasure Count = new("count", "Cuántos hay", null);
}
