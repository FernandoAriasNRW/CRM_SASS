using FluentAssertions;
using Reporting.Domain.Definitions;
using Reporting.Domain.Entities;
using Reporting.Domain.Dashboards;
using Xunit;

namespace UnitTests;

/// <summary>
/// La rejilla del panel.
///
/// Son reglas de colocación y por tanto código puro: se prueban sin base de datos y sin navegador,
/// que es donde de otro modo se descubrirían —viendo un recuadro cortado y sin saber si la culpa
/// es del CSS, del navegador o de los datos—.
/// </summary>
public class DashboardLayoutTests
{
    private static Widget One(int x = 0, int y = 0, int width = 6, int height = 4)
        => new(Guid.NewGuid(), Guid.NewGuid(), x, y, width, height);

    [Fact]
    public void A_normal_widget_is_valid()
    {
        One().Validate().IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Un widget que empieza tarde y mide mucho se sale de la rejilla.
    ///
    /// En pantalla eso se ve como un recuadro cortado o bajado de fila según el navegador, así que
    /// se rechaza aquí en vez de dejar que cada uno lo dibuje a su manera.
    /// </summary>
    [Fact]
    public void A_widget_beyond_twelve_columns_is_rejected()
    {
        var result = One(x: 10, width: 6).Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("12 columnas");
    }

    [Fact]
    public void Right_on_the_edge_fits()
    {
        // Columna 6 con ancho 6 termina exactamente en la 12: cabe.
        One(x: 6, width: 6).Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_too_narrow_widget_is_rejected()
    {
        One(width: 1).Validate().Error.Should().Contain("ancho");
    }

    [Fact]
    public void A_widget_without_report_is_rejected()
    {
        var huerfano = new Widget(Guid.NewGuid(), Guid.Empty, 0, 0, 6, 4);

        huerfano.Validate().Error.Should().Be(Widget.Rules.MissingReport);
    }

    [Fact]
    public void A_negative_position_is_rejected()
    {
        One(y: -1).Validate().Error.Should().Be(Widget.Rules.NegativePosition);
    }

    /// <summary>
    /// Dos widgets con el mismo identificador se rechazan.
    ///
    /// Con ellos, mover uno movería los dos y borrar uno borraría el otro: el tipo de fallo que se
    /// achaca al navegador y se pasa media tarde buscando en el sitio equivocado.
    /// </summary>
    [Fact]
    public void Two_widgets_with_the_same_id_are_rejected()
    {
        var one = One();
        var clone = one with { X = 6 };

        var result = new DashboardLayout([one, clone]).Validate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("repetidos");
    }

    [Fact]
    public void A_dashboard_with_too_many_widgets_is_rejected()
    {
        var many = Enumerable.Range(0, DashboardLayout.MaxWidgets + 1)
            .Select(_ => One())
            .ToList();

        new DashboardLayout(many).Validate().Error.Should().Contain("como mucho");
    }

    [Fact]
    public void A_layout_survives_saving_and_reading()
    {
        var original = new DashboardLayout([One(x: 3, y: 2, width: 6, height: 5)]);

        var read = DashboardLayout.Read(original.Serialize());

        read.Placed.Should().HaveCount(1);
        read.Placed[0].X.Should().Be(3);
        read.Placed[0].Height.Should().Be(5);
    }

    /// <summary>
    /// Un JSON corrupto abre el panel vacío, no revienta la pantalla.
    ///
    /// Un panel con datos viejos se puede volver a montar; una pantalla que no abre, no.
    /// </summary>
    [Fact]
    public void A_json_that_is_not_a_layout_leaves_the_dashboard_empty()
    {
        DashboardLayout.Read("{roto").Placed.Should().BeEmpty();
        DashboardLayout.Read(null).Placed.Should().BeEmpty();
    }

    /// <summary>
    /// El mismo `with` que ya mordió en la definición de informes.
    ///
    /// Con `{ get; } = Widgets ?? []`, el `with` copia el campo de respaldo del original en vez de
    /// recalcularlo, y una disposición modificada se quedaría con la lista vieja: mover un recuadro
    /// no movería nada, sin dar ningún error.
    /// </summary>
    [Fact]
    public void Changing_a_layout_with_with_sees_the_new_widgets()
    {
        var empty = new DashboardLayout();

        var withOne = empty with { Widgets = [One()] };

        withOne.Placed.Should().HaveCount(1);
    }
}

/// <summary>
/// El panel personal y los informes con los que arranca.
/// </summary>
public class StarterDashboardTests
{
    [Fact]
    public void The_personal_dashboard_starts_private_and_owned()
    {
        var userId = Guid.NewGuid();

        var panel = Dashboard.CreatePersonal(Guid.NewGuid(), userId);

        panel.CreatedById.Should().Be(userId);
        panel.IsDefault.Should().BeTrue();
        panel.IsPublic.Should().BeFalse(
            "lo que alguien coloca para sí no aparece en la pantalla de los demás hasta que lo comparta");
    }

    /// <summary>
    /// <b>Todos los informes de partida son definiciones válidas.</b>
    ///
    /// Uno que no valide se salta al crear el panel, así que el fallo no se vería: quien entrase
    /// por primera vez tendría cinco recuadros en vez de seis y nadie sabría que falta uno.
    /// </summary>
    [Fact]
    public void All_starter_reports_are_valid()
    {
        foreach (var suggested in StarterDashboard.Reports())
        {
            var result = suggested.Definition.Validate();

            result.IsSuccess.Should().BeTrue(
                $"«{suggested.Name}» es un informe de partida y tiene que poder generarse: {result.Error}");
        }
    }

    /// <summary>Y todos caben en la rejilla una vez colocados.</summary>
    [Fact]
    public void Starter_reports_fit_the_grid()
    {
        var reports = StarterDashboard.Reports()
            .Select(s => (Guid.NewGuid(), s))
            .ToList();

        var widgets = StarterDashboard.Place(reports);

        widgets.Should().HaveCount(reports.Count);

        var result = new DashboardLayout(widgets).Validate();
        result.IsSuccess.Should().BeTrue(result.Error);
    }

    /// <summary>
    /// La colocación no deja huecos raros: lo que no cabe en la fila baja a la siguiente.
    ///
    /// Se comprueba que ningún par de recuadros de la misma fila se solape en columnas, que es lo
    /// que produce el recuadro montado encima de otro.
    /// </summary>
    [Fact]
    public void Widgets_in_the_same_row_do_not_overlap()
    {
        var widgets = StarterDashboard.Place(
            StarterDashboard.Reports().Select(s => (Guid.NewGuid(), s)).ToList());

        foreach (var row in widgets.GroupBy(w => w.Y))
        {
            var sorted = row.OrderBy(w => w.X).ToList();

            for (var i = 1; i < sorted.Count; i++)
            {
                sorted[i].X.Should().BeGreaterThanOrEqualTo(
                    sorted[i - 1].X + sorted[i - 1].Width,
                    $"«{sorted[i].Title}» se monta encima de «{sorted[i - 1].Title}»");
            }
        }
    }

    /// <summary>
    /// Colocar un panel válido lo guarda; uno inválido lo rechaza y no lo toca.
    ///
    /// Guardar y dejar que la pantalla se apañe significa que el panel se rompe al abrirlo, cuando
    /// quien lo movió ya ha cerrado.
    /// </summary>
    [Fact]
    public void A_dashboard_does_not_keep_an_invalid_layout()
    {
        var panel = Dashboard.CreatePersonal(Guid.NewGuid(), Guid.NewGuid());
        var before = panel.WidgetsJson;

        var invalid = new DashboardLayout(
            [new Widget(Guid.NewGuid(), Guid.NewGuid(), X: 10, Y: 0, Width: 6, Height: 4)]);

        panel.Place(invalid).IsFailure.Should().BeTrue();
        panel.WidgetsJson.Should().Be(before, "una disposición rechazada no puede haberse guardado a medias");
    }
}
