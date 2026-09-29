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
public sealed class ValidadorDeValorTests
{
    private static CustomFieldDefinition Campo(string tipo, bool obligatorio = false, params string[] opciones)
        => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Un campo", tipo, TargetEntityTypes.Task, obligatorio,
            opciones.Length > 0 ? opciones : null, 0);

    [Fact]
    public void Un_campo_opcional_admite_vacio_y_lo_guarda_como_nulo()
    {
        // Cadena vacía y nulo son lo mismo para quien rellena el formulario. Guardar las dos
        // formas daría recuentos distintos según cuál se consulte.
        var resultado = ValueValidator.Validate(Campo(FieldType.Text), "   ");

        resultado.IsValid.Should().BeTrue();
        resultado.CanonicalValue.Should().BeNull();
    }

    [Fact]
    public void Un_campo_obligatorio_rechaza_el_vacio_y_dice_cual_es()
    {
        var resultado = ValueValidator.Validate(Campo(FieldType.Text, obligatorio: true), "");

        resultado.IsValid.Should().BeFalse();
        resultado.Error.Should().Contain("Un campo", "el mensaje tiene que nombrar el campo que falta");
    }

    [Theory]
    [InlineData("12", "12")]
    [InlineData("12.5", "12.5")]
    [InlineData("12,5", "12.5")]      // coma decimal española, guardada con punto
    [InlineData("-3.25", "-3.25")]
    [InlineData("  8  ", "8")]
    public void El_numero_se_guarda_siempre_con_punto(string entrada, string esperado)
    {
        // Si cada quien guardara en su formato, ordenar o sumar daría resultados distintos según
        // quién escribió cada fila.
        var resultado = ValueValidator.Validate(Campo(FieldType.Number), entrada);

        resultado.IsValid.Should().BeTrue();
        resultado.CanonicalValue.Should().Be(esperado);
    }

    [Theory]
    [InlineData("doce")]
    [InlineData("12 euros")]
    [InlineData("--3")]
    public void Lo_que_no_es_numero_se_rechaza(string entrada)
    {
        ValueValidator.Validate(Campo(FieldType.Number), entrada).IsValid.Should().BeFalse();
    }

    [Fact]
    public void La_fecha_se_guarda_en_ISO()
    {
        var resultado = ValueValidator.Validate(Campo(FieldType.Date), "2026-03-04");

        resultado.IsValid.Should().BeTrue();
        resultado.CanonicalValue.Should().Be("2026-03-04");
    }

    [Theory]
    [InlineData("2026-13-01")]  // mes 13
    [InlineData("2026-02-30")]  // día que no existe
    [InlineData("ayer")]
    public void Una_fecha_imposible_se_rechaza(string entrada)
    {
        ValueValidator.Validate(Campo(FieldType.Date), entrada).IsValid.Should().BeFalse();
    }

    [Fact]
    public void La_seleccion_sólo_admite_una_de_sus_opciones()
    {
        var campo = Campo(FieldType.Select, false, "Alta", "Media", "Baja");

        ValueValidator.Validate(campo, "Media").IsValid.Should().BeTrue();

        var invalida = ValueValidator.Validate(campo, "Altísima");
        invalida.IsValid.Should().BeFalse();
        invalida.Error.Should().Contain("Altísima", "el mensaje tiene que decir qué valor sobra");
    }

    [Fact]
    public void La_seleccion_multiple_se_guarda_en_el_orden_de_la_definicion()
    {
        // Así dos entidades con la misma selección tienen el mismo valor guardado y se pueden
        // comparar y agrupar; si se guardara en el orden en que se marcó, no.
        var campo = Campo(FieldType.MultiSelect, false, "Rojo", "Verde", "Azul");

        var resultado = ValueValidator.Validate(campo, "Azul\nRojo");

        resultado.IsValid.Should().BeTrue();
        resultado.CanonicalValue.Should().Be("Rojo\nAzul");
    }

    [Fact]
    public void La_seleccion_multiple_no_duplica()
    {
        var campo = Campo(FieldType.MultiSelect, false, "Rojo", "Verde");

        ValueValidator.Validate(campo, "Rojo\nRojo").CanonicalValue.Should().Be("Rojo");
    }

    [Fact]
    public void La_seleccion_multiple_rechaza_una_opcion_que_no_existe()
    {
        var campo = Campo(FieldType.MultiSelect, false, "Rojo", "Verde");

        ValueValidator.Validate(campo, "Rojo\nMorado").IsValid.Should().BeFalse();
    }

    [Fact]
    public void El_campo_de_usuario_exige_un_identificador_de_verdad()
    {
        var campo = Campo(FieldType.User);
        var alguien = Guid.NewGuid();

        ValueValidator.Validate(campo, alguien.ToString()).CanonicalValue.Should().Be(alguien.ToString());
        ValueValidator.Validate(campo, "Fernando").IsValid.Should().BeFalse();
        ValueValidator.Validate(campo, Guid.Empty.ToString()).IsValid.Should().BeFalse(
            "el Guid vacío no es una persona");
    }

    [Fact]
    public void Un_campo_de_seleccion_no_se_puede_definir_sin_opciones()
    {
        var crear = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Estado", FieldType.Select, TargetEntityTypes.Task, false, null, 0);

        crear.Should().Throw<InvalidOperationException>().WithMessage("*al menos una opción*");
    }

    [Fact]
    public void Las_opciones_repetidas_o_vacias_se_limpian_al_definir()
    {
        var campo = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Estado", FieldType.Select, TargetEntityTypes.Task, false,
            ["Alta", "  ", "Alta", " Baja "], 0);

        campo.Options.Should().Equal("Alta", "Baja");
    }

    [Fact]
    public void Un_campo_que_no_es_de_seleccion_no_guarda_opciones()
    {
        var campo = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Notas", FieldType.Text, TargetEntityTypes.Task, false, ["sobra"], 0);

        campo.Options.Should().BeEmpty();
    }

    [Fact]
    public void El_tipo_y_la_entidad_se_validan_al_definir()
    {
        var tipoRaro = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "X", "Semaforo", TargetEntityTypes.Task, false, null, 0);
        var entidadRara = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "X", FieldType.Text, "Factura", false, null, 0);

        tipoRaro.Should().Throw<InvalidOperationException>().WithMessage("*tipo de campo no existe*");
        entidadRara.Should().Throw<InvalidOperationException>().WithMessage("*Tarea o Proyecto*");
    }

    /// <summary>
    /// La fórmula estuvo fuera de la lista de tipos mientras no hubo motor detrás, y esta
    /// prueba lo vigilaba. Ya lo hay —analizador, evaluador y detector de ciclos, con sus
    /// pruebas en FormulasTests—, así que lo que hay que vigilar ahora es lo contrario: que un
    /// campo calculado no se pueda definir sin fórmula, que era justo el «tipo que se puede
    /// elegir y no calcula nada» que se quería evitar.
    /// </summary>
    [Fact]
    public void Un_campo_calculado_no_se_puede_definir_sin_formula()
    {
        FieldType.All().Should().Contain(FieldType.Formula);

        var sinFormula = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Total", FieldType.Formula, TargetEntityTypes.Task, false, null, 0);

        sinFormula.Should().Throw<InvalidOperationException>()
            .WithMessage(CustomFieldDefinition.Rules.MissingFormula);
    }

    /// <summary>
    /// Y que la fórmula se comprueba al definirla, no al leerla: quien la escribe es quien
    /// puede arreglarla, y sólo la tiene delante en ese momento.
    /// </summary>
    [Fact]
    public void Una_formula_ilegible_se_rechaza_al_definir_el_campo()
    {
        var rota = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Total", FieldType.Formula, TargetEntityTypes.Task, false, null, 0, "2 * (3 +");

        rota.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Un campo calculado nunca es obligatorio: no hay a quién exigírselo, y marcarlo dejaría
    /// el formulario sin poder guardarse jamás.
    /// </summary>
    [Fact]
    public void Un_campo_calculado_no_puede_ser_obligatorio()
    {
        var campo = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Total", FieldType.Formula, TargetEntityTypes.Task,
            isRequired: true, null, 0, "1 + 1");

        campo.IsRequired.Should().BeFalse();
    }
}
