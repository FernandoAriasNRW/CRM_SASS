using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Reporting.Domain.Events;
using Reporting.Domain.ValueObjects;

namespace Reporting.Domain.Entities;

/// <summary>
/// En qué punto está una exportación.
///
/// Los cuatro estados del plan, y sólo cuatro. La tentación es añadir «encolada» y «subiendo»;
/// no aportan nada a quien mira la pantalla y multiplican los sitios donde algo se puede quedar
/// atascado.
/// </summary>
public sealed class ExportStatus : Enumeration
{
    /// <summary>Pedida, esperando a que un trabajador la coja.</summary>
    public static readonly ExportStatus Pending = new(1, "Pending");

    /// <summary>Un trabajador la está generando ahora mismo.</summary>
    public static readonly ExportStatus Generating = new(2, "Generating");

    /// <summary>Hay fichero y se puede descargar.</summary>
    public static readonly ExportStatus Ready = new(3, "Ready");

    /// <summary>No salió, y <c>Error</c> dice por qué.</summary>
    public static readonly ExportStatus Failed = new(4, "Failed");

    private ExportStatus() : base(0, string.Empty) { }
    private ExportStatus(int value, string name) : base(value, name) { }

    public static IReadOnlyList<ExportStatus> All() => GetAll<ExportStatus>();
}
