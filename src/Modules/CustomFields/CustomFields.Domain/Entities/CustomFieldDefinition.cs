using BuildingBlocks.Domain.Primitives;
using CustomFields.Domain.Events;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Domain.Entities;

/// <summary>
/// La definición de un campo personalizado: qué se pregunta, de qué tipo y sobre qué entidad.
///
/// La definición y el valor son cosas distintas y viven separadas. Un cliente define «Cliente
/// facturable» una vez y luego hay miles de tareas con su valor; meterlo todo en la misma tabla
/// obligaría a repetir el nombre y el tipo en cada fila, y renombrar el campo sería un UPDATE
/// masivo en lugar de tocar una fila.
/// </summary>
public sealed class CustomFieldDefinition : AggregateRoot, ITenantEntity
{
    public const int MaxNameLength = 80;
    public const int MaxOptions = 50;

    public Guid TenantId { get; private set; }

    /// <summary>Lo que ve quien rellena el campo.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Uno de <see cref="FieldType"/>.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Sobre qué entidad aplica: tarea o proyecto.</summary>
    public string TargetEntity { get; private set; } = string.Empty;

    /// <summary>Si hay que rellenarlo para guardar la entidad.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>Opciones de los tipos de selección, en el orden en que se muestran.</summary>
    public List<string> Options { get; private set; } = [];

    /// <summary>Orden en que aparece el campo en el formulario.</summary>
    public int Position { get; private set; }

    /// <summary>
    /// La expresión de un campo calculado, o <c>null</c> en los demás.
    ///
    /// Se guarda el texto tal como se escribió, no el árbol: es lo que hay que volver a enseñar
    /// para editarla, y un árbol serializado quedaría atado a la forma interna del analizador.
    /// Se vuelve a analizar al leer, que cuesta microsegundos.
    /// </summary>
    public string? Formula { get; private set; }

    private CustomFieldDefinition() { }

    public static CustomFieldDefinition Create(
        Guid tenantId, string name, string type, string targetEntity,
        bool isRequired, IEnumerable<string>? options, int position, string? formula = null)
    {
        var cleanName = (name ?? string.Empty).Trim();

        if (cleanName.Length == 0)
            throw new InvalidOperationException(Rules.NameRequired);

        if (cleanName.Length > MaxNameLength)
            throw new InvalidOperationException(Rules.NameTooLong);

        if (!FieldType.Exists(type))
            throw new InvalidOperationException(Rules.UnknownType);

        if (!TargetEntityTypes.Exists(targetEntity))
            throw new InvalidOperationException(Rules.UnknownEntity);

        var optionList = NormalizeOptions(type, options);
        var cleanFormula = NormalizeFormula(type, formula);

        var definition = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = cleanName,
            Type = type,
            TargetEntity = targetEntity,
            // Un campo calculado no se rellena, así que exigirlo no tendría a quién
            // exigírselo: marcarlo obligatorio dejaría el formulario sin poder guardarse.
            IsRequired = !FieldType.IsComputed(type) && isRequired,
            Options = optionList,
            Position = position,
            Formula = cleanFormula
        };

        definition.RaiseDomainEvent(new CustomFieldDefinedEvent(definition.Id, tenantId, cleanName, type, targetEntity));

        return definition;
    }

    /// <summary>
    /// Cambia el nombre, la obligatoriedad y las opciones.
    ///
    /// **El tipo y la entidad no se pueden cambiar.** Pasar un campo de texto a número dejaría
    /// todos los valores ya guardados sin validez, y cambiar la entidad dejaría huérfanos los de
    /// la anterior. Para eso se borra el campo y se crea otro, que además deja claro que los
    /// datos viejos se pierden.
    /// </summary>
    public void Update(string name, bool isRequired, IEnumerable<string>? options, int position,
        string? formula = null)
    {
        var cleanName = (name ?? string.Empty).Trim();

        if (cleanName.Length == 0)
            throw new InvalidOperationException(Rules.NameRequired);

        if (cleanName.Length > MaxNameLength)
            throw new InvalidOperationException(Rules.NameTooLong);

        Name = cleanName;
        IsRequired = !FieldType.IsComputed(Type) && isRequired;
        Options = NormalizeOptions(Type, options);
        Position = position;
        Formula = NormalizeFormula(Type, formula);

        RaiseDomainEvent(new CustomFieldUpdatedEvent(Id, TenantId, Name));
    }

    /// <summary>
    /// Comprueba que la fórmula se puede leer, y la rechaza si no.
    ///
    /// Sólo el análisis: que las referencias apunten a campos que existen y que no formen un
    /// ciclo se comprueba en la capa de aplicación, que es la única que ve los demás campos del
    /// inquilino. El dominio comprueba lo que puede comprobar solo.
    /// </summary>
    private static string? NormalizeFormula(string type, string? formula)
    {
        if (!FieldType.IsComputed(type))
            return null;

        var text = (formula ?? string.Empty).Trim();

        if (text.Length == 0)
            throw new InvalidOperationException(Rules.MissingFormula);

        var analysis = Services.FormulaParser.Parse(text);

        if (!analysis.IsValid)
            throw new InvalidOperationException(analysis.Error);

        return text;
    }

    private static List<string> NormalizeOptions(string type, IEnumerable<string>? options)
    {
        if (!FieldType.UsesOptions(type))
            return [];

        var list = (options ?? [])
            .Select(o => (o ?? string.Empty).Trim())
            .Where(o => o.Length > 0)
            .Distinct()
            .ToList();

        if (list.Count == 0)
            throw new InvalidOperationException(Rules.MissingOptions);

        if (list.Count > MaxOptions)
            throw new InvalidOperationException(Rules.TooManyOptions);

        return list;
    }

    public static class Rules
    {
        public const string NameRequired = "El campo necesita un nombre";
        public static readonly string NameTooLong =
            $"El nombre del campo no puede pasar de {MaxNameLength} caracteres";
        public const string UnknownType = "El tipo de campo no existe";
        public const string UnknownEntity = "El campo sólo puede aplicarse a Tarea o Proyecto";
        public const string MissingOptions = "Un campo de selección necesita al menos una opción";
        public static readonly string TooManyOptions =
            $"Un campo de selección no puede tener más de {MaxOptions} opciones";
        public const string DuplicateName = "Ya hay un campo con ese nombre para esa entidad";
        public const string MissingFormula = "Un campo calculado necesita una fórmula";
        public const string FormulaCycle =
            "La fórmula se refiere a sí misma, directa o indirectamente, y no se podría calcular";
        public static readonly string NonNumericReference =
            "Una fórmula sólo puede usar campos de tipo Número u otros campos calculados";
        public const string ComputedIsReadOnly =
            "Un campo calculado no se rellena: su valor sale de su fórmula";
    }
}
