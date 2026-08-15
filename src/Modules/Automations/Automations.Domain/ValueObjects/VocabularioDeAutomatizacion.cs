namespace Automations.Domain.ValueObjects;

/// <summary>
/// Qué puede disparar una regla.
///
/// La lista es cerrada a propósito: cada disparador tiene que traducirse a un evento de dominio
/// que algún módulo emita de verdad. Ofrecer disparadores que no están conectados sería dejar
/// que alguien configure una automatización que nunca se ejecuta y no sepa por qué.
/// </summary>
public static class TipoDeDisparador
{
    public const string TareaCreada = "TareaCreada";
    public const string TareaCambiaDeEstado = "TareaCambiaDeEstado";
    public const string TareaCambiaDePrioridad = "TareaCambiaDePrioridad";

    public static IReadOnlyList<string> Todos() =>
        [TareaCreada, TareaCambiaDeEstado, TareaCambiaDePrioridad];

    public static bool Existe(string tipo) => Todos().Contains(tipo);
}

/// <summary>
/// Los datos que una condición puede mirar. Se nombran igual en todos los disparadores que los
/// tengan, para que cambiar el disparador de una regla no obligue a reescribir sus condiciones.
/// </summary>
public static class CampoDelEvento
{
    public const string Estado = "Estado";
    public const string EstadoAnterior = "EstadoAnterior";
    public const string Prioridad = "Prioridad";
    public const string PrioridadAnterior = "PrioridadAnterior";
    public const string ProyectoId = "ProyectoId";
    public const string ResponsableId = "ResponsableId";

    public static IReadOnlyList<string> Todos() =>
        [Estado, EstadoAnterior, Prioridad, PrioridadAnterior, ProyectoId, ResponsableId];

    public static bool Existe(string campo) => Todos().Contains(campo);
}

/// <summary>
/// Cómo se compara.
///
/// No hay «mayor que» ni comparaciones numéricas: todos los campos que hoy expone un evento son
/// identificadores o etiquetas, y un «mayor que» sobre texto haría comparaciones alfabéticas que
/// nadie espera. Cuando haya un campo numérico, se añade entonces.
/// </summary>
public static class Operador
{
    public const string Igual = "Igual";
    public const string Distinto = "Distinto";
    public const string Contiene = "Contiene";
    public const string EstaVacio = "EstaVacio";

    public static IReadOnlyList<string> Todos() => [Igual, Distinto, Contiene, EstaVacio];

    public static bool Existe(string operador) => Todos().Contains(operador);

    /// <summary>El único que no necesita valor de comparación.</summary>
    public static bool NecesitaValor(string operador) => operador != EstaVacio;
}

/// <summary>
/// Qué puede hacer una regla.
///
/// **Todas las acciones son sobre la propia tarea que disparó la regla.** Actuar sobre otras
/// entidades exigiría decir cuáles, y eso es un lenguaje de selección entero. Mandar correos o
/// llamar a webhooks tampoco entra: para eso ya está el módulo de webhooks, y duplicarlo aquí
/// daría dos sitios donde configurar lo mismo.
/// </summary>
public static class TipoDeAccion
{
    public const string CambiarEstado = "CambiarEstado";
    public const string CambiarPrioridad = "CambiarPrioridad";
    public const string AsignarA = "AsignarA";

    public static IReadOnlyList<string> Todos() =>
        [CambiarEstado, CambiarPrioridad, AsignarA];

    public static bool Existe(string tipo) => Todos().Contains(tipo);
}
