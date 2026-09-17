using Microsoft.EntityFrameworkCore;
using Reporting.Application.Abstractions;
using Reporting.Application.Exportaciones;
using Reporting.Domain.Entities;
using Reporting.Infrastructure.Exportaciones;
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
/// siguiente vuelta la recoge pasado <see cref="Exportacion.SeDaPorPerdidaTras"/>; si se agotan
/// los intentos, se marca fallida **con el motivo**.
/// </summary>
public sealed class GeneradorDeExportaciones(
    IServiceScopeFactory ambitos,
    ILogger<GeneradorDeExportaciones> registro) : BackgroundService
{
    /// <summary>
    /// Cada cuánto se pregunta por trabajo nuevo.
    ///
    /// Cinco segundos es lo que tarda alguien en mirar la pantalla después de pulsar «exportar»:
    /// más y parece que no pasa nada, menos y son consultas de sobra.
    /// </summary>
    private static readonly TimeSpan CadaCuanto = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cuántas se cogen por vuelta.
    ///
    /// Se procesan de una en una dentro de la tanda. Hacerlas en paralelo tentaría, pero cada una
    /// consulta tres módulos y construye un fichero en memoria: en paralelo, cinco informes
    /// grandes a la vez se comen la memoria del proceso que además atiende las peticiones.
    /// </summary>
    private const int PorTanda = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        registro.LogInformation("Generador de exportaciones iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UnaTandaAsync(stoppingToken);
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
                registro.LogError(ex, "El generador de exportaciones falló buscando trabajo");
            }

            try
            {
                await Task.Delay(CadaCuanto, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        registro.LogInformation("Generador de exportaciones detenido");
    }

    private async Task UnaTandaAsync(CancellationToken ct)
    {
        using var ambito = ambitos.CreateScope();
        var repositorio = ambito.ServiceProvider.GetRequiredService<IRepositorioDeExportaciones>();

        var pendientes = await repositorio.PendientesAsync(PorTanda, ct);
        if (pendientes.Count == 0) return;

        foreach (var exportacion in pendientes)
        {
            if (ct.IsCancellationRequested) return;

            // Cada exportación en su propio ámbito: los DbContext no se comparten entre
            // trabajos, así que un informe grande no deja el rastreador de cambios lleno para el
            // siguiente, y un fallo no arrastra al resto de la tanda.
            using var suyo = ambitos.CreateScope();
            await UnaAsync(suyo.ServiceProvider, exportacion.Id, ct);
        }
    }

    private async Task UnaAsync(IServiceProvider servicios, Guid exportacionId, CancellationToken ct)
    {
        var contexto = servicios.GetRequiredService<ReportingDbContext>();
        var datos = servicios.GetRequiredService<DatosDelInforme>();
        var escritores = servicios.GetRequiredService<EscritoresDeInforme>();

        // Se guarda por la unidad de trabajo y no por el contexto: es lo único que reparte los
        // eventos de dominio en proceso, y de eso depende que salga el aviso. Guardando por el
        // contexto, la exportación terminaba, el fichero quedaba bien y **nadie se enteraba**.
        var unidadDeTrabajo = servicios.GetRequiredService<IReportingUnitOfWork>();

        // Se relee sin el filtro de inquilino porque aquí no hay petición: el trabajo trae su
        // propio TenantId y con él se buscan el informe y todo lo demás.
        var exportacion = await contexto.Exportaciones
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == exportacionId, ct);

        if (exportacion is null) return;

        // Segunda comprobación, ahora sobre la fila recién leída. Entre que la tanda la eligió y
        // este momento, otro proceso pudo cogerla; `Comenzar` devuelve false y se pasa.
        if (!exportacion.Comenzar()) return;

        // «Empezar» no levanta ningún evento, así que aquí basta con guardar.
        await contexto.SaveChangesAsync(ct);

        try
        {
            var informe = await contexto.Reports
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == exportacion.ReportId && r.TenantId == exportacion.TenantId, ct);

            if (informe is null)
                throw new InvalidOperationException("El informe ya no existe");

            var tabla = await datos.ResolveAsync(informe, ct);
            var escritor = escritores.Para(exportacion.Formato.Name);
            var bytes = escritor.Escribir(tabla);

            var nombre = NombreDeFichero(informe.Name, escritor.Extension);

            var contenido = ContenidoDeExportacion.Crear(
                exportacion.TenantId, exportacion.Id, bytes, escritor.TipoDeContenido);

            await contexto.ContenidosDeExportacion.AddAsync(contenido, ct);

            exportacion.Terminar(nombre, bytes.LongLength);
            await unidadDeTrabajo.SaveChangesAndDispatchAsync(ct);

            registro.LogInformation(
                "Exportación {Exportacion} lista: {Nombre}, {Bytes} bytes",
                exportacion.Id, nombre, bytes.LongLength);
        }
        catch (Exception ex)
        {
            // El motivo se guarda en la fila, no sólo en el registro del servidor: quien pregunta
            // «¿por qué no salió mi informe?» no tiene acceso a los registros del servidor.
            registro.LogError(ex, "La exportación {Exportacion} falló", exportacion.Id);

            exportacion.Fallar(ex.Message);

            try
            {
                // También del fallo se avisa, así que también va por la unidad de trabajo.
                await unidadDeTrabajo.SaveChangesAndDispatchAsync(ct);
            }
            catch (Exception alGuardar)
            {
                // Si ni siquiera se puede anotar el fallo, la exportación se quedaría colgada.
                // La recogerá el reintento por antigüedad, y esto queda dicho para que quien lea
                // el registro sepa por qué.
                registro.LogError(alGuardar,
                    "No se pudo anotar el fallo de la exportación {Exportacion}; quedará para reintento",
                    exportacion.Id);
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
    private static string NombreDeFichero(string nombreDelInforme, string extension)
    {
        var prohibidos = Path.GetInvalidFileNameChars();
        var limpio = new string(nombreDelInforme.Where(c => !prohibidos.Contains(c)).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(limpio)) limpio = "informe";
        if (limpio.Length > 80) limpio = limpio[..80];

        return $"{limpio} {DateTime.UtcNow:yyyy-MM-dd}{extension}";
    }
}
