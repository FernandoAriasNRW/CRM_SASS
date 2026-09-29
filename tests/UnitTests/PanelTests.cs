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
public class DisposicionDelPanelTests
{
    private static Widget Uno(int x = 0, int y = 0, int ancho = 6, int alto = 4)
        => new(Guid.NewGuid(), Guid.NewGuid(), x, y, ancho, alto);

    [Fact]
    public void Un_widget_normal_vale()
    {
        Uno().Validate().IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Un widget que empieza tarde y mide mucho se sale de la rejilla.
    ///
    /// En pantalla eso se ve como un recuadro cortado o bajado de fila según el navegador, así que
    /// se rechaza aquí en vez de dejar que cada uno lo dibuje a su manera.
    /// </summary>
    [Fact]
    public void Un_widget_que_se_sale_de_las_doce_columnas_se_rechaza()
    {
        var resultado = Uno(x: 10, ancho: 6).Validate();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("12 columnas");
    }

    [Fact]
    public void Justo_en_el_borde_cabe()
    {
        // Columna 6 con ancho 6 termina exactamente en la 12: cabe.
        Uno(x: 6, ancho: 6).Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Un_widget_demasiado_estrecho_se_rechaza()
    {
        Uno(ancho: 1).Validate().Error.Should().Contain("ancho");
    }

    [Fact]
    public void Un_widget_sin_informe_se_rechaza()
    {
        var huerfano = new Widget(Guid.NewGuid(), Guid.Empty, 0, 0, 6, 4);

        huerfano.Validate().Error.Should().Be(Widget.Rules.MissingReport);
    }

    [Fact]
    public void Una_posicion_negativa_se_rechaza()
    {
        Uno(y: -1).Validate().Error.Should().Be(Widget.Rules.NegativePosition);
    }

    /// <summary>
    /// Dos widgets con el mismo identificador se rechazan.
    ///
    /// Con ellos, mover uno movería los dos y borrar uno borraría el otro: el tipo de fallo que se
    /// achaca al navegador y se pasa media tarde buscando en el sitio equivocado.
    /// </summary>
    [Fact]
    public void Dos_widgets_con_el_mismo_identificador_se_rechazan()
    {
        var uno = Uno();
        var clon = uno with { X = 6 };

        var resultado = new DashboardLayout([uno, clon]).Validate();

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("repetidos");
    }

    [Fact]
    public void Un_panel_con_demasiados_recuadros_se_rechaza()
    {
        var muchos = Enumerable.Range(0, DashboardLayout.MaxWidgets + 1)
            .Select(_ => Uno())
            .ToList();

        new DashboardLayout(muchos).Validate().Error.Should().Contain("como mucho");
    }

    [Fact]
    public void Una_disposicion_sobrevive_a_guardarse_y_leerse()
    {
        var original = new DashboardLayout([Uno(x: 3, y: 2, ancho: 6, alto: 5)]);

        var leida = DashboardLayout.Read(original.Serialize());

        leida.Placed.Should().HaveCount(1);
        leida.Placed[0].X.Should().Be(3);
        leida.Placed[0].Alto.Should().Be(5);
    }

    /// <summary>
    /// Un JSON corrupto abre el panel vacío, no revienta la pantalla.
    ///
    /// Un panel con datos viejos se puede volver a montar; una pantalla que no abre, no.
    /// </summary>
    [Fact]
    public void Un_json_que_no_es_una_disposicion_deja_el_panel_vacio()
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
    public void Modificar_una_disposicion_con_with_ve_los_widgets_nuevos()
    {
        var vacia = new DashboardLayout();

        var conUno = vacia with { Widgets = [Uno()] };

        conUno.Placed.Should().HaveCount(1);
    }
}

/// <summary>
/// El panel personal y los informes con los que arranca.
/// </summary>
public class PanelDeInicioTests
{
    [Fact]
    public void El_panel_personal_nace_privado_y_de_su_dueno()
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
    public void Todos_los_informes_de_partida_son_validos()
    {
        foreach (var sugerido in StarterDashboard.Reports())
        {
            var resultado = sugerido.Definition.Validate();

            resultado.IsSuccess.Should().BeTrue(
                $"«{sugerido.Name}» es un informe de partida y tiene que poder generarse: {resultado.Error}");
        }
    }

    /// <summary>Y todos caben en la rejilla una vez colocados.</summary>
    [Fact]
    public void Los_informes_de_partida_caben_en_la_rejilla()
    {
        var informes = StarterDashboard.Reports()
            .Select(s => (Guid.NewGuid(), s))
            .ToList();

        var widgets = StarterDashboard.Place(informes);

        widgets.Should().HaveCount(informes.Count);

        var resultado = new DashboardLayout(widgets).Validate();
        resultado.IsSuccess.Should().BeTrue(resultado.Error);
    }

    /// <summary>
    /// La colocación no deja huecos raros: lo que no cabe en la fila baja a la siguiente.
    ///
    /// Se comprueba que ningún par de recuadros de la misma fila se solape en columnas, que es lo
    /// que produce el recuadro montado encima de otro.
    /// </summary>
    [Fact]
    public void Los_recuadros_de_una_misma_fila_no_se_pisan()
    {
        var widgets = StarterDashboard.Place(
            StarterDashboard.Reports().Select(s => (Guid.NewGuid(), s)).ToList());

        foreach (var fila in widgets.GroupBy(w => w.Y))
        {
            var ordenados = fila.OrderBy(w => w.X).ToList();

            for (var i = 1; i < ordenados.Count; i++)
            {
                ordenados[i].X.Should().BeGreaterThanOrEqualTo(
                    ordenados[i - 1].X + ordenados[i - 1].Ancho,
                    $"«{ordenados[i].Titulo}» se monta encima de «{ordenados[i - 1].Titulo}»");
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
    public void Un_panel_no_se_queda_con_una_disposicion_invalida()
    {
        var panel = Dashboard.CreatePersonal(Guid.NewGuid(), Guid.NewGuid());
        var antes = panel.WidgetsJson;

        var invalida = new DashboardLayout(
            [new Widget(Guid.NewGuid(), Guid.NewGuid(), X: 10, Y: 0, Ancho: 6, Alto: 4)]);

        panel.Place(invalida).IsFailure.Should().BeTrue();
        panel.WidgetsJson.Should().Be(antes, "una disposición rechazada no puede haberse guardado a medias");
    }
}
