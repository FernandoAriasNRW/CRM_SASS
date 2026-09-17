using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

/// <summary>Dónde viven las claves de entrada. Ver <see cref="IntakeKey"/>.</summary>
public interface IIntakeKeyRepository
{
    /// <summary>
    /// Busca una clave activa por su hash, <b>en cualquier organización</b>.
    ///
    /// Es la única consulta del módulo que cruza inquilinos, y tiene que hacerlo: quien envía un
    /// ticket desde fuera no tiene sesión, y la organización es justo lo que la clave dice.
    /// </summary>
    Task<IntakeKey?> FindActiveByHashAsync(string hash, CancellationToken ct);

    Task<List<IntakeKey>> GetByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<IntakeKey?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task AddAsync(IntakeKey key, CancellationToken ct);
}
