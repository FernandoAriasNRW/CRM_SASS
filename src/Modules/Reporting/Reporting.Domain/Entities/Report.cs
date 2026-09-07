using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Reporting.Domain.Events;
using Reporting.Domain.ValueObjects;

namespace Reporting.Domain.Entities;

public sealed class Report : AggregateRoot, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public Guid CreatedById { get; private set; }
    public List<Guid> TagIds { get; private set; } = new();
    public string Name { get; private set; } = string.Empty;
    public int TypeValue { get; private set; }
    public int FormatValue { get; private set; }
    public string? Parameters { get; private set; }

    /// <summary>
    /// La definición del informe a medida, serializada, o <c>null</c> si es de los de serie.
    ///
    /// Va en su propia columna y no dentro de <see cref="Parameters"/>, que es un campo de texto
    /// libre heredado sin forma conocida: mezclar una estructura que se valida con otra que no,
    /// en la misma columna, obliga a adivinar cuál es cuál al leerla.
    ///
    /// Se guarda como JSON y no en columnas porque su forma es un árbol —filtros con campo,
    /// operador y valor— y normalizarlo serían tres tablas para algo que siempre se lee entero y
    /// nunca se consulta por partes.
    /// </summary>
    /// <remarks>
    /// Se llama <c>DefinicionJson</c> y no <c>Definicion</c> porque lo segundo tapaba al espacio
    /// de nombres <c>Reporting.Domain.Definicion</c> dentro de esta clase. El nombre además dice
    /// la verdad: lo que hay en la columna es el JSON, no el objeto.
    /// </remarks>
    public string? DefinicionJson { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    // Aquí vivían GeneratedFileUrl, GeneratedAt, IsGenerated y ErrorMessage, con MarkAsGenerated
    // y MarkAsFailed para moverlos. Se han quitado, y no por limpieza:
    //
    // 1. **No decían la verdad.** `MarkAsGenerated` guardaba una URL construida a mano
    //    —`/reports/{id}/{nombre}.pdf`— que no apuntaba a ningún fichero y que ningún endpoint
    //    servía. El informe constaba como generado y no había nada que descargar.
    //
    // 2. **Eran un solo juego de campos para muchas exportaciones.** El mismo informe se exporta
    //    en PDF hoy y en Excel mañana, y por dos personas a la vez: la segunda pisaba a la
    //    primera.
    //
    // El estado de una exportación vive ahora en `Exportacion`, una por petición, con su formato,
    // su fichero y su motivo de fallo. Un informe es la definición de qué se quiere ver; una
    // exportación es una copia concreta de eso en un momento concreto.

    public ReportType Type => ReportType.FromValue<ReportType>(TypeValue);
    public ReportFormat Format => ReportFormat.FromValue<ReportFormat>(FormatValue);

    private Report() { }

    public static Result<Report> Create(
        Guid tenantId,
        Guid createdById,
        string name,
        ReportType type,
        ReportFormat format,
        string? parameters = null)
    {
        var nameResult = ReportName.Create(name);
        if (nameResult.IsFailure)
            return Result<Report>.Failure(nameResult.Error!);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedById = createdById,
            Name = name,
            TypeValue = type.Value,
            FormatValue = format.Value,
            Parameters = parameters,
            CreatedAt = DateTime.UtcNow
        };

        report.RaiseDomainEvent(new ReportCreatedEvent(report.Id, tenantId, createdById));
        return Result<Report>.Success(report);
    }

    /// <summary>
    /// Convierte el informe en uno a medida, o le cambia la definición.
    ///
    /// La definición se valida aquí, en el dominio, y no sólo en el borde: un informe con una
    /// definición que el motor no sabe traducir se guarda bien y **falla al exportarlo**, cuando
    /// quien lo construyó ya no está mirando.
    /// </summary>
    public Result DefinirAMedida(Definicion.DefinicionDeInforme definicion)
    {
        var validacion = definicion.Validar();
        if (validacion.IsFailure) return validacion;

        DefinicionJson = definicion.ASerializar();

        // El tipo pasa a «Custom» por coherencia: un informe con definición es a medida, y dejarlo
        // como «TaskSummary» haría que el motor eligiera el camino de los de serie e ignorara
        // silenciosamente todo lo que la persona configuró.
        TypeValue = ValueObjects.ReportType.Custom.Value;

        return Result.Success();
    }

    /// <summary>La definición ya leída, o <c>null</c> si no es a medida.</summary>
    public Definicion.DefinicionDeInforme? LeerDefinicion() => Definicion.DefinicionDeInforme.Leer(DefinicionJson);

    public void AddTag(Guid tagId)
    {
        if (!TagIds.Contains(tagId))
        {
            TagIds.Add(tagId);
        }
    }

    public void RemoveTag(Guid tagId)
    {
        if (TagIds.Contains(tagId))
        {
            TagIds.Remove(tagId);
        }
    }
}
