using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Domain;

namespace Reporting.Domain.Dashboards;

/// <summary>
/// Los widgets de un panel, con su colocación.
///
/// Se guarda serializada en el propio panel porque siempre se lee entera —para pintar el panel
/// hacen falta todos los widgets— y nunca se consulta por partes. Una tabla de widgets sería una
/// unión más en cada carga sin ganar nada.
/// </summary>
public sealed record DashboardLayout(IReadOnlyList<Widget>? Widgets = null)
{
    /// <summary>
    /// Cuántos widgets caben en un panel.
    ///
    /// Cada uno es una consulta al abrir la pantalla. Sin tope, un panel de cien recuadros tarda
    /// medio minuto en cargar y quien lo montó no relaciona una cosa con la otra.
    /// </summary>
    public const int MaxWidgets = 24;

    /// <summary>
    /// Los widgets, nunca nulos.
    ///
    /// Calculada y no con inicializador: con <c>{ get; } = Widgets ?? []</c>, el <c>with</c> de
    /// los records copia el campo de respaldo del original en vez de recalcularlo. Ya mordió una
    /// vez en la definición de informes, donde dejó los filtros sin validar ni aplicar.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Widget> Placed => Widgets ?? [];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Result Validate()
    {
        if (Placed.Count > MaxWidgets)
            return Result.Failure($"Un panel admite {MaxWidgets} recuadros como mucho");

        // Dos widgets con el mismo identificador harían que mover uno moviera los dos, y que
        // borrar uno borrara el otro. Es el tipo de fallo que se achaca al navegador.
        var duplicates = Placed.GroupBy(w => w.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            return Result.Failure($"Hay widgets repetidos en el panel: {string.Join(", ", duplicates)}");

        foreach (var widget in Placed)
        {
            var result = widget.Validate();
            if (result.IsFailure) return result;
        }

        return Result.Success();
    }

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Lee una disposición guardada.
    ///
    /// Devuelve una vacía —no nula, ni lanza— si el JSON no es una disposición: un panel con datos
    /// viejos se abre vacío y se puede volver a montar, en vez de reventar la pantalla entera.
    /// </summary>
    public static DashboardLayout Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DashboardLayout();

        try
        {
            return JsonSerializer.Deserialize<DashboardLayout>(json, Json) ?? new DashboardLayout();
        }
        catch (JsonException)
        {
            return new DashboardLayout();
        }
    }
}
