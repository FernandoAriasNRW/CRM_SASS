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
public sealed class NotificationPreferencesTests
{
    private static NotificationPreferences Defaults() =>
        NotificationPreferences.CreateDefault(Guid.NewGuid(), Guid.NewGuid());

    #region Valores de partida y ajustes por tipo

    /// <summary>
    /// Lo que la persona espera llega encendido; el ruido —cada edición de un campo—, apagado.
    /// Si todo llegara encendido, la persona acabaría ignorando todos los avisos.
    /// </summary>
    [Theory]
    [InlineData(NotificationCatalog.TaskAssigned, true)]
    [InlineData(NotificationCatalog.TaskDueSoon, true)]
    [InlineData(NotificationCatalog.Mention, true)]
    [InlineData(NotificationCatalog.ExportReady, true)]
    [InlineData(NotificationCatalog.TaskCommented, true)]
    [InlineData(NotificationCatalog.TaskUpdated, false)]
    [InlineData(NotificationCatalog.TicketUpdated, false)]
    public void Defaults_follow_the_catalog(string kind, bool expected)
    {
        Defaults().IsEnabled(kind, isAdmin: false).Should().Be(expected);
    }

    [Fact]
    public void A_type_can_be_turned_off_and_back_on()
    {
        var p = Defaults();

        p.SetType(NotificationCatalog.ExportReady, false, isAdmin: false).IsSuccess.Should().BeTrue();
        p.IsEnabled(NotificationCatalog.ExportReady, isAdmin: false).Should().BeFalse();

        p.SetType(NotificationCatalog.ExportReady, true, isAdmin: false);
        p.IsEnabled(NotificationCatalog.ExportReady, isAdmin: false).Should().BeTrue();
        p.Types.Should().BeEmpty("volver al valor del catálogo no deja nada guardado: sigue al catálogo");
    }

    /// <summary>
    /// Un tipo que no está en el catálogo no se manda: no habría forma de apagarlo, y lo que no
    /// se puede apagar acaba en ruido. Tampoco se puede guardar.
    /// </summary>
    [Fact]
    public void An_unknown_type_is_neither_sent_nor_saved()
    {
        var p = Defaults();

        p.IsEnabled("UnAvisoQueNadieDeclaro", isAdmin: true).Should().BeFalse();
        p.SetType("UnAvisoQueNadieDeclaro", true, isAdmin: true).IsFailure.Should().BeTrue();
    }

    #endregion

    #region Horas de silencio

    private static NotificationPreferences WithQuietHours(TimeOnly from, TimeOnly to)
    {
        var p = Defaults();
        p.SetQuietHours(true, from, to);
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
    public void Night_quiet_hours_cross_midnight(int hour, int minute, bool silenced)
    {
        var p = WithQuietHours(new TimeOnly(22, 0), new TimeOnly(8, 0));

        p.IsQuietAt(new TimeOnly(hour, minute)).Should().Be(silenced);
    }

    /// <summary>Un tramo que no cruza la medianoche se comporta como uno espera.</summary>
    [Theory]
    [InlineData(10, 0, true)]
    [InlineData(13, 59, true)]
    [InlineData(14, 0, false)]
    [InlineData(9, 59, false)]
    [InlineData(2, 0, false)]
    public void Same_day_quiet_hours_are_not_inverted(int hour, int minute, bool silenced)
    {
        var p = WithQuietHours(new TimeOnly(10, 0), new TimeOnly(14, 0));

        p.IsQuietAt(new TimeOnly(hour, minute)).Should().Be(silenced);
    }

    [Fact]
    public void With_quiet_hours_off_no_hour_is_silenced()
    {
        var p = Defaults();   // nace con el silencio desactivado

        p.ShouldDeliver(NotificationCatalog.TaskAssigned, new TimeOnly(3, 0), isAdmin: false).Should().BeTrue();
    }

    /// <summary>
    /// Las dos condiciones se combinan: el silencio detiene un aviso que la persona quiere, y
    /// no resucita uno que apagó.
    /// </summary>
    [Fact]
    public void Quiet_hours_stop_what_would_be_received()
    {
        var p = WithQuietHours(new TimeOnly(22, 0), new TimeOnly(8, 0));

        p.ShouldDeliver(NotificationCatalog.TaskAssigned, new TimeOnly(23, 30), isAdmin: false).Should().BeFalse("es de noche");
        p.ShouldDeliver(NotificationCatalog.TaskAssigned, new TimeOnly(10, 0), isAdmin: false).Should().BeTrue("ya es de día");
        p.ShouldDeliver(NotificationCatalog.TaskUpdated, new TimeOnly(10, 0), isAdmin: false).Should().BeFalse("ese aviso está apagado");
    }

    #endregion
}
