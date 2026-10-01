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

    #region Valores de partida

    /// <summary>
    /// Lo que la persona espera llega encendido; lo que informa de actividad ajena, apagado.
    /// El criterio importa: si todo llegara encendido, el ruido acabaría con la persona
    /// ignorando todos los avisos, incluidos los que sí importaban.
    /// </summary>
    [Theory]
    [InlineData(NotificationTypes.TaskAssigned, true)]
    [InlineData(NotificationTypes.TaskDueSoon, true)]
    [InlineData(NotificationTypes.Mention, true)]
    [InlineData(NotificationTypes.ExportReady, true)]
    [InlineData(NotificationTypes.TaskCompleted, false)]
    [InlineData(NotificationTypes.TicketUpdated, false)]
    public void Defaults_tell_own_from_others(string type, bool expected)
    {
        Defaults().IsEnabled(type).Should().Be(expected);
    }

    /// <summary>
    /// El aviso de exportación terminada llega encendido: era una condición explícita del
    /// encargo, y es la respuesta a algo que la persona pidió.
    /// </summary>
    [Fact]
    public void The_export_notification_starts_on_and_can_be_turned_off()
    {
        var p = Defaults();
        p.ExportReady.Should().BeTrue();

        p.Update(
            emailEnabled: true, pushEnabled: false,
            taskAssigned: true, taskDueSoon: true, mentionEnabled: true, exportReady: false,
            taskCompleted: false, ticketCreated: true, ticketUpdated: false, projectUpdated: true,
            quietHoursEnabled: false, quietHoursStart: new TimeOnly(22, 0), quietHoursEnd: new TimeOnly(8, 0));

        p.ExportReady.Should().BeFalse();
        p.IsEnabled(NotificationTypes.ExportReady).Should().BeFalse();
    }

    /// <summary>
    /// Un tipo que nadie declaró pasa. Es deliberado: si alguien añade un aviso y olvida
    /// ponerlo en la lista, el fallo es que se recibe de más —molesto y visible— y no que se
    /// pierde en silencio, que es el fallo que nadie detecta.
    /// </summary>
    [Fact]
    public void An_unknown_type_passes_instead_of_being_lost()
    {
        Defaults().IsEnabled("UnAvisoQueNadieDeclaro").Should().BeTrue();
    }

    #endregion

    #region Horas de silencio

    private static NotificationPreferences WithQuietHours(TimeOnly from, TimeOnly to)
    {
        var p = Defaults();
        p.Update(
            emailEnabled: true, pushEnabled: false,
            taskAssigned: true, taskDueSoon: true, mentionEnabled: true, exportReady: true,
            taskCompleted: false, ticketCreated: true, ticketUpdated: false, projectUpdated: true,
            quietHoursEnabled: true, quietHoursStart: from, quietHoursEnd: to);
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

        p.ShouldDeliver(NotificationTypes.TaskAssigned, new TimeOnly(3, 0)).Should().BeTrue();
    }

    /// <summary>
    /// Las dos condiciones se combinan: el silencio detiene un aviso que la persona quiere, y
    /// no resucita uno que apagó.
    /// </summary>
    [Fact]
    public void Quiet_hours_stop_what_would_be_received()
    {
        var p = WithQuietHours(new TimeOnly(22, 0), new TimeOnly(8, 0));

        p.ShouldDeliver(NotificationTypes.TaskAssigned, new TimeOnly(23, 30)).Should().BeFalse("es de noche");
        p.ShouldDeliver(NotificationTypes.TaskAssigned, new TimeOnly(10, 0)).Should().BeTrue("ya es de día");
        p.ShouldDeliver(NotificationTypes.TaskCompleted, new TimeOnly(10, 0)).Should().BeFalse("ese aviso está apagado");
    }

    #endregion
}
