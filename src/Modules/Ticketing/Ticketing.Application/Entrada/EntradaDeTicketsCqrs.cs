using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
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

/// <summary>Una clave tal como se enseña en la lista: sin la clave.</summary>
public sealed record ClaveDeEntradaDto(
    Guid Id, string Nombre, string Inicio, DateTime CreadaUtc, DateTime? UltimoUsoUtc, DateTime? RevocadaUtc);

/// <summary>Lo que se devuelve al crear una clave: la única vez que se ve entera.</summary>
public sealed record ClaveDeEntradaCreadaDto(Guid Id, string Nombre, string Inicio, string Clave);

public sealed record CrearClaveDeEntradaCommand(Guid TenantId, Guid CreadaPor, string Nombre)
    : ICommand<ClaveDeEntradaCreadaDto>;

public sealed record RevocarClaveDeEntradaCommand(Guid TenantId, Guid Id) : ICommand<bool>;

public sealed record GetClavesDeEntradaQuery(Guid TenantId) : IQuery<List<ClaveDeEntradaDto>>;

/// <summary>
/// Un ticket que llega desde fuera con una clave de entrada.
///
/// <b>No implementa <c>IAuthorizeEntity</c> a propósito.</b> No hay usuario que autorizar: la
/// clave es la autorización, y sólo permite esto. Lo que el cliente manda sobre la organización no
/// existe en el comando; sale de la clave.
/// </summary>
public sealed record CrearTicketExternoCommand(
    string Clave,
    string Title,
    string Description,
    string? Priority,
    string? SolicitanteNombre,
    string? SolicitanteEmail) : ICommand<TicketExternoCreadoDto>;

public sealed record TicketExternoCreadoDto(Guid Id, string Status, DateTime CreatedAt);

public static class ErroresDeEntrada
{
    /// <summary>
    /// El mismo mensaje para una clave que no existe, que está revocada o que no se mandó. Decir
    /// cuál de las tres es ayudaría a quien prueba claves a ciegas, no a quien integra.
    /// </summary>
    public const string ClaveNoValida = "Clave de entrada no válida";
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
    Abstractions.Repositories.ITicketRepository tickets,
    ITicketingUnitOfWork unitOfWork) : ICommandHandler<CrearTicketExternoCommand, TicketExternoCreadoDto>
{
    public async Task<Result<TicketExternoCreadoDto>> Handle(CrearTicketExternoCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Clave))
            return Result<TicketExternoCreadoDto>.Failure(ErroresDeEntrada.ClaveNoValida);

        var clave = await claves.BuscarActivaPorHashAsync(ClaveDeEntrada.HashDe(request.Clave.Trim()), ct);
        if (clave is null)
            return Result<TicketExternoCreadoDto>.Failure(ErroresDeEntrada.ClaveNoValida);

        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<TicketExternoCreadoDto>.Failure("La descripción es obligatoria");

        // Sin prioridad, «Medium»: un formulario de cliente no suele preguntarla, y exigirla
        // obligaría a cada web a inventarse una.
        var priority = string.IsNullOrWhiteSpace(request.Priority)
            ? TicketPriority.Medium
            : TicketPriority.FromName<TicketPriority>(request.Priority.Trim());
        if (priority is null)
            return Result<TicketExternoCreadoDto>.Failure("Prioridad no válida");

        if (!string.IsNullOrWhiteSpace(request.SolicitanteEmail) && !EsEmail(request.SolicitanteEmail.Trim()))
            return Result<TicketExternoCreadoDto>.Failure("El email del solicitante no es válido");

        var creado = Ticket.CrearDesdeFuera(clave, request.Title ?? string.Empty, request.Description.Trim(),
            priority, Recortar(request.SolicitanteNombre, 200), Recortar(request.SolicitanteEmail, 320));
        if (creado.IsFailure)
            return Result<TicketExternoCreadoDto>.Failure(creado.Error!);

        clave.Usada(DateTime.UtcNow);
        await tickets.AddAsync(creado.Value!, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var ticket = creado.Value!;
        return Result<TicketExternoCreadoDto>.Success(new(ticket.Id, ticket.Status.Name, ticket.CreatedAt));
    }

    private static string? Recortar(string? texto, int maximo)
        => texto is null ? null : texto.Trim() is var t && t.Length > maximo ? t[..maximo] : texto.Trim();

    private static bool EsEmail(string email)
        => MailAddress.TryCreate(email, out var direccion) && direccion.Address == email;
}
