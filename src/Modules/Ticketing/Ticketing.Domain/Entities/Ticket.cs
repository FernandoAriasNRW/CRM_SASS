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
    /// Cambia lo que se edita desde la ficha: título, descripción y prioridad.
    ///
    /// Es una actualización parcial: lo que llega como <c>null</c> se queda como estaba. La
    /// pantalla manda a veces sólo un campo —al arrastrar una tarjeta va sólo el estado— y con
    /// una actualización total eso borraría la descripción del ticket sin que nadie lo pidiera.
    ///
    /// El título pasa por <see cref="TicketTitle"/> en vez de asignarse a pelo, que es lo que ya
    /// hace la creación: si no, un título de dos letras entraría por la edición y no por el alta.
    /// </summary>
    public Result<bool> Actualizar(string? title, string? description, TicketPriority? priority)
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
    public DateTime? ArchivadoEnUtc { get; private set; }

    /// <summary>Si está en la papelera. El filtro global lo esconde salvo que se pida verlo.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Cuándo se envió a la papelera, para poder vaciarla por antigüedad algún día.</summary>
    public DateTime? BorradoEnUtc { get; private set; }

    /// <summary>
    /// Aparta el ticket de las listas sin borrarlo.
    ///
    /// Es idempotente: archivar dos veces no cambia la fecha original. Importa porque dos
    /// pestañas abiertas pueden mandar la misma orden, y reescribir la fecha haría parecer
    /// reciente algo archivado hace meses.
    /// </summary>
    public void Archivar()
    {
        if (ArchivadoEnUtc is not null) return;
        ArchivadoEnUtc = DateTime.UtcNow;
    }

    /// <summary>Devuelve el ticket a las listas.</summary>
    public void Desarchivar() => ArchivadoEnUtc = null;

    /// <summary>
    /// Manda el ticket a la papelera: deja de verse pero se puede recuperar.
    ///
    /// Archivar y borrar no se pisan. Un ticket archivado que se borra sigue archivado al
    /// restaurarlo, que es lo que espera quien lo archivó.
    /// </summary>
    public void EnviarAPapelera()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        BorradoEnUtc = DateTime.UtcNow;
    }

    /// <summary>Saca el ticket de la papelera y lo deja como estaba.</summary>
    public void RestaurarDePapelera()
    {
        IsDeleted = false;
        BorradoEnUtc = null;
    }

    #endregion
}
