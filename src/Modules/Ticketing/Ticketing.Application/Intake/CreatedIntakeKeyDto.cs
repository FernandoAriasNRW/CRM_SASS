using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

/// <summary>Lo que se devuelve al crear una clave: la única vez que se ve entera.</summary>
public sealed record CreatedIntakeKeyDto(Guid Id, string Name, string Prefix, string Key);
