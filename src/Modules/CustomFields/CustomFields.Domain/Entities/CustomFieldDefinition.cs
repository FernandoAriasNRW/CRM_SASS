using BuildingBlocks.Domain.Primitives;
using CustomFields.Domain.Events;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Domain.Entities;

/// <summary>
/// La definición de un campo personalizado: qué se pregunta, de qué tipo y sobre qué entidad.
///
/// La definición y el valor son cosas distintas y viven separadas. Un cliente define «Cliente
/// facturable» una vez y luego hay miles de tareas con su valor; meterlo todo en la misma tabla
/// obligaría a repetir el nombre y el tipo en cada fila, y renombrar el campo sería un UPDATE
/// masivo en lugar de tocar una fila.
/// </summary>
public sealed class CustomFieldDefinition : AggregateRoot, ITenantEntity
{
    public const int LargoMaximoDelNombre = 80;
    public const int MaximoDeOpciones = 50;

    public Guid TenantId { get; private set; }

    /// <summary>Lo que ve quien rellena el campo.</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Uno de <see cref="TipoDeCampo"/>.</summary>
    public string Tipo { get; private set; } = string.Empty;

    /// <summary>Sobre qué entidad aplica: tarea o proyecto.</summary>
    public string EntidadDestino { get; private set; } = string.Empty;

    /// <summary>Si hay que rellenarlo para guardar la entidad.</summary>
    public bool Obligatorio { get; private set; }

    /// <summary>Opciones de los tipos de selección, en el orden en que se muestran.</summary>
    public List<string> Opciones { get; private set; } = [];

    /// <summary>Orden en que aparece el campo en el formulario.</summary>
    public int Posicion { get; private set; }

    private CustomFieldDefinition() { }

    public static CustomFieldDefinition Create(
        Guid tenantId, string nombre, string tipo, string entidadDestino,
        bool obligatorio, IEnumerable<string>? opciones, int posicion)
    {
        var nombreLimpio = (nombre ?? string.Empty).Trim();

        if (nombreLimpio.Length == 0)
            throw new InvalidOperationException(Reglas.NombreObligatorio);

        if (nombreLimpio.Length > LargoMaximoDelNombre)
            throw new InvalidOperationException(Reglas.NombreDemasiadoLargo);

        if (!TipoDeCampo.Existe(tipo))
            throw new InvalidOperationException(Reglas.TipoDesconocido);

        if (!TipoDeEntidad.Existe(entidadDestino))
            throw new InvalidOperationException(Reglas.EntidadDesconocida);

        var listaDeOpciones = NormalizarOpciones(tipo, opciones);

        var definicion = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Nombre = nombreLimpio,
            Tipo = tipo,
            EntidadDestino = entidadDestino,
            Obligatorio = obligatorio,
            Opciones = listaDeOpciones,
            Posicion = posicion
        };

        definicion.RaiseDomainEvent(new CustomFieldDefinedEvent(definicion.Id, tenantId, nombreLimpio, tipo, entidadDestino));

        return definicion;
    }

    /// <summary>
    /// Cambia el nombre, la obligatoriedad y las opciones.
    ///
    /// **El tipo y la entidad no se pueden cambiar.** Pasar un campo de texto a número dejaría
    /// todos los valores ya guardados sin validez, y cambiar la entidad dejaría huérfanos los de
    /// la anterior. Para eso se borra el campo y se crea otro, que además deja claro que los
    /// datos viejos se pierden.
    /// </summary>
    public void Actualizar(string nombre, bool obligatorio, IEnumerable<string>? opciones, int posicion)
    {
        var nombreLimpio = (nombre ?? string.Empty).Trim();

        if (nombreLimpio.Length == 0)
            throw new InvalidOperationException(Reglas.NombreObligatorio);

        if (nombreLimpio.Length > LargoMaximoDelNombre)
            throw new InvalidOperationException(Reglas.NombreDemasiadoLargo);

        Nombre = nombreLimpio;
        Obligatorio = obligatorio;
        Opciones = NormalizarOpciones(Tipo, opciones);
        Posicion = posicion;

        RaiseDomainEvent(new CustomFieldUpdatedEvent(Id, TenantId, Nombre));
    }

    private static List<string> NormalizarOpciones(string tipo, IEnumerable<string>? opciones)
    {
        if (!TipoDeCampo.UsaOpciones(tipo))
            return [];

        var lista = (opciones ?? [])
            .Select(o => (o ?? string.Empty).Trim())
            .Where(o => o.Length > 0)
            .Distinct()
            .ToList();

        if (lista.Count == 0)
            throw new InvalidOperationException(Reglas.SinOpciones);

        if (lista.Count > MaximoDeOpciones)
            throw new InvalidOperationException(Reglas.DemasiadasOpciones);

        return lista;
    }

    public static class Reglas
    {
        public const string NombreObligatorio = "El campo necesita un nombre";
        public static readonly string NombreDemasiadoLargo =
            $"El nombre del campo no puede pasar de {LargoMaximoDelNombre} caracteres";
        public const string TipoDesconocido = "El tipo de campo no existe";
        public const string EntidadDesconocida = "El campo sólo puede aplicarse a Tarea o Proyecto";
        public const string SinOpciones = "Un campo de selección necesita al menos una opción";
        public static readonly string DemasiadasOpciones =
            $"Un campo de selección no puede tener más de {MaximoDeOpciones} opciones";
        public const string NombreRepetido = "Ya hay un campo con ese nombre para esa entidad";
    }
}
