using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferencias;

/// <summary>
/// Las preferencias tal como viajan por la API. Los nombres son los que ya usaba la pantalla.
/// </summary>
public sealed record PreferenciasDto(
    bool EmailEnabled,
    bool PushEnabled,
    bool TaskAssigned,
    bool TaskCompleted,
    bool TaskDueSoon,
    bool TicketCreated,
    bool TicketUpdated,
    bool ProjectUpdated,
    bool MentionEnabled,
    bool ExportReady,
    bool QuietHoursEnabled,
    string QuietHoursStart,
    string QuietHoursEnd)
{
    /// <summary>«HH:mm», que es lo que produce y espera un &lt;input type="time"&gt;.</summary>
    private const string FormatoDeHora = "HH\\:mm";

    public static PreferenciasDto De(PreferenciasDeNotificacion p) => new(
        p.EmailEnabled, p.PushEnabled,
        p.TaskAssigned, p.TaskCompleted, p.TaskDueSoon,
        p.TicketCreated, p.TicketUpdated, p.ProjectUpdated,
        p.MentionEnabled, p.ExportacionLista,
        p.QuietHoursEnabled,
        p.QuietHoursStart.ToString(FormatoDeHora),
        p.QuietHoursEnd.ToString(FormatoDeHora));
}

public interface IRepositorioDePreferencias
{
    Task<PreferenciasDeNotificacion?> DeLaPersonaAsync(Guid tenantId, Guid userId, CancellationToken ct);
    Task AñadirAsync(PreferenciasDeNotificacion preferencias, CancellationToken ct);
    Task GuardarAsync(CancellationToken ct);
}

public sealed record GetPreferenciasQuery(Guid TenantId, Guid UserId) : IQuery<PreferenciasDto>;

public sealed record SetPreferenciasCommand(
    Guid TenantId,
    Guid UserId,
    bool EmailEnabled,
    bool PushEnabled,
    bool TaskAssigned,
    bool TaskCompleted,
    bool TaskDueSoon,
    bool TicketCreated,
    bool TicketUpdated,
    bool ProjectUpdated,
    bool MentionEnabled,
    bool ExportReady,
    bool QuietHoursEnabled,
    string QuietHoursStart,
    string QuietHoursEnd) : ICommand<PreferenciasDto>;

/// <summary>
/// Devuelve las preferencias de quien pregunta, creándolas por defecto si nunca las tocó.
///
/// **No se guardan al leerlas.** Consultar no es modificar, y escribir en una petición de
/// lectura convierte cada apertura de la pantalla en una escritura, con el añadido de que dos
/// pestañas abiertas a la vez pueden crear dos filas. Se materializan al guardar por primera
/// vez; hasta entonces, quien pregunta recibe las de por defecto y nadie nota la diferencia.
/// </summary>
public sealed class GetPreferenciasHandler(IRepositorioDePreferencias repositorio)
    : IQueryHandler<GetPreferenciasQuery, PreferenciasDto>
{
    public async Task<Result<PreferenciasDto>> Handle(GetPreferenciasQuery peticion, CancellationToken ct)
    {
        var guardadas = await repositorio.DeLaPersonaAsync(peticion.TenantId, peticion.UserId, ct);

        return Result<PreferenciasDto>.Success(PreferenciasDto.De(
            guardadas ?? PreferenciasDeNotificacion.PorDefecto(peticion.TenantId, peticion.UserId)));
    }
}

public sealed class SetPreferenciasHandler(IRepositorioDePreferencias repositorio)
    : ICommandHandler<SetPreferenciasCommand, PreferenciasDto>
{
    public async Task<Result<PreferenciasDto>> Handle(SetPreferenciasCommand peticion, CancellationToken ct)
    {
        // Las horas llegan como texto desde un <input type="time">. Si no se pueden leer se
        // rechaza la petición entera en vez de sustituirlas por un valor razonable: guardar en
        // silencio unas horas de silencio distintas de las que la persona escribió es la clase
        // de fallo que se descubre semanas después, al no recibir un aviso.
        if (!TimeOnly.TryParse(peticion.QuietHoursStart, out var inicio))
            return Result<PreferenciasDto>.Failure($"La hora de inicio del silencio no es válida: '{peticion.QuietHoursStart}'");

        if (!TimeOnly.TryParse(peticion.QuietHoursEnd, out var fin))
            return Result<PreferenciasDto>.Failure($"La hora de fin del silencio no es válida: '{peticion.QuietHoursEnd}'");

        // Un tramo de silencio de longitud cero no silencia nada, así que activarlo con las dos
        // horas iguales sólo puede ser un error de quien lo rellenó.
        if (peticion.QuietHoursEnabled && inicio == fin)
            return Result<PreferenciasDto>.Failure("Las horas de silencio no pueden empezar y terminar a la misma hora");

        var preferencias = await repositorio.DeLaPersonaAsync(peticion.TenantId, peticion.UserId, ct);

        if (preferencias is null)
        {
            preferencias = PreferenciasDeNotificacion.PorDefecto(peticion.TenantId, peticion.UserId);
            await repositorio.AñadirAsync(preferencias, ct);
        }

        preferencias.Actualizar(
            peticion.EmailEnabled, peticion.PushEnabled,
            peticion.TaskAssigned, peticion.TaskDueSoon, peticion.MentionEnabled, peticion.ExportReady,
            peticion.TaskCompleted, peticion.TicketCreated, peticion.TicketUpdated, peticion.ProjectUpdated,
            peticion.QuietHoursEnabled, inicio, fin);

        await repositorio.GuardarAsync(ct);

        // Se devuelve lo guardado, no lo recibido. El `PUT` anterior devolvía el cuerpo de la
        // petición tal cual, así que la pantalla confirmaba cambios que nunca llegaron a la
        // base: parecía funcionar hasta que alguien recargaba.
        return Result<PreferenciasDto>.Success(PreferenciasDto.De(preferencias));
    }
}
