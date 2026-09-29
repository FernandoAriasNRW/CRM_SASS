namespace Ticketing.Domain.Entities;

/// <summary>
/// Lo que manda un cliente de la organización al abrir un ticket desde fuera: quién es y qué le
/// pasa. Prioridad, estado, clasificación, equipo y etiquetas los decide quien lo atiende, dentro
/// de la aplicación, a mano, con automatizaciones o con IA.
/// </summary>
public sealed record ExternalTicketRequest(
    string Title,
    string Description,
    string? RequesterName,
    string? RequesterEmail,
    string? RequesterPhone,
    string? RequesterCompany);
