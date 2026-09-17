using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Entrada;

/// <summary>Dónde viven las claves de entrada. Ver <see cref="ClaveDeEntrada"/>.</summary>
public interface IClavesDeEntradaRepository
{
    /// <summary>
    /// Busca una clave activa por su hash, <b>en cualquier organización</b>.
    ///
    /// Es la única consulta del módulo que cruza inquilinos, y tiene que hacerlo: quien envía un
    /// ticket desde fuera no tiene sesión, y la organización es justo lo que la clave dice.
    /// </summary>
    Task<ClaveDeEntrada?> BuscarActivaPorHashAsync(string hash, CancellationToken ct);

    Task<List<ClaveDeEntrada>> DeLaOrganizacionAsync(Guid tenantId, CancellationToken ct);
    Task<ClaveDeEntrada?> PorIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task AddAsync(ClaveDeEntrada clave, CancellationToken ct);
}

public interface IAdjuntosDeTicketRepository
{
    Task<List<AdjuntoDeTicket>> DelTicketAsync(Guid tenantId, Guid ticketId, CancellationToken ct);
    Task AddAsync(AdjuntoDeTicket adjunto, CancellationToken ct);
}

/// <summary>Un fichero recibido en la petición, antes de guardarlo.</summary>
public sealed record FicheroRecibido(string Nombre, string TipoDeContenido, long Tamano, Func<Stream> Abrir);

/// <summary>Una clave tal como se enseña en la lista: sin la clave.</summary>
public sealed record ClaveDeEntradaDto(
    Guid Id, string Nombre, string Inicio, DateTime CreadaUtc, DateTime? UltimoUsoUtc, DateTime? RevocadaUtc);

/// <summary>Lo que se devuelve al crear una clave: la única vez que se ve entera.</summary>
public sealed record ClaveDeEntradaCreadaDto(Guid Id, string Nombre, string Inicio, string Clave);

public sealed record AdjuntoDeTicketDto(
    Guid Id, string Nombre, string Url, string TipoDeContenido, long Tamano, DateTime SubidoUtc, bool DesdeFuera);

public sealed record CrearClaveDeEntradaCommand(Guid TenantId, Guid CreadaPor, string Nombre)
    : ICommand<ClaveDeEntradaCreadaDto>;

public sealed record RevocarClaveDeEntradaCommand(Guid TenantId, Guid Id) : ICommand<bool>;

public sealed record GetClavesDeEntradaQuery(Guid TenantId) : IQuery<List<ClaveDeEntradaDto>>;

/// <summary>
/// Un ticket que llega desde fuera con una clave de entrada.
///
/// <b>No implementa <c>IAuthorizeEntity</c> a propósito.</b> No hay usuario que autorizar: la
/// clave es la autorización, y sólo permite esto. Lo que el cliente pudiera mandar sobre la
/// organización no existe en el comando; sale de la clave.
///
/// Obligatorio: asunto, mensaje, nombre, email, teléfono y empresa. Todo lo demás —adjuntos,
/// clasificación, etiquetas, equipo, estado y prioridad— es opcional.
/// </summary>
public sealed record CrearTicketExternoCommand(
    string Clave,
    string? Title,
    string? Description,
    string? RequesterName,
    string? RequesterEmail,
    string? RequesterPhone,
    string? RequesterCompany,
    string? Priority,
    string? Status,
    string? Classification,
    Guid? TeamId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<FicheroRecibido> Adjuntos) : ICommand<TicketExternoCreadoDto>;

public sealed record TicketExternoCreadoDto(Guid Id, string Status, DateTime CreatedAt, int Adjuntos);

/// <summary>Adjuntar imágenes o vídeos a un ticket desde la aplicación.</summary>
public sealed record SubirAdjuntosDeTicketCommand(
    Guid TicketId, Guid SubidoPor, IReadOnlyList<FicheroRecibido> Adjuntos)
    : ICommand<List<AdjuntoDeTicketDto>>, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => TicketId;
    public string RequiredPermission => "Write";
}

public sealed record GetAdjuntosDeTicketQuery(Guid TenantId, Guid TicketId) : IQuery<List<AdjuntoDeTicketDto>>;

public static class ErroresDeEntrada
{
    /// <summary>
    /// El mismo mensaje para una clave que no existe, que está revocada o que no se mandó. Decir
    /// cuál de las tres es ayudaría a quien prueba claves a ciegas, no a quien integra.
    /// </summary>
    public const string ClaveNoValida = "Clave de entrada no válida";

    public const string TicketNoEncontrado = "Ticket no encontrado";
}

/// <summary>Guardar adjuntos, igual desde fuera que desde la ficha.</summary>
internal static class GuardadoDeAdjuntos
{
    public static string? Rechazo(IReadOnlyList<FicheroRecibido> ficheros)
    {
        if (ficheros.Count > ReglasDeAdjuntos.MaximoDeFicheros)
            return $"Se admiten como mucho {ReglasDeAdjuntos.MaximoDeFicheros} adjuntos";

        return ficheros
            .Select(f => ReglasDeAdjuntos.Rechazo(f.Nombre, f.TipoDeContenido, f.Tamano))
            .FirstOrDefault(r => r is not null);
    }

    /// <summary>
    /// Sube los ficheros y devuelve sus adjuntos, o el motivo por el que uno no se pudo guardar.
    /// Si uno falla, borra los que ya subió: un ticket que no se crea no debe dejar ficheros
    /// sueltos en el almacenamiento.
    ///
    /// <b>Un fichero que el almacenamiento rechaza es culpa del fichero, no del servidor.</b>
    /// Salió probando desde el navegador: un vídeo que no era vídeo lo rechazaba Cloudinary y la
    /// entrada respondía 500, así que la web del cliente no podía decir qué adjunto quitar.
    /// </summary>
    public static async Task<(List<AdjuntoDeTicket> Subidos, string? Error)> SubirAsync(
        IStorageService almacen, Ticket ticket, IReadOnlyList<FicheroRecibido> ficheros, Guid? subidoPor, CancellationToken ct)
    {
        var subidos = new List<AdjuntoDeTicket>();
        foreach (var fichero in ficheros)
        {
            string url;
            try
            {
                await using var contenido = fichero.Abrir();
                url = await almacen.UploadFileAsync(contenido, fichero.Nombre, fichero.TipoDeContenido, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await DeshacerAsync(almacen, subidos);
                return (subidos, $"No se pudo guardar «{fichero.Nombre}»: comprueba que es una imagen o un vídeo válido");
            }

            subidos.Add(AdjuntoDeTicket.Crear(ticket, fichero.Nombre, url, fichero.TipoDeContenido, fichero.Tamano, subidoPor, DateTime.UtcNow));
        }
        return (subidos, null);
    }

    public static async Task DeshacerAsync(IStorageService almacen, IEnumerable<AdjuntoDeTicket> subidos)
    {
        foreach (var adjunto in subidos)
        {
            try { await almacen.DeleteFileAsync(adjunto.Url); }
            catch { /* Lo importante es el error original; un fichero huérfano no lo tapa. */ }
        }
    }

    public static AdjuntoDeTicketDto ADto(AdjuntoDeTicket a)
        => new(a.Id, a.Nombre, a.Url, a.TipoDeContenido, a.Tamano, a.SubidoUtc, a.SubidoPor is null);
}

public sealed class CrearClaveDeEntradaHandler(IClavesDeEntradaRepository claves, ITicketingUnitOfWork unitOfWork)
    : ICommandHandler<CrearClaveDeEntradaCommand, ClaveDeEntradaCreadaDto>
{
    public async Task<Result<ClaveDeEntradaCreadaDto>> Handle(CrearClaveDeEntradaCommand request, CancellationToken ct)
    {
        var nombre = request.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length is 0 or > 100)
            return Result<ClaveDeEntradaCreadaDto>.Failure("El nombre de la clave es obligatorio y admite hasta 100 caracteres");

        var (clave, enClaro) = ClaveDeEntrada.Generar(request.TenantId, nombre, request.CreadaPor, DateTime.UtcNow);
        await claves.AddAsync(clave, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<ClaveDeEntradaCreadaDto>.Success(new(clave.Id, clave.Nombre, clave.Inicio, enClaro));
    }
}

public sealed class RevocarClaveDeEntradaHandler(IClavesDeEntradaRepository claves, ITicketingUnitOfWork unitOfWork)
    : ICommandHandler<RevocarClaveDeEntradaCommand, bool>
{
    public async Task<Result<bool>> Handle(RevocarClaveDeEntradaCommand request, CancellationToken ct)
    {
        var clave = await claves.PorIdAsync(request.TenantId, request.Id, ct);
        if (clave is null)
            return Result<bool>.Failure("Clave no encontrada");

        clave.Revocar(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

public sealed class GetClavesDeEntradaHandler(IClavesDeEntradaRepository claves)
    : IQueryHandler<GetClavesDeEntradaQuery, List<ClaveDeEntradaDto>>
{
    public async Task<Result<List<ClaveDeEntradaDto>>> Handle(GetClavesDeEntradaQuery request, CancellationToken ct)
    {
        var lista = await claves.DeLaOrganizacionAsync(request.TenantId, ct);
        return Result<List<ClaveDeEntradaDto>>.Success(lista
            .Select(c => new ClaveDeEntradaDto(c.Id, c.Nombre, c.Inicio, c.CreadaUtc, c.UltimoUsoUtc, c.RevocadaUtc))
            .ToList());
    }
}

public sealed class CrearTicketExternoHandler(
    IClavesDeEntradaRepository claves,
    ITicketRepository tickets,
    IAdjuntosDeTicketRepository adjuntos,
    IStorageService almacen,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<CrearTicketExternoCommand, TicketExternoCreadoDto>
{
    private static readonly (Func<CrearTicketExternoCommand, string?> Campo, string Nombre)[] Obligatorios =
    [
        (r => r.Title, "title"),
        (r => r.Description, "description"),
        (r => r.RequesterName, "requesterName"),
        (r => r.RequesterEmail, "requesterEmail"),
        (r => r.RequesterPhone, "requesterPhone"),
        (r => r.RequesterCompany, "requesterCompany"),
    ];

    public async Task<Result<TicketExternoCreadoDto>> Handle(CrearTicketExternoCommand request, CancellationToken ct)
    {
        // La clave va primero: a quien no la tiene no se le dice nada sobre qué campos faltan.
        if (string.IsNullOrWhiteSpace(request.Clave))
            return Fallo(ErroresDeEntrada.ClaveNoValida);

        var clave = await claves.BuscarActivaPorHashAsync(ClaveDeEntrada.HashDe(request.Clave.Trim()), ct);
        if (clave is null)
            return Fallo(ErroresDeEntrada.ClaveNoValida);

        // Todos los que faltan a la vez, no el primero: quien integra un formulario arregla la
        // lista entera de una pasada en vez de descubrirla campo a campo.
        var faltan = Obligatorios.Where(o => string.IsNullOrWhiteSpace(o.Campo(request))).Select(o => o.Nombre).ToList();
        if (faltan.Count > 0)
            return Fallo("Faltan campos obligatorios: " + string.Join(", ", faltan));

        if (!EsEmail(request.RequesterEmail!.Trim()))
            return Fallo("requesterEmail no es un email válido");

        var largos = new (string? Valor, int Maximo, string Nombre)[]
        {
            (request.RequesterName, 200, "requesterName"),
            (request.RequesterEmail, 320, "requesterEmail"),
            (request.RequesterPhone, 40, "requesterPhone"),
            (request.RequesterCompany, 200, "requesterCompany"),
            (request.Classification, 100, "classification"),
        }.FirstOrDefault(c => c.Valor is not null && c.Valor.Trim().Length > c.Maximo);
        if (largos.Nombre is not null)
            return Fallo($"{largos.Nombre} admite hasta {largos.Maximo} caracteres");

        // Sin prioridad, la media: un formulario de cliente no suele preguntarla.
        var prioridad = string.IsNullOrWhiteSpace(request.Priority)
            ? TicketPriority.Medium
            : TicketPriority.FromName<TicketPriority>(request.Priority.Trim());
        if (prioridad is null)
            return Fallo("priority no es válida: Low, Medium, High o Critical");

        TicketStatus? estado = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            estado = TicketStatus.FromName<TicketStatus>(request.Status.Trim());
            if (estado is null)
                return Fallo("status no es válido: Open, InProgress, PendingInfo, Resolved o Closed");
        }

        if (request.Tags.Count > 20 || request.Tags.Any(t => t.Trim().Length > 40))
            return Fallo("Se admiten hasta 20 etiquetas de hasta 40 caracteres");

        var rechazo = GuardadoDeAdjuntos.Rechazo(request.Adjuntos);
        if (rechazo is not null)
            return Fallo(rechazo);

        var creado = Ticket.CrearDesdeFuera(clave, new SolicitudExterna(
            request.Title!.Trim(), request.Description!.Trim(), prioridad, estado,
            request.RequesterName, request.RequesterEmail, request.RequesterPhone, request.RequesterCompany,
            request.Classification, request.TeamId, request.Tags));
        if (creado.IsFailure)
            return Fallo(creado.Error!);

        var ticket = creado.Value!;
        var (subidos, errorDeSubida) = await GuardadoDeAdjuntos.SubirAsync(almacen, ticket, request.Adjuntos, subidoPor: null, ct);
        if (errorDeSubida is not null)
            return Fallo(errorDeSubida);

        try
        {
            clave.Usada(DateTime.UtcNow);
            await tickets.AddAsync(ticket, ct);
            foreach (var adjunto in subidos)
                await adjuntos.AddAsync(adjunto, ct);

            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await GuardadoDeAdjuntos.DeshacerAsync(almacen, subidos);
            throw;
        }

        return Result<TicketExternoCreadoDto>.Success(new(ticket.Id, ticket.Status.Name, ticket.CreatedAt, subidos.Count));
    }

    private static Result<TicketExternoCreadoDto> Fallo(string error) => Result<TicketExternoCreadoDto>.Failure(error);

    private static bool EsEmail(string email)
        => MailAddress.TryCreate(email, out var direccion) && direccion.Address == email;
}

public sealed class SubirAdjuntosDeTicketHandler(
    IUserContext usuario,
    ITicketRepository tickets,
    IAdjuntosDeTicketRepository adjuntos,
    IStorageService almacen,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<SubirAdjuntosDeTicketCommand, List<AdjuntoDeTicketDto>>
{
    public async Task<Result<List<AdjuntoDeTicketDto>>> Handle(SubirAdjuntosDeTicketCommand request, CancellationToken ct)
    {
        if (request.Adjuntos.Count == 0)
            return Result<List<AdjuntoDeTicketDto>>.Failure("No llegó ningún fichero");

        var rechazo = GuardadoDeAdjuntos.Rechazo(request.Adjuntos);
        if (rechazo is not null)
            return Result<List<AdjuntoDeTicketDto>>.Failure(rechazo);

        var ticket = await tickets.GetByIdAsync(usuario.TenantId, request.TicketId, ct);
        if (ticket is null)
            return Result<List<AdjuntoDeTicketDto>>.Failure(ErroresDeEntrada.TicketNoEncontrado);

        var (subidos, errorDeSubida) = await GuardadoDeAdjuntos.SubirAsync(almacen, ticket, request.Adjuntos, request.SubidoPor, ct);
        if (errorDeSubida is not null)
            return Result<List<AdjuntoDeTicketDto>>.Failure(errorDeSubida);
        try
        {
            foreach (var adjunto in subidos)
                await adjuntos.AddAsync(adjunto, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await GuardadoDeAdjuntos.DeshacerAsync(almacen, subidos);
            throw;
        }

        return Result<List<AdjuntoDeTicketDto>>.Success(subidos.Select(GuardadoDeAdjuntos.ADto).ToList());
    }
}

public sealed class GetAdjuntosDeTicketHandler(IAdjuntosDeTicketRepository adjuntos)
    : IQueryHandler<GetAdjuntosDeTicketQuery, List<AdjuntoDeTicketDto>>
{
    public async Task<Result<List<AdjuntoDeTicketDto>>> Handle(GetAdjuntosDeTicketQuery request, CancellationToken ct)
    {
        var lista = await adjuntos.DelTicketAsync(request.TenantId, request.TicketId, ct);
        return Result<List<AdjuntoDeTicketDto>>.Success(lista.Select(GuardadoDeAdjuntos.ADto).ToList());
    }
}
