using BuildingBlocks.Application.Events;
using MediatR;
using Notifications.Application.Commands;
using Notifications.Application.Preferencias;
using Notifications.Domain.Entities;
using Reporting.Domain.Events;

namespace ApiHost.Reporting;

/// <summary>
/// Avisa a quien pidió una exportación de que ya está —o de que no salió—.
///
/// <b>Vive en el host porque cruza dos módulos</b>: Reporting sabe que la exportación terminó y
/// Notifications sabe entregar el aviso; ninguno referencia al otro. Es el mismo reparto que
/// <see cref="ApiHost.Services.AvisoDeAutomatizacion"/>, y se sigue igual a propósito: dos formas
/// distintas de avisar acabarían respetando las preferencias de dos maneras distintas.
///
/// <b>Se avisa también del fallo.</b> Es lo que el plan señalaba: quien pide un informe y no
/// recibe nada no sabe si esperar más. Un fallo callado convierte cada exportación en una
/// pregunta al soporte.
/// </summary>
public sealed class AvisoDeExportacionLista(
    IMediator mediator,
    IRepositorioDePreferencias preferencias,
    ILogger<AvisoDeExportacionLista> registro)
    : INotificationHandler<DomainEventNotification<ExportacionListaEvent>>
{
    public async Task Handle(DomainEventNotification<ExportacionListaEvent> notificacion, CancellationToken ct)
    {
        var evento = notificacion.DomainEvent;

        await AvisosDeExportacion.AvisarAsync(
            mediator, preferencias, registro,
            evento.TenantId, evento.SolicitadaPorId,
            "Tu exportación está lista",
            $"«{evento.NombreDeFichero}» ya se puede descargar.",
            ct);
    }
}

public sealed class AvisoDeExportacionFallida(
    IMediator mediator,
    IRepositorioDePreferencias preferencias,
    ILogger<AvisoDeExportacionFallida> registro)
    : INotificationHandler<DomainEventNotification<ExportacionFallidaEvent>>
{
    public async Task Handle(DomainEventNotification<ExportacionFallidaEvent> notificacion, CancellationToken ct)
    {
        var evento = notificacion.DomainEvent;

        await AvisosDeExportacion.AvisarAsync(
            mediator, preferencias, registro,
            evento.TenantId, evento.SolicitadaPorId,
            "Tu exportación no salió",
            // El motivo va en el aviso, no sólo en la pantalla de exportaciones. Quien recibe
            // «falló» a secas tiene que ir a buscar el porqué; quien recibe el porqué a veces
            // puede arreglarlo solo.
            $"No se pudo generar el informe: {evento.Error}",
            ct);
    }
}

internal static class AvisosDeExportacion
{
    public static async Task AvisarAsync(
        IMediator mediator,
        IRepositorioDePreferencias preferencias,
        ILogger registro,
        Guid tenantId,
        Guid destinatario,
        string asunto,
        string cuerpo,
        CancellationToken ct)
    {
        if (destinatario == Guid.Empty) return;

        // Las preferencias mandan, igual que en las automatizaciones. Se usa la propia función
        // del dominio de Notifications en vez de repetir aquí las reglas —incluidas las horas de
        // silencio y su cruce de medianoche—: son las mismas reglas y no pueden divergir.
        var suyas = await preferencias.DeLaPersonaAsync(tenantId, destinatario, ct)
                    ?? PreferenciasDeNotificacion.PorDefecto(tenantId, destinatario);

        var ahora = TimeOnly.FromDateTime(DateTime.UtcNow);

        if (!suyas.DejaPasar(TiposDeAviso.ExportacionLista, ahora))
        {
            // No es un fallo: es la persona ejerciendo la preferencia que la pantalla le ofrece.
            // La exportación sigue estando en su lista, así que apagar el aviso no esconde el
            // fichero, sólo deja de interrumpir.
            return;
        }

        var resultado = await mediator.Send(new CreateNotificationCommand(
            TenantId: tenantId,
            RecipientUserId: destinatario,
            Type: "InApp",
            Subject: asunto,
            Body: cuerpo,
            // Sin remitente: no lo manda una persona, lo manda el propio sistema al terminar un
            // trabajo que esa misma persona pidió.
            SenderUserId: null), ct);

        if (!resultado.IsSuccess)
        {
            // Que el aviso no salga no puede tumbar la exportación: el fichero ya está generado y
            // se descarga igual desde la pantalla. Se anota y se sigue.
            registro.LogWarning(
                "No se pudo avisar a {Persona} de su exportación: {Error}", destinatario, resultado.Error);
        }
    }
}
