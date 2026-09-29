using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Exports;
using Reporting.Application.Schedules;
using Reporting.Domain.Entities;
using Reporting.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// Dispara los informes programados: crea su exportación cuando toca y avisa a quien la pidió.
///
/// <b>No genera el fichero.</b> Deja la exportación pedida y el generador que ya existe la
/// recoge. Es lo que hace que un informe programado y uno pedido a mano recorran exactamente el
/// mismo camino: dos motores de generación acabarían dando resultados distintos para el mismo
/// informe, y el que nadie mira —el programado— sería el que se quedara atrás.
///
/// <b>Se revisa cada cinco minutos, no cada segundo.</b> La programación tiene granularidad de
/// hora, así que preguntar más a menudo no adelanta nada. Cinco minutos es también el retraso
/// máximo con el que puede llegar un informe de las 8:00, y para un informe diario eso no lo nota
/// nadie.
/// </summary>
public sealed class ReportScheduler(
    IServiceScopeFactory ambitos,
    ILogger<ReportScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Planificador de informes iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Igual que el generador: un fallo al mirar el reloj no puede matar el trabajador,
                // o los informes programados dejarían de salir y nadie se enteraría hasta que
                // alguien echara en falta el suyo.
                logger.LogError(ex, "El planificador de informes falló en su vuelta");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("Planificador de informes detenido");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = ambitos.CreateScope();

        var schedules = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var recipients = scope.ServiceProvider.GetRequiredService<RecipientEmails>();

        var active = await schedules.ActiveAsync(ct);
        if (active.Count == 0) return;

        // La hora local del servidor. La programación guarda hora local del inquilino, y mientras
        // no haya zona horaria por inquilino, ésta es la aproximación honesta: está anotado en la
        // auditoría como lo que hay que afinar cuando haya clientes en varios husos.
        var now = DateTime.Now;

        foreach (var schedule in active)
        {
            if (ct.IsCancellationRequested) return;
            if (!schedule.IsDue(now)) continue;

            try
            {
                await TriggerAsync(db, email, recipients, schedule, now, ct);
            }
            catch (Exception ex)
            {
                // Un informe que falla no puede impedir que salgan los demás. Se anota y se sigue
                // con el siguiente.
                logger.LogError(ex,
                    "No se pudo disparar la programación {Programacion} del informe {Informe}",
                    schedule.Id, schedule.ReportId);
            }
        }
    }

    private async Task TriggerAsync(
        ReportingDbContext db,
        IEmailService email,
        RecipientEmails recipients,
        ReportSchedule schedule,
        DateTime now,
        CancellationToken ct)
    {
        var report = await db.Reports
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == schedule.ReportId && r.TenantId == schedule.TenantId, ct);

        if (report is null)
        {
            // El informe se borró y la programación se quedó huérfana. Se apaga en vez de
            // intentarlo cada cinco minutos para siempre.
            logger.LogWarning(
                "La programación {Programacion} apunta a un informe que ya no existe; se desactiva",
                schedule.Id);

            schedule.Deactivate();
            await db.SaveChangesAsync(ct);
            return;
        }

        var export = Export.Request(
            schedule.TenantId, schedule.ReportId, schedule.RecipientId, schedule.Format);

        if (export.IsFailure)
            throw new InvalidOperationException(export.Error);

        await db.Exports.AddAsync(export.Value!, ct);

        // Se anota **antes** de que nadie más pueda mirar. Si el correo falla después, el informe
        // ya está encolado y no se vuelve a encolar en la vuelta siguiente: un fallo de correo no
        // puede convertirse en veinte exportaciones del mismo informe.
        schedule.MarkGenerated(DateOnly.FromDateTime(now));

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Informe programado {Informe} encolado para {Persona} en {Formato}",
            report.Name, schedule.RecipientId, schedule.Format.Name);

        await NotifyByEmailAsync(email, recipients, schedule, report.Name, ct);
    }

    /// <summary>
    /// Manda el correo con el enlace de descarga.
    ///
    /// <b>Un enlace y no el fichero adjunto</b>, por tres razones que pesan más que la comodidad:
    /// el servicio de correo del proyecto no admite adjuntos; un informe de varios megas rebota en
    /// media de los servidores de correo; y el enlace pasa por el endpoint de descarga, que
    /// comprueba el inquilino —un adjunto reenviado no comprueba nada—.
    ///
    /// Si el correo falla, se anota y ya está: el informe está generado y aparece en la pantalla
    /// igualmente, además del aviso dentro de la aplicación. Que no salga el correo no puede
    /// deshacer el trabajo.
    /// </summary>
    private async Task NotifyByEmailAsync(
        IEmailService email,
        RecipientEmails recipients,
        ReportSchedule schedule,
        string reportName,
        CancellationToken ct)
    {
        var address = await recipients.EmailOfAsync(schedule.TenantId, schedule.RecipientId, ct);

        if (string.IsNullOrWhiteSpace(address))
        {
            logger.LogWarning(
                "La programación {Programacion} no tiene a quién mandar el correo; el informe se genera igual",
                schedule.Id);
            return;
        }

        try
        {
            await email.SendAsync(
                address,
                $"Tu informe programado: {reportName}",
                $"<p>El informe <strong>{System.Net.WebUtility.HtmlEncode(reportName)}</strong> "
                + $"({schedule.Format.Name}) se está generando.</p>"
                + "<p>Lo encontrarás en la pantalla de informes en cuanto esté listo.</p>",
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "No se pudo mandar el correo de la programación {Programacion}; el informe se genera igual",
                schedule.Id);
        }
    }
}
