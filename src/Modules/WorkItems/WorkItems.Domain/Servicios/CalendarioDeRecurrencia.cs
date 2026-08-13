using WorkItems.Domain.ValueObjects;

namespace WorkItems.Domain.Servicios;

/// <summary>
/// Calcula cuándo toca la siguiente tarea de una serie.
///
/// Es una función pura, como el detector de ciclos, y por el mismo motivo: son cuentas de
/// calendario, que es donde se esconden los errores que nadie ve hasta que un cliente reclama.
/// El caso que lo demuestra es el 31 de enero repitiéndose cada mes: sumar «un mes» no tiene
/// respuesta obvia, y la que se elija hay que sostenerla todos los meses del año.
/// </summary>
public static class CalendarioDeRecurrencia
{
    /// <summary>
    /// La fecha siguiente a <paramref name="desde"/> según el patrón.
    ///
    /// En las mensuales se conserva el **día del mes de la serie** y se recorta al último día
    /// cuando el mes destino es más corto: 31 de enero, cada mes, da 28 de febrero (o 29 en
    /// bisiesto) y **vuelve a 31 en marzo**. Avanzar desde el día ya recortado iría
    /// degradándose —31 → 28 → 28 → 28— y la serie acabaría cayendo el 28 para siempre, que no
    /// es lo que pidió nadie.
    /// </summary>
    public static DateOnly Siguiente(DateOnly desde, PatronDeRecurrencia patron)
        => patron.Frecuencia switch
        {
            PatronDeRecurrencia.Frecuencias.Diaria => desde.AddDays(patron.Intervalo),
            PatronDeRecurrencia.Frecuencias.Semanal => desde.AddDays(7 * patron.Intervalo),
            PatronDeRecurrencia.Frecuencias.Mensual => SiguienteMensual(desde, patron.Intervalo, patron.DiaDeLaSerie),
            _ => throw new InvalidOperationException(PatronDeRecurrencia.Reglas.FrecuenciaDesconocida)
        };

    private static DateOnly SiguienteMensual(DateOnly desde, int intervalo, int diaDeLaSerie)
    {
        // Se cuenta desde el primero de mes para que el recorte de un mes corto no arrastre al
        // siguiente: es lo que hace que 31 → 28 de febrero → 31 de marzo.
        var primeroDelMes = new DateOnly(desde.Year, desde.Month, 1).AddMonths(intervalo);
        var diasDelMes = DateTime.DaysInMonth(primeroDelMes.Year, primeroDelMes.Month);

        return new DateOnly(primeroDelMes.Year, primeroDelMes.Month, Math.Min(diaDeLaSerie, diasDelMes));
    }
}
