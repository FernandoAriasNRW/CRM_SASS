namespace BuildingBlocks.Domain.Primitives;

/// <summary>
/// Marca una entidad que se puede archivar: apartar de la vista normal sin borrarla.
///
/// Archivar y borrar son cosas distintas y se guardan por separado a propósito. Archivado es
/// «esto ya no me estorba»: sale de las listas, sigue existiendo, se consulta cuando hace falta
/// y no hay ninguna promesa de que vaya a desaparecer. La papelera
/// (<see cref="ISoftDeletable"/>) es «esto lo he borrado» y lleva implícita la idea de que algún
/// día se vacía. Con una sola columna para las dos cosas, vaciar la papelera se llevaría por
/// delante lo archivado.
///
/// <b>Por qué es una interfaz y no una columna suelta en cada agregado:</b> implementarla es lo
/// que hace que <c>TenantQueryFilter</c> excluya lo archivado en <b>todas</b> las consultas del
/// módulo. El plan de la Fase 5 avisaba de que ésta es «la parte que se rompe en silencio: una
/// consulta que se olvide de filtrar enseña archivado como si estuviera vivo». Poniéndolo en el
/// filtro global, no hay consulta que se pueda olvidar: para ver lo archivado hay que pedirlo.
/// </summary>
public interface IArchivable
{
    /// <summary>
    /// Cuándo se archivó, o <c>null</c> si está vivo.
    ///
    /// Es una fecha y no un booleano porque «¿cuándo dejó esto de estar a la vista?» es una
    /// pregunta que se acaba haciendo —para ordenar el archivo, para saber si una decisión es de
    /// antes o de después— y un booleano no la contesta. Cuesta lo mismo guardarla.
    /// </summary>
    DateTime? ArchivadoEnUtc { get; }
}
