using BuildingBlocks.Domain;
using Identity.Application.DTOs;

namespace Identity.Application.Abstractions.Queries;

public interface IUserQueries
{
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<PagedResult<UserDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    /// <param name="search">
    /// Texto a buscar en el nombre y el correo, o nulo para traerlos todos.
    ///
    /// La búsqueda es del servidor y recorre **todo** el inquilino: filtrar en el cliente lo que
    /// quepa en una página funciona con veinte personas y falla en silencio con quinientas.
    /// </param>
    Task<System.Collections.Generic.IReadOnlyList<UserDto>> GetByTenantIdAsync(
        Guid tenantId, string? search = null, int? limit = null, CancellationToken ct = default);
}
