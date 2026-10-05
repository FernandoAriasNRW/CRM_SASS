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
public sealed class ReportSchedule : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid ReportId { get; private set; }

    /// <summary>A quién se le manda y de quién es la programación.</summary>
    public Guid RecipientId { get; private set; }

    public int FrequencyValue { get; private set; }
    public int FormatValue { get; private set; }

    /// <summary>Hora local a la que toca. Ver la nota de la clase sobre por qué no es UTC.</summary>
    public TimeOnly Time { get; private set; }

    /// <summary>
    /// Día de la semana para las semanales, o día del mes para las mensuales. Nulo en las diarias.
    ///
    /// Un solo campo para las dos cosas porque nunca se usan a la vez, y dos campos harían posible
    /// guardar «cada lunes día 15», que no significa nada.
    /// </summary>
    public int? Day { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// El último día en que se generó, para no repetir.
    ///
    /// Es una fecha y no una marca de tiempo a propósito: la pregunta que hay que contestar es
    /// «¿ya toca hoy?», y con eso es una comparación de igualdad que además entra en un índice.
    /// Es el mismo razonamiento que el registro de ejecuciones de las automatizaciones.
    /// </summary>
    public DateOnly? LastGeneratedDay { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public ScheduleFrequency Frequency => ScheduleFrequency.FromValue<ScheduleFrequency>(FrequencyValue);
    public ReportFormat Format => ReportFormat.FromValue<ReportFormat>(FormatValue);

    private ReportSchedule() { }

    public static Result<ReportSchedule> Create(
        DateTime nowUtc,
        Guid tenantId, Guid reportId, Guid recipientId,
        ScheduleFrequency frequency, ReportFormat format, TimeOnly time, int? day)
    {
        if (reportId == Guid.Empty)
            return Result<ReportSchedule>.Failure(Rules.MissingReport);

        if (recipientId == Guid.Empty)
            return Result<ReportSchedule>.Failure(Rules.MissingRecipient);

        var validacion = ValidateDay(frequency, day);
        if (validacion.IsFailure)
            return Result<ReportSchedule>.Failure(validacion.Error!);

        return Result<ReportSchedule>.Success(new ReportSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReportId = reportId,
            RecipientId = recipientId,
            FrequencyValue = frequency.Value,
            FormatValue = format.Value,
            Time = time,
            Day = frequency == ScheduleFrequency.Daily ? null : day,
            IsActive = true,
            CreatedAtUtc = nowUtc
        });
    }

    private static Result ValidateDay(ScheduleFrequency frequency, int? day)
    {
        if (frequency == ScheduleFrequency.Daily)
            return Result.Success();

        if (day is null)
            return Result.Failure(Rules.MissingDay);

        if (frequency == ScheduleFrequency.Weekly && day is < 1 or > 7)
            return Result.Failure(Rules.DayOfWeekOutOfRange);

        // Hasta 28 y no hasta 31: un informe programado el 31 no se generaría en febrero, y en
        // los meses de 30 días tampoco. Quien quiera «fin de mes» necesita otra frecuencia, y es
        // mejor no ofrecerla que ofrecer una que falla cuatro meses al año en silencio.
        if (frequency == ScheduleFrequency.Monthly && day is < 1 or > 28)
            return Result.Failure(Rules.DayOfMonthOutOfRange);

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
    public bool IsDue(DateTime localNow)
    {
        if (!IsActive) return false;

        var today = DateOnly.FromDateTime(localNow);

        if (LastGeneratedDay == today) return false;

        if (TimeOnly.FromDateTime(localNow) < Time) return false;

        return Frequency.Value switch
        {
            var v when v == ScheduleFrequency.Daily.Value => true,

            // El día de la semana se guarda de 1 (lunes) a 7 (domingo), como la norma ISO, y no
            // con el DayOfWeek de .NET, donde el domingo es 0: guardarlo tal cual haría que
            // «día 1» significara lunes para quien lo configura y domingo para quien lo lee.
            var v when v == ScheduleFrequency.Weekly.Value =>
                Day == (int)localNow.DayOfWeek switch { 0 => 7, var d => d },

            var v when v == ScheduleFrequency.Monthly.Value => Day == localNow.Day,

            _ => false
        };
    }

    /// <summary>Deja constancia de que hoy ya se generó.</summary>
    public void MarkGenerated(DateOnly day) => LastGeneratedDay = day;

    public void Activate() => IsActive = true;

    /// <summary>
    /// Se apaga, no se borra.
    ///
    /// Borrarla perdería el <see cref="LastGeneratedDay"/>, así que volver a activarla el mismo
    /// día generaría el informe por segunda vez.
    /// </summary>
    public void Deactivate() => IsActive = false;

    public static class Rules
    {
        public const string MissingReport = "Falta el informe que se quiere programar";
        public const string MissingRecipient = "Falta a quién mandárselo";
        public const string MissingDay = "Una programación semanal o mensual necesita decir qué día";
        public const string DayOfWeekOutOfRange = "El día de la semana va de 1 (lunes) a 7 (domingo)";
        public const string DayOfMonthOutOfRange =
            "El día del mes va del 1 al 28: más allá, el informe no se generaría en febrero";
    }
}
