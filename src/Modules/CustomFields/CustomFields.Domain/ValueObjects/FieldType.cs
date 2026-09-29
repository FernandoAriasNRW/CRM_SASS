namespace CustomFields.Domain.ValueObjects;

/// <summary>
/// Los tipos que puede tener un campo personalizado.
///
/// La fórmula estuvo fuera de esta lista a propósito mientras no hubo motor detrás, porque un
/// tipo que se puede elegir y no calcula nada es peor que no ofrecerlo. Ya lo hay:
/// <see cref="Services.FormulaParser"/> lee la expresión,
/// <see cref="Services.FormulaEvaluator"/> la calcula y
/// <see cref="Services.FormulaCycleDetector"/> impide que se refiera a sí misma.
/// </summary>
public static class FieldType
{
    public const string Text = "Text";
    public const string Number = "Number";
    public const string Date = "Date";
    public const string Select = "Select";
    public const string MultiSelect = "MultiSelect";
    public const string User = "User";

    /// <summary>
    /// Un campo que no se rellena: se calcula a partir de otros.
    ///
    /// **Su valor no se guarda.** Se calcula al leerlo, cada vez. Guardarlo obligaría a
    /// recalcular en cascada cada vez que cambia cualquier campo del que dependa —y a acertar
    /// siempre, porque un valor guardado que se quedó atrás no se distingue de uno correcto—.
    /// Calcularlo al vuelo cuesta una consulta que ya se está haciendo y no puede quedar
    /// desfasado nunca.
    ///
    /// El precio, y hay que saberlo: **no se puede ordenar ni filtrar por un campo calculado en
    /// la base de datos**, porque allí no hay ninguna columna con ese valor. El día que haga
    /// falta, se resuelve guardándolo además de calcularlo, no en vez de.
    /// </summary>
    public const string Formula = "Formula";

    public static IReadOnlyList<string> All() =>
        [Text, Number, Date, Select, MultiSelect, User, Formula];

    public static bool Exists(string type) => All().Contains(type);

    /// <summary>Los tipos que se definen con una lista de opciones.</summary>
    public static bool UsesOptions(string type) => type is Select or MultiSelect;

    /// <summary>Los que se calculan solos y por tanto no se rellenan a mano.</summary>
    public static bool IsComputed(string type) => type == Formula;

    /// <summary>
    /// Los que una fórmula puede usar como número.
    ///
    /// Sólo <see cref="Number"/> y otras fórmulas. Se descartó dejar que un texto que «parece un
    /// número» cuente: haría que la fórmula funcionara o no según lo que alguien tecleara ese
    /// día en un campo de texto libre, y el fallo aparecería como un hueco sin explicación.
    /// </summary>
    public static bool UsableInFormula(string type) => type is Number or Formula;
}
