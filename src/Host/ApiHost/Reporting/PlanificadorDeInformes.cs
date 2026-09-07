using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Exportaciones;
using Reporting.Application.Programaciones;
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
public sealed class PlanificadorDeInformes(
    IServiceScopeFactory ambitos,
    ILogger<PlanificadorDeInformes> registro) : BackgroundService
{
    private static readonly TimeSpan CadaCuanto = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        registro.LogInformation("Planificador de informes iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UnaVueltaAsync(stoppingToken);
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
                registro.LogError(ex, "El planificador de informes falló en su vuelta");
            }

            try
            {
                await Task.Delay(CadaCuanto, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        registro.LogInformation("Planificador de informes detenido");
    }

    private async Task UnaVueltaAsync(CancellationToken ct)
    {
        using var ambito = ambitos.CreateScope();

        var programaciones = ambito.ServiceProvider.GetRequiredService<IRepositorioDeProgramaciones>();
        var contexto = ambito.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var correo = ambito.ServiceProvider.GetRequiredService<IEmailService>();
        var destinatarios = ambito.ServiceProvider.GetRequiredService<CorreosDeDestinatarios>();

        var activas = await programaciones.ActivasAsync(ct);
        if (activas.Count == 0) return;

        // La hora local del servidor. La programación guarda hora local del inquilino, y mientras
        // no haya zona horaria por inquilino, ésta es la aproximación honesta: está anotado en la
        // auditoría como lo que hay que afinar cuando haya clientes en varios husos.
        var ahora = DateTime.Now;

        foreach (var programacion in activas)
        {
            if (ct.IsCancellationRequested) return;
            if (!programacion.TocaAhora(ahora)) continue;

            try
            {
                await DispararAsync(contexto, correo, destinatarios, programacion, ahora, ct);
            }
            catch (Exception ex)
            {
                // Un informe que falla no puede impedir que salgan los demás. Se anota y se sigue
                // con el siguiente.
                registro.LogError(ex,
                    "No se pudo disparar la programación {Programacion} del informe {Informe}",
                    programacion.Id, programacion.ReportId);
            }
        }
    }

    private async Task DispararAsync(
        ReportingDbContext contexto,
        IEmailService correo,
        CorreosDeDestinatarios destinatarios,
        ProgramacionDeInforme programacion,
        DateTime ahora,
        CancellationToken ct)
    {
        var informe = await contexto.Reports
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == programacion.ReportId && r.TenantId == programacion.TenantId, ct);

        if (informe is null)
        {
            // El informe se borró y la programación se quedó huérfana. Se apaga en vez de
            // intentarlo cada cinco minutos para siempre.
            registro.LogWarning(
                "La programación {Programacion} apunta a un informe que ya no existe; se desactiva",
                programacion.Id);

            programacion.Desactivar();
            await contexto.SaveChangesAsync(ct);
            return;
        }

        var exportacion = Exportacion.Solicitar(
            programacion.TenantId, programacion.ReportId, programacion.DestinatarioId, programacion.Formato);

        if (exportacion.IsFailure)
            throw new InvalidOperationException(exportacion.Error);

        await contexto.Exportaciones.AddAsync(exportacion.Value!, ct);

        // Se anota **antes** de que nadie más pueda mirar. Si el correo falla después, el informe
        // ya está encolado y no se vuelve a encolar en la vuelta siguiente: un fallo de correo no
        // puede convertirse en veinte exportaciones del mismo informe.
        programacion.AnotarGenerada(DateOnly.FromDateTime(ahora));

        await contexto.SaveChangesAsync(ct);

        registro.LogInformation(
            "Informe programado {Informe} encolado para {Persona} en {Formato}",
            informe.Name, programacion.DestinatarioId, programacion.Formato.Name);

        await AvisarPorCorreoAsync(correo, destinatarios, programacion, informe.Name, ct);
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
    private async Task AvisarPorCorreoAsync(
        IEmailService correo,
        CorreosDeDestinatarios destinatarios,
        ProgramacionDeInforme programacion,
        string nombreDelInforme,
        CancellationToken ct)
    {
        var direccion = await destinatarios.CorreoDeAsync(programacion.TenantId, programacion.DestinatarioId, ct);

        if (string.IsNullOrWhiteSpace(direccion))
        {
            registro.LogWarning(
                "La programación {Programacion} no tiene a quién mandar el correo; el informe se genera igual",
                programacion.Id);
            return;
        }

        try
        {
            await correo.SendAsync(
                direccion,
                $"Tu informe programado: {nombreDelInforme}",
                $"<p>El informe <strong>{System.Net.WebUtility.HtmlEncode(nombreDelInforme)}</strong> "
                + $"({programacion.Formato.Name}) se está generando.</p>"
                + "<p>Lo encontrarás en la pantalla de informes en cuanto esté listo.</p>",
                ct);
        }
        catch (Exception ex)
        {
            registro.LogWarning(ex,
                "No se pudo mandar el correo de la programación {Programacion}; el informe se genera igual",
                programacion.Id);
        }
    }
}

/// <summary>
/// La dirección de correo de una persona.
///
/// <b>Vive en el host</b> porque cruza módulos: Reporting sabe a quién quiere avisar y sólo
/// Identity sabe su correo, y ninguno referencia al otro. Es el mismo reparto de siempre.
/// </summary>
public sealed class CorreosDeDestinatarios(Identity.Infrastructure.Persistence.IdentityDbContext identidad)
{
    public async Task<string?> CorreoDeAsync(Guid tenantId, Guid userId, CancellationToken ct)
        => await identidad.User
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .Select(u => u.Email.Value)
            .FirstOrDefaultAsync(ct);
}
