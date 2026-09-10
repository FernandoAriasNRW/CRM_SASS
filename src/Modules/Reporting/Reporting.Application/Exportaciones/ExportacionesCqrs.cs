using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exportaciones;

/// <summary>
/// Cómo se ve una exportación desde fuera.
///
/// Lleva el error dentro, no en un canal aparte: la pantalla que pinta la lista es la misma que
/// tiene que explicar por qué una falló, y obligarla a una segunda llamada para eso garantiza
/// que alguien se la salte y enseñe «Fallida» a secas.
/// </summary>
public sealed record ExportacionDto(
    Guid Id,
    Guid ReportId,
    string Formato,
    string Estado,
    DateTime SolicitadaUtc,
    DateTime? TerminadaUtc,
    string? NombreDeFichero,
    long TamanoBytes,
    string? Error,
    int Intentos);

/// <summary>
/// Pide exportar un informe y **devuelve enseguida**.
///
/// Es la primera condición del plan: «el usuario pide la exportación y recupera el control
/// inmediatamente; no se queda mirando una barra». Este comando sólo deja la petición apuntada;
/// el fichero lo hace un trabajador en segundo plano.
///
/// Hacerlo aquí, en la petición HTTP, sería más corto de escribir y tendría dos problemas: un
/// informe grande agotaría el tiempo de espera del navegador, y no serviría para los informes
/// programados, que ocurren sin nadie delante.
/// </summary>
public sealed record SolicitarExportacionCommand(
    Guid TenantId,
    Guid ReportId,
    Guid SolicitadaPorId,
    string Formato) : ICommand<ExportacionDto>;

public sealed record GetExportacionesQuery(Guid TenantId, Guid ReportId) : IQuery<IReadOnlyList<ExportacionDto>>;

public sealed record GetExportacionQuery(Guid TenantId, Guid ExportacionId) : IQuery<ExportacionDto>;

/// <summary>El fichero listo para servir. Sólo lo pide el endpoint de descarga.</summary>
public sealed record DescargarExportacionQuery(Guid TenantId, Guid ExportacionId) : IQuery<FicheroExportado>;

public sealed record FicheroExportado(string Nombre, string TipoDeContenido, byte[] Bytes);

public sealed class SolicitarExportacionHandler(
    IReportRepository informes,
    IRepositorioDeExportaciones exportaciones,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<SolicitarExportacionCommand, ExportacionDto>
{
    public async Task<Result<ExportacionDto>> Handle(SolicitarExportacionCommand request, CancellationToken ct)
    {
        var formato = ReportFormat.FromName<ReportFormat>(request.Formato);
        if (formato is null)
        {
            // Se dice cuáles valen. El mensaje genérico anterior de este módulo —«Invalid report
            // type or format»— dejaba a quien lo recibía sin saber qué cambiar.
            return Result<ExportacionDto>.Failure(
                $"El formato «{request.Formato}» no existe. Los que hay: "
                + string.Join(", ", ReportFormat.All().Select(f => f.Name)));
        }

        var informe = await informes.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (informe is null)
            return Result<ExportacionDto>.Failure("El informe no existe");

        // Si ya hay una igual en marcha, se devuelve **esa** en vez de encolar otra.
        //
        // Sin esto, pulsar dos veces el botón genera el mismo fichero dos veces y manda dos
        // avisos. Se compara por formato porque pedir el mismo informe en PDF y en Excel sí son
        // dos trabajos distintos.
        var enMarcha = await exportaciones.EnMarchaAsync(
            request.TenantId, request.ReportId, formato.Value, request.SolicitadaPorId, ct);

        if (enMarcha is not null)
            return Result<ExportacionDto>.Success(ADto(enMarcha));

        var nueva = Exportacion.Solicitar(request.TenantId, request.ReportId, request.SolicitadaPorId, formato);
        if (nueva.IsFailure)
            return Result<ExportacionDto>.Failure(nueva.Error!);

        await exportaciones.AnadirAsync(nueva.Value!, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<ExportacionDto>.Success(ADto(nueva.Value!));
    }

    internal static ExportacionDto ADto(Exportacion e) => new(
        e.Id, e.ReportId, e.Formato.Name, e.Estado.Name,
        e.SolicitadaUtc, e.TerminadaUtc, e.NombreDeFichero, e.TamanoBytes, e.Error, e.Intentos);
}

public sealed class GetExportacionesHandler(IRepositorioDeExportaciones exportaciones)
    : IQueryHandler<GetExportacionesQuery, IReadOnlyList<ExportacionDto>>
{
    public async Task<Result<IReadOnlyList<ExportacionDto>>> Handle(GetExportacionesQuery request, CancellationToken ct)
    {
        var lista = await exportaciones.DelInformeAsync(request.TenantId, request.ReportId, ct);

        return Result<IReadOnlyList<ExportacionDto>>.Success(
            lista.Select(SolicitarExportacionHandler.ADto).ToList());
    }
}

public sealed class GetExportacionHandler(IRepositorioDeExportaciones exportaciones)
    : IQueryHandler<GetExportacionQuery, ExportacionDto>
{
    public async Task<Result<ExportacionDto>> Handle(GetExportacionQuery request, CancellationToken ct)
    {
        var exportacion = await exportaciones.PorIdAsync(request.TenantId, request.ExportacionId, ct);

        return exportacion is null
            ? Result<ExportacionDto>.Failure("Esa exportación no existe")
            : Result<ExportacionDto>.Success(SolicitarExportacionHandler.ADto(exportacion));
    }
}

public sealed class DescargarExportacionHandler(IRepositorioDeExportaciones exportaciones)
    : IQueryHandler<DescargarExportacionQuery, FicheroExportado>
{
    public async Task<Result<FicheroExportado>> Handle(DescargarExportacionQuery request, CancellationToken ct)
    {
        var exportacion = await exportaciones.PorIdAsync(request.TenantId, request.ExportacionId, ct);
        if (exportacion is null)
            return Result<FicheroExportado>.Failure("Esa exportación no existe");

        // Se distingue «todavía no» de «no salió». Son dos respuestas distintas para quien
        // pregunta, y juntarlas en un «no disponible» deja a la pantalla adivinando si merece la
        // pena reintentar.
        if (exportacion.Estado != EstadoDeExportacion.Lista)
        {
            return Result<FicheroExportado>.Failure(
                exportacion.Estado == EstadoDeExportacion.Fallida
                    ? $"La exportación falló: {exportacion.Error}"
                    : $"La exportación todavía no está lista (está {exportacion.Estado.Name.ToLowerInvariant()})");
        }

        var contenido = await exportaciones.ContenidoAsync(request.TenantId, request.ExportacionId, ct);

        // Estado «Lista» sin bytes es una incoherencia, no un caso de uso. Se dice tal cual en
        // vez de devolver un fichero vacío, que en el navegador parece un fichero corrupto.
        if (contenido is null)
            return Result<FicheroExportado>.Failure("La exportación consta como lista pero no tiene fichero");

        return Result<FicheroExportado>.Success(
            new FicheroExportado(exportacion.NombreDeFichero ?? "informe", contenido.TipoDeContenido, contenido.Bytes));
    }
}

/// <summary>
/// Acceso a las exportaciones y a sus ficheros.
///
/// El trabajador de segundo plano usa <see cref="PendientesAsync"/>; todo lo demás es para las
/// pantallas.
/// </summary>
public interface IRepositorioDeExportaciones
{
    Task<Exportacion?> PorIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Exportacion>> DelInformeAsync(Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// La misma exportación ya pedida y sin terminar, si la hay. Evita duplicar el trabajo cuando
    /// alguien pulsa dos veces.
    /// </summary>
    Task<Exportacion?> EnMarchaAsync(Guid tenantId, Guid reportId, int formatoValue, Guid solicitadaPorId, CancellationToken ct = default);

    /// <summary>
    /// Lo que hay por generar, **de todos los inquilinos**.
    ///
    /// El trabajador corre sin petición y por tanto sin inquilino, así que esta consulta cruza a
    /// propósito el filtro global. Es la excepción que el propio <c>TenantDbContext</c> documenta
    /// —«un proceso que legítimamente deba cruzar tenants ha de declararlo explícitamente»— y por
    /// eso está aquí, en un método con nombre, y no repartida por el código del trabajador.
    /// </summary>
    Task<IReadOnlyList<Exportacion>> PendientesAsync(int cuantas, CancellationToken ct = default);

    Task AnadirAsync(Exportacion exportacion, CancellationToken ct = default);

    Task GuardarContenidoAsync(ContenidoDeExportacion contenido, CancellationToken ct = default);

    Task<ContenidoDeExportacion?> ContenidoAsync(Guid tenantId, Guid exportacionId, CancellationToken ct = default);

    /// <summary>Guarda los cambios del trabajador, que no tiene unidad de trabajo de petición.</summary>
    Task GuardarAsync(CancellationToken ct = default);
}
