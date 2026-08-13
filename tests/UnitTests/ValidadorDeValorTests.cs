using CustomFields.Domain.Entities;
using CustomFields.Domain.Servicios;
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
            Guid.NewGuid(), "Un campo", tipo, TipoDeEntidad.Tarea, obligatorio,
            opciones.Length > 0 ? opciones : null, 0);

    [Fact]
    public void Un_campo_opcional_admite_vacio_y_lo_guarda_como_nulo()
    {
        // Cadena vacía y nulo son lo mismo para quien rellena el formulario. Guardar las dos
        // formas daría recuentos distintos según cuál se consulte.
        var resultado = ValidadorDeValor.Validar(Campo(TipoDeCampo.Texto), "   ");

        resultado.EsValido.Should().BeTrue();
        resultado.ValorCanonico.Should().BeNull();
    }

    [Fact]
    public void Un_campo_obligatorio_rechaza_el_vacio_y_dice_cual_es()
    {
        var resultado = ValidadorDeValor.Validar(Campo(TipoDeCampo.Texto, obligatorio: true), "");

        resultado.EsValido.Should().BeFalse();
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
        var resultado = ValidadorDeValor.Validar(Campo(TipoDeCampo.Numero), entrada);

        resultado.EsValido.Should().BeTrue();
        resultado.ValorCanonico.Should().Be(esperado);
    }

    [Theory]
    [InlineData("doce")]
    [InlineData("12 euros")]
    [InlineData("--3")]
    public void Lo_que_no_es_numero_se_rechaza(string entrada)
    {
        ValidadorDeValor.Validar(Campo(TipoDeCampo.Numero), entrada).EsValido.Should().BeFalse();
    }

    [Fact]
    public void La_fecha_se_guarda_en_ISO()
    {
        var resultado = ValidadorDeValor.Validar(Campo(TipoDeCampo.Fecha), "2026-03-04");

        resultado.EsValido.Should().BeTrue();
        resultado.ValorCanonico.Should().Be("2026-03-04");
    }

    [Theory]
    [InlineData("2026-13-01")]  // mes 13
    [InlineData("2026-02-30")]  // día que no existe
    [InlineData("ayer")]
    public void Una_fecha_imposible_se_rechaza(string entrada)
    {
        ValidadorDeValor.Validar(Campo(TipoDeCampo.Fecha), entrada).EsValido.Should().BeFalse();
    }

    [Fact]
    public void La_seleccion_sólo_admite_una_de_sus_opciones()
    {
        var campo = Campo(TipoDeCampo.Seleccion, false, "Alta", "Media", "Baja");

        ValidadorDeValor.Validar(campo, "Media").EsValido.Should().BeTrue();

        var invalida = ValidadorDeValor.Validar(campo, "Altísima");
        invalida.EsValido.Should().BeFalse();
        invalida.Error.Should().Contain("Altísima", "el mensaje tiene que decir qué valor sobra");
    }

    [Fact]
    public void La_seleccion_multiple_se_guarda_en_el_orden_de_la_definicion()
    {
        // Así dos entidades con la misma selección tienen el mismo valor guardado y se pueden
        // comparar y agrupar; si se guardara en el orden en que se marcó, no.
        var campo = Campo(TipoDeCampo.SeleccionMultiple, false, "Rojo", "Verde", "Azul");

        var resultado = ValidadorDeValor.Validar(campo, "Azul\nRojo");

        resultado.EsValido.Should().BeTrue();
        resultado.ValorCanonico.Should().Be("Rojo\nAzul");
    }

    [Fact]
    public void La_seleccion_multiple_no_duplica()
    {
        var campo = Campo(TipoDeCampo.SeleccionMultiple, false, "Rojo", "Verde");

        ValidadorDeValor.Validar(campo, "Rojo\nRojo").ValorCanonico.Should().Be("Rojo");
    }

    [Fact]
    public void La_seleccion_multiple_rechaza_una_opcion_que_no_existe()
    {
        var campo = Campo(TipoDeCampo.SeleccionMultiple, false, "Rojo", "Verde");

        ValidadorDeValor.Validar(campo, "Rojo\nMorado").EsValido.Should().BeFalse();
    }

    [Fact]
    public void El_campo_de_usuario_exige_un_identificador_de_verdad()
    {
        var campo = Campo(TipoDeCampo.Usuario);
        var alguien = Guid.NewGuid();

        ValidadorDeValor.Validar(campo, alguien.ToString()).ValorCanonico.Should().Be(alguien.ToString());
        ValidadorDeValor.Validar(campo, "Fernando").EsValido.Should().BeFalse();
        ValidadorDeValor.Validar(campo, Guid.Empty.ToString()).EsValido.Should().BeFalse(
            "el Guid vacío no es una persona");
    }

    [Fact]
    public void Un_campo_de_seleccion_no_se_puede_definir_sin_opciones()
    {
        var crear = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "Estado", TipoDeCampo.Seleccion, TipoDeEntidad.Tarea, false, null, 0);

        crear.Should().Throw<InvalidOperationException>().WithMessage("*al menos una opción*");
    }

    [Fact]
    public void Las_opciones_repetidas_o_vacias_se_limpian_al_definir()
    {
        var campo = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Estado", TipoDeCampo.Seleccion, TipoDeEntidad.Tarea, false,
            ["Alta", "  ", "Alta", " Baja "], 0);

        campo.Opciones.Should().Equal("Alta", "Baja");
    }

    [Fact]
    public void Un_campo_que_no_es_de_seleccion_no_guarda_opciones()
    {
        var campo = CustomFieldDefinition.Create(
            Guid.NewGuid(), "Notas", TipoDeCampo.Texto, TipoDeEntidad.Tarea, false, ["sobra"], 0);

        campo.Opciones.Should().BeEmpty();
    }

    [Fact]
    public void El_tipo_y_la_entidad_se_validan_al_definir()
    {
        var tipoRaro = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "X", "Formula", TipoDeEntidad.Tarea, false, null, 0);
        var entidadRara = () => CustomFieldDefinition.Create(
            Guid.NewGuid(), "X", TipoDeCampo.Texto, "Factura", false, null, 0);

        tipoRaro.Should().Throw<InvalidOperationException>().WithMessage("*tipo de campo no existe*");
        entidadRara.Should().Throw<InvalidOperationException>().WithMessage("*Tarea o Proyecto*");
    }

    [Fact]
    public void La_formula_no_esta_entre_los_tipos()
    {
        // Queda fuera a propósito: un campo calculado necesita un motor de expresiones, y
        // ofrecerlo a medias sería un tipo que se puede elegir y no calcula nada.
        TipoDeCampo.Todos().Should().NotContain("Formula");
    }
}
