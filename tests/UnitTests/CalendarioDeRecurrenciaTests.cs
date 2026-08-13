using FluentAssertions;
using WorkItems.Domain.Servicios;
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
public sealed class CalendarioDeRecurrenciaTests
{
    private static PatronDeRecurrencia Patron(string frecuencia, int intervalo, DateOnly desde)
        => new(frecuencia, intervalo, desde, null);

    [Theory]
    [InlineData(1, "2026-08-13")]
    [InlineData(2, "2026-08-14")]
    [InlineData(30, "2026-09-11")]
    public void La_diaria_suma_dias(int intervalo, string esperada)
    {
        var desde = new DateOnly(2026, 8, 12);

        CalendarioDeRecurrencia.Siguiente(desde, Patron(PatronDeRecurrencia.Frecuencias.Diaria, intervalo, desde))
            .Should().Be(DateOnly.Parse(esperada));
    }

    [Theory]
    [InlineData(1, "2026-08-19")]
    [InlineData(2, "2026-08-26")]
    public void La_semanal_suma_semanas_y_cae_el_mismo_dia(int intervalo, string esperada)
    {
        var desde = new DateOnly(2026, 8, 12); // miércoles
        var siguiente = CalendarioDeRecurrencia.Siguiente(desde, Patron(PatronDeRecurrencia.Frecuencias.Semanal, intervalo, desde));

        siguiente.Should().Be(DateOnly.Parse(esperada));
        siguiente.DayOfWeek.Should().Be(desde.DayOfWeek);
    }

    [Fact]
    public void La_mensual_conserva_el_dia_del_mes()
    {
        var desde = new DateOnly(2026, 1, 15);

        CalendarioDeRecurrencia.Siguiente(desde, Patron(PatronDeRecurrencia.Frecuencias.Mensual, 1, desde))
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
    public void La_mensual_del_31_se_recorta_en_febrero_y_vuelve_al_31_en_marzo()
    {
        var patron = Patron(PatronDeRecurrencia.Frecuencias.Mensual, 1, new DateOnly(2026, 1, 31));

        var febrero = CalendarioDeRecurrencia.Siguiente(new DateOnly(2026, 1, 31), patron);
        febrero.Should().Be(new DateOnly(2026, 2, 28), "2026 no es bisiesto");

        var marzo = CalendarioDeRecurrencia.Siguiente(febrero, patron);
        marzo.Should().Be(new DateOnly(2026, 3, 31), "la serie recupera su día, no se queda en 28");

        var abril = CalendarioDeRecurrencia.Siguiente(marzo, patron);
        abril.Should().Be(new DateOnly(2026, 4, 30), "abril tiene 30");
    }

    [Fact]
    public void En_año_bisiesto_el_31_de_enero_cae_el_29()
    {
        var patron = Patron(PatronDeRecurrencia.Frecuencias.Mensual, 1, new DateOnly(2028, 1, 31));

        CalendarioDeRecurrencia.Siguiente(new DateOnly(2028, 1, 31), patron)
            .Should().Be(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void La_mensual_cruza_el_fin_de_año()
    {
        var patron = Patron(PatronDeRecurrencia.Frecuencias.Mensual, 2, new DateOnly(2026, 11, 30));

        CalendarioDeRecurrencia.Siguiente(new DateOnly(2026, 11, 30), patron)
            .Should().Be(new DateOnly(2027, 1, 30));
    }

    [Fact]
    public void Un_año_entero_de_una_serie_del_31_siempre_cae_a_fin_de_mes()
    {
        // Recorre los doce meses: cada fecha tiene que ser el día 31 o el último del mes, nunca
        // un día suelto del medio.
        var patron = Patron(PatronDeRecurrencia.Frecuencias.Mensual, 1, new DateOnly(2026, 1, 31));
        var fecha = new DateOnly(2026, 1, 31);

        for (var mes = 0; mes < 12; mes++)
        {
            fecha = CalendarioDeRecurrencia.Siguiente(fecha, patron);
            var ultimoDelMes = DateTime.DaysInMonth(fecha.Year, fecha.Month);

            fecha.Day.Should().Be(Math.Min(31, ultimoDelMes),
                $"la ocurrencia de {fecha:yyyy-MM} debe caer a fin de mes");
        }
    }

    [Fact]
    public void Una_frecuencia_desconocida_no_se_puede_construir()
    {
        var crear = () => new PatronDeRecurrencia("Trimestral", 1, DateOnly.FromDateTime(DateTime.UtcNow), null);

        crear.Should().Throw<InvalidOperationException>().WithMessage("*Diaria, Semanal o Mensual*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void Un_intervalo_fuera_de_rango_no_se_acepta(int intervalo)
    {
        var crear = () => new PatronDeRecurrencia(
            PatronDeRecurrencia.Frecuencias.Diaria, intervalo, DateOnly.FromDateTime(DateTime.UtcNow), null);

        crear.Should().Throw<InvalidOperationException>().WithMessage("*intervalo*");
    }

    [Fact]
    public void Una_fecha_de_fin_anterior_al_principio_no_se_acepta()
    {
        var crear = () => new PatronDeRecurrencia(
            PatronDeRecurrencia.Frecuencias.Diaria, 1,
            new DateOnly(2026, 8, 12), new DateOnly(2026, 8, 1));

        crear.Should().Throw<InvalidOperationException>().WithMessage("*no puede ser anterior*");
    }
}
