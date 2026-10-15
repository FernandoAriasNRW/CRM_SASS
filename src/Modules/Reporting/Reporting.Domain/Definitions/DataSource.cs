namespace Reporting.Domain.Definitions;

/// <summary>Un sitio de donde salen filas, con lo que se puede hacer sobre ellas.</summary>
public sealed record DataSource(
    string Key,
    string Name,
    IReadOnlyList<AvailableField> Fields,
    IReadOnlyList<AvailableMeasure> Measures)
{
    /// <summary>
    /// El campo de esa clave. Un campo personalizado (<c>custom:{id}</c>) vale en cualquier origen
    /// como texto: su nombre real y si existe los pone el motor. Ver
    /// <see cref="ReportCatalog.CustomFieldPrefix"/>.
    /// </summary>
    public AvailableField? Field(string? key)
        => ReportCatalog.CustomFieldId(key) is not null
            ? new AvailableField(key!, "Campo personalizado", FieldType.Text, Optional: true)
            : Fields.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public AvailableMeasure? Measure(string? key)
        => Measures.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
}
