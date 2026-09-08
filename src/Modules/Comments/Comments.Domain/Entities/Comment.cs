using BuildingBlocks.Domain.Primitives;
using Comments.Domain.Events;

namespace Comments.Domain.Entities;

/// <summary>
/// Sobre qué se puede comentar.
///
/// La lista es cerrada: un comentario colgado de un tipo que nadie pinta es un dato que no
/// vuelve a ver nadie. Se amplía cuando haya una pantalla que lo muestre.
/// </summary>
public static class TipoDeEntidadComentable
{
    public const string Tarea = "Tarea";
    public const string Ticket = "Ticket";
    public const string Proyecto = "Proyecto";

    public static IReadOnlyList<string> Todos() => [Tarea, Ticket, Proyecto];

    public static bool Existe(string tipo) => Todos().Contains(tipo);
}

/// <summary>
/// Un comentario sobre una tarea, un ticket o un proyecto.
///
/// **Un solo módulo para los tres** y no uno por entidad: comentar es la misma operación en
/// todos, con las mismas reglas, y triplicarla daría tres sitios donde arreglar el mismo fallo.
/// Es la misma decisión que en campos personalizados, que ya se resuelven así.
///
/// **Se guarda quién escribió y cuándo, y no se borra el rastro al editar.** Un comentario que
/// cambia sin decir que cambió convierte un hilo en algo que no se puede leer con confianza.
/// </summary>
public sealed class Comment : AggregateRoot, ITenantEntity
{
    public const int LargoMaximo = 5000;

    public Guid TenantId { get; private set; }

    /// <summary>Uno de <see cref="TipoDeEntidadComentable"/>.</summary>
    public string EntidadDestino { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public Guid AutorId { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public DateTime CreadoUtc { get; private set; }

    /// <summary>Cuándo se editó por última vez, o <c>null</c> si nunca se tocó.</summary>
    public DateTime? EditadoUtc { get; private set; }

    /// <summary>
    /// Comentario al que responde, si es una respuesta.
    ///
    /// **Un solo nivel**, igual que las subtareas: hay comentarios y respuestas, y no respuestas
    /// de respuestas. Es lo que permite pintar el hilo con una cuenta y no con un recorrido de
    /// árbol, y evita de raíz los hilos que se van a la derecha hasta no caber.
    /// </summary>
    public Guid? RespondeAId { get; private set; }

    private Comment() { }

    public static Comment Create(
        Guid tenantId, string entidadDestino, Guid entityId, Guid autorId, string texto,
        Guid? respondeAId = null)
    {
        if (!TipoDeEntidadComentable.Existe(entidadDestino))
            throw new InvalidOperationException(Reglas.EntidadDesconocida);

        if (entityId == Guid.Empty)
            throw new InvalidOperationException(Reglas.SinEntidad);

        if (autorId == Guid.Empty)
            throw new InvalidOperationException(Reglas.SinAutor);

        var limpio = Validar(texto);

        var comentario = new Comment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntidadDestino = entidadDestino,
            EntityId = entityId,
            AutorId = autorId,
            Texto = limpio,
            CreadoUtc = DateTime.UtcNow,
            RespondeAId = respondeAId,
        };

        comentario.RaiseDomainEvent(
            new CommentAddedEvent(comentario.Id, tenantId, entidadDestino, entityId, autorId));

        return comentario;
    }

    /// <summary>
    /// Cambia el texto.
    ///
    /// **Sólo lo puede hacer quien lo escribió.** Un comentario es de quien lo firma: si otro lo
    /// puede reescribir, la firma deja de significar nada. Ni siquiera un administrador; para eso
    /// está borrarlo, que sí deja constancia de que desapareció.
    /// </summary>
    public void Editar(Guid quien, string texto)
    {
        if (quien != AutorId)
            throw new InvalidOperationException(Reglas.SoloElAutorEdita);

        Texto = Validar(texto);
        EditadoUtc = DateTime.UtcNow;

        RaiseDomainEvent(new CommentEditedEvent(Id, TenantId, AutorId));
    }

    /// <summary>
    /// Si alguien puede borrarlo: su autor, o quien administre.
    ///
    /// Aquí sí entra el administrador, porque moderar es parte de su trabajo y borrar no pone
    /// palabras en boca de nadie.
    /// </summary>
    public bool LoPuedeBorrar(Guid quien, string rol)
        => quien == AutorId || rol == "Admin";

    private static string Validar(string texto)
    {
        var limpio = (texto ?? string.Empty).Trim();

        if (limpio.Length == 0)
            throw new InvalidOperationException(Reglas.TextoObligatorio);

        if (limpio.Length > LargoMaximo)
            throw new InvalidOperationException(Reglas.TextoDemasiadoLargo);

        return limpio;
    }

    public static class Reglas
    {
        public const string TextoObligatorio = "El comentario no puede estar vacío";
        public static readonly string TextoDemasiadoLargo =
            $"Un comentario no puede pasar de {LargoMaximo} caracteres";
        public const string EntidadDesconocida =
            "Sólo se puede comentar sobre una tarea, un ticket o un proyecto";
        public const string SinEntidad = "Falta sobre qué se comenta";
        public const string SinAutor = "Un comentario necesita autor";
        public const string SoloElAutorEdita = "Sólo quien escribió un comentario puede editarlo";
        public const string SoloElAutorOAdminBorra =
            "Sólo quien escribió un comentario, o quien administra, puede borrarlo";
        public const string NoEncontrado = "Comentario no encontrado";
        public const string RespuestaDeRespuesta =
            "No se puede responder a una respuesta: los hilos tienen un solo nivel";
        public const string RespondeAOtraEntidad =
            "Una respuesta tiene que estar en el mismo hilo que el comentario al que responde";
    }
}
