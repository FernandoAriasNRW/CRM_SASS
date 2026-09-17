using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

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
public sealed record CreateExternalTicketCommand(
    string Key,
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
    IReadOnlyList<IncomingFile> Attachments) : ICommand<ExternalTicketCreatedDto>;
