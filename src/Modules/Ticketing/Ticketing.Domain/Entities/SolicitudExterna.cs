using Ticketing.Domain.ValueObjects;

namespace Ticketing.Domain.Entities;

/// <summary>Lo que manda un cliente de la organización al abrir un ticket desde fuera.</summary>
public sealed record SolicitudExterna(
    string Titulo,
    string Descripcion,
    TicketPriority Prioridad,
    TicketStatus? Estado,
    string? Nombre,
    string? Email,
    string? Telefono,
    string? Empresa,
    string? Clasificacion,
    Guid? TeamId,
    IReadOnlyList<string> Etiquetas);
