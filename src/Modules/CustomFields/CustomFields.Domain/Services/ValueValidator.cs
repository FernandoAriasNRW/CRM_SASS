using System.Globalization;
using CustomFields.Domain.Entities;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Domain.Services;

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
public static class ValueValidator
{
    /// <summary>Separador de la selección múltiple. Un salto de línea no aparece en una opción.</summary>
    public const string MultiSeparator = "\n";

    public sealed record ValidationOutcome(bool IsValid, string? CanonicalValue, string? Error)
    {
        public static ValidationOutcome Ok(string? value) => new(true, value, null);
        public static ValidationOutcome Fail(string error) => new(false, null, error);
    }

    public static ValidationOutcome Validate(CustomFieldDefinition definition, string? value)
    {
        var text = (value ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            return definition.IsRequired
                ? ValidationOutcome.Fail(string.Format(Errors.RequiredValue, definition.Name))
                // Vacío se guarda como nulo y no como cadena vacía: son lo mismo para quien
                // rellena el formulario, y dos formas de decir «sin valor» acaban dando
                // recuentos distintos según cuál se consulte.
                : ValidationOutcome.Ok(null);
        }

        return definition.Type switch
        {
            FieldType.Text => ValidationOutcome.Ok(text),
            FieldType.Number => ValidateNumber(text),
            FieldType.Date => ValidateDate(text),
            FieldType.User => ValidateUser(text),
            FieldType.Select => ValidateSelect(definition, text),
            FieldType.MultiSelect => ValidateMultiSelect(definition, text),
            _ => ValidationOutcome.Fail(CustomFieldDefinition.Rules.UnknownType)
        };
    }

    private static ValidationOutcome ValidateNumber(string text)
    {
        // Se admite la coma decimal al escribir —en español es lo natural— pero se guarda con
        // punto: si cada quien guardara en su formato, ordenar o sumar daría resultados según
        // quién escribió cada fila.
        var normalized = text.Replace(',', '.');

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? ValidationOutcome.Ok(number.ToString(CultureInfo.InvariantCulture))
            : ValidationOutcome.Fail(Errors.NotANumber);
    }

    private static ValidationOutcome ValidateDate(string text)
        => DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? ValidationOutcome.Ok(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : ValidationOutcome.Fail(Errors.NotADate);

    private static ValidationOutcome ValidateUser(string text)
        => Guid.TryParse(text, out var id) && id != Guid.Empty
            ? ValidationOutcome.Ok(id.ToString())
            : ValidationOutcome.Fail(Errors.NotAUser);

    private static ValidationOutcome ValidateSelect(CustomFieldDefinition definition, string text)
        => definition.Options.Contains(text)
            ? ValidationOutcome.Ok(text)
            : ValidationOutcome.Fail(string.Format(Errors.InvalidOption, text));

    private static ValidationOutcome ValidateMultiSelect(CustomFieldDefinition definition, string text)
    {
        var selected = text
            .Split(MultiSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        var invalid = selected.FirstOrDefault(e => !definition.Options.Contains(e));
        if (invalid is not null)
            return ValidationOutcome.Fail(string.Format(Errors.InvalidOption, invalid));

        if (selected.Count == 0)
            return ValidationOutcome.Ok(null);

        // Se guardan en el orden de la definición, no en el que las marcó el usuario: así dos
        // entidades con la misma selección tienen el mismo valor y se pueden comparar y agrupar.
        var sorted = definition.Options.Where(selected.Contains);

        return ValidationOutcome.Ok(string.Join(MultiSeparator, sorted));
    }

    public static class Errors
    {
        public const string RequiredValue = "El campo «{0}» es obligatorio";
        public const string NotANumber = "El valor no es un número";
        public const string NotADate = "El valor no es una fecha válida (aaaa-mm-dd)";
        public const string NotAUser = "El valor no es un usuario válido";
        public const string InvalidOption = "«{0}» no está entre las opciones del campo";
    }
}
