namespace CustomFields.Domain.ValueObjects;

/// <summary>
/// Los tipos que puede tener un campo personalizado.
///
/// La fórmula estuvo fuera de esta lista a propósito mientras no hubo motor detrás, porque un
/// tipo que se puede elegir y no calcula nada es peor que no ofrecerlo. Ya lo hay:
/// <see cref="Servicios.AnalizadorDeFormula"/> lee la expresión,
/// <see cref="Servicios.EvaluadorDeFormula"/> la calcula y
/// <see cref="Servicios.DetectorDeCiclosDeFormula"/> impide que se refiera a sí misma.
/// </summary>
public static class TipoDeCampo
{
    public const string Texto = "Texto";
    public const string Numero = "Numero";
    public const string Fecha = "Fecha";
    public const string Seleccion = "Seleccion";
    public const string SeleccionMultiple = "SeleccionMultiple";
    public const string Usuario = "Usuario";

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

    public static IReadOnlyList<string> Todos() =>
        [Texto, Numero, Fecha, Seleccion, SeleccionMultiple, Usuario, Formula];

    public static bool Existe(string tipo) => Todos().Contains(tipo);

    /// <summary>Los tipos que se definen con una lista de opciones.</summary>
    public static bool UsaOpciones(string tipo) => tipo is Seleccion or SeleccionMultiple;

    /// <summary>Los que se calculan solos y por tanto no se rellenan a mano.</summary>
    public static bool SeCalcula(string tipo) => tipo == Formula;

    /// <summary>
    /// Los que una fórmula puede usar como número.
    ///
    /// Sólo <see cref="Numero"/> y otras fórmulas. Se descartó dejar que un texto que «parece un
    /// número» cuente: haría que la fórmula funcionara o no según lo que alguien tecleara ese
    /// día en un campo de texto libre, y el fallo aparecería como un hueco sin explicación.
    /// </summary>
    public static bool SirveEnFormula(string tipo) => tipo is Numero or Formula;
}
