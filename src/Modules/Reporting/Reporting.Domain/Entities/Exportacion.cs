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
public sealed class Exportacion : AggregateRoot, ITenantEntity
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
    public static readonly TimeSpan SeDaPorPerdidaTras = TimeSpan.FromMinutes(10);

    /// <summary>Cuántas veces se reintenta antes de rendirse y decirlo.</summary>
    public const int IntentosMaximos = 3;

    public Guid TenantId { get; private set; }
    public Guid ReportId { get; private set; }

    /// <summary>A quién hay que avisar cuando esté lista. No es quien creó el informe.</summary>
    public Guid SolicitadaPorId { get; private set; }

    public int FormatoValue { get; private set; }
    public int EstadoValue { get; private set; }

    public DateTime SolicitadaUtc { get; private set; }
    public DateTime? ComenzadaUtc { get; private set; }
    public DateTime? TerminadaUtc { get; private set; }

    /// <summary>Por qué falló, en un idioma que se pueda enseñar. Nulo si no ha fallado.</summary>
    public string? Error { get; private set; }

    public string? NombreDeFichero { get; private set; }
    public long TamanoBytes { get; private set; }

    /// <summary>Cuántas veces se ha empezado a generar, reintentos incluidos.</summary>
    public int Intentos { get; private set; }

    public ReportFormat Formato => ReportFormat.FromValue<ReportFormat>(FormatoValue);
    public EstadoDeExportacion Estado => EstadoDeExportacion.FromValue<EstadoDeExportacion>(EstadoValue);

    private Exportacion() { }

    public static Result<Exportacion> Solicitar(
        Guid tenantId, Guid reportId, Guid solicitadaPorId, ReportFormat formato)
    {
        if (reportId == Guid.Empty)
            return Result<Exportacion>.Failure(Reglas.SinInforme);

        if (solicitadaPorId == Guid.Empty)
            return Result<Exportacion>.Failure(Reglas.SinSolicitante);

        var exportacion = new Exportacion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReportId = reportId,
            SolicitadaPorId = solicitadaPorId,
            FormatoValue = formato.Value,
            EstadoValue = EstadoDeExportacion.Pendiente.Value,
            SolicitadaUtc = DateTime.UtcNow
        };

        return Result<Exportacion>.Success(exportacion);
    }

    /// <summary>
    /// La toma un trabajador y empieza a generarla.
    ///
    /// Devuelve <c>false</c> si otro se la llevó primero. No lanza: que dos trabajadores compitan
    /// por la misma fila es normal, no un error, y el que pierde simplemente coge la siguiente.
    /// </summary>
    public bool Comenzar()
    {
        if (!PuedeComenzar()) return false;

        EstadoValue = EstadoDeExportacion.Generando.Value;
        ComenzadaUtc = DateTime.UtcNow;
        Intentos++;
        return true;
    }

    /// <summary>
    /// Si esta exportación está esperando a que alguien la genere.
    ///
    /// Incluye las que se quedaron colgadas: una que lleve en «generando» más de
    /// <see cref="SeDaPorPerdidaTras"/> se vuelve a coger. Es el caso del trabajador que se cayó
    /// a mitad —el contenedor se reinicia y la fila se queda como estaba—, y sin esto nadie la
    /// tocaría nunca más.
    /// </summary>
    public bool PuedeComenzar()
    {
        if (EstadoValue == EstadoDeExportacion.Pendiente.Value)
            return true;

        if (EstadoValue == EstadoDeExportacion.Generando.Value
            && Intentos < IntentosMaximos
            && ComenzadaUtc is not null
            && DateTime.UtcNow - ComenzadaUtc.Value > SeDaPorPerdidaTras)
        {
            return true;
        }

        return false;
    }

    public void Terminar(string nombreDeFichero, long tamanoBytes)
    {
        EstadoValue = EstadoDeExportacion.Lista.Value;
        TerminadaUtc = DateTime.UtcNow;
        NombreDeFichero = nombreDeFichero;
        TamanoBytes = tamanoBytes;
        Error = null;

        RaiseDomainEvent(new ExportacionListaEvent(Id, TenantId, ReportId, SolicitadaPorId, nombreDeFichero));
    }

    /// <summary>
    /// Falla con un motivo.
    ///
    /// Nunca se guarda sin motivo: un fallo mudo obliga a mirar los registros del servidor para
    /// contestar «¿por qué no salió mi informe?», y esa pregunta la hace quien no tiene acceso a
    /// los registros.
    /// </summary>
    public void Fallar(string motivo)
    {
        EstadoValue = EstadoDeExportacion.Fallida.Value;
        TerminadaUtc = DateTime.UtcNow;
        Error = string.IsNullOrWhiteSpace(motivo) ? Reglas.FalloSinExplicacion : motivo;

        RaiseDomainEvent(new ExportacionFallidaEvent(Id, TenantId, ReportId, SolicitadaPorId, Error));
    }

    /// <summary>Si se puede reintentar tras un fallo, o ya se agotaron los intentos.</summary>
    public bool QuedanIntentos() => Intentos < IntentosMaximos;

    /// <summary>Devuelve una fallida a la cola, para volver a intentarlo.</summary>
    public void Reencolar()
    {
        EstadoValue = EstadoDeExportacion.Pendiente.Value;
        ComenzadaUtc = null;
        TerminadaUtc = null;
    }

    public static class Reglas
    {
        public const string SinInforme = "Falta el informe que se quiere exportar";
        public const string SinSolicitante = "Falta saber a quién avisar cuando esté lista";
        public const string FalloSinExplicacion = "Falló sin dejar explicación";
    }
}

/// <summary>
/// En qué punto está una exportación.
///
/// Los cuatro estados del plan, y sólo cuatro. La tentación es añadir «encolada» y «subiendo»;
/// no aportan nada a quien mira la pantalla y multiplican los sitios donde algo se puede quedar
/// atascado.
/// </summary>
public sealed class EstadoDeExportacion : Enumeration
{
    /// <summary>Pedida, esperando a que un trabajador la coja.</summary>
    public static readonly EstadoDeExportacion Pendiente = new(1, "Pendiente");

    /// <summary>Un trabajador la está generando ahora mismo.</summary>
    public static readonly EstadoDeExportacion Generando = new(2, "Generando");

    /// <summary>Hay fichero y se puede descargar.</summary>
    public static readonly EstadoDeExportacion Lista = new(3, "Lista");

    /// <summary>No salió, y <c>Error</c> dice por qué.</summary>
    public static readonly EstadoDeExportacion Fallida = new(4, "Fallida");

    private EstadoDeExportacion() : base(0, string.Empty) { }
    private EstadoDeExportacion(int value, string name) : base(value, name) { }

    public static IReadOnlyList<EstadoDeExportacion> All() => GetAll<EstadoDeExportacion>();
}

/// <summary>
/// El fichero generado, en su propia tabla.
///
/// <b>Separado de <see cref="Exportacion"/> porque son bytes.</b> Listar las exportaciones de un
/// informe es una consulta que se hace cada pocos segundos mientras alguien espera; si los bytes
/// estuvieran en la misma fila, cada sondeo arrastraría los ficheros enteros aunque la pantalla
/// sólo pinte un estado.
///
/// <b>Y están en la base de datos, no en disco ni en Cloudinary.</b> Las tres opciones se
/// miraron:
///
/// - <i>Disco del contenedor</i>: se pierde al reiniciar, y con dos réplicas el fichero está en
///   una y la descarga llega a la otra.
/// - <i>Cloudinary</i>, que es el almacenamiento que ya hay configurado: sube por la ruta de
///   imágenes (<c>ImageUploadParams</c>), que no es donde va un <c>.xlsx</c>, y además exige
///   credenciales que en desarrollo no están puestas.
/// - <i>La base</i>: el fichero hereda el aislamiento por inquilino sin escribir una línea, se
///   borra con su exportación y no añade infraestructura nueva.
///
/// El límite de esta decisión, para saber cuándo cambiarla: son informes de esta aplicación, de
/// miles de filas, no de millones. Cuando un fichero pase de unas decenas de megas, esto debe
/// mudarse a almacenamiento de objetos —y entonces sólo cambia esta clase, porque nadie más toca
/// los bytes—.
/// </summary>
public sealed class ContenidoDeExportacion : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid ExportacionId { get; private set; }
    public byte[] Bytes { get; private set; } = [];
    public string TipoDeContenido { get; private set; } = string.Empty;

    private ContenidoDeExportacion() { }

    public static ContenidoDeExportacion Crear(Guid tenantId, Guid exportacionId, byte[] bytes, string tipoDeContenido)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExportacionId = exportacionId,
            Bytes = bytes,
            TipoDeContenido = tipoDeContenido
        };
}
