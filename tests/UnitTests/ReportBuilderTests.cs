using FluentAssertions;
using Reporting.Domain.Definitions;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

/// <summary>
/// La definición de un informe a medida.
///
/// Lo que se vigila es que **nada pase sin estar en el catálogo**. Un constructor de informes
/// multiplica por veinte la superficie del fallo que este módulo ya cometió dos veces —ofrecer
/// opciones que el servidor no conoce—, con el agravante de que aquí el usuario guarda la
/// definición: el error aparecería al exportar, días después, cuando ya no está mirando.
/// </summary>
public class ReportDefinitionTests
{
    private static ReportDefinition Valid() =>
        new(DataSource: "Tasks", GroupBy: "status", Measure: "count", Visualization: "bar");

    [Fact]
    public void A_complete_definition_is_valid()
    {
        Valid().Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void An_unknown_source_lists_the_available_ones()
    {
        var definition = Valid() with { DataSource = "Facturas" };

        var result = definition.Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Facturas").And.Contain("Tasks");
    }

    [Fact]
    public void A_group_by_field_from_another_source_is_rejected()
    {
        // «agente» es de tickets, no de tareas. Sin esta comprobación el motor caería en su
        // `default` al generar, que es cuando quien lo construyó ya no está delante.
        var definition = Valid() with { GroupBy = "agent" };

        var result = definition.Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("agent").And.Contain("status");
    }

    [Fact]
    public void A_measure_the_source_lacks_is_rejected()
    {
        // Los proyectos sólo se pueden contar; no tienen horas que sumar.
        var definition = Valid() with { DataSource = "Projects", GroupBy = "status", Measure = "sum_estimated_hours" };

        var result = definition.Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("sum_estimated_hours").And.Contain("count");
    }

    [Fact]
    public void A_made_up_visualization_is_rejected()
    {
        var result = (Valid() with { Visualization = "holograma" }).Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("holograma");
    }

    /// <summary>
    /// Agrupar por una fecha exige decir la granularidad.
    ///
    /// Sin ella habría que elegir una por defecto, y la elección sería invisible: un informe
    /// «tareas por vencimiento» que sale con una barra por día parece roto, y quien lo mire no
    /// sabrá que podía pedir meses.
    /// </summary>
    [Fact]
    public void Grouping_by_date_without_granularity_is_rejected()
    {
        var result = (Valid() with { GroupBy = "due_date" }).Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("día, semana, mes o año");
    }

    [Fact]
    public void Grouping_by_date_with_granularity_is_valid()
    {
        var definition = Valid() with { GroupBy = "due_date", Granularity = "month" };

        definition.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_made_up_granularity_is_rejected()
    {
        var definition = Valid() with { GroupBy = "due_date", Granularity = "quincena" };

        definition.Validate().Error.Should().Contain("quincena");
    }

    #region Filtros

    [Fact]
    public void A_filter_on_an_unknown_field_is_rejected()
    {
        var definition = Valid() with { Filters = [new ReportFilter("color", "is", "azul")] };

        definition.Validate().Error.Should().Contain("color");
    }

    /// <summary>
    /// El operador tiene que encajar con el tipo del campo.
    ///
    /// «Mayor que» sobre un estado no significa nada. Sin esta comprobación el motor devolvería
    /// una lista arbitraria —o lanzaría— y el informe parecería roto sin decir por qué.
    /// </summary>
    [Fact]
    public void GreaterThan_on_text_is_rejected()
    {
        var definition = Valid() with { Filters = [new ReportFilter("status", "greater_than", "Done")] };

        var result = definition.Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Mayor que").And.Contain("Estado");
    }

    [Fact]
    public void Contains_on_a_person_is_rejected()
    {
        // Un responsable es un identificador; «contiene» sobre él sería buscar dentro de un GUID.
        var definition = Valid() with { Filters = [new ReportFilter("assignee", "contains", "ana")] };

        definition.Validate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void An_operator_that_needs_a_value_without_one_is_rejected()
    {
        var definition = Valid() with { Filters = [new ReportFilter("status", "is", null)] };

        definition.Validate().Error.Should().Contain("necesita un valor");
    }

    /// <summary>«Está vacío» no necesita valor, y exigírselo sería un formulario imposible.</summary>
    [Fact]
    public void IsEmpty_needs_no_value()
    {
        var definition = Valid() with
        {
            DataSource = "Tickets",
            GroupBy = "status",
            Filters = [new ReportFilter("resolved_at", "empty", null)]
        };

        definition.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void All_filters_are_validated()
    {
        var definition = Valid() with
        {
            Filters =
            [
                new ReportFilter("status", "is", "Done"),
                new ReportFilter("priority", "greater_than", "High")
            ]
        };

        // El primero vale y el segundo no: el resultado tiene que señalar al segundo.
        definition.Validate().Error.Should().Contain("Prioridad");
    }

    #endregion

    #region Ida y vuelta

    /// <summary>
    /// Lo guardado se vuelve a leer igual.
    ///
    /// Importa porque la definición viaja a la base como JSON y vuelve para reabrir el
    /// constructor: si perdiera los filtros por el camino, el informe se seguiría generando —con
    /// otros datos— y nadie lo notaría hasta comparar cifras.
    /// </summary>
    [Fact]
    public void A_definition_survives_saving_and_reading()
    {
        var original = new ReportDefinition(
            "Tickets", "created_at", "avg_days_to_resolve", "line",
            [new ReportFilter("priority", "is", "High")],
            Granularity: "week",
            MaxGroups: 5);

        var read = ReportDefinition.Read(original.Serialize());

        read.Should().NotBeNull();
        read!.DataSource.Should().Be("Tickets");
        read.Granularity.Should().Be("week");
        read.MaxGroups.Should().Be(5);
        read.AppliedFilters.Should().ContainSingle(f => f.Field == "priority" && f.Value == "High");
    }

    /// <summary>
    /// Un JSON corrupto devuelve nulo, no lanza.
    ///
    /// Una fila con datos viejos no puede tumbar el listado de informes; como mucho, ese informe
    /// no se ofrece como construible.
    /// </summary>
    [Fact]
    public void A_json_that_is_not_a_definition_does_not_crash()
    {
        ReportDefinition.Read("{esto no es json").Should().BeNull();
        ReportDefinition.Read(null).Should().BeNull();
        ReportDefinition.Read("   ").Should().BeNull();
    }

    #endregion

    #region El catálogo

    /// <summary>
    /// Cada medida que dice calcularse sobre un campo, nombra un campo que existe en su origen.
    ///
    /// Es la clase de desajuste que no da error al guardar y revienta al generar: la medida
    /// «suma de horas» sobre un origen sin campo de horas.
    /// </summary>
    [Fact]
    public void Measures_point_to_existing_fields()
    {
        foreach (var source in ReportCatalog.DataSources())
        {
            foreach (var measure in source.Measures.Where(m => m.OnField is not null))
            {
                source.Field(measure.OnField).Should().NotBeNull(
                    $"la medida «{measure.Key}» de {source.Key} dice calcularse sobre "
                    + $"«{measure.OnField}», que no es un campo de ese origen");
            }
        }
    }

    /// <summary>Todo origen se puede contar; sin eso no habría informe posible sobre él.</summary>
    [Fact]
    public void Every_data_source_can_count()
    {
        foreach (var source in ReportCatalog.DataSources())
            source.Measure("count").Should().NotBeNull($"{source.Key} tiene que poder contarse");
    }

    /// <summary>
    /// Todo campo tiene al menos un operador aplicable.
    ///
    /// Un campo sin operadores es un filtro que la pantalla ofrece y que no se puede completar.
    /// </summary>
    [Fact]
    public void Every_field_can_be_filtered_somehow()
    {
        foreach (var source in ReportCatalog.DataSources())
        {
            foreach (var field in source.Fields)
            {
                ReportCatalog.Operators().Any(o => o.AppliesTo(field.Type)).Should().BeTrue(
                    $"«{field.Key}» de {source.Key} es de tipo {field.Type} y ningún operador lo admite");
            }
        }
    }

    #endregion
}

/// <summary>
/// Los informes programados.
///
/// <c>TocaAhora</c> es una función pura justamente para esto: los casos límite de un planificador
/// —el domingo, el día 1, el minuto antes de la hora— sólo se pueden comprobar de verdad si no
/// hace falta esperar a que llegue ese momento.
/// </summary>
public class ReportScheduleTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Report = Guid.NewGuid();
    private static readonly Guid Person = Guid.NewGuid();

    private static ReportSchedule Create(
        ScheduleFrequency frequency, string hour = "08:00", int? day = null)
        => ReportSchedule.Create(
            Tenant, Report, Person, frequency, ReportFormat.Pdf, TimeOnly.Parse(hour), day).Value!;

    /// <summary>Un lunes a las 9, para tener una referencia con nombre.</summary>
    private static readonly DateTime MondayAtNine = new(2026, 9, 7, 9, 0, 0);

    [Fact]
    public void Starts_active_and_never_generated()
    {
        var schedule = Create(ScheduleFrequency.Daily);

        schedule.IsActive.Should().BeTrue();
        schedule.LastGeneratedDay.Should().BeNull();
    }

    #region Cuándo toca

    [Fact]
    public void Daily_is_due_after_its_time()
    {
        Create(ScheduleFrequency.Daily, "08:00").IsDue(MondayAtNine).Should().BeTrue();
    }

    [Fact]
    public void Daily_is_not_due_before_its_time()
    {
        Create(ScheduleFrequency.Daily, "10:00").IsDue(MondayAtNine).Should().BeFalse();
    }

    [Fact]
    public void Weekly_is_due_on_its_day()
    {
        // 1 es lunes, según ISO. El 7 de septiembre de 2026 es lunes.
        Create(ScheduleFrequency.Weekly, "08:00", day: 1).IsDue(MondayAtNine).Should().BeTrue();
    }

    [Fact]
    public void Weekly_is_not_due_on_another_day()
    {
        Create(ScheduleFrequency.Weekly, "08:00", day: 3).IsDue(MondayAtNine).Should().BeFalse();
    }

    /// <summary>
    /// El domingo es el 7, no el 0.
    ///
    /// En .NET, <c>DayOfWeek.Sunday</c> vale 0. Guardarlo tal cual haría que «día 1» significara
    /// lunes para quien lo configura y domingo para el planificador, y el informe llegaría un día
    /// tarde toda la vida.
    /// </summary>
    [Fact]
    public void Sunday_is_day_seven()
    {
        var sunday = new DateTime(2026, 9, 13, 9, 0, 0);
        sunday.DayOfWeek.Should().Be(DayOfWeek.Sunday);

        Create(ScheduleFrequency.Weekly, "08:00", day: 7).IsDue(sunday).Should().BeTrue();
        Create(ScheduleFrequency.Weekly, "08:00", day: 1).IsDue(sunday).Should().BeFalse();
    }

    [Fact]
    public void Monthly_is_due_on_its_day_of_month()
    {
        Create(ScheduleFrequency.Monthly, "08:00", day: 7).IsDue(MondayAtNine).Should().BeTrue();
        Create(ScheduleFrequency.Monthly, "08:00", day: 8).IsDue(MondayAtNine).Should().BeFalse();
    }

    /// <summary>
    /// Lo que impide que un informe diario llegue doce veces.
    ///
    /// El planificador corre cada cinco minutos; sin esta marca, cada vuelta del día volvería a
    /// disparar el mismo informe.
    /// </summary>
    [Fact]
    public void Not_due_twice_on_the_same_day()
    {
        var schedule = Create(ScheduleFrequency.Daily, "08:00");

        schedule.IsDue(MondayAtNine).Should().BeTrue();

        schedule.MarkGenerated(DateOnly.FromDateTime(MondayAtNine));

        schedule.IsDue(MondayAtNine).Should().BeFalse();
        schedule.IsDue(MondayAtNine.AddHours(5)).Should().BeFalse();
    }

    [Fact]
    public void It_is_due_again_the_next_day()
    {
        var schedule = Create(ScheduleFrequency.Daily, "08:00");
        schedule.MarkGenerated(DateOnly.FromDateTime(MondayAtNine));

        schedule.IsDue(MondayAtNine.AddDays(1)).Should().BeTrue();
    }

    [Fact]
    public void A_deactivated_one_is_never_due()
    {
        var schedule = Create(ScheduleFrequency.Daily, "08:00");
        schedule.Deactivate();

        schedule.IsDue(MondayAtNine).Should().BeFalse();
    }

    /// <summary>
    /// Desactivar no borra la marca del día, y por eso reactivar no duplica.
    ///
    /// Es la razón de que desactivar y borrar sean cosas distintas: borrar la programación y
    /// volver a crearla el mismo día sí generaría el informe dos veces.
    /// </summary>
    [Fact]
    public void Reactivating_on_the_same_day_does_not_fire_again()
    {
        var schedule = Create(ScheduleFrequency.Daily, "08:00");
        schedule.MarkGenerated(DateOnly.FromDateTime(MondayAtNine));

        schedule.Deactivate();
        schedule.Activate();

        schedule.IsDue(MondayAtNine).Should().BeFalse();
    }

    #endregion

    #region Lo que no se admite

    [Fact]
    public void A_weekly_without_day_is_rejected()
    {
        var result = ReportSchedule.Create(
            Tenant, Report, Person, ScheduleFrequency.Weekly, ReportFormat.Pdf,
            new TimeOnly(8, 0), day: null);

        result.Error.Should().Be(ReportSchedule.Rules.MissingDay);
    }

    [Fact]
    public void An_out_of_range_weekday_is_rejected()
    {
        ReportSchedule.Create(
                Tenant, Report, Person, ScheduleFrequency.Weekly, ReportFormat.Pdf,
                new TimeOnly(8, 0), day: 8)
            .Error.Should().Be(ReportSchedule.Rules.DayOfWeekOutOfRange);
    }

    /// <summary>
    /// El día 31 no se admite, y el mensaje dice por qué.
    ///
    /// Un informe programado el 31 no se generaría en febrero ni en los meses de treinta días:
    /// cuatro meses al año fallando en silencio. Es mejor no ofrecerlo.
    /// </summary>
    [Fact]
    public void A_day_of_month_above_28_is_rejected()
    {
        ReportSchedule.Create(
                Tenant, Report, Person, ScheduleFrequency.Monthly, ReportFormat.Pdf,
                new TimeOnly(8, 0), day: 31)
            .Error.Should().Contain("febrero");
    }

    /// <summary>La diaria ignora el día en vez de rechazarlo: no molesta y no significa nada.</summary>
    [Fact]
    public void Daily_ignores_the_day_it_is_given()
    {
        var schedule = Create(ScheduleFrequency.Daily, "08:00", day: 5);

        schedule.Day.Should().BeNull();
        schedule.IsDue(MondayAtNine).Should().BeTrue();
    }

    #endregion
}
