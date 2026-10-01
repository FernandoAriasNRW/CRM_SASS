using CustomFields.Domain.Entities;
using CustomFields.Domain.Services;
using CustomFields.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace UnitTests;

/// <summary>
/// La validación de valores de campos personalizados.
///
/// Es la puerta por donde entra la basura al sistema, y los errores que deja pasar no dan
/// ningún error al escribir: se descubren meses después, al sumar una columna que resulta que
/// tiene textos, o al ordenar fechas guardadas en dos formatos distintos. Por eso se prueba
/// aquí, sin base de datos y a fondo.
/// </summary>
public sealed class ValueValidatorTests
{
    private static CustomFieldDefinition Field(string type, bool required = false, params string[] options)
        => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Un campo", type, TargetEntityTypes.Task, required,
            options.Length > 0 ? options : null, 0);

    [Fact]
    public void An_optional_field_accepts_empty_and_saves_null()
    {
        // Cadena vacía y nulo son lo mismo para quien rellena el formulario. Guardar las dos
        // formas daría recuentos distintos según cuál se consulte.
        var result = ValueValidator.Validate(Field(FieldType.Text), "   ");

        result.IsValid.Should().BeTrue();
        result.CanonicalValue.Should().BeNull();
    }

    [Fact]
    public void A_required_field_rejects_empty_and_names_itself()
    {
        var result = ValueValidator.Validate(Field(FieldType.Text, required: true), "");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Un campo", "el mensaje tiene que nombrar el campo que falta");
    }

    [Theory]
    [InlineData("12", "12")]
    [InlineData("12.5", "12.5")]
    [InlineData("12,5", "12.5")]      // coma decimal española, guardada con punto
    [InlineData("-3.25", "-3.25")]
    [InlineData("  8  ", "8")]
    public void Numbers_are_always_saved_with_a_dot(string input, string expected)
    {
        // Si cada quien guardara en su formato, ordenar o sumar daría resultados distintos según
        // quién escribió cada fila.
        var result = ValueValidator.Validate(Field(FieldType.Number), input);

        result.IsValid.Should().BeTrue();
        result.CanonicalValue.Should().Be(expected);
    }

    [Theory]
    [InlineData("doce")]
    [InlineData("12 euros")]
    [InlineData("--3")]
    public void Non_numbers_are_rejected(string input)
    {
        ValueValidator.Validate(Field(FieldType.Number), input).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Dates_are_saved_as_ISO()
    {
        var result = ValueValidator.Validate(Field(FieldType.Date), "2026-03-04");

        result.IsValid.Should().BeTrue();
        result.CanonicalValue.Should().Be("2026-03-04");
    }

    [Theory]
    [InlineData("2026-13-01")]  // mes 13
    [InlineData("2026-02-30")]  // día que no existe
    [InlineData("ayer")]
    public void An_impossible_date_is_rejected(string input)
    {
        ValueValidator.Validate(Field(FieldType.Date), input).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Select_only_accepts_one_of_its_options()
    {
        var field = Field(FieldType.Select, false, "Alta", "Media", "Baja");

        ValueValidator.Validate(field, "Media").IsValid.Should().BeTrue();

        var invalid = ValueValidator.Validate(field, "Altísima");
        invalid.IsValid.Should().BeFalse();
        invalid.Error.Should().Contain("Altísima", "el mensaje tiene que decir qué valor sobra");
    }

    [Fact]
    public void Multi_select_is_saved_in_definition_order()
    {
        // Así dos entidades con la misma selección tienen el mismo valor guardado y se pueden
        // comparar y agrupar; si se guardara en el orden en que se marcó, no.
        var field = Field(FieldType.MultiSelect, false, "Rojo", "Verde", "Azul");

        var result = ValueValidator.Validate(field, "Azul\nRojo");

        result.IsValid.Should().BeTrue();
        result.CanonicalValue.Should().Be("Rojo\nAzul");
    }

    [Fact]
    public void Multi_select_does_not_duplicate()
    {
        var field = Field(FieldType.MultiSelect, false, "Rojo", "Verde");

        ValueValidator.Validate(field, "Rojo\nRojo").CanonicalValue.Should().Be("Rojo");
    }

    [Fact]
    public void Multi_select_rejects_an_unknown_option()
    {
        var field = Field(FieldType.MultiSelect, false, "Rojo", "Verde");

        ValueValidator.Validate(field, "Rojo\nMorado").IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_user_field_requires_a_real_identifier()
    {
        var field = Field(FieldType.User);
        var someone = Guid.NewGuid();

        ValueValidator.Validate(field, someone.ToString()).CanonicalValue.Should().Be(someone.ToString());
        ValueValidator.Validate(field, "Fernando").IsValid.Should().BeFalse();
        ValueValidator.Validate(field, Guid.Empty.ToString()).IsValid.Should().BeFalse(
            "el Guid vacío no es una persona");
    }

    [Fact]
    public void A_select_field_cannot_be_defined_without_options()
    {
        var create = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Estado", FieldType.Select, TargetEntityTypes.Task, false, null, 0);

        create.Should().Throw<InvalidOperationException>().WithMessage("*al menos una opción*");
    }

    [Fact]
    public void Repeated_or_empty_options_are_cleaned_when_defining()
    {
        var field = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Estado", FieldType.Select, TargetEntityTypes.Task, false,
            ["Alta", "  ", "Alta", " Baja "], 0);

        field.Options.Should().Equal("Alta", "Baja");
    }

    [Fact]
    public void A_non_select_field_keeps_no_options()
    {
        var field = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Notas", FieldType.Text, TargetEntityTypes.Task, false, ["sobra"], 0);

        field.Options.Should().BeEmpty();
    }

    [Fact]
    public void Type_and_entity_are_validated_when_defining()
    {
        var oddType = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "X", "Semaforo", TargetEntityTypes.Task, false, null, 0);
        var oddEntity = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "X", FieldType.Text, "Factura", false, null, 0);

        oddType.Should().Throw<InvalidOperationException>().WithMessage("*tipo de campo no existe*");
        oddEntity.Should().Throw<InvalidOperationException>().WithMessage("*Tarea o Proyecto*");
    }

    /// <summary>
    /// La fórmula estuvo fuera de la lista de tipos mientras no hubo motor detrás, y esta
    /// prueba lo vigilaba. Ya lo hay —analizador, evaluador y detector de ciclos, con sus
    /// pruebas en FormulasTests—, así que lo que hay que vigilar ahora es lo contrario: que un
    /// campo calculado no se pueda definir sin fórmula, que era justo el «tipo que se puede
    /// elegir y no calcula nada» que se quería evitar.
    /// </summary>
    [Fact]
    public void A_computed_field_cannot_be_defined_without_formula()
    {
        FieldType.All().Should().Contain(FieldType.Formula);

        var withoutFormula = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Total", FieldType.Formula, TargetEntityTypes.Task, false, null, 0);

        withoutFormula.Should().Throw<InvalidOperationException>()
            .WithMessage(CustomFieldDefinition.Rules.MissingFormula);
    }

    /// <summary>
    /// Y que la fórmula se comprueba al definirla, no al leerla: quien la escribe es quien
    /// puede arreglarla, y sólo la tiene delante en ese momento.
    /// </summary>
    [Fact]
    public void An_unreadable_formula_is_rejected_when_defining_the_field()
    {
        var broken = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Total", FieldType.Formula, TargetEntityTypes.Task, false, null, 0, "2 * (3 +");

        broken.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Un campo calculado nunca es obligatorio: no hay a quién exigírselo, y marcarlo dejaría
    /// el formulario sin poder guardarse jamás.
    /// </summary>
    [Fact]
    public void A_computed_field_cannot_be_required()
    {
        var field = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Total", FieldType.Formula, TargetEntityTypes.Task,
            isRequired: true, null, 0, "1 + 1");

        field.IsRequired.Should().BeFalse();
    }
}
