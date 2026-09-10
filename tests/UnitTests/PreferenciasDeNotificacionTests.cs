using FluentAssertions;
using Notifications.Domain.Entities;
using Xunit;

namespace UnitTests;

/// <summary>
/// Las reglas de las preferencias de aviso.
///
/// Son funciones puras —la hora entra como argumento, no se lee del reloj— para poder probar la
/// medianoche sin esperar a que sean las doce. Es la misma disciplina que en el Gantt y en el
/// reparto de carga.
/// </summary>
public sealed class PreferenciasDeNotificacionTests
{
    private static PreferenciasDeNotificacion PorDefecto() =>
        PreferenciasDeNotificacion.PorDefecto(Guid.NewGuid(), Guid.NewGuid());

    #region Valores de partida

    /// <summary>
    /// Lo que la persona espera llega encendido; lo que informa de actividad ajena, apagado.
    /// El criterio importa: si todo llegara encendido, el ruido acabaría con la persona
    /// ignorando todos los avisos, incluidos los que sí importaban.
    /// </summary>
    [Theory]
    [InlineData(TiposDeAviso.TareaAsignada, true)]
    [InlineData(TiposDeAviso.TareaPorVencer, true)]
    [InlineData(TiposDeAviso.Mencion, true)]
    [InlineData(TiposDeAviso.ExportacionLista, true)]
    [InlineData(TiposDeAviso.TareaCompletada, false)]
    [InlineData(TiposDeAviso.TicketActualizado, false)]
    public void Los_valores_de_partida_distinguen_lo_propio_de_lo_ajeno(string tipo, bool esperado)
    {
        PorDefecto().QuiereRecibir(tipo).Should().Be(esperado);
    }

    /// <summary>
    /// El aviso de exportación terminada llega encendido: era una condición explícita del
    /// encargo, y es la respuesta a algo que la persona pidió.
    /// </summary>
    [Fact]
    public void El_aviso_de_exportacion_llega_encendido_y_se_puede_apagar()
    {
        var p = PorDefecto();
        p.ExportacionLista.Should().BeTrue();

        p.Actualizar(
            emailEnabled: true, pushEnabled: false,
            taskAssigned: true, taskDueSoon: true, mentionEnabled: true, exportacionLista: false,
            taskCompleted: false, ticketCreated: true, ticketUpdated: false, projectUpdated: true,
            quietHoursEnabled: false, quietHoursStart: new TimeOnly(22, 0), quietHoursEnd: new TimeOnly(8, 0));

        p.ExportacionLista.Should().BeFalse();
        p.QuiereRecibir(TiposDeAviso.ExportacionLista).Should().BeFalse();
    }

    /// <summary>
    /// Un tipo que nadie declaró pasa. Es deliberado: si alguien añade un aviso y olvida
    /// ponerlo en la lista, el fallo es que se recibe de más —molesto y visible— y no que se
    /// pierde en silencio, que es el fallo que nadie detecta.
    /// </summary>
    [Fact]
    public void Un_tipo_desconocido_pasa_en_vez_de_perderse()
    {
        PorDefecto().QuiereRecibir("UnAvisoQueNadieDeclaro").Should().BeTrue();
    }

    #endregion

    #region Horas de silencio

    private static PreferenciasDeNotificacion ConSilencio(TimeOnly desde, TimeOnly hasta)
    {
        var p = PorDefecto();
        p.Actualizar(
            emailEnabled: true, pushEnabled: false,
            taskAssigned: true, taskDueSoon: true, mentionEnabled: true, exportacionLista: true,
            taskCompleted: false, ticketCreated: true, ticketUpdated: false, projectUpdated: true,
            quietHoursEnabled: true, quietHoursStart: desde, quietHoursEnd: hasta);
        return p;
    }

    /// <summary>
    /// El tramo de noche cruza la medianoche, que es el caso normal y el que se escribe mal.
    /// Con la comparación ingenua —«después del inicio Y antes del fin»— de 22:00 a 08:00 no
    /// silencia nunca nada, y el fallo sólo se nota de madrugada.
    /// </summary>
    [Theory]
    [InlineData(23, 0, true)]   // después del inicio, antes de medianoche
    [InlineData(3, 0, true)]    // pasada la medianoche
    [InlineData(7, 59, true)]   // justo antes del fin
    [InlineData(22, 0, true)]   // el inicio entra
    [InlineData(8, 0, false)]   // el fin no entra: a las 08:00 ya se recibe
    [InlineData(12, 0, false)]  // mediodía
    [InlineData(21, 59, false)] // justo antes de empezar
    public void El_silencio_nocturno_cruza_la_medianoche(int hora, int minuto, bool silenciado)
    {
        var p = ConSilencio(new TimeOnly(22, 0), new TimeOnly(8, 0));

        p.EnSilencio(new TimeOnly(hora, minuto)).Should().Be(silenciado);
    }

    /// <summary>Un tramo que no cruza la medianoche se comporta como uno espera.</summary>
    [Theory]
    [InlineData(10, 0, true)]
    [InlineData(13, 59, true)]
    [InlineData(14, 0, false)]
    [InlineData(9, 59, false)]
    [InlineData(2, 0, false)]
    public void Un_silencio_dentro_del_mismo_dia_no_se_invierte(int hora, int minuto, bool silenciado)
    {
        var p = ConSilencio(new TimeOnly(10, 0), new TimeOnly(14, 0));

        p.EnSilencio(new TimeOnly(hora, minuto)).Should().Be(silenciado);
    }

    [Fact]
    public void Con_el_silencio_apagado_no_se_silencia_ninguna_hora()
    {
        var p = PorDefecto();   // nace con el silencio desactivado

        p.DejaPasar(TiposDeAviso.TareaAsignada, new TimeOnly(3, 0)).Should().BeTrue();
    }

    /// <summary>
    /// Las dos condiciones se combinan: el silencio detiene un aviso que la persona quiere, y
    /// no resucita uno que apagó.
    /// </summary>
    [Fact]
    public void El_silencio_detiene_lo_que_se_queria_recibir()
    {
        var p = ConSilencio(new TimeOnly(22, 0), new TimeOnly(8, 0));

        p.DejaPasar(TiposDeAviso.TareaAsignada, new TimeOnly(23, 30)).Should().BeFalse("es de noche");
        p.DejaPasar(TiposDeAviso.TareaAsignada, new TimeOnly(10, 0)).Should().BeTrue("ya es de día");
        p.DejaPasar(TiposDeAviso.TareaCompletada, new TimeOnly(10, 0)).Should().BeFalse("ese aviso está apagado");
    }

    #endregion
}
