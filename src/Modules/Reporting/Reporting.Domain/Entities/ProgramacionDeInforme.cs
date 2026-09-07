using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Reporting.Domain.ValueObjects;

namespace Reporting.Domain.Entities;

/// <summary>
/// «Genera este informe solo, cada tanto, y mándamelo».
///
/// <b>Qué guarda y qué no.</b> Guarda cada cuánto y a qué hora, no una expresión de cron. Cron es
/// más potente y aquí sería peor: nadie configura un informe semanal escribiendo <c>0 8 * * 1</c>,
/// y admitir la expresión entera obliga a soportar combinaciones que nadie va a usar —«cada cinco
/// minutos los martes de febrero»— y a explicar en la pantalla por qué un informe se generó
/// cuarenta veces.
///
/// <b>La hora es local del inquilino, no UTC.</b> Quien pide un informe «cada lunes a las 8» lo
/// quiere a las 8 de su mañana. Guardar UTC obligaría a la pantalla a convertir, y la conversión
/// se rompe dos veces al año con el cambio de hora: el informe llegaría a las 7 o a las 9 durante
/// seis meses y nadie sabría por qué.
/// </summary>
public sealed class ProgramacionDeInforme : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid ReportId { get; private set; }

    /// <summary>A quién se le manda y de quién es la programación.</summary>
    public Guid DestinatarioId { get; private set; }

    public int FrecuenciaValue { get; private set; }
    public int FormatoValue { get; private set; }

    /// <summary>Hora local a la que toca. Ver la nota de la clase sobre por qué no es UTC.</summary>
    public TimeOnly Hora { get; private set; }

    /// <summary>
    /// Día de la semana para las semanales, o día del mes para las mensuales. Nulo en las diarias.
    ///
    /// Un solo campo para las dos cosas porque nunca se usan a la vez, y dos campos harían posible
    /// guardar «cada lunes día 15», que no significa nada.
    /// </summary>
    public int? Dia { get; private set; }

    public bool Activa { get; private set; }

    /// <summary>
    /// El último día en que se generó, para no repetir.
    ///
    /// Es una fecha y no una marca de tiempo a propósito: la pregunta que hay que contestar es
    /// «¿ya toca hoy?», y con eso es una comparación de igualdad que además entra en un índice.
    /// Es el mismo razonamiento que el registro de ejecuciones de las automatizaciones.
    /// </summary>
    public DateOnly? UltimoDiaGenerado { get; private set; }

    public DateTime CreadaUtc { get; private set; }

    public FrecuenciaDeInforme Frecuencia => FrecuenciaDeInforme.FromValue<FrecuenciaDeInforme>(FrecuenciaValue);
    public ReportFormat Formato => ReportFormat.FromValue<ReportFormat>(FormatoValue);

    private ProgramacionDeInforme() { }

    public static Result<ProgramacionDeInforme> Crear(
        Guid tenantId, Guid reportId, Guid destinatarioId,
        FrecuenciaDeInforme frecuencia, ReportFormat formato, TimeOnly hora, int? dia)
    {
        if (reportId == Guid.Empty)
            return Result<ProgramacionDeInforme>.Failure(Reglas.SinInforme);

        if (destinatarioId == Guid.Empty)
            return Result<ProgramacionDeInforme>.Failure(Reglas.SinDestinatario);

        var validacion = ValidarDia(frecuencia, dia);
        if (validacion.IsFailure)
            return Result<ProgramacionDeInforme>.Failure(validacion.Error!);

        return Result<ProgramacionDeInforme>.Success(new ProgramacionDeInforme
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReportId = reportId,
            DestinatarioId = destinatarioId,
            FrecuenciaValue = frecuencia.Value,
            FormatoValue = formato.Value,
            Hora = hora,
            Dia = frecuencia == FrecuenciaDeInforme.Diaria ? null : dia,
            Activa = true,
            CreadaUtc = DateTime.UtcNow
        });
    }

    private static Result ValidarDia(FrecuenciaDeInforme frecuencia, int? dia)
    {
        if (frecuencia == FrecuenciaDeInforme.Diaria)
            return Result.Success();

        if (dia is null)
            return Result.Failure(Reglas.FaltaElDia);

        if (frecuencia == FrecuenciaDeInforme.Semanal && dia is < 1 or > 7)
            return Result.Failure(Reglas.DiaDeSemanaFuera);

        // Hasta 28 y no hasta 31: un informe programado el 31 no se generaría en febrero, y en
        // los meses de 30 días tampoco. Quien quiera «fin de mes» necesita otra frecuencia, y es
        // mejor no ofrecerla que ofrecer una que falla cuatro meses al año en silencio.
        if (frecuencia == FrecuenciaDeInforme.Mensual && dia is < 1 or > 28)
            return Result.Failure(Reglas.DiaDeMesFuera);

        return Result.Success();
    }

    /// <summary>
    /// Si toca generarlo ahora mismo.
    ///
    /// <b>Es una función pura y por eso se puede probar sin base de datos ni esperar a un lunes.</b>
    /// La alternativa —decidirlo dentro del trabajador, mirando el reloj— convierte cada caso
    /// límite en algo que sólo se puede comprobar en producción y a la hora exacta.
    ///
    /// «Ya toca» significa tres cosas a la vez: está activa, hoy es su día, ya pasó su hora, y no
    /// se generó hoy. Lo último es lo que impide que un trabajador que corre cada minuto lo mande
    /// sesenta veces.
    /// </summary>
    public bool TocaAhora(DateTime ahoraLocal)
    {
        if (!Activa) return false;

        var hoy = DateOnly.FromDateTime(ahoraLocal);

        if (UltimoDiaGenerado == hoy) return false;

        if (TimeOnly.FromDateTime(ahoraLocal) < Hora) return false;

        return Frecuencia.Value switch
        {
            var v when v == FrecuenciaDeInforme.Diaria.Value => true,

            // El día de la semana se guarda de 1 (lunes) a 7 (domingo), como la norma ISO, y no
            // con el DayOfWeek de .NET, donde el domingo es 0: guardarlo tal cual haría que
            // «día 1» significara lunes para quien lo configura y domingo para quien lo lee.
            var v when v == FrecuenciaDeInforme.Semanal.Value =>
                Dia == (int)ahoraLocal.DayOfWeek switch { 0 => 7, var d => d },

            var v when v == FrecuenciaDeInforme.Mensual.Value => Dia == ahoraLocal.Day,

            _ => false
        };
    }

    /// <summary>Deja constancia de que hoy ya se generó.</summary>
    public void AnotarGenerada(DateOnly dia) => UltimoDiaGenerado = dia;

    public void Activar() => Activa = true;

    /// <summary>
    /// Se apaga, no se borra.
    ///
    /// Borrarla perdería el <see cref="UltimoDiaGenerado"/>, así que volver a activarla el mismo
    /// día generaría el informe por segunda vez.
    /// </summary>
    public void Desactivar() => Activa = false;

    public static class Reglas
    {
        public const string SinInforme = "Falta el informe que se quiere programar";
        public const string SinDestinatario = "Falta a quién mandárselo";
        public const string FaltaElDia = "Una programación semanal o mensual necesita decir qué día";
        public const string DiaDeSemanaFuera = "El día de la semana va de 1 (lunes) a 7 (domingo)";
        public const string DiaDeMesFuera =
            "El día del mes va del 1 al 28: más allá, el informe no se generaría en febrero";
    }
}

/// <summary>
/// Cada cuánto se genera un informe programado.
///
/// Tres y sólo tres. Añadir «cada hora» tentaría, y sería un generador de correo no deseado: un
/// informe que llega cada hora se deja de leer el segundo día y se convierte en ruido que además
/// esconde los que sí importan.
/// </summary>
public sealed class FrecuenciaDeInforme : Enumeration
{
    public static readonly FrecuenciaDeInforme Diaria = new(1, "Diaria");
    public static readonly FrecuenciaDeInforme Semanal = new(2, "Semanal");
    public static readonly FrecuenciaDeInforme Mensual = new(3, "Mensual");

    private FrecuenciaDeInforme() : base(0, string.Empty) { }
    private FrecuenciaDeInforme(int value, string name) : base(value, name) { }

    public static IReadOnlyList<FrecuenciaDeInforme> All() => GetAll<FrecuenciaDeInforme>();
}
