namespace WorkItems.Domain.Entities;

/// <summary>
/// Un punto de la checklist de una tarea.
///
/// Como los responsables, es una colección propiedad de <see cref="WorkTask"/>: el texto, el
/// orden y el estado de cada punto son cosa de la tarea, y una tabla suelta con su repositorio
/// permitiría dejar puntos huérfanos o con posiciones repetidas sin que nadie se enterara.
///
/// <see cref="Posicion"/> es explícita porque el orden de una checklist **lo decide quien la
/// escribe**, y una colección propiedad del agregado no vuelve ordenada de la base de datos.
/// Confiar en el orden de llegada es lo que ya nos jugó una mala pasada con los responsables.
/// </summary>
public sealed class ChecklistItem
{
    public const int LargoMaximo = 200;

    public Guid Id { get; private set; }
    public string Texto { get; private set; } = string.Empty;
    public bool Hecho { get; private set; }
    public int Posicion { get; private set; }

    private ChecklistItem() { }

    internal ChecklistItem(string texto, int posicion)
    {
        Id = Guid.NewGuid();
        Texto = Normalizar(texto);
        Posicion = posicion;
    }

    internal void Renombrar(string texto) => Texto = Normalizar(texto);

    internal void Marcar(bool hecho) => Hecho = hecho;

    internal void MoverA(int posicion) => Posicion = posicion;

    private static string Normalizar(string texto)
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
        public const string TextoObligatorio = "El punto de la checklist necesita un texto";
        public static readonly string TextoDemasiadoLargo =
            $"El punto de la checklist no puede pasar de {LargoMaximo} caracteres";
        public const string NoExiste = "Ese punto de la checklist no existe";
    }
}
