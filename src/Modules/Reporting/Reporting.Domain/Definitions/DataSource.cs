namespace Reporting.Domain.Definitions;

/// <summary>Un sitio de donde salen filas, con lo que se puede hacer sobre ellas.</summary>
public sealed record DataSource(
    string Key,
    string Name,
    IReadOnlyList<AvailableField> Fields,
    IReadOnlyList<AvailableMeasure> Measures)
{
    public AvailableField? Field(string? key)
        => Fields.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public AvailableMeasure? Measure(string? key)
        => Measures.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
}
