using CustomFields.Domain.Services;
using FluentAssertions;
using Xunit;
using static CustomFields.Domain.Services.FormulaParser;

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
    private static Node TreeOf(string formula)
    {
        var r = Parse(formula);
        r.IsValid.Should().BeTrue($"«{formula}» debería analizarse. Error: {r.Error}");
        return r.Tree!;
    }

    private static FormulaEvaluator.EvaluationResult EvaluateFormula(string formula, Dictionary<string, decimal?>? fields = null)
    {
        var values = new Dictionary<string, decimal?>(fields ?? [], StringComparer.OrdinalIgnoreCase);

        return FormulaEvaluator.Evaluate(TreeOf(formula), name =>
            values.TryGetValue(name, out var v) ? v : throw new FormulaEvaluator.UnknownFieldException(name));
    }

    private static decimal? ValueOf(string formula, Dictionary<string, decimal?>? fields = null)
        => EvaluateFormula(formula, fields).Value;

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
    public void Basic_arithmetic_works(string formula, decimal expected)
    {
        ValueOf(formula).Should().Be(expected);
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
    public void Precedence_and_associativity_are_the_usual_ones(string formula, decimal expected)
    {
        ValueOf(formula).Should().Be(expected);
    }

    #endregion

    #region Referencias a campos

    [Fact]
    public void A_formula_uses_another_field_value()
    {
        var fields = new Dictionary<string, decimal?> { ["Horas"] = 8, ["Precio"] = 50 };

        ValueOf("[Horas] * [Precio]", fields).Should().Be(400);
    }

    /// <summary>
    /// El nombre no distingue mayúsculas: quien escribe `[horas]` se refiere a «Horas». Que una
    /// fórmula funcionara o no según cómo se teclee una mayúscula sería una crueldad.
    /// </summary>
    [Fact]
    public void Field_names_ignore_case()
    {
        var fields = new Dictionary<string, decimal?> { ["Horas Estimadas"] = 10 };

        ValueOf("[horas estimadas] * 2", fields).Should().Be(20);
    }

    [Fact]
    public void References_are_extracted_from_the_tree_without_repeats()
    {
        var references = ReferencesOf(TreeOf("[A] + [B] * [A] - [C]"));

        references.Should().BeEquivalentTo(["A", "B", "C"]);
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
    public void An_unfilled_field_leaves_the_result_blank()
    {
        var fields = new Dictionary<string, decimal?> { ["Horas"] = null, ["Precio"] = 50 };

        var result = EvaluateFormula("[Horas] * [Precio]", fields);

        result.HasValue.Should().BeFalse();
        result.NoData.Should().BeTrue();
        result.IsError.Should().BeFalse("faltar un dato no es un error de la fórmula");
    }

    [Fact]
    public void A_blank_propagates_through_the_whole_expression()
    {
        var fields = new Dictionary<string, decimal?> { ["A"] = 1, ["B"] = null };

        EvaluateFormula("([A] + 1) * 2 + [B] * 100", fields).NoData.Should().BeTrue();
    }

    /// <summary>
    /// La excepción, y la que hace el mecanismo usable: SI sólo evalúa la rama que toma. Sin
    /// esto no habría forma de escribir una fórmula que tolere campos en blanco.
    /// </summary>
    [Fact]
    public void IF_does_not_evaluate_the_branch_it_skips()
    {
        var fields = new Dictionary<string, decimal?> { ["Horas"] = 0, ["Coste"] = null };

        // La rama del entonces dividiría por cero y usaría un campo vacío. No se evalúa.
        ValueOf("SI([Horas] > 0; [Coste] / [Horas]; 0)", fields).Should().Be(0);
    }

    #endregion

    #region Errores de verdad, distintos de los huecos

    /// <summary>
    /// Dividir entre cero es un error, no un hueco. La diferencia importa: el hueco dice «falta
    /// un dato» y aquí los datos están, lo que no se puede es hacer la cuenta. Quien lo vea
    /// tiene que arreglar la fórmula, no rellenar un campo.
    /// </summary>
    [Fact]
    public void Dividing_by_zero_is_an_error_not_a_blank()
    {
        var result = EvaluateFormula("10 / 0");

        result.IsError.Should().BeTrue();
        result.NoData.Should().BeFalse();
        result.Error.Should().Be(FormulaEvaluator.Errors.DivisionByZero);
    }

    [Fact]
    public void Referring_to_an_unknown_field_is_an_error()
    {
        var result = EvaluateFormula("[Campo Fantasma] + 1", new Dictionary<string, decimal?>());

        result.IsError.Should().BeTrue();
        result.Error.Should().Contain("Campo Fantasma");
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
    public void Functions_do_what_they_say(string formula, decimal expected)
    {
        ValueOf(formula).Should().Be(expected);
    }

    [Theory]
    [InlineData("1 = 1", 1)]
    [InlineData("1 = 2", 0)]
    [InlineData("1 <> 2", 1)]
    [InlineData("2 >= 2", 1)]
    [InlineData("1 <= 0", 0)]
    public void Comparisons_return_one_or_zero(string formula, decimal expected)
    {
        ValueOf(formula).Should().Be(expected);
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
    public void A_malformed_formula_is_rejected_when_saved(string formula)
    {
        var result = Parse(formula);

        result.IsValid.Should().BeFalse($"«{formula}» no debería aceptarse");
        result.Error.Should().NotBeNullOrWhiteSpace("el mensaje es lo único que tiene quien la escribió para arreglarla");
    }

    /// <summary>
    /// Un tope al tamaño. Sin él, una fórmula escrita para hacer daño —o pegada por error—
    /// consume la petición entera analizándola.
    /// </summary>
    [Fact]
    public void An_oversized_formula_is_rejected()
    {
        var tooLong = string.Join(" + ", Enumerable.Repeat("1", 400));

        Parse(tooLong).IsValid.Should().BeFalse();
    }

    #endregion

    #region Ciclos

    private static FormulaCycleDetector.Dependency Dep(string field, params string[] references)
        => new(field, references);

    /// <summary>El ciclo más corto y el más fácil de escribir sin querer.</summary>
    [Fact]
    public void A_field_referencing_itself_is_a_cycle()
    {
        FormulaCycleDetector.WouldCreateCycle([], "Total", ["Total"]).Should().BeTrue();
    }

    [Fact]
    public void An_indirect_cycle_is_also_detected()
    {
        // B depende de C, C depende de A. Dar a A una referencia a B cierra A → B → C → A.
        var existingFields = new[] { Dep("B", "C"), Dep("C", "A") };

        FormulaCycleDetector.WouldCreateCycle(existingFields, "A", ["B"]).Should().BeTrue();
    }

    [Fact]
    public void A_chain_without_cycle_is_accepted()
    {
        var existingFields = new[] { Dep("B", "C"), Dep("C", "Horas") };

        FormulaCycleDetector.WouldCreateCycle(existingFields, "A", ["B"]).Should().BeFalse();
    }

    /// <summary>
    /// Dos caminos hasta el mismo campo no son un ciclo, aunque el recorrido pase dos veces por
    /// él. Confundirlo prohibiría fórmulas perfectamente legales, y eso se nota enseguida.
    /// </summary>
    [Fact]
    public void A_diamond_is_not_a_cycle()
    {
        var existingFields = new[] { Dep("B", "D"), Dep("C", "D") };

        FormulaCycleDetector.WouldCreateCycle(existingFields, "A", ["B", "C"]).Should().BeFalse();
    }

    [Fact]
    public void Computation_order_puts_each_field_after_the_ones_it_uses()
    {
        // A usa B, B usa C. Hay que calcular C, luego B, luego A.
        var order = FormulaCycleDetector.ComputationOrder([Dep("A", "B"), Dep("B", "C")]);

        order.Should().NotBeNull();
        order!.Should().ContainInOrder("B", "A");
    }

    [Fact]
    public void There_is_no_computation_order_with_a_cycle()
    {
        FormulaCycleDetector.ComputationOrder([Dep("A", "B"), Dep("B", "A")]).Should().BeNull();
    }

    #endregion
}
