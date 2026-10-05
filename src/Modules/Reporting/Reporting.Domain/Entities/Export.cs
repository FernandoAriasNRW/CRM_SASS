using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Reporting.Domain.Events;
using Reporting.Domain.ValueObjects;

namespace Reporting.Domain.Entities;

/// <summary>
/// Una petición de exportar un informe a un fichero.
///
/// <b>Es un agregado aparte de <see cref="Report"/> a propósito.</b> Un informe se exporta muchas
/// veces: en PDF hoy, en Excel mañana, y por dos personas distintas a la vez. Con el estado
/// metido en el informe —que es como estaba: <c>IsGenerated</c>, <c>GeneratedAt</c>,
/// <c>GeneratedFileUrl</c>, un solo juego de campos— la segunda exportación pisa a la primera y
/// quien pidió la primera se queda mirando el fichero de otro.
///
/// <b>Y sustituye a algo que no exportaba nada.</b> El camino anterior llamaba a
/// <c>MarkAsGenerated</c> con una URL inventada —<c>/reports/{id}/{nombre}.pdf</c>— que no
/// apuntaba a ningún fichero y que ningún endpoint servía. La pantalla decía «generado» y no
/// había nada que descargar.
/// </summary>
public sealed class Export : AggregateRoot, ITenantEntity
{
    /// <summary>
    /// Cuánto puede pasar una exportación en «generando» antes de darla por perdida.
    ///
    /// Existe porque el plan lo señalaba como lo que pudre estos sistemas: «una exportación que
    /// se queda en generando para siempre es peor que un error, porque nadie sabe si esperar».
    /// Pasado este tiempo se reintenta, y si se agotan los intentos se marca fallida **con el
    /// motivo**, que es lo que alguien necesita leer.
    ///
    /// Diez minutos es holgado para un informe de esta aplicación y corto para que alguien se
    /// canse de esperar sin noticias.
    /// </summary>
    public static readonly TimeSpan GivenUpAfter = TimeSpan.FromMinutes(10);

    /// <summary>Cuántas veces se reintenta antes de rendirse y decirlo.</summary>
    public const int MaxAttempts = 3;

    public Guid TenantId { get; private set; }
    public Guid ReportId { get; private set; }

    /// <summary>A quién hay que avisar cuando esté lista. No es quien creó el informe.</summary>
    public Guid RequestedById { get; private set; }

    public int FormatValue { get; private set; }
    public int StatusValue { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }

    /// <summary>Por qué falló, en un idioma que se pueda enseñar. Nulo si no ha fallado.</summary>
    public string? Error { get; private set; }

    public string? FileName { get; private set; }
    public long SizeBytes { get; private set; }

    /// <summary>Cuántas veces se ha empezado a generar, reintentos incluidos.</summary>
    public int Attempts { get; private set; }

    public ReportFormat Format => ReportFormat.FromValue<ReportFormat>(FormatValue);
    public ExportStatus Status => ExportStatus.FromValue<ExportStatus>(StatusValue);

    private Export() { }

    public static Result<Export> Request(
        DateTime nowUtc,
        Guid tenantId, Guid reportId, Guid requestedById, ReportFormat format)
    {
        if (reportId == Guid.Empty)
            return Result<Export>.Failure(Rules.MissingReport);

        if (requestedById == Guid.Empty)
            return Result<Export>.Failure(Rules.MissingRequester);

        var export = new Export
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReportId = reportId,
            RequestedById = requestedById,
            FormatValue = format.Value,
            StatusValue = ExportStatus.Pending.Value,
            RequestedAtUtc = nowUtc
        };

        return Result<Export>.Success(export);
    }

    /// <summary>
    /// La toma un trabajador y empieza a generarla.
    ///
    /// Devuelve <c>false</c> si otro se la llevó primero. No lanza: que dos trabajadores compitan
    /// por la misma fila es normal, no un error, y el que pierde simplemente coge la siguiente.
    /// </summary>
    public bool Start(DateTime nowUtc)
    {
        if (!CanStart(nowUtc)) return false;

        StatusValue = ExportStatus.Generating.Value;
        StartedAtUtc = nowUtc;
        Attempts++;
        return true;
    }

    /// <summary>
    /// Si esta exportación está esperando a que alguien la genere.
    ///
    /// Incluye las que se quedaron colgadas: una que lleve en «generando» más de
    /// <see cref="GivenUpAfter"/> se vuelve a coger. Es el caso del trabajador que se cayó
    /// a mitad —el contenedor se reinicia y la fila se queda como estaba—, y sin esto nadie la
    /// tocaría nunca más.
    /// </summary>
    public bool CanStart(DateTime nowUtc)
    {
        if (StatusValue == ExportStatus.Pending.Value)
            return true;

        if (StatusValue == ExportStatus.Generating.Value
            && Attempts < MaxAttempts
            && StartedAtUtc is not null
            && nowUtc - StartedAtUtc.Value > GivenUpAfter)
        {
            return true;
        }

        return false;
    }

    public void Finish(DateTime nowUtc, string fileName, long tamanoBytes)
    {
        StatusValue = ExportStatus.Ready.Value;
        FinishedAtUtc = nowUtc;
        FileName = fileName;
        SizeBytes = tamanoBytes;
        Error = null;

        RaiseDomainEvent(new ExportReadyEvent(Id, TenantId, ReportId, RequestedById, fileName));
    }

    /// <summary>
    /// Falla con un motivo.
    ///
    /// Nunca se guarda sin motivo: un fallo mudo obliga a mirar los registros del servidor para
    /// contestar «¿por qué no salió mi informe?», y esa pregunta la hace quien no tiene acceso a
    /// los registros.
    /// </summary>
    public void Fail(DateTime nowUtc, string reason)
    {
        StatusValue = ExportStatus.Failed.Value;
        FinishedAtUtc = nowUtc;
        Error = string.IsNullOrWhiteSpace(reason) ? Rules.FailureWithoutReason : reason;

        RaiseDomainEvent(new ExportFailedEvent(Id, TenantId, ReportId, RequestedById, Error));
    }

    /// <summary>Si se puede reintentar tras un fallo, o ya se agotaron los intentos.</summary>
    public bool HasAttemptsLeft() => Attempts < MaxAttempts;

    /// <summary>Devuelve una fallida a la cola, para volver a intentarlo.</summary>
    public void Requeue()
    {
        StatusValue = ExportStatus.Pending.Value;
        StartedAtUtc = null;
        FinishedAtUtc = null;
    }

    public static class Rules
    {
        public const string MissingReport = "Falta el informe que se quiere exportar";
        public const string MissingRequester = "Falta saber a quién avisar cuando esté lista";
        public const string FailureWithoutReason = "Falló sin dejar explicación";
    }
}
