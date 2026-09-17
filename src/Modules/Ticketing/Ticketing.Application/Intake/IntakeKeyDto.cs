using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

/// <summary>Una clave tal como se enseña en la lista: sin la clave.</summary>
public sealed record IntakeKeyDto(
    Guid Id, string Name, string Prefix, DateTime CreatedAtUtc, DateTime? LastUsedAtUtc, DateTime? RevokedAtUtc);
