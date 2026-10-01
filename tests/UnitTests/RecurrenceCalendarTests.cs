using FluentAssertions;
using WorkItems.Domain.Services;
using WorkItems.Domain.ValueObjects;
using Xunit;

namespace UnitTests;

/// <summary>
/// El cálculo de la siguiente fecha de una serie.
///
/// Es una función pura y se prueba a fondo por la misma razón que el detector de ciclos: son
/// cuentas de calendario, y ahí se esconden los errores que nadie ve hasta que un cliente
/// reclama que su tarea del día 31 lleva medio año cayendo el 28.
/// </summary>
public sealed class RecurrenceCalendarTests
{
    private static RecurrencePattern Patron(string frequency, int interval, DateOnly from)
        => new(frequency, interval, from, null);

    [Theory]
    [InlineData(1, "2026-08-13")]
    [InlineData(2, "2026-08-14")]
    [InlineData(30, "2026-09-11")]
    public void Daily_adds_days(int interval, string expected)
    {
        var from = new DateOnly(2026, 8, 12);

        RecurrenceCalendar.Next(from, Patron(RecurrencePattern.Frequencies.Daily, interval, from))
            .Should().Be(DateOnly.Parse(expected));
    }

    [Theory]
    [InlineData(1, "2026-08-19")]
    [InlineData(2, "2026-08-26")]
    public void Weekly_adds_weeks_and_lands_on_the_same_weekday(int interval, string expected)
    {
        var from = new DateOnly(2026, 8, 12); // miércoles
        var next = RecurrenceCalendar.Next(from, Patron(RecurrencePattern.Frequencies.Weekly, interval, from));

        next.Should().Be(DateOnly.Parse(expected));
        next.DayOfWeek.Should().Be(from.DayOfWeek);
    }

    [Fact]
    public void Monthly_keeps_the_day_of_month()
    {
        var from = new DateOnly(2026, 1, 15);

        RecurrenceCalendar.Next(from, Patron(RecurrencePattern.Frequencies.Monthly, 1, from))
            .Should().Be(new DateOnly(2026, 2, 15));
    }

    /// <summary>
    /// El caso que justifica guardar el día de la serie.
    ///
    /// Una serie que empieza el 31 de enero cae el 28 en febrero, pero **tiene que volver al 31**
    /// en marzo. Si la siguiente fecha se calculara desde la última ya recortada, la serie se
    /// degradaría a 28 para siempre y nadie sabría por qué.
    /// </summary>
    [Fact]
    public void Monthly_on_the_31st_shrinks_in_February_and_returns_in_March()
    {
        var patron = Patron(RecurrencePattern.Frequencies.Monthly, 1, new DateOnly(2026, 1, 31));

        var february = RecurrenceCalendar.Next(new DateOnly(2026, 1, 31), patron);
        february.Should().Be(new DateOnly(2026, 2, 28), "2026 no es bisiesto");

        var march = RecurrenceCalendar.Next(february, patron);
        march.Should().Be(new DateOnly(2026, 3, 31), "la serie recupera su día, no se queda en 28");

        var april = RecurrenceCalendar.Next(march, patron);
        april.Should().Be(new DateOnly(2026, 4, 30), "abril tiene 30");
    }

    [Fact]
    public void In_a_leap_year_January_31_lands_on_the_29th()
    {
        var patron = Patron(RecurrencePattern.Frequencies.Monthly, 1, new DateOnly(2028, 1, 31));

        RecurrenceCalendar.Next(new DateOnly(2028, 1, 31), patron)
            .Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void Monthly_crosses_the_year_end()
    {
        var patron = Patron(RecurrencePattern.Frequencies.Monthly, 2, new DateOnly(2026, 11, 30));

        RecurrenceCalendar.Next(new DateOnly(2026, 11, 30), patron)
            .Should().Be(new DateOnly(2027, 1, 30));
    }

    [Fact]
    public void A_whole_year_of_a_31st_series_always_lands_on_month_end()
    {
        // Recorre los doce meses: cada fecha tiene que ser el día 31 o el último del mes, nunca
        // un día suelto del medio.
        var patron = Patron(RecurrencePattern.Frequencies.Monthly, 1, new DateOnly(2026, 1, 31));
        var date = new DateOnly(2026, 1, 31);

        for (var month = 0; month < 12; month++)
        {
            date = RecurrenceCalendar.Next(date, patron);
            var lastOfMonth = DateTime.DaysInMonth(date.Year, date.Month);

            date.Day.Should().Be(Math.Min(31, lastOfMonth),
                $"la ocurrencia de {date:yyyy-MM} debe caer a fin de mes");
        }
    }

    [Fact]
    public void An_unknown_frequency_cannot_be_built()
    {
        var create = () => new RecurrencePattern("Trimestral", 1, DateOnly.FromDateTime(DateTime.UtcNow), null);

        create.Should().Throw<InvalidOperationException>().WithMessage("*Daily, Weekly o Monthly*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void An_out_of_range_interval_is_not_accepted(int interval)
    {
        var create = () => new RecurrencePattern(
            RecurrencePattern.Frequencies.Daily, interval, DateOnly.FromDateTime(DateTime.UtcNow), null);

        create.Should().Throw<InvalidOperationException>().WithMessage("*intervalo*");
    }

    [Fact]
    public void An_end_date_before_the_start_is_not_accepted()
    {
        var create = () => new RecurrencePattern(
            RecurrencePattern.Frequencies.Daily, 1,
            new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 1));

        create.Should().Throw<InvalidOperationException>().WithMessage("*no puede ser anterior*");
    }
}
