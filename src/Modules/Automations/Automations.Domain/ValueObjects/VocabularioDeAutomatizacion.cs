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

    /// <summary>
    /// Se revisa una vez al día: para cada tarea sin terminar, cuántos días faltan para su
    /// vencimiento.
    ///
    /// Es el disparador que faltaba y el que más se usa en un producto de este tipo: casi toda
    /// automatización real es «esto va a llegar tarde, que alguien se entere». Los otros tres
    /// reaccionan a algo que alguien hizo; éste reacciona a que **no** ha pasado nada, que es
    /// justo lo que no se nota solo.
    ///
    /// A diferencia de los demás no lo dispara un evento sino un trabajo en segundo plano, y eso
    /// trae un problema que los otros no tienen: volvería a saltar cada día sobre la misma
    /// tarea. Lo resuelve el registro de ejecuciones, que le hace de memoria.
    /// </summary>
    public const string TareaPorVencer = "TareaPorVencer";

    public static IReadOnlyList<string> Todos() =>
        [TareaCreada, TareaCambiaDeEstado, TareaCambiaDePrioridad, TareaPorVencer];

    public static bool Existe(string tipo) => Todos().Contains(tipo);

    /// <summary>
    /// Los que revisa el trabajo diario en vez de un evento. Sólo éstos necesitan protegerse de
    /// repetirse, porque son los únicos que se evalúan una y otra vez sobre la misma tarea.
    /// </summary>
    public static bool EsPorTiempo(string tipo) => tipo == TareaPorVencer;
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

    /// <summary>
    /// Cuántos días faltan para el vencimiento. Negativo si ya venció, 0 si vence hoy.
    ///
    /// Es el primer campo numérico, y lo trae sólo el disparador por tiempo. Se expresa en días
    /// y no como fecha absoluta a propósito: quien escribe la regla piensa en «avísame dos días
    /// antes», no en «el 14 de marzo».
    /// </summary>
    public const string DiasParaVencer = "DiasParaVencer";

    /// <summary>El título. Útil con «Contiene» para reglas por convención de nombre.</summary>
    public const string Titulo = "Titulo";

    public static IReadOnlyList<string> Todos() =>
        [Estado, EstadoAnterior, Prioridad, PrioridadAnterior, ProyectoId, ResponsableId,
         DiasParaVencer, Titulo];

    public static bool Existe(string campo) => Todos().Contains(campo);

    /// <summary>
    /// Los que se comparan como número y no como texto.
    ///
    /// Importa más de lo que parece: sobre texto, «-1» es mayor que «10» porque se compara
    /// carácter a carácter, y una regla de «lleva más de diez días de retraso» no saltaría nunca
    /// sin dar ningún error.
    /// </summary>
    public static bool EsNumerico(string campo) => campo == DiasParaVencer;
}

/// <summary>
/// Cómo se compara.
///
/// Aquí decía que no había comparaciones numéricas «porque todos los campos que hoy expone un
/// evento son identificadores o etiquetas», y que se añadirían cuando hubiera un campo numérico.
/// Ya lo hay: <see cref="CampoDelEvento.DiasParaVencer"/>.
///
/// **Sólo comparan como número los campos numéricos.** Sobre el resto se rechaza al guardar la
/// regla, en lugar de caer en la comparación alfabética que era lo que había que evitar.
/// </summary>
public static class Operador
{
    public const string Igual = "Igual";
    public const string Distinto = "Distinto";
    public const string Contiene = "Contiene";
    public const string EstaVacio = "EstaVacio";
    public const string MenorOIgual = "MenorOIgual";
    public const string MayorOIgual = "MayorOIgual";

    public static IReadOnlyList<string> Todos() =>
        [Igual, Distinto, Contiene, EstaVacio, MenorOIgual, MayorOIgual];

    public static bool Existe(string operador) => Todos().Contains(operador);

    /// <summary>El único que no necesita valor de comparación.</summary>
    public static bool NecesitaValor(string operador) => operador != EstaVacio;

    /// <summary>Los que exigen un campo numérico enfrente.</summary>
    public static bool EsNumerico(string operador) => operador is MenorOIgual or MayorOIgual;
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

    /// <summary>
    /// Avisar a una persona. El valor es su identificador, o
    /// <see cref="DestinatarioResponsable"/> para quien tenga la tarea asignada.
    ///
    /// Es la acción que faltaba, y la que hace útiles a las demás: hasta ahora una regla podía
    /// cambiar datos pero no contárselo a nadie, y buena parte de lo que se automatiza en un
    /// producto de este tipo es precisamente avisar.
    ///
    /// **Respeta las preferencias de quien recibe.** Quien apagó un tipo de aviso no empieza a
    /// recibirlo porque alguien configure una automatización: automatizar no es un permiso para
    /// saltarse lo que la persona ya decidió.
    /// </summary>
    public const string Notificar = "Notificar";

    /// <summary>
    /// Valor especial de <see cref="Notificar"/>: avisa a quien tenga la tarea.
    ///
    /// Sin esto habría que escribir el identificador de una persona concreta en la regla, y la
    /// regla dejaría de valer en cuanto la tarea cambiara de manos — que es justo cuando más
    /// falta hace el aviso.
    /// </summary>
    public const string DestinatarioResponsable = "Responsable";

    public static IReadOnlyList<string> Todos() =>
        [CambiarEstado, CambiarPrioridad, AsignarA, Notificar];

    public static bool Existe(string tipo) => Todos().Contains(tipo);
}
