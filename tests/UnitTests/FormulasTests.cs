using CustomFields.Domain.Servicios;
using FluentAssertions;
using Xunit;
using static CustomFields.Domain.Servicios.AnalizadorDeFormula;

namespace UnitTests;

/// <summary>
/// El motor de fórmulas de los campos calculados.
///
/// Se prueba a fondo y sin base de datos porque es exactamente el tipo de código donde un fallo
/// no da error: devuelve un número, y nadie sabe que es el número equivocado hasta que alguien
/// factura con él.
/// </summary>
public sealed class FormulasTests
{
    private static Nodo Arbol(string formula)
    {
        var r = Analizar(formula);
        r.EsValida.Should().BeTrue($"«{formula}» debería analizarse. Error: {r.Error}");
        return r.Arbol!;
    }

    private static EvaluadorDeFormula.Resultado Evaluar(string formula, Dictionary<string, decimal?>? campos = null)
    {
        var valores = new Dictionary<string, decimal?>(campos ?? [], StringComparer.OrdinalIgnoreCase);

        return EvaluadorDeFormula.Evaluar(Arbol(formula), nombre =>
            valores.TryGetValue(nombre, out var v) ? v : throw new EvaluadorDeFormula.CampoDesconocidoException(nombre));
    }

    private static decimal? ValorDe(string formula, Dictionary<string, decimal?>? campos = null)
        => Evaluar(formula, campos).Valor;

    #region Aritmética

    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("10 - 4", 6)]
    [InlineData("6 * 7", 42)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("-5", -5)]
    [InlineData("--5", 5)]
    [InlineData("1,5 + 1,5", 3)]        // coma decimal, como se escribe en español
    [InlineData("1.5 + 1.5", 3)]        // y punto, como lo guarda el validador
    public void Las_cuentas_basicas_salen(string formula, decimal esperado)
    {
        ValorDe(formula).Should().Be(esperado);
    }

    /// <summary>
    /// La precedencia. Si se equivoca, `2 + 3 * 4` da 20 en lugar de 14 — un número plausible,
    /// que es lo que lo hace peligroso: nadie mira dos veces un total que parece razonable.
    /// </summary>
    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("2 * 3 + 4 * 5", 26)]
    [InlineData("100 / 10 / 2", 5)]     // asociativa por la izquierda: (100/10)/2, no 100/(10/2)
    [InlineData("10 - 3 - 2", 5)]       // igual: (10-3)-2 = 5, no 10-(3-2) = 9
    [InlineData("-2 + 3", 1)]
    [InlineData("-(2 + 3)", -5)]
    public void La_precedencia_y_la_asociatividad_son_las_de_siempre(string formula, decimal esperado)
    {
        ValorDe(formula).Should().Be(esperado);
    }

    #endregion

    #region Referencias a campos

    [Fact]
    public void Una_formula_usa_el_valor_de_otro_campo()
    {
        var campos = new Dictionary<string, decimal?> { ["Horas"] = 8, ["Precio"] = 50 };

        ValorDe("[Horas] * [Precio]", campos).Should().Be(400);
    }

    /// <summary>
    /// El nombre no distingue mayúsculas: quien escribe `[horas]` se refiere a «Horas». Que una
    /// fórmula funcionara o no según cómo se teclee una mayúscula sería una crueldad.
    /// </summary>
    [Fact]
    public void El_nombre_del_campo_no_distingue_mayusculas()
    {
        var campos = new Dictionary<string, decimal?> { ["Horas Estimadas"] = 10 };

        ValorDe("[horas estimadas] * 2", campos).Should().Be(20);
    }

    [Fact]
    public void Las_referencias_se_extraen_del_arbol_sin_repetir()
    {
        var referencias = ReferenciasDe(Arbol("[A] + [B] * [A] - [C]"));

        referencias.Should().BeEquivalentTo(["A", "B", "C"]);
    }

    #endregion

    #region El hueco: la decisión que más se nota

    /// <summary>
    /// Un campo sin rellenar hace que el resultado no exista, en vez de valer cero.
    ///
    /// Va a contracorriente de una hoja de cálculo y es deliberado: un total que dice «1.200 €»
    /// con la mitad de sus sumandos en blanco se lee como un dato y se usa para decidir. Uno que
    /// no dice nada se ve que falta rellenarlo.
    /// </summary>
    [Fact]
    public void Un_campo_sin_rellenar_deja_el_resultado_sin_dato()
    {
        var campos = new Dictionary<string, decimal?> { ["Horas"] = null, ["Precio"] = 50 };

        var resultado = Evaluar("[Horas] * [Precio]", campos);

        resultado.HayValor.Should().BeFalse();
        resultado.SinDato.Should().BeTrue();
        resultado.EsError.Should().BeFalse("faltar un dato no es un error de la fórmula");
    }

    [Fact]
    public void El_hueco_se_propaga_por_toda_la_expresion()
    {
        var campos = new Dictionary<string, decimal?> { ["A"] = 1, ["B"] = null };

        Evaluar("([A] + 1) * 2 + [B] * 100", campos).SinDato.Should().BeTrue();
    }

    /// <summary>
    /// La excepción, y la que hace el mecanismo usable: SI sólo evalúa la rama que toma. Sin
    /// esto no habría forma de escribir una fórmula que tolere campos en blanco.
    /// </summary>
    [Fact]
    public void SI_no_mira_la_rama_que_no_toma()
    {
        var campos = new Dictionary<string, decimal?> { ["Horas"] = 0, ["Coste"] = null };

        // La rama del entonces dividiría por cero y usaría un campo vacío. No se evalúa.
        ValorDe("SI([Horas] > 0; [Coste] / [Horas]; 0)", campos).Should().Be(0);
    }

    #endregion

    #region Errores de verdad, distintos de los huecos

    /// <summary>
    /// Dividir entre cero es un error, no un hueco. La diferencia importa: el hueco dice «falta
    /// un dato» y aquí los datos están, lo que no se puede es hacer la cuenta. Quien lo vea
    /// tiene que arreglar la fórmula, no rellenar un campo.
    /// </summary>
    [Fact]
    public void Dividir_entre_cero_es_un_error_y_no_un_hueco()
    {
        var resultado = Evaluar("10 / 0");

        resultado.EsError.Should().BeTrue();
        resultado.SinDato.Should().BeFalse();
        resultado.Error.Should().Be(EvaluadorDeFormula.Errores.DivisionPorCero);
    }

    [Fact]
    public void Referirse_a_un_campo_que_no_existe_es_un_error()
    {
        var resultado = Evaluar("[Campo Fantasma] + 1", new Dictionary<string, decimal?>());

        resultado.EsError.Should().BeTrue();
        resultado.Error.Should().Contain("Campo Fantasma");
    }

    #endregion

    #region Funciones

    [Theory]
    [InlineData("SI(1 > 0; 10; 20)", 10)]
    [InlineData("SI(1 < 0; 10; 20)", 20)]
    [InlineData("SI(0; 10; 20)", 20)]        // cero es falso
    [InlineData("SI(-3; 10; 20)", 10)]       // cualquier cosa distinta de cero es verdadero
    [InlineData("ABS(-7)", 7)]
    [InlineData("MIN(3; 1; 2)", 1)]
    [InlineData("MAX(3; 1; 2)", 3)]
    [InlineData("REDONDEAR(2,555; 2)", 2.56)]
    [InlineData("REDONDEAR(2,5; 0)", 3)]     // el medio se aleja del cero, no al par
    [InlineData("REDONDEAR(-2,5; 0)", -3)]
    public void Las_funciones_hacen_lo_que_dicen(string formula, decimal esperado)
    {
        ValorDe(formula).Should().Be(esperado);
    }

    [Theory]
    [InlineData("1 = 1", 1)]
    [InlineData("1 = 2", 0)]
    [InlineData("1 <> 2", 1)]
    [InlineData("2 >= 2", 1)]
    [InlineData("1 <= 0", 0)]
    public void Las_comparaciones_dan_uno_o_cero(string formula, decimal esperado)
    {
        ValorDe(formula).Should().Be(esperado);
    }

    #endregion

    #region Lo que se rechaza al escribirla

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2 +")]
    [InlineData("* 3")]
    [InlineData("(2 + 3")]
    [InlineData("2 + 3)")]
    [InlineData("[Sin cerrar")]
    [InlineData("[]")]
    [InlineData("2 $ 3")]
    [InlineData("1,2,3")]
    [InlineData("RAIZ(4)")]              // función que no existe
    [InlineData("ABS(1; 2)")]            // sobran argumentos
    [InlineData("SI(1; 2)")]             // faltan
    [InlineData("MIN(1)")]               // MIN necesita al menos dos
    [InlineData("1 < 2 < 3")]            // comparaciones encadenadas
    public void Una_formula_mal_escrita_se_rechaza_al_guardarla(string formula)
    {
        var resultado = Analizar(formula);

        resultado.EsValida.Should().BeFalse($"«{formula}» no debería aceptarse");
        resultado.Error.Should().NotBeNullOrWhiteSpace("el mensaje es lo único que tiene quien la escribió para arreglarla");
    }

    /// <summary>
    /// Un tope al tamaño. Sin él, una fórmula escrita para hacer daño —o pegada por error—
    /// consume la petición entera analizándola.
    /// </summary>
    [Fact]
    public void Una_formula_desmesurada_se_rechaza()
    {
        var larga = string.Join(" + ", Enumerable.Repeat("1", 400));

        Analizar(larga).EsValida.Should().BeFalse();
    }

    #endregion

    #region Ciclos

    private static DetectorDeCiclosDeFormula.Dependencia Dep(string campo, params string[] referencias)
        => new(campo, referencias);

    /// <summary>El ciclo más corto y el más fácil de escribir sin querer.</summary>
    [Fact]
    public void Un_campo_que_se_referencia_a_si_mismo_es_un_ciclo()
    {
        DetectorDeCiclosDeFormula.CerrariaUnCiclo([], "Total", ["Total"]).Should().BeTrue();
    }

    [Fact]
    public void Un_ciclo_indirecto_tambien_se_detecta()
    {
        // B depende de C, C depende de A. Dar a A una referencia a B cierra A → B → C → A.
        var existentes = new[] { Dep("B", "C"), Dep("C", "A") };

        DetectorDeCiclosDeFormula.CerrariaUnCiclo(existentes, "A", ["B"]).Should().BeTrue();
    }

    [Fact]
    public void Una_cadena_sin_ciclo_se_acepta()
    {
        var existentes = new[] { Dep("B", "C"), Dep("C", "Horas") };

        DetectorDeCiclosDeFormula.CerrariaUnCiclo(existentes, "A", ["B"]).Should().BeFalse();
    }

    /// <summary>
    /// Dos caminos hasta el mismo campo no son un ciclo, aunque el recorrido pase dos veces por
    /// él. Confundirlo prohibiría fórmulas perfectamente legales, y eso se nota enseguida.
    /// </summary>
    [Fact]
    public void Un_rombo_no_es_un_ciclo()
    {
        var existentes = new[] { Dep("B", "D"), Dep("C", "D") };

        DetectorDeCiclosDeFormula.CerrariaUnCiclo(existentes, "A", ["B", "C"]).Should().BeFalse();
    }

    [Fact]
    public void El_orden_de_calculo_pone_cada_campo_despues_de_los_que_usa()
    {
        // A usa B, B usa C. Hay que calcular C, luego B, luego A.
        var orden = DetectorDeCiclosDeFormula.OrdenDeCalculo([Dep("A", "B"), Dep("B", "C")]);

        orden.Should().NotBeNull();
        orden!.Should().ContainInOrder("B", "A");
    }

    [Fact]
    public void No_hay_orden_de_calculo_si_hay_ciclo()
    {
        DetectorDeCiclosDeFormula.OrdenDeCalculo([Dep("A", "B"), Dep("B", "A")]).Should().BeNull();
    }

    #endregion
}
