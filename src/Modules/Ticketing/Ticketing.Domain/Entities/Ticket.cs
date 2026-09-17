using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Ticketing.Domain.ValueObjects;
using Ticketing.Domain.Events;

namespace Ticketing.Domain.Entities;

public sealed class Ticket : AggregateRoot, ITenantEntity, ISoftDeletable, IArchivable
{
    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int PriorityValue { get; private set; }
    public int StatusValue { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public List<Guid> TagIds { get; private set; } = new();

    /// <summary>
    /// Por dónde entró: <see cref="SourceApp"/> si lo abrió alguien con sesión, o
    /// <see cref="SourceExternal"/> si llegó con una clave de entrada desde fuera.
    /// </summary>
    public string Source { get; private set; } = SourceApp;

    public const string SourceApp = "App";
    public const string SourceExternal = "External";

    /// <summary>
    /// Quién lo pidió, cuando viene de fuera. Un cliente de la organización no es un usuario de la
    /// aplicación, así que no tiene <see cref="CustomerId"/>: sin esto no habría a quién contestar.
    /// </summary>
    public string? RequesterName { get; private set; }
    public string? RequesterEmail { get; private set; }
    public string? RequesterPhone { get; private set; }
    public string? RequesterCompany { get; private set; }

    /// <summary>De qué va: «Facturación», «Acceso», lo que use la organización. Texto libre y opcional.</summary>
    public string? Classification { get; private set; }

    /// <summary>
    /// El equipo que lo atiende. Sólo el identificador: los equipos viven en otro módulo, y este no
    /// puede consultarlos para validarlo.
    /// </summary>
    public Guid? TeamId { get; private set; }

    /// <summary>
    /// Las etiquetas por su clave («billing», «bug»), separadas por comas. Son las del vocabulario
    /// de la pantalla, que no son las entidades del módulo de etiquetas de <see cref="TagIds"/>.
    /// </summary>
    public string Tags { get; private set; } = string.Empty;

    public IReadOnlyList<string> TagList =>
        Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Con qué clave entró, para saber qué integración lo envió y revocarla si abusa.</summary>
    public Guid? IntakeKeyId { get; private set; }

    public TicketPriority Priority => TicketPriority.FromValue<TicketPriority>(PriorityValue);
    public TicketStatus Status => TicketStatus.FromValue<TicketStatus>(StatusValue);

    private Ticket() { }

    public static Result<Ticket> Create(
        Guid tenantId,
        Guid customerId,
        string title,
        string description,
        TicketPriority priority)
    {
        var titleResult = TicketTitle.Create(title);
        if (titleResult.IsFailure)
            return Result<Ticket>.Failure(titleResult.Error!);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            Title = title,
            Description = description,
            PriorityValue = priority.Value,
            StatusValue = TicketStatus.Open.Value,
            CreatedAt = DateTime.UtcNow
        };

        ticket.RaiseDomainEvent(new TicketCreatedEvent(ticket.Id, tenantId));
        return Result<Ticket>.Success(ticket);
    }

    /// <summary>
    /// Un ticket enviado desde fuera de la aplicación con una clave de entrada.
    ///
    /// La organización es la de la clave, nunca un dato de la petición. No hay usuario detrás,
    /// así que <see cref="CustomerId"/> queda vacío y el contacto va en los campos del solicitante.
    /// Qué es obligatorio lo decide quien recibe la petición; aquí sólo se guarda.
    /// </summary>
    public static Result<Ticket> CreateFromExternal(IntakeKey key, ExternalTicketRequest request)
    {
        var created = Create(key.TenantId, Guid.Empty, request.Title, request.Description, request.Priority);
        if (created.IsFailure)
            return created;

        var ticket = created.Value!;
        ticket.Source = SourceExternal;
        ticket.IntakeKeyId = key.Id;
        ticket.RequesterName = Trimmed(request.RequesterName);
        ticket.RequesterEmail = Trimmed(request.RequesterEmail);
        ticket.RequesterPhone = Trimmed(request.RequesterPhone);
        ticket.RequesterCompany = Trimmed(request.RequesterCompany);
        ticket.Classify(request.Classification);
        ticket.AssignTeam(request.TeamId);
        ticket.ChangeTags(request.Tags);

        // Es el alta: no hay estado anterior del que venir, así que no pasa por las transiciones.
        if (request.Status is not null)
            ticket.StatusValue = request.Status.Value;

        return Result<Ticket>.Success(ticket);
    }

    public void Classify(string? classification) => Classification = Trimmed(classification);

    public void AssignTeam(Guid? teamId) => TeamId = teamId == Guid.Empty ? null : teamId;

    /// <summary>Sustituye las etiquetas. Sin repetidas, en minúsculas y en el orden en que llegan.</summary>
    public void ChangeTags(IEnumerable<string>? tags)
        => Tags = string.Join(',', (tags ?? [])
            .Select(e => e.Trim().ToLowerInvariant())
            .Where(e => e.Length > 0 && !e.Contains(','))
            .Distinct());

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// Cambia lo que se edita desde la ficha: título, descripción y prioridad.
    ///
    /// Es una actualización parcial: lo que llega como <c>null</c> se queda como estaba. La
    /// pantalla manda a veces sólo un campo —al arrastrar una tarjeta va sólo el estado— y con
    /// una actualización total eso borraría la descripción del ticket sin que nadie lo pidiera.
    ///
    /// El título pasa por <see cref="TicketTitle"/> en vez de asignarse a pelo, que es lo que ya
    /// hace la creación: si no, un título de dos letras entraría por la edición y no por el alta.
    /// </summary>
    public Result<bool> Update(string? title, string? description, TicketPriority? priority)
    {
        if (title is not null)
        {
            var titleResult = TicketTitle.Create(title);
            if (titleResult.IsFailure)
                return Result<bool>.Failure(titleResult.Error!);

            Title = title;
        }

        if (description is not null)
            Description = description;

        if (priority is not null)
            PriorityValue = priority.Value;

        return Result<bool>.Success(true);
    }

    public bool ChangeStatus(TicketStatus newStatus)
    {
        if (!Status.CanTransitionTo(newStatus))
            return false;

        var previousStatus = StatusValue;
        StatusValue = newStatus.Value;

        if (newStatus == TicketStatus.Resolved)
            ResolvedAt = DateTime.UtcNow;

        RaiseDomainEvent(new TicketStatusChangedEvent(Id, TenantId, previousStatus, newStatus.Value));
        return true;
    }

    public void AssignTo(Guid agentId)
    {
        AssignedAgentId = agentId;
        RaiseDomainEvent(new TicketAssignedEvent(Id, TenantId, agentId));
    }

    public void Unassign()
    {
        AssignedAgentId = null;
        RaiseDomainEvent(new TicketUnassignedEvent(Id, TenantId));
    }

    public void AddTag(Guid tagId)
    {
        if (!TagIds.Contains(tagId))
        {
            TagIds.Add(tagId);
        }
    }

    public void RemoveTag(Guid tagId)
    {
        if (TagIds.Contains(tagId))
        {
            TagIds.Remove(tagId);
        }
    }

    #region Archivo y papelera

    /// <summary>Cuándo se archivó, o <c>null</c> si está a la vista. Ver <see cref="IArchivable"/>.</summary>
    public DateTime? ArchivedAtUtc { get; private set; }

    /// <summary>Si está en la papelera. El filtro global lo esconde salvo que se pida verlo.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Cuándo se envió a la papelera, para poder vaciarla por antigüedad algún día.</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>
    /// Aparta el ticket de las listas sin borrarlo.
    ///
    /// Es idempotente: archivar dos veces no cambia la fecha original. Importa porque dos
    /// pestañas abiertas pueden mandar la misma orden, y reescribir la fecha haría parecer
    /// reciente algo archivado hace meses.
    /// </summary>
    public void Archive()
    {
        if (ArchivedAtUtc is not null) return;
        ArchivedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Devuelve el ticket a las listas.</summary>
    public void Unarchive() => ArchivedAtUtc = null;

    /// <summary>
    /// Manda el ticket a la papelera: deja de verse pero se puede recuperar.
    ///
    /// Archivar y borrar no se pisan. Un ticket archivado que se borra sigue archivado al
    /// restaurarlo, que es lo que espera quien lo archivó.
    /// </summary>
    public void MoveToTrash()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Saca el ticket de la papelera y lo deja como estaba.</summary>
    public void RestoreFromTrash()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
    }

    #endregion
}
