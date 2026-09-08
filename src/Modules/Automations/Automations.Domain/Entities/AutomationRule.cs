using Automations.Domain.Events;
using Automations.Domain.ValueObjects;
using BuildingBlocks.Domain.Primitives;

namespace Automations.Domain.Entities;

/// <summary>Una condición: qué campo del evento se mira, cómo se compara y contra qué.</summary>
public sealed class CondicionDeAutomatizacion
{
    public Guid Id { get; private set; }
    public string Campo { get; private set; } = string.Empty;
    public string Operador { get; private set; } = string.Empty;
    public string? Valor { get; private set; }

    private CondicionDeAutomatizacion() { }

    public CondicionDeAutomatizacion(string campo, string operador, string? valor)
    {
        if (!CampoDelEvento.Existe(campo))
            throw new InvalidOperationException(AutomationRule.Reglas.CampoDesconocido);

        if (!ValueObjects.Operador.Existe(operador))
            throw new InvalidOperationException(AutomationRule.Reglas.OperadorDesconocido);

        if (ValueObjects.Operador.NecesitaValor(operador) && string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException(AutomationRule.Reglas.CondicionSinValor);

        Id = Guid.NewGuid();
        Campo = campo;
        Operador = operador;
        Valor = valor?.Trim();
    }
}

/// <summary>Una acción: qué se le hace a la tarea que disparó la regla.</summary>
public sealed class AccionDeAutomatizacion
{
    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Valor { get; private set; } = string.Empty;

    private AccionDeAutomatizacion() { }

    public AccionDeAutomatizacion(string tipo, string valor)
    {
        if (!TipoDeAccion.Existe(tipo))
            throw new InvalidOperationException(AutomationRule.Reglas.AccionDesconocida);

        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException(AutomationRule.Reglas.AccionSinValor);

        Id = Guid.NewGuid();
        Tipo = tipo;
        Valor = valor.Trim();
    }
}

/// <summary>
/// Una regla de automatización: cuando pasa X, si se cumple Y, haz Z.
///
/// **Las acciones de una regla no disparan otras reglas.** Es la decisión de fondo de este
/// módulo. Encadenarlas exige detectar ciclos —una regla que pone «En progreso» y otra que al
/// verlo lo devuelve a «Por hacer» se llamarían para siempre— y un presupuesto de profundidad, y
/// eso es un proyecto en sí mismo. Prometerlo a medias sería peor: la cascada funcionaría casi
/// siempre y un día se comería la base de datos. Sin cadenas, lo que se configura es lo que pasa.
/// </summary>
public sealed class AutomationRule : AggregateRoot, ITenantEntity
{
    public const int LargoMaximoDelNombre = 100;
    public const int MaximoDeCondiciones = 10;
    public const int MaximoDeAcciones = 5;

    public Guid TenantId { get; private set; }

    /// <summary>Lo que se lee en la lista. Es la única pista de para qué existe la regla.</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Uno de <see cref="TipoDeDisparador"/>.</summary>
    public string Disparador { get; private set; } = string.Empty;

    /// <summary>
    /// Una regla desactivada se conserva pero no se ejecuta. Es lo que permite apagar una
    /// automatización que está haciendo daño sin perder cómo estaba configurada.
    /// </summary>
    public bool Activa { get; private set; } = true;

    private readonly List<CondicionDeAutomatizacion> _condiciones = [];
    public IReadOnlyCollection<CondicionDeAutomatizacion> Condiciones => _condiciones.AsReadOnly();

    private readonly List<AccionDeAutomatizacion> _acciones = [];
    public IReadOnlyCollection<AccionDeAutomatizacion> Acciones => _acciones.AsReadOnly();

    /// <summary>
    /// Cuántas veces se ha ejecutado. Es lo primero que se mira cuando alguien dice «esta
    /// automatización no funciona»: separa «no salta» de «salta y hace otra cosa».
    /// </summary>
    public int VecesEjecutada { get; private set; }

    public DateTime? UltimaEjecucionUtc { get; private set; }

    private AutomationRule() { }

    public static AutomationRule Create(
        Guid tenantId,
        string nombre,
        string disparador,
        IEnumerable<CondicionDeAutomatizacion>? condiciones,
        IEnumerable<AccionDeAutomatizacion> acciones)
    {
        var regla = new AutomationRule { Id = Guid.NewGuid(), TenantId = tenantId };

        regla.Configurar(nombre, disparador, condiciones, acciones);
        regla.RaiseDomainEvent(new AutomationRuleDefinedEvent(regla.Id, tenantId, regla.Nombre, regla.Disparador));

        return regla;
    }

    public void Actualizar(
        string nombre,
        string disparador,
        IEnumerable<CondicionDeAutomatizacion>? condiciones,
        IEnumerable<AccionDeAutomatizacion> acciones)
    {
        Configurar(nombre, disparador, condiciones, acciones);
        RaiseDomainEvent(new AutomationRuleUpdatedEvent(Id, TenantId, Nombre));
    }

    private void Configurar(
        string nombre,
        string disparador,
        IEnumerable<CondicionDeAutomatizacion>? condiciones,
        IEnumerable<AccionDeAutomatizacion> acciones)
    {
        var nombreLimpio = (nombre ?? string.Empty).Trim();

        if (nombreLimpio.Length == 0)
            throw new InvalidOperationException(Reglas.NombreObligatorio);

        if (nombreLimpio.Length > LargoMaximoDelNombre)
            throw new InvalidOperationException(Reglas.NombreDemasiadoLargo);

        if (!TipoDeDisparador.Existe(disparador))
            throw new InvalidOperationException(Reglas.DisparadorDesconocido);

        var listaDeAcciones = (acciones ?? []).ToList();

        // Una regla sin acciones se ejecutaría entera para no hacer nada. Es un error de
        // configuración silencioso, así que no se admite.
        if (listaDeAcciones.Count == 0)
            throw new InvalidOperationException(Reglas.SinAcciones);

        if (listaDeAcciones.Count > MaximoDeAcciones)
            throw new InvalidOperationException(Reglas.DemasiadasAcciones);

        var listaDeCondiciones = (condiciones ?? []).ToList();

        if (listaDeCondiciones.Count > MaximoDeCondiciones)
            throw new InvalidOperationException(Reglas.DemasiadasCondiciones);

        Nombre = nombreLimpio;
        Disparador = disparador;

        _condiciones.Clear();
        _condiciones.AddRange(listaDeCondiciones);

        _acciones.Clear();
        _acciones.AddRange(listaDeAcciones);
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    /// <summary>Deja constancia de una ejecución. Lo llama el motor, no la interfaz.</summary>
    public void AnotarEjecucion(DateTime cuandoUtc)
    {
        VecesEjecutada++;
        UltimaEjecucionUtc = cuandoUtc;
    }

    public static class Reglas
    {
        public const string NombreObligatorio = "La automatización necesita un nombre";
        public static readonly string NombreDemasiadoLargo =
            $"El nombre no puede pasar de {LargoMaximoDelNombre} caracteres";
        public const string DisparadorDesconocido = "Ese disparador no existe";
        public const string CampoDesconocido = "Ese campo no existe en el evento";
        public const string OperadorDesconocido = "Ese operador no existe";
        public const string CondicionSinValor = "La condición necesita un valor con el que comparar";
        public const string AccionDesconocida = "Esa acción no existe";
        public const string AccionSinValor = "La acción necesita un valor";
        public const string SinAcciones = "La automatización necesita al menos una acción";
        public static readonly string DemasiadasAcciones =
            $"Una automatización no puede tener más de {MaximoDeAcciones} acciones";
        public static readonly string DemasiadasCondiciones =
            $"Una automatización no puede tener más de {MaximoDeCondiciones} condiciones";
        public const string NombreRepetido = "Ya hay una automatización con ese nombre";
    }
}
