using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

/// <summary>
/// Acceso a las exportaciones y a sus ficheros.
///
/// El trabajador de segundo plano usa <see cref="PendingAsync"/>; todo lo demás es para las
/// pantallas.
/// </summary>
public interface IExportRepository
{
    Task<Export?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Export>> ForReportAsync(Guid tenantId, Guid reportId, CancellationToken ct = default);

    /// <summary>
    /// La misma exportación ya pedida y sin terminar, si la hay. Evita duplicar el trabajo cuando
    /// alguien pulsa dos veces.
    /// </summary>
    Task<Export?> InProgressAsync(Guid tenantId, Guid reportId, int formatValue, Guid requestedById, CancellationToken ct = default);

    /// <summary>
    /// Lo que hay por generar, **de todos los inquilinos**.
    ///
    /// El trabajador corre sin petición y por tanto sin inquilino, así que esta consulta cruza a
    /// propósito el filtro global. Es la excepción que el propio <c>TenantDbContext</c> documenta
    /// —«un proceso que legítimamente deba cruzar tenants ha de declararlo explícitamente»— y por
    /// eso está aquí, en un método con nombre, y no repartida por el código del trabajador.
    /// </summary>
    Task<IReadOnlyList<Export>> PendingAsync(int count, CancellationToken ct = default);

    Task AddAsync(Export export, CancellationToken ct = default);

    Task SaveContentAsync(ExportContent content, CancellationToken ct = default);

    Task<ExportContent?> ContentAsync(Guid tenantId, Guid exportId, CancellationToken ct = default);

    /// <summary>Guarda los cambios del trabajador, que no tiene unidad de trabajo de petición.</summary>
    Task SaveAsync(CancellationToken ct = default);
}
