namespace Reporting.Domain.Definitions;

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
public static class ReportCatalog
{
    /// <summary>De dónde salen las filas.</summary>
    public static IReadOnlyList<DataSource> DataSources() => [Tasks, Tickets, Projects];

    public static DataSource? FindDataSource(string? key)
        => DataSources().FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));

    public static readonly DataSource Tasks = new(
        Key: "Tareas",
        Name: "Tareas",
        Fields:
        [
            new("estado", "Estado", FieldType.Text),
            new("prioridad", "Prioridad", FieldType.Text),
            new("responsable", "Responsable", FieldType.Person, Optional: true),
            new("proyecto", "Proyecto", FieldType.Reference),
            new("vencimiento", "Vencimiento", FieldType.Date),
            new("creacion", "Creación", FieldType.Date),
            new("horas", "Horas estimadas", FieldType.Number)
        ],
        Measures:
        [
            AvailableMeasure.Count,
            new("suma_horas", "Suma de horas estimadas", "horas"),
            new("media_horas", "Media de horas estimadas", "horas")
        ]);

    public static readonly DataSource Tickets = new(
        Key: "Tickets",
        Name: "Tickets",
        Fields:
        [
            new("estado", "Estado", FieldType.Text),
            new("prioridad", "Prioridad", FieldType.Text),
            new("agente", "Agente asignado", FieldType.Person, Optional: true),
            new("creacion", "Creación", FieldType.Date),
            new("resolucion", "Resolución", FieldType.Date, Optional: true)
        ],
        Measures:
        [
            AvailableMeasure.Count,
            new("media_dias_resolucion", "Días medios hasta resolver", "resolucion")
        ]);

    public static readonly DataSource Projects = new(
        Key: "Proyectos",
        Name: "Proyectos",
        Fields:
        [
            new("estado", "Estado", FieldType.Text),
            new("dueno", "Responsable", FieldType.Person, Optional: true),
            new("inicio", "Fecha de inicio", FieldType.Date)
        ],
        Measures: [AvailableMeasure.Count]);

    /// <summary>
    /// Cómo se agrupa una fecha.
    ///
    /// Agrupar por la fecha exacta daría una categoría por día y una gráfica ilegible; agrupar
    /// siempre por mes impediría ver una semana. Se elige.
    /// </summary>
    public static IReadOnlyList<Option> DateGranularities() =>
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
    public static IReadOnlyList<Option> Visualizations() =>
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
    public static IReadOnlyList<AvailableOperator> Operators() =>
    [
        new("es", "Es", [FieldType.Text, FieldType.Person, FieldType.Reference]),
        new("no_es", "No es", [FieldType.Text, FieldType.Person, FieldType.Reference]),
        new("contiene", "Contiene", [FieldType.Text]),
        new("mayor_que", "Mayor que", [FieldType.Number, FieldType.Date]),
        new("menor_que", "Menor que", [FieldType.Number, FieldType.Date]),
        new("vacio", "Está vacío", [FieldType.Date, FieldType.Person, FieldType.Reference]),
        new("no_vacio", "No está vacío", [FieldType.Date, FieldType.Person, FieldType.Reference])
    ];

    public static AvailableOperator? Operator(string? key)
        => Operators().FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Los operadores que se pueden aplicar a un campo concreto.
    ///
    /// El tipo no basta: «está vacío» tiene sentido sobre una fecha de resolución —un ticket sin
    /// resolver— y ninguno sobre una fecha de vencimiento, que toda tarea tiene. Ofrecerlo sobre
    /// un campo obligatorio produce un filtro que no se puede evaluar, y el informe falla al
    /// generarse.
    /// </summary>
    public static IReadOnlyList<AvailableOperator> OperatorsFor(AvailableField field)
        => Operators()
            .Where(o => o.AppliesTo(field.Type) && (field.Optional || !o.ChecksEmptiness))
            .ToList();
}
