using BuildingBlocks.Domain.Primitives;

namespace Automations.Domain.Entities;

/// <summary>Cómo acabó una ejecución.</summary>
public static class ResultadoDeEjecucion
{
    public const string Aplicada = "Aplicada";

    /// <summary>Saltó el disparador, pero las condiciones no se cumplieron.</summary>
    public const string NoCumplioCondiciones = "NoCumplioCondiciones";

    /// <summary>Las condiciones se cumplieron y alguna acción falló.</summary>
    public const string Fallida = "Fallida";

    public static IReadOnlyList<string> Todos() => [Aplicada, NoCumplioCondiciones, Fallida];
}

/// <summary>
/// Una ejecución de una regla: cuándo, sobre qué y en qué acabó.
///
/// Sirve para dos cosas distintas, y por eso existe una sola tabla en vez de dos.
///
/// **1. Responder «por qué no funciona mi automatización».** La regla ya llevaba un contador de
/// ejecuciones, y su propio comentario decía que era «lo primero que se mira cuando alguien dice
/// que esto no funciona: separa "no salta" de "salta y hace otra cosa"». Pero un contador no
/// distingue el tercer caso, que es el más frecuente: **salta y las condiciones no se cumplen**.
/// Quien configuró la regla ve el contador a cero y concluye que el disparador está roto, cuando
/// lo que falla es una condición que él escribió. Aquí se guarda cuál fue el resultado, así que
/// la respuesta está a la vista en lugar de deducirse.
///
/// **2. Que el disparador por tiempo no se repita.** Los disparadores de evento saltan una vez
/// porque el evento ocurre una vez. El de vencimiento lo revisa un trabajo diario, así que una
/// tarea que vence en dos días **volvería a disparar mañana, y pasado**. Sin memoria, «avisar
/// dos días antes» se convierte en avisar todos los días hasta que venza, y la persona deja de
/// leer los avisos. Con esto, el trabajo diario pregunta si esa regla ya se aplicó hoy sobre esa
/// tarea y se salta las que sí.
///
/// Se anotan también las que no cumplieron condiciones, aunque para la memoria no harían falta:
/// son precisamente las que hay que poder consultar cuando alguien pregunta por qué no pasó
/// nada, y guardarlas cuesta una fila.
/// </summary>
public sealed class EjecucionDeAutomatizacion : AggregateRoot, ITenantEntity
{
    /// <summary>Lo que cabe de un mensaje de error. Un volcado entero no aporta y ocupa.</summary>
    public const int LargoMaximoDelDetalle = 500;

    public Guid TenantId { get; private set; }
    public Guid RuleId { get; private set; }

    /// <summary>La tarea sobre la que se ejecutó.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Uno de <see cref="ResultadoDeEjecucion"/>.</summary>
    public string Resultado { get; private set; } = string.Empty;

    /// <summary>
    /// El porqué, cuando lo hay: el error de la acción que falló, o qué condición no se cumplió.
    /// </summary>
    public string? Detalle { get; private set; }

    public DateTime CuandoUtc { get; private set; }

    /// <summary>
    /// El día, aparte de la marca de tiempo, y no es redundante.
    ///
    /// Es por lo que se pregunta para saber si una regla ya se aplicó hoy, y filtrar por un
    /// rango de `datetime` para eso obliga a calcular los límites del día en cada consulta y a
    /// acertar con la zona horaria. Una columna de fecha se compara con una igualdad y se indexa
    /// bien.
    /// </summary>
    public DateOnly Dia { get; private set; }

    private EjecucionDeAutomatizacion() { }

    public static EjecucionDeAutomatizacion Anotar(
        Guid tenantId, Guid ruleId, Guid entityId, string resultado, string? detalle, DateTime cuandoUtc)
    {
        if (!ResultadoDeEjecucion.Todos().Contains(resultado))
            throw new InvalidOperationException($"El resultado «{resultado}» no existe");

        var recortado = detalle is null || detalle.Length <= LargoMaximoDelDetalle
            ? detalle
            : detalle[..LargoMaximoDelDetalle];

        return new EjecucionDeAutomatizacion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleId = ruleId,
            EntityId = entityId,
            Resultado = resultado,
            Detalle = recortado,
            CuandoUtc = cuandoUtc,
            Dia = DateOnly.FromDateTime(cuandoUtc),
        };
    }
}
