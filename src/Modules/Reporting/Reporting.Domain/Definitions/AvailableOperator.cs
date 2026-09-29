namespace Reporting.Domain.Definitions;

public sealed record AvailableOperator(string Key, string Name, IReadOnlyList<FieldType> Types)
{
    public bool AppliesTo(FieldType type) => Types.Contains(type);

    /// <summary>Si el operador necesita un valor. «Está vacío» no lo necesita.</summary>
    public bool NeedsValue => !ChecksEmptiness;

    /// <summary>Si el operador pregunta por la ausencia de valor, y por tanto sólo vale en campos opcionales.</summary>
    public bool ChecksEmptiness => Key is "vacio" or "no_vacio";
}
