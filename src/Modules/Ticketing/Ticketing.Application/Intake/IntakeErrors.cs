using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public static class IntakeErrors
{
    /// <summary>
    /// El mismo mensaje para una clave que no existe, que está revocada o que no se mandó. Decir
    /// cuál de las tres es ayudaría a quien prueba claves a ciegas, no a quien integra.
    /// </summary>
    public const string InvalidKey = "Clave de entrada no válida";

    public const string TicketNotFound = "Ticket no encontrado";
}
