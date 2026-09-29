using Microsoft.EntityFrameworkCore;
using Reporting.Application.Abstractions;
using Reporting.Application.Exports;
using Reporting.Domain.Entities;
using Reporting.Infrastructure.Exports;
using Reporting.Infrastructure.Persistence;

namespace ApiHost.Reporting;

/// <summary>
/// Genera los ficheros de las exportaciones pendientes, fuera de la petición HTTP.
///
/// <b>Por qué en segundo plano y no en el endpoint.</b> El plan lo pedía en primer lugar: quien
/// exporta «recupera el control inmediatamente; no se queda mirando una barra». Y hay una razón
/// más que no es de comodidad: los informes programados se generan sin nadie delante, así que el
/// camino de generación no puede depender de que haya una petición abierta. Un solo camino para
/// las dos cosas evita tener dos motores que se separan.
///
/// <b>Sondeo y no cola de mensajes.</b> El proyecto tiene RabbitMQ, y aun así esto pregunta a la
/// base cada pocos segundos. Es a propósito: el estado de la exportación **ya tiene que estar en
/// la base** —la pantalla lo consulta y hay que sobrevivir a un reinicio—, así que una cola
/// añadiría un segundo sitio donde vive el mismo trabajo, con la posibilidad de que discrepen.
/// Con este volumen, una consulta indexada cada cinco segundos no se nota.
///
/// <b>Lo que este trabajador nunca hace: dejar algo en «generando» para siempre.</b> Era el aviso
/// del plan —«una exportación que se queda en generando para siempre es peor que un error,
/// porque nadie sabe si esperar»—. Si el proceso muere a mitad, la fila queda «generando» y la
/// siguiente vuelta la recoge pasado <see cref="Export.GivenUpAfter"/>; si se agotan
/// los intentos, se marca fallida **con el motivo**.
/// </summary>
public sealed class ExportGenerator(
    IServiceScopeFactory ambitos,
    ILogger<ExportGenerator> logger) : BackgroundService
{
    /// <summary>
    /// Cada cuánto se pregunta por trabajo nuevo.
    ///
    /// Cinco segundos es lo que tarda alguien en mirar la pantalla después de pulsar «exportar»:
    /// más y parece que no pasa nada, menos y son consultas de sobra.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cuántas se cogen por vuelta.
    ///
    /// Se procesan de una en una dentro de la tanda. Hacerlas en paralelo tentaría, pero cada una
    /// consulta tres módulos y construye un fichero en memoria: en paralelo, cinco informes
    /// grandes a la vez se comen la memoria del proceso que además atiende las peticiones.
    /// </summary>
    private const int BatchSize = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Generador de exportaciones iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Un fallo al buscar trabajo —la base caída, por ejemplo— no puede tumbar el
                // trabajador: si muere, las exportaciones dejan de generarse para siempre y nadie
                // se entera hasta que alguien pregunte. Se anota y se sigue en la vuelta
                // siguiente.
                logger.LogError(ex, "El generador de exportaciones falló buscando trabajo");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("Generador de exportaciones detenido");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = ambitos.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IExportRepository>();

        var pending = await repository.PendingAsync(BatchSize, ct);
        if (pending.Count == 0) return;

        foreach (var export in pending)
        {
            if (ct.IsCancellationRequested) return;

            // Cada exportación en su propio ámbito: los DbContext no se comparten entre
            // trabajos, así que un informe grande no deja el rastreador de cambios lleno para el
            // siguiente, y un fallo no arrastra al resto de la tanda.
            using var own = ambitos.CreateScope();
            await ProcessOneAsync(own.ServiceProvider, export.Id, ct);
        }
    }

    private async Task ProcessOneAsync(IServiceProvider services, Guid exportId, CancellationToken ct)
    {
        var db = services.GetRequiredService<ReportingDbContext>();
        var data = services.GetRequiredService<ReportData>();
        var writers = services.GetRequiredService<ReportWriters>();

        // Se guarda por la unidad de trabajo y no por el contexto: es lo único que reparte los
        // eventos de dominio en proceso, y de eso depende que salga el aviso. Guardando por el
        // contexto, la exportación terminaba, el fichero quedaba bien y **nadie se enteraba**.
        var unitOfWork = services.GetRequiredService<IReportingUnitOfWork>();

        // Se relee sin el filtro de inquilino porque aquí no hay petición: el trabajo trae su
        // propio TenantId y con él se buscan el informe y todo lo demás.
        var export = await db.Exports
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == exportId, ct);

        if (export is null) return;

        // Segunda comprobación, ahora sobre la fila recién leída. Entre que la tanda la eligió y
        // este momento, otro proceso pudo cogerla; `Comenzar` devuelve false y se pasa.
        if (!export.Start()) return;

        // «Empezar» no levanta ningún evento, así que aquí basta con guardar.
        await db.SaveChangesAsync(ct);

        try
        {
            var report = await db.Reports
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == export.ReportId && r.TenantId == export.TenantId, ct);

            if (report is null)
                throw new InvalidOperationException("El informe ya no existe");

            var table = await data.ResolveAsync(report, ct);
            var writer = writers.For(export.Format.Name);
            var bytes = writer.Write(table);

            var name = FileName(report.Name, writer.Extension);

            var content = ExportContent.Create(
                export.TenantId, export.Id, bytes, writer.ContentType);

            await db.ExportContents.AddAsync(content, ct);

            export.Finish(name, bytes.LongLength);
            await unitOfWork.SaveChangesAndDispatchAsync(ct);

            logger.LogInformation(
                "Exportación {Exportacion} lista: {Nombre}, {Bytes} bytes",
                export.Id, name, bytes.LongLength);
        }
        catch (Exception ex)
        {
            // El motivo se guarda en la fila, no sólo en el registro del servidor: quien pregunta
            // «¿por qué no salió mi informe?» no tiene acceso a los registros del servidor.
            logger.LogError(ex, "La exportación {Exportacion} falló", export.Id);

            export.Fail(ex.Message);

            try
            {
                // También del fallo se avisa, así que también va por la unidad de trabajo.
                await unitOfWork.SaveChangesAndDispatchAsync(ct);
            }
            catch (Exception onSaved)
            {
                // Si ni siquiera se puede anotar el fallo, la exportación se quedaría colgada.
                // La recogerá el reintento por antigüedad, y esto queda dicho para que quien lea
                // el registro sepa por qué.
                logger.LogError(onSaved,
                    "No se pudo anotar el fallo de la exportación {Exportacion}; quedará para reintento",
                    export.Id);
            }
        }
    }

    /// <summary>
    /// El nombre con el que se descarga.
    ///
    /// Lleva la fecha porque quien exporta el mismo informe dos semanas seguidas acaba con dos
    /// ficheros en la carpeta de descargas y necesita distinguirlos. Y se limpian los caracteres
    /// que Windows no admite en un nombre de fichero: un informe llamado «Ventas 2026/2027»
    /// generaría una ruta con una carpeta por medio.
    /// </summary>
    private static string FileName(string reportName, string extension)
    {
        var forbidden = Path.GetInvalidFileNameChars();
        var clean = new string(reportName.Where(c => !forbidden.Contains(c)).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(clean)) clean = "informe";
        if (clean.Length > 80) clean = clean[..80];

        return $"{clean} {DateTime.UtcNow:yyyy-MM-dd}{extension}";
    }
}
