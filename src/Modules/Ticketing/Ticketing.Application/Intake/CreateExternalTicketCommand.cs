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
/// Obligatorio: asunto (el título del ticket), mensaje, nombre, email, teléfono y empresa.
/// Opcional: adjuntos. Nada más: prioridad, estado, clasificación, equipo y etiquetas se deciden
/// dentro, una vez creado. <c>RetiredFields</c> son los de esa lista que la petición trajo
/// igualmente, para rechazarla en vez de fingir que se aplicaron.
/// </summary>
public sealed record CreateExternalTicketCommand(
    string Key,
    string? Title,
    string? Description,
    string? RequesterName,
    string? RequesterEmail,
    string? RequesterPhone,
    string? RequesterCompany,
    IReadOnlyList<IncomingFile> Attachments,
    IReadOnlyList<string>? RetiredFields = null) : ICommand<ExternalTicketCreatedDto>;
