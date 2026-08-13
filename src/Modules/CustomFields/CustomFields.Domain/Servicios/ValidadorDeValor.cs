using System.Globalization;
using CustomFields.Domain.Entities;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Domain.Servicios;

/// <summary>
/// Comprueba que un valor encaja con la definición de su campo, y lo deja en forma canónica.
///
/// Es una función pura, como el detector de ciclos y el calendario de recurrencia, y por el
/// mismo motivo: aquí es donde se cuela la basura. Un campo «Número» que acepte «12,50» en un
/// servidor y lo rechace en otro, o una fecha guardada unas veces como 03/04 y otras como
/// 04/03, son fallos que no dan error al escribir y se descubren meses después, al sumar o al
/// ordenar.
///
/// Por eso se guarda **siempre en el mismo formato**: los números con punto decimal e
/// invariantes de cultura, las fechas en ISO, y las selecciones múltiples separadas por saltos
/// de línea —no por comas, que aparecen dentro de las propias opciones—.
/// </summary>
public static class ValidadorDeValor
{
    /// <summary>Separador de la selección múltiple. Un salto de línea no aparece en una opción.</summary>
    public const string SeparadorDeMultiple = "\n";

    public sealed record Resultado(bool EsValido, string? ValorCanonico, string? Error)
    {
        public static Resultado Bien(string? valor) => new(true, valor, null);
        public static Resultado Mal(string error) => new(false, null, error);
    }

    public static Resultado Validar(CustomFieldDefinition definicion, string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();

        if (texto.Length == 0)
        {
            return definicion.Obligatorio
                ? Resultado.Mal(string.Format(Errores.Obligatorio, definicion.Nombre))
                // Vacío se guarda como nulo y no como cadena vacía: son lo mismo para quien
                // rellena el formulario, y dos formas de decir «sin valor» acaban dando
                // recuentos distintos según cuál se consulte.
                : Resultado.Bien(null);
        }

        return definicion.Tipo switch
        {
            TipoDeCampo.Texto => Resultado.Bien(texto),
            TipoDeCampo.Numero => ValidarNumero(texto),
            TipoDeCampo.Fecha => ValidarFecha(texto),
            TipoDeCampo.Usuario => ValidarUsuario(texto),
            TipoDeCampo.Seleccion => ValidarSeleccion(definicion, texto),
            TipoDeCampo.SeleccionMultiple => ValidarSeleccionMultiple(definicion, texto),
            _ => Resultado.Mal(CustomFieldDefinition.Reglas.TipoDesconocido)
        };
    }

    private static Resultado ValidarNumero(string texto)
    {
        // Se admite la coma decimal al escribir —en español es lo natural— pero se guarda con
        // punto: si cada quien guardara en su formato, ordenar o sumar daría resultados según
        // quién escribió cada fila.
        var normalizado = texto.Replace(',', '.');

        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero)
            ? Resultado.Bien(numero.ToString(CultureInfo.InvariantCulture))
            : Resultado.Mal(Errores.NoEsNumero);
    }

    private static Resultado ValidarFecha(string texto)
        => DateOnly.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? Resultado.Bien(fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : Resultado.Mal(Errores.NoEsFecha);

    private static Resultado ValidarUsuario(string texto)
        => Guid.TryParse(texto, out var id) && id != Guid.Empty
            ? Resultado.Bien(id.ToString())
            : Resultado.Mal(Errores.NoEsUsuario);

    private static Resultado ValidarSeleccion(CustomFieldDefinition definicion, string texto)
        => definicion.Opciones.Contains(texto)
            ? Resultado.Bien(texto)
            : Resultado.Mal(string.Format(Errores.OpcionInvalida, texto));

    private static Resultado ValidarSeleccionMultiple(CustomFieldDefinition definicion, string texto)
    {
        var elegidas = texto
            .Split(SeparadorDeMultiple, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        var invalida = elegidas.FirstOrDefault(e => !definicion.Opciones.Contains(e));
        if (invalida is not null)
            return Resultado.Mal(string.Format(Errores.OpcionInvalida, invalida));

        if (elegidas.Count == 0)
            return Resultado.Bien(null);

        // Se guardan en el orden de la definición, no en el que las marcó el usuario: así dos
        // entidades con la misma selección tienen el mismo valor y se pueden comparar y agrupar.
        var ordenadas = definicion.Opciones.Where(elegidas.Contains);

        return Resultado.Bien(string.Join(SeparadorDeMultiple, ordenadas));
    }

    public static class Errores
    {
        public const string Obligatorio = "El campo «{0}» es obligatorio";
        public const string NoEsNumero = "El valor no es un número";
        public const string NoEsFecha = "El valor no es una fecha válida (aaaa-mm-dd)";
        public const string NoEsUsuario = "El valor no es un usuario válido";
        public const string OpcionInvalida = "«{0}» no está entre las opciones del campo";
    }
}
