namespace Reporting.Domain.Definicion;

/// <summary>
/// Todo lo que un informe a medida puede pedir: de dónde saca los datos, por qué campos se puede
/// agrupar y filtrar, qué se puede medir y cómo se puede pintar.
///
/// <b>Está en el dominio y es una sola lista, y eso es lo importante.</b> El constructor de la
/// pantalla se alimenta de aquí a través de un endpoint, el motor que resuelve el informe valida
/// contra aquí, y las pruebas recorren esto. Nadie escribe la lista por su cuenta.
///
/// Es la lección más cara de este proyecto, y ya la ha dado dos veces el mismo módulo: el
/// desplegable de tipos de informe ofrecía «TaskBreakdown» y «KpiSummary», que el enum del
/// servidor no conocía, y pedirlos devolvía 400 con un mensaje que no decía cuál de los dos
/// campos estaba mal. Un constructor de informes multiplica esa superficie por veinte: un campo
/// que la pantalla ofrezca y el motor no sepa traducir es un informe que no se puede generar, y
/// el usuario ya lo habrá guardado.
/// </summary>
public static class CatalogoDeInformes
{
    /// <summary>De dónde salen las filas.</summary>
    public static IReadOnlyList<OrigenDeDatos> Origenes() => [Tareas, Tickets, Proyectos];

    public static OrigenDeDatos? Origen(string? clave)
        => Origenes().FirstOrDefault(o => string.Equals(o.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public static readonly OrigenDeDatos Tareas = new(
        Clave: "Tareas",
        Nombre: "Tareas",
        Campos:
        [
            new("estado", "Estado", TipoDeCampo.Texto),
            new("prioridad", "Prioridad", TipoDeCampo.Texto),
            new("responsable", "Responsable", TipoDeCampo.Persona, Opcional: true),
            new("proyecto", "Proyecto", TipoDeCampo.Referencia),
            new("vencimiento", "Vencimiento", TipoDeCampo.Fecha),
            new("creacion", "Creación", TipoDeCampo.Fecha),
            new("horas", "Horas estimadas", TipoDeCampo.Numero)
        ],
        Medidas:
        [
            MedidaDisponible.Conteo,
            new("suma_horas", "Suma de horas estimadas", "horas"),
            new("media_horas", "Media de horas estimadas", "horas")
        ]);

    public static readonly OrigenDeDatos Tickets = new(
        Clave: "Tickets",
        Nombre: "Tickets",
        Campos:
        [
            new("estado", "Estado", TipoDeCampo.Texto),
            new("prioridad", "Prioridad", TipoDeCampo.Texto),
            new("agente", "Agente asignado", TipoDeCampo.Persona, Opcional: true),
            new("creacion", "Creación", TipoDeCampo.Fecha),
            new("resolucion", "Resolución", TipoDeCampo.Fecha, Opcional: true)
        ],
        Medidas:
        [
            MedidaDisponible.Conteo,
            new("media_dias_resolucion", "Días medios hasta resolver", "resolucion")
        ]);

    public static readonly OrigenDeDatos Proyectos = new(
        Clave: "Proyectos",
        Nombre: "Proyectos",
        Campos:
        [
            new("estado", "Estado", TipoDeCampo.Texto),
            new("dueno", "Responsable", TipoDeCampo.Persona, Opcional: true),
            new("inicio", "Fecha de inicio", TipoDeCampo.Fecha)
        ],
        Medidas: [MedidaDisponible.Conteo]);

    /// <summary>
    /// Cómo se agrupa una fecha.
    ///
    /// Agrupar por la fecha exacta daría una categoría por día y una gráfica ilegible; agrupar
    /// siempre por mes impediría ver una semana. Se elige.
    /// </summary>
    public static IReadOnlyList<Opcion> GranularidadesDeFecha() =>
    [
        new("dia", "Por día"),
        new("semana", "Por semana"),
        new("mes", "Por mes"),
        new("ano", "Por año")
    ];

    /// <summary>
    /// Cómo se pinta.
    ///
    /// <b>Son nombres neutros, no de la librería de gráficas.</b> Es la decisión que el plan dejó
    /// escrita: lo que se guarda es una definición neutra, y guardar opciones de ECharts ataría
    /// todos los informes que la gente construya a esa librería. Cambiarla algún día invalidaría
    /// el trabajo de los usuarios, no sólo el nuestro.
    ///
    /// La exportación a fichero pinta siempre una tabla —un PDF no tiene gráficas todavía— pero
    /// la forma se guarda igual, porque el dashboard la va a necesitar y el informe es el mismo.
    /// </summary>
    public static IReadOnlyList<Opcion> Formas() =>
    [
        new("tabla", "Tabla"),
        new("barras", "Barras"),
        new("barras_apiladas", "Barras apiladas"),
        new("lineas", "Líneas"),
        new("tarta", "Tarta")
    ];

    /// <summary>
    /// Los operadores de filtro, con los tipos de campo a los que se pueden aplicar.
    ///
    /// «Mayor que» sobre un estado no significa nada, y ofrecerlo produce informes que devuelven
    /// cualquier cosa sin dar error. El tipo del campo decide qué se ofrece.
    /// </summary>
    public static IReadOnlyList<OperadorDisponible> Operadores() =>
    [
        new("es", "Es", [TipoDeCampo.Texto, TipoDeCampo.Persona, TipoDeCampo.Referencia]),
        new("no_es", "No es", [TipoDeCampo.Texto, TipoDeCampo.Persona, TipoDeCampo.Referencia]),
        new("contiene", "Contiene", [TipoDeCampo.Texto]),
        new("mayor_que", "Mayor que", [TipoDeCampo.Numero, TipoDeCampo.Fecha]),
        new("menor_que", "Menor que", [TipoDeCampo.Numero, TipoDeCampo.Fecha]),
        new("vacio", "Está vacío", [TipoDeCampo.Fecha, TipoDeCampo.Persona, TipoDeCampo.Referencia]),
        new("no_vacio", "No está vacío", [TipoDeCampo.Fecha, TipoDeCampo.Persona, TipoDeCampo.Referencia])
    ];

    public static OperadorDisponible? Operador(string? clave)
        => Operadores().FirstOrDefault(o => string.Equals(o.Clave, clave, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Los operadores que se pueden aplicar a un campo concreto.
    ///
    /// El tipo no basta: «está vacío» tiene sentido sobre una fecha de resolución —un ticket sin
    /// resolver— y ninguno sobre una fecha de vencimiento, que toda tarea tiene. Ofrecerlo sobre
    /// un campo obligatorio produce un filtro que no se puede evaluar, y el informe falla al
    /// generarse.
    /// </summary>
    public static IReadOnlyList<OperadorDisponible> OperadoresPara(CampoDisponible campo)
        => Operadores()
            .Where(o => o.ValePara(campo.Tipo) && (campo.Opcional || !o.EsDeVacuidad))
            .ToList();
}

/// <summary>Un sitio de donde salen filas, con lo que se puede hacer sobre ellas.</summary>
public sealed record OrigenDeDatos(
    string Clave,
    string Nombre,
    IReadOnlyList<CampoDisponible> Campos,
    IReadOnlyList<MedidaDisponible> Medidas)
{
    public CampoDisponible? Campo(string? clave)
        => Campos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public MedidaDisponible? Medida(string? clave)
        => Medidas.FirstOrDefault(m => string.Equals(m.Clave, clave, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// El tipo de un campo, que decide qué operadores tienen sentido y cómo se agrupa.
///
/// <c>Persona</c> y <c>Referencia</c> se distinguen de <c>Texto</c> porque su valor es un
/// identificador: se filtran por igualdad y nunca por «contiene», y al pintarlos hay que
/// resolverlos a un nombre.
/// </summary>
public enum TipoDeCampo { Texto, Numero, Fecha, Persona, Referencia }

/// <param name="Opcional">
/// Si el campo puede no tener valor. Decide si «está vacío» tiene sentido sobre él: una fecha de
/// resolución puede faltar —el ticket sigue abierto—, una de vencimiento no.
/// </param>
public sealed record CampoDisponible(string Clave, string Nombre, TipoDeCampo Tipo, bool Opcional = false);

/// <summary>
/// Qué se calcula por cada grupo.
///
/// <paramref name="SobreElCampo"/> es el campo del que se saca el número; en el conteo es nulo
/// porque contar no necesita ningún campo.
/// </summary>
public sealed record MedidaDisponible(string Clave, string Nombre, string? SobreElCampo)
{
    public static readonly MedidaDisponible Conteo = new("conteo", "Cuántos hay", null);
}

public sealed record OperadorDisponible(string Clave, string Nombre, IReadOnlyList<TipoDeCampo> Tipos)
{
    public bool ValePara(TipoDeCampo tipo) => Tipos.Contains(tipo);

    /// <summary>Si el operador necesita un valor. «Está vacío» no lo necesita.</summary>
    public bool NecesitaValor => !EsDeVacuidad;

    /// <summary>Si el operador pregunta por la ausencia de valor, y por tanto sólo vale en campos opcionales.</summary>
    public bool EsDeVacuidad => Clave is "vacio" or "no_vacio";
}

public sealed record Opcion(string Clave, string Nombre);
