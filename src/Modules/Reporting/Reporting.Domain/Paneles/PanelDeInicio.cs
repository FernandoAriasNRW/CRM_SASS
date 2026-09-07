using Reporting.Domain.Definicion;

namespace Reporting.Domain.Paneles;

/// <summary>
/// Los informes con los que arranca el panel de alguien que entra por primera vez.
///
/// <b>El criterio, del plan y literal: «sólo se ofrece de serie lo que los datos de hoy pueden
/// responder».</b> Un widget de serie que sale vacío para todo el mundo enseña que el producto no
/// sabe de qué habla, y el que sale con datos inventados es peor todavía —se toman decisiones con
/// él—.
///
/// Por eso esta lista es corta y aburrida: seis preguntas que cualquier inquilino con datos puede
/// contestar desde el primer día. Lo que el plan pedía y no está:
///
/// - <b>Tickets por área</b>, el ejemplo que se pidió expresamente. No hay campo de área en los
///   tickets, y la solución acordada —usar un campo personalizado como dimensión— exige extender
///   los campos personalizados a tickets, que hoy son de tareas y proyectos.
/// - <b>Cumplimiento de fechas</b> y <b>tiempo medio de resolución</b> como serie: el motor sabe
///   calcular la media de días hasta resolver, pero sin tickets resueltos sale una raya en todos
///   los meses. Se deja fuera del arranque hasta que haya datos que enseñar; ponerlo sería
///   estrenar el producto con una gráfica vacía.
///
/// Cada uno se crea como un informe de verdad, guardado y editable: quien no quiera «Tareas por
/// responsable» lo cambia en el constructor o lo quita del panel, en vez de mirar un recuadro que
/// no puede tocar.
/// </summary>
public static class PanelDeInicio
{
    /// <summary>Un informe de partida: cómo se llama y qué pide.</summary>
    public sealed record Sugerido(string Nombre, DefinicionDeInforme Definicion, int Ancho, int Alto);

    public static IReadOnlyList<Sugerido> Informes() =>
    [
        new("Tickets por estado",
            new DefinicionDeInforme("Tickets", "estado", "conteo", "tarta"),
            Ancho: 4, Alto: 4),

        new("Tickets por prioridad",
            new DefinicionDeInforme("Tickets", "prioridad", "conteo", "barras"),
            Ancho: 4, Alto: 4),

        new("Tareas por estado",
            new DefinicionDeInforme("Tareas", "estado", "conteo", "barras"),
            Ancho: 4, Alto: 4),

        new("Tareas por responsable",
            new DefinicionDeInforme("Tareas", "responsable", "conteo", "barras"),
            Ancho: 6, Alto: 4),

        new("Tareas por proyecto",
            new DefinicionDeInforme("Tareas", "proyecto", "conteo", "barras"),
            Ancho: 6, Alto: 4),

        new("Tickets abiertos por mes",
            new DefinicionDeInforme("Tickets", "creacion", "conteo", "lineas", Granularidad: "mes"),
            Ancho: 12, Alto: 4)
    ];

    /// <summary>
    /// Coloca los widgets en la rejilla, de izquierda a derecha y bajando de fila.
    ///
    /// La colocación se calcula aquí y no se guarda en la lista de arriba para que añadir o quitar
    /// un informe de partida no obligue a recolocar los demás a mano: es la clase de detalle que
    /// se olvida y deja un hueco raro en el panel de todo el mundo.
    /// </summary>
    public static IReadOnlyList<Widget> Colocar(IReadOnlyList<(Guid ReportId, Sugerido Informe)> informes)
    {
        var widgets = new List<Widget>();

        var x = 0;
        var y = 0;
        var altoDeLaFila = 0;

        foreach (var (reportId, sugerido) in informes)
        {
            // Si no cabe en lo que queda de fila, se baja a la siguiente.
            if (x + sugerido.Ancho > Widget.Columnas)
            {
                x = 0;
                y += altoDeLaFila;
                altoDeLaFila = 0;
            }

            widgets.Add(new Widget(
                Id: Guid.NewGuid(),
                ReportId: reportId,
                X: x,
                Y: y,
                Ancho: sugerido.Ancho,
                Alto: sugerido.Alto,
                Forma: sugerido.Definicion.Forma,
                Titulo: sugerido.Nombre));

            x += sugerido.Ancho;
            altoDeLaFila = Math.Max(altoDeLaFila, sugerido.Alto);
        }

        return widgets;
    }
}
